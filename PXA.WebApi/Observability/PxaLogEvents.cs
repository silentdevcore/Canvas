namespace PXA.WebApi.Observability;

internal static class PxaLogEvents
{
    public static readonly EventId OcrImportStarted = new(1001, nameof(OcrImportStarted));
    public static readonly EventId OcrImportCompleted = new(1002, nameof(OcrImportCompleted));
    public static readonly EventId JobProcessingFailed = new(2001, nameof(JobProcessingFailed));
    public static readonly EventId JobPollingFailed = new(2002, nameof(JobPollingFailed));
    public static readonly EventId JobRetentionFailed = new(2003, nameof(JobRetentionFailed));
    public static readonly EventId JobMetricsFailed = new(2004, nameof(JobMetricsFailed));
    public static readonly EventId MailProcessingFailed = new(3001, nameof(MailProcessingFailed));
    public static readonly EventId TrialExpiryCheckFailed = new(3002, nameof(TrialExpiryCheckFailed));
    public static readonly EventId LicensingMetricsFailed = new(4001, nameof(LicensingMetricsFailed));
    public static readonly EventId TemporaryDebugLoggingEnabled = new(5001, nameof(TemporaryDebugLoggingEnabled));
    public static readonly EventId TemporaryDebugLoggingExpired = new(5002, nameof(TemporaryDebugLoggingExpired));
}
