using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PXA.FileImporter.ImageOcr;

namespace PXA.WebApi.Observability;

internal static class PxaObservabilityExtensions
{
    public static WebApplicationBuilder AddPxaObservability(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(PxaObservabilityOptions.SectionName);
        var settings = section.Get<PxaObservabilityOptions>() ?? new PxaObservabilityOptions();
        Validate(settings);
        if (settings.EnableOcrFailureInjection &&
            !builder.Environment.IsDevelopment() &&
            !builder.Environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "OCR failure injection is allowed only in Development or Testing.");
        }
        var debugLoggingError = GetDebugLoggingConfigurationError(
            builder.Configuration,
            builder.Environment.EnvironmentName,
            settings,
            DateTimeOffset.UtcNow);
        if (debugLoggingError is not null)
            throw new InvalidOperationException(debugLoggingError);

        builder.Services.AddOptions<PxaObservabilityOptions>()
            .Bind(section)
            .Validate(Validate, "Observability configuration is invalid.")
            .ValidateOnStart();

        ConfigureStructuredLogging(builder);
        if (IsDebugOrTraceConfigured(builder.Configuration) &&
            !builder.Environment.IsDevelopment())
        {
            builder.Services.AddHostedService<PxaTemporaryDebugLoggingExpiryService>();
        }
        if (!settings.Enabled)
            return builder;

        var resource = CreateResource(builder, settings);
        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
                AddPxaResource(resourceBuilder, builder, settings));

        openTelemetry.WithTracing(tracing =>
        {
            tracing
                .AddSource(PxaTelemetry.ActivitySourceName)
                .AddSource(ProcessIsolatedTesseractOcrEngine.ActivitySourceName)
                .AddNpgsql()
                .AddProcessor(new PxaTelemetrySanitizingProcessor())
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation();

            if (settings.ExportTraces && TryGetEndpoint(settings, out var endpoint))
                tracing.AddOtlpExporter(options => ConfigureExporter(options, endpoint));
        });

        openTelemetry.WithMetrics(metrics =>
        {
            metrics
                .AddMeter(PxaTelemetry.MeterName)
                .AddMeter("Npgsql")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (settings.ExportMetrics && TryGetEndpoint(settings, out var endpoint))
                metrics.AddOtlpExporter(options => ConfigureExporter(options, endpoint));
        });

        if (settings.ExportLogs && TryGetEndpoint(settings, out var logEndpoint))
        {
            builder.Logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = false;
                options.IncludeScopes = false;
                options.ParseStateValues = true;
                options.AddProcessor(new PxaLogRecordSanitizingProcessor());
                options.SetResourceBuilder(resource);
                options.AddOtlpExporter(exporter => ConfigureExporter(exporter, logEndpoint));
            });
        }

        builder.Services.AddSingleton<IHealthCheckPublisher, PxaHealthMetricsPublisher>();
        builder.Services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(5);
            options.Period = TimeSpan.FromSeconds(settings.HealthCheckIntervalSeconds);
            options.Predicate = registration => registration.Tags.Contains("ready");
        });

        return builder;
    }

    private static void ConfigureStructuredLogging(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId);
        builder.Logging.AddConsole(options =>
            options.FormatterName = PxaJsonConsoleFormatter.FormatterName);
        builder.Logging.AddConsoleFormatter<PxaJsonConsoleFormatter, ConsoleFormatterOptions>();
    }

    private static ResourceBuilder CreateResource(
        WebApplicationBuilder builder,
        PxaObservabilityOptions settings) =>
        AddPxaResource(ResourceBuilder.CreateDefault(), builder, settings);

    private static ResourceBuilder AddPxaResource(
        ResourceBuilder resourceBuilder,
        WebApplicationBuilder builder,
        PxaObservabilityOptions settings)
    {
        var instanceId = Environment.GetEnvironmentVariable("HOSTNAME")
            ?? Environment.MachineName;
        return resourceBuilder
            .AddService(
                settings.ServiceName,
                settings.ServiceNamespace,
                GetServiceVersion())
            .AddAttributes(
            [
                new("deployment.environment.name", builder.Environment.EnvironmentName),
                new("service.instance.id", instanceId),
            ]);
    }

    private static string GetServiceVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    private static bool Validate(PxaObservabilityOptions settings) =>
        !string.IsNullOrWhiteSpace(settings.ServiceName) &&
        !string.IsNullOrWhiteSpace(settings.ServiceNamespace) &&
        settings.HealthCheckIntervalSeconds is >= 5 and <= 3600 &&
        settings.OcrFailureInjectionCount is >= 1 and <= 10 &&
        (string.IsNullOrWhiteSpace(settings.OtlpEndpoint) ||
         Uri.TryCreate(settings.OtlpEndpoint, UriKind.Absolute, out var endpoint) &&
         endpoint.Scheme is "http" or "https");

    internal static string? GetDebugLoggingConfigurationError(
        IConfiguration configuration,
        string environmentName,
        PxaObservabilityOptions settings,
        DateTimeOffset now)
    {
        if (!IsDebugOrTraceConfigured(configuration) ||
            string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!settings.AllowTemporaryDebugLogging)
            return "Debug or Trace logging outside Development requires Observability:AllowTemporaryDebugLogging=true.";
        if (settings.DebugLoggingExpiresAtUtc is not { } expiresAt)
            return "Temporary Debug or Trace logging requires Observability:DebugLoggingExpiresAtUtc.";
        if (expiresAt <= now)
            return "Temporary Debug or Trace logging has expired.";
        if (expiresAt > now.AddHours(24))
            return "Temporary Debug or Trace logging may be enabled for at most 24 hours.";
        return null;
    }

    internal static bool IsDebugOrTraceConfigured(IConfiguration configuration) =>
        configuration.AsEnumerable().Any(value =>
            value.Value is not null &&
            value.Key.StartsWith("Logging:", StringComparison.OrdinalIgnoreCase) &&
            value.Key.Contains(":LogLevel:", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<LogLevel>(value.Value, true, out var level) &&
            level < LogLevel.Information);

    private static bool TryGetEndpoint(
        PxaObservabilityOptions settings,
        out Uri endpoint) =>
        Uri.TryCreate(settings.OtlpEndpoint, UriKind.Absolute, out endpoint!);

    private static void ConfigureExporter(OtlpExporterOptions options, Uri endpoint)
    {
        options.Endpoint = endpoint;
        options.Protocol = OtlpExportProtocol.Grpc;
        options.ExportProcessorType = ExportProcessorType.Batch;
    }
}
