using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PVAD.Observability.WebhookRelay;

namespace PVAD.Observability.WebhookRelay.Tests;

public sealed class WebhookRelayTests
{
    [Fact]
    public void Sanitizer_keeps_only_bounded_operational_fields()
    {
        using var input = JsonDocument.Parse(
            """
            {
              "status": "firing",
              "receiver": "must-not-appear",
              "externalURL": "https://internal.example.test",
              "alerts": [
                {
                  "status": "firing",
                  "startsAt": "2026-07-26T20:00:00Z",
                  "endsAt": "0001-01-01T00:00:00Z",
                  "fingerprint": "must-not-appear",
                  "labels": {
                    "alertname": "PxaWebApiHighErrorRate",
                    "severity": "critical",
                    "service": "pxa-webapi",
                    "environment": "production",
                    "tenant_id": "must-not-appear",
                    "user_email": "must-not-appear"
                  },
                  "annotations": {
                    "summary": "WebApi error rate is high",
                    "description": "Operational description",
                    "dashboard_path": "/d/pxa-platform-overview",
                    "runbook_id": "PXA-OBS-004",
                    "request_body": "must-not-appear"
                  }
                }
              ]
            }
            """);

        var payload = AlertmanagerPayloadSanitizer.Sanitize(input.RootElement);
        var json = Encoding.UTF8.GetString(payload);

        Assert.Contains("PxaWebApiHighErrorRate", json, StringComparison.Ordinal);
        Assert.Contains("PXA-OBS-004", json, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-appear", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fingerprint", json, StringComparison.Ordinal);
        Assert.DoesNotContain("externalURL", json, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant_id", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Signature_is_stable_and_changes_with_timestamp_or_payload()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var payload = Encoding.UTF8.GetBytes("""{"status":"firing"}""");

        var first = WebhookSignature.Create(1_800_000_000, payload, secret);
        var same = WebhookSignature.Create(1_800_000_000, payload, secret);
        var later = WebhookSignature.Create(1_800_000_001, payload, secret);
        var changed = WebhookSignature.Create(
            1_800_000_000,
            Encoding.UTF8.GetBytes("""{"status":"resolved"}"""),
            secret);

        Assert.Equal(first, same);
        Assert.StartsWith("sha256=", first, StringComparison.Ordinal);
        Assert.NotEqual(first, later);
        Assert.NotEqual(first, changed);
    }

    [Fact]
    public void Signature_matches_the_independently_calculated_protocol_vector()
    {
        Assert.Equal(
            "sha256=059d03265cfce94dc5afc4a76af6e5f27fbed76cb477e0321a3ddced0388e7fb",
            WebhookSignature.Create(1_800_000_000,
                Encoding.UTF8.GetBytes("""{"status":"firing"}"""),
                Encoding.UTF8.GetBytes("synthetic-test-key-with-at-least-32-bytes")));
    }

    [Fact]
    public void Sanitizer_rejects_invalid_or_unbounded_alert_batches()
    {
        using var missingAlerts = JsonDocument.Parse("""{"status":"firing"}""");
        Assert.Throws<InvalidDataException>(() =>
            AlertmanagerPayloadSanitizer.Sanitize(missingAlerts.RootElement));

        var alerts = string.Join(",", Enumerable.Repeat("""{"status":"firing"}""", 101));
        using var oversizedBatch = JsonDocument.Parse($$"""{"alerts":[{{alerts}}]}""");
        Assert.Throws<InvalidDataException>(() =>
            AlertmanagerPayloadSanitizer.Sanitize(oversizedBatch.RootElement));
    }

    [Theory]
    [InlineData("PVAD")]
    [InlineData("PXA")]
    public async Task Relay_sends_equal_signed_headers_for_canonical_and_legacy_receivers(string identity)
    {
        var secret = Encoding.UTF8.GetBytes("pvad-test-webhook-signing-secret-value-2026");
        var secretFile = Path.GetTempFileName();
        await File.WriteAllBytesAsync(secretFile, secret);
        var capture = new CaptureHandler(identity);
        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureAppConfiguration((_, configuration) =>
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["WebhookRelay:Destination"] = "https://hooks.example.test/pvad",
                            ["WebhookRelay:SecretFile"] = secretFile,
                        }));
                    builder.ConfigureServices(services =>
                        services.AddHttpClient("webhook")
                            .ConfigurePrimaryHttpMessageHandler(() => capture));
                });
            using var client = factory.CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, "/alerts")
            {
                Content = new StringContent(
                    """
                    {
                      "status": "firing",
                      "alerts": [{
                        "status": "firing",
                        "labels": {
                          "alertname": "PvadTestAlert",
                          "severity": "warning",
                          "tenant_id": "must-not-appear"
                        },
                        "annotations": {
                          "summary": "Synthetic alert",
                          "request_body": "must-not-appear"
                        }
                      }]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Add("X-PVAD-Webhook-Signature", "caller-supplied-value");
            request.Headers.Add("X-PXA-Webhook-Signature", "caller-supplied-value");
            using var response = await client.SendAsync(request);

            Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal(new Uri("https://hooks.example.test/pvad"), capture.RequestUri);
            Assert.NotNull(capture.Body);
            var body = Encoding.UTF8.GetString(capture.Body);
            Assert.Contains("PvadTestAlert", body, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-appear", body, StringComparison.Ordinal);
            Assert.True(long.TryParse(capture.Timestamp, out var timestamp));
            Assert.Equal(
                WebhookSignature.Create(timestamp, capture.Body, secret),
                capture.Signature);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(capture.Body)).ToLowerInvariant(),
                capture.IdempotencyKey);
        }
        finally
        {
            File.Delete(secretFile);
        }
    }

    [Fact]
    public async Task Relay_rejects_a_signing_secret_shorter_than_32_bytes_at_startup()
    {
        var secretFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(secretFile, "too-short");
        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.ConfigureAppConfiguration((_, configuration) =>
                        configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["WebhookRelay:Destination"] = "https://hooks.example.test/pvad",
                            ["WebhookRelay:SecretFile"] = secretFile,
                        }));
                });

            Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        }
        finally
        {
            File.Delete(secretFile);
        }
    }

    private sealed class CaptureHandler(string identity) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public byte[]? Body { get; private set; }
        public string? Timestamp { get; private set; }
        public string? Signature { get; private set; }
        public string? IdempotencyKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            foreach (var suffix in new[] { "Timestamp", "Signature", "Idempotency-Key" })
            {
                Assert.Equal(
                    Assert.Single(request.Headers.GetValues($"X-PVAD-Webhook-{suffix}")),
                    Assert.Single(request.Headers.GetValues($"X-PXA-Webhook-{suffix}")));
            }
            Timestamp = Assert.Single(request.Headers.GetValues($"X-{identity}-Webhook-Timestamp"));
            Signature = Assert.Single(request.Headers.GetValues($"X-{identity}-Webhook-Signature"));
            IdempotencyKey = Assert.Single(
                request.Headers.GetValues($"X-{identity}-Webhook-Idempotency-Key"));
            return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
        }
    }
}
