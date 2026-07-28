using OpenTelemetry;
using OpenTelemetry.Logs;

namespace PXA.WebApi.Observability;

internal sealed class PxaLogRecordSanitizingProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        var originalAttributes = data.Attributes;
        data.Attributes = PxaLogPrivacy.SanitizeAttributes(originalAttributes, data.Exception);
        data.Body = PxaLogPrivacy.ResolveMessageTemplate(originalAttributes);
        data.FormattedMessage = null;
        data.Exception = null;
    }
}
