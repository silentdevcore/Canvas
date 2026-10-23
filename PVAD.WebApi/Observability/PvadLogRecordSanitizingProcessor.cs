using OpenTelemetry;
using OpenTelemetry.Logs;

namespace PVAD.WebApi.Observability;

internal sealed class PvadLogRecordSanitizingProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        var originalAttributes = data.Attributes;
        data.Attributes = PvadLogPrivacy.SanitizeAttributes(originalAttributes, data.Exception);
        data.Body = PvadLogPrivacy.ResolveMessageTemplate(originalAttributes);
        data.FormattedMessage = null;
        data.Exception = null;
    }
}
