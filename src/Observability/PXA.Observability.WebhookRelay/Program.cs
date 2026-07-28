using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using PXA.Observability.WebhookRelay;

if (args is ["--health-check"])
{
    using var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var healthResponse = await healthClient.GetAsync("http://127.0.0.1:8080/health/live");
        Environment.ExitCode = healthResponse.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        Environment.ExitCode = 1;
    }
    catch (TaskCanceledException)
    {
        Environment.ExitCode = 1;
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);
var section = builder.Configuration.GetSection(WebhookRelayOptions.SectionName);
builder.WebHost.ConfigureKestrel(server =>
    server.Limits.MaxRequestBodySize = 1024 * 1024);
builder.Services.AddOptions<WebhookRelayOptions>()
    .Bind(section)
    .Validate(
        options => options.Destination is { IsAbsoluteUri: true, IsLoopback: false } destination &&
                   destination.Scheme == Uri.UriSchemeHttps,
        "WebhookRelay:Destination must be a non-loopback HTTPS URL.")
    .Validate(
        options => options.MaxPayloadBytes is >= 1024 and <= 1024 * 1024,
        "WebhookRelay:MaxPayloadBytes must be between 1 KiB and 1 MiB.")
    .Validate(
        options => options.RequestTimeoutSeconds is >= 1 and <= 60,
        "WebhookRelay:RequestTimeoutSeconds must be between 1 and 60.")
    .Validate(
        options => HasValidSigningSecret(options.SecretFile),
        "The webhook signing-secret file must exist, be readable, and contain at least 32 bytes.")
    .ValidateOnStart();
builder.Services.AddHttpClient("webhook")
    .ConfigureHttpClient((services, client) =>
    {
        var options = services.GetRequiredService<IOptions<WebhookRelayOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
    });

var app = builder.Build();
app.MapGet("/health/live", () => Results.NoContent());
app.MapPost("/alerts", RelayAsync);
app.Run();

static async Task<IResult> RelayAsync(
    HttpRequest request,
    IOptions<WebhookRelayOptions> configuredOptions,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken)
{
    var options = configuredOptions.Value;
    if (request.ContentLength is > 0 && request.ContentLength > options.MaxPayloadBytes)
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    JsonDocument document;
    try
    {
        document = await JsonDocument.ParseAsync(
            request.Body,
            new JsonDocumentOptions
            {
                MaxDepth = 16,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            },
            cancellationToken);
    }
    catch (JsonException)
    {
        return Results.BadRequest();
    }

    byte[] payload;
    using (document)
    {
        try
        {
            payload = AlertmanagerPayloadSanitizer.Sanitize(document.RootElement);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest();
        }
    }

    var secret = Array.Empty<byte>();
    try
    {
        secret = await File.ReadAllBytesAsync(options.SecretFile, cancellationToken);
        secret = TrimTrailingWhitespace(secret);
        if (secret.Length < 32)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var outbound = new HttpRequestMessage(HttpMethod.Post, options.Destination)
        {
            Content = new ByteArrayContent(payload),
        };
        outbound.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        outbound.Headers.Add("X-PXA-Webhook-Timestamp", timestamp.ToString());
        outbound.Headers.Add(
            "X-PXA-Webhook-Signature",
            WebhookSignature.Create(timestamp, payload, secret));
        outbound.Headers.Add(
            "X-PXA-Webhook-Idempotency-Key",
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());

        using var response = await httpClientFactory
            .CreateClient("webhook")
            .SendAsync(outbound, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return response.IsSuccessStatusCode
            ? Results.Accepted()
            : Results.StatusCode(StatusCodes.Status502BadGateway);
    }
    catch (HttpRequestException)
    {
        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
    }
    catch (IOException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(secret);
        CryptographicOperations.ZeroMemory(payload);
    }
}

static bool HasValidSigningSecret(string path)
{
    if (string.IsNullOrWhiteSpace(path))
        return false;

    byte[] secret = [];
    try
    {
        secret = File.ReadAllBytes(path);
        var length = secret.Length;
        while (length > 0 && char.IsWhiteSpace((char)secret[length - 1]))
            length--;
        return length >= 32;
    }
    catch (IOException)
    {
        return false;
    }
    catch (UnauthorizedAccessException)
    {
        return false;
    }
    finally
    {
        CryptographicOperations.ZeroMemory(secret);
    }
}

static byte[] TrimTrailingWhitespace(byte[] value)
{
    var length = value.Length;
    while (length > 0 && char.IsWhiteSpace((char)value[length - 1]))
        length--;
    if (length == value.Length)
        return value;

    var trimmed = value[..length];
    CryptographicOperations.ZeroMemory(value);
    return trimmed;
}

public partial class Program;
