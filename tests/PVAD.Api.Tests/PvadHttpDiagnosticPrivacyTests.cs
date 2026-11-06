using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Observability;

namespace PVAD.Api.Tests;

public sealed class PvadHttpDiagnosticPrivacyTests
{
    [Theory]
    [InlineData("QueryString")]
    [InlineData("http.query_string")]
    [InlineData("request.query-string")]
    public void Structured_logs_remove_http_query_values(string attribute)
    {
        var values = new List<KeyValuePair<string, object?>>
        {
            new(attribute, "?token=never-persist"),
            new("Method", "GET"),
            new("{OriginalFormat}", "Request {Method} {" + attribute + "}"),
        };

        var sanitized = PvadLogPrivacy.SanitizeAttributes(values);

        Assert.DoesNotContain(sanitized, value => value.Key == attribute);
        Assert.Contains(sanitized, value => value.Key == "Method" && Equals(value.Value, "GET"));
        Assert.Equal(PvadLogPrivacy.SuppressedMessage, PvadLogPrivacy.ResolveMessageTemplate(values));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Http_console_diagnostics_keep_correlation_without_query_payloads(bool legacy)
    {
        var settings = Options.Create(new PvadObservabilityOptions());
        PvadJsonConsoleFormatter formatter = legacy
            ? new LegacyPvadJsonConsoleFormatter(settings, new TestEnvironment())
            : new PvadJsonConsoleFormatter(settings, new TestEnvironment());
        var values = new List<KeyValuePair<string, object?>>
        {
            new("{OriginalFormat}", "Request starting {Method} {Path}{QueryString}"),
            new("Method", "GET"), new("Path", "/api/pvad/v1/version"),
            new("QueryString", "?token=never-persist"),
        };
        var entry = new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(LogLevel.Information,
            "Microsoft.AspNetCore.Hosting.Diagnostics", new EventId(1), values, null, static (_, _) => "never-persist");
        using var activity = new Activity("synthetic-http-log").SetIdFormat(ActivityIdFormat.W3C).Start();
        using var output = new StringWriter();

        formatter.Write(in entry, null, output);

        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(activity.TraceId.ToHexString(), document.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("GET", document.RootElement.GetProperty("attributes").GetProperty("Method").GetString());
        Assert.DoesNotContain("never-persist", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("QueryString", output.ToString(), StringComparison.Ordinal);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Synthetic privacy regression";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
