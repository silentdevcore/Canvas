using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace PXA.WebApi.Observability;

internal sealed class PxaJsonConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "pxa-json";

    private readonly PxaObservabilityOptions settings;
    private readonly IHostEnvironment environment;

    public PxaJsonConsoleFormatter(
        IOptions<PxaObservabilityOptions> settings,
        IHostEnvironment environment)
        : base(FormatterName)
    {
        this.settings = settings.Value;
        this.environment = environment;
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var attributes = logEntry.State as IEnumerable<KeyValuePair<string, object?>>;
        var activity = Activity.Current;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("timestamp", DateTimeOffset.UtcNow);
            writer.WriteString("logLevel", logEntry.LogLevel.ToString());
            writer.WriteString("category", logEntry.Category);
            writer.WriteNumber("eventId", logEntry.EventId.Id);
            if (!string.IsNullOrWhiteSpace(logEntry.EventId.Name))
                writer.WriteString("eventName", logEntry.EventId.Name);
            writer.WriteString("messageTemplate", PxaLogPrivacy.ResolveMessageTemplate(attributes));

            if (activity is not null)
            {
                writer.WriteString("traceId", activity.TraceId.ToHexString());
                writer.WriteString("spanId", activity.SpanId.ToHexString());
                if (activity.ParentSpanId != default)
                    writer.WriteString("parentSpanId", activity.ParentSpanId.ToHexString());
            }

            writer.WriteString("service.name", settings.ServiceName);
            writer.WriteString("service.namespace", settings.ServiceNamespace);
            writer.WriteString(
                "service.version",
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");
            writer.WriteString("service.instance.id", ResolveInstanceId());
            writer.WriteString("deployment.environment.name", environment.EnvironmentName);

            var safeAttributes = PxaLogPrivacy.SanitizeAttributes(attributes, logEntry.Exception);
            if (safeAttributes.Any(value => value.Key != "{OriginalFormat}"))
            {
                writer.WritePropertyName("attributes");
                writer.WriteStartObject();
                foreach (var attribute in safeAttributes)
                {
                    if (attribute.Key == "{OriginalFormat}")
                        continue;
                    writer.WritePropertyName(attribute.Key);
                    JsonSerializer.Serialize(writer, attribute.Value);
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        textWriter.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string ResolveInstanceId() =>
        Environment.GetEnvironmentVariable("HOSTNAME")
        ?? Environment.MachineName;
}
