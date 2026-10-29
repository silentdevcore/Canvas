using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using PVAD.WebApi.Observability;

namespace PVAD.Api.Tests;

public sealed class TelemetryDependencyCompatibilityTests
{
    [Fact]
    public void Sdk_exports_sanitized_traces_without_a_collector()
    {
        using var exporter = new CaptureExporter<Activity>(activity =>
            new Dictionary<string, object?>(activity.TagObjects));
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource("PVAD.Tests.DependencyCompatibility")
            .AddProcessor(new PvadTelemetrySanitizingProcessor())
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();
        using var source = new ActivitySource("PVAD.Tests.DependencyCompatibility");
        using (var activity = source.StartActivity("synthetic-export"))
        {
            Assert.NotNull(activity);
            activity.SetTag("url.full", "https://example.test/export?token=synthetic-secret");
            activity.SetTag("user.email", "synthetic@example.test");
            activity.SetTag("operation.outcome", "completed");
        }

        Assert.True(provider.ForceFlush());
        var attributes = Assert.IsType<Dictionary<string, object?>>(Assert.Single(exporter.Items));
        Assert.Equal("https://example.test/export", attributes["url.full"]);
        Assert.Equal("completed", attributes["operation.outcome"]);
        Assert.DoesNotContain("user.email", attributes.Keys);
    }

    [Fact]
    public void Sdk_exports_counter_measurements_without_a_collector()
    {
        using var exporter = new CaptureExporter<Metric>(metric =>
        {
            var sum = 0L;
            foreach (ref readonly var point in metric.GetMetricPoints())
                sum += point.GetSumLong();
            return (metric.Name, sum);
        });
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("PVAD.Tests.DependencyCompatibility")
            .AddReader(new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds: 60_000))
            .Build();
        using var meter = new Meter("PVAD.Tests.DependencyCompatibility");
        meter.CreateCounter<long>("synthetic.operations").Add(3);

        Assert.True(provider.ForceFlush());
        Assert.Contains(exporter.Items, value => value is ValueTuple<string, long> item
            && item.Item1 == "synthetic.operations" && item.Item2 == 3);
    }

    [Fact]
    public void Sdk_exports_sanitized_logs_without_retaining_secrets_or_exceptions()
    {
        using var exporter = new CaptureExporter<LogRecord>(record => new LogSnapshot(
            record.Body, record.FormattedMessage, record.Exception,
            record.Attributes?.ToDictionary(attribute => attribute.Key, attribute => attribute.Value)
                ?? []));
        using var factory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.AddProcessor(new PvadLogRecordSanitizingProcessor());
            options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
        }));
        factory.CreateLogger("PVAD.Tests.DependencyCompatibility").LogWarning(
            new InvalidOperationException("synthetic-secret"),
            "Operation {operation.outcome} for {user.email}", "failed", "synthetic@example.test");

        var snapshot = Assert.IsType<LogSnapshot>(Assert.Single(exporter.Items));
        Assert.Equal(PvadLogPrivacy.SuppressedMessage, snapshot.Body);
        Assert.Null(snapshot.FormattedMessage);
        Assert.Null(snapshot.Exception);
        Assert.Equal("failed", snapshot.Attributes["operation.outcome"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, snapshot.Attributes["exception.type"]);
        Assert.DoesNotContain("user.email", snapshot.Attributes.Keys);
    }

    private sealed record LogSnapshot(string? Body, string? FormattedMessage,
        Exception? Exception, Dictionary<string, object?> Attributes);

    // Snapshot during Export: SDK records can be pooled and reused after this call.
    private sealed class CaptureExporter<T>(Func<T, object> snapshot) : BaseExporter<T> where T : class
    {
        public List<object> Items { get; } = [];

        public override ExportResult Export(in Batch<T> batch)
        {
            foreach (var item in batch)
                Items.Add(snapshot(item));
            return ExportResult.Success;
        }
    }
}
