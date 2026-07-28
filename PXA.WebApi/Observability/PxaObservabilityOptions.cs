namespace PXA.WebApi.Observability;

public sealed class PxaObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool Enabled { get; set; } = true;
    public string ServiceName { get; set; } = "pxa-webapi";
    public string ServiceNamespace { get; set; } = "PowerDoxAutomation";
    public string? OtlpEndpoint { get; set; }
    public bool ExportLogs { get; set; } = true;
    public bool ExportMetrics { get; set; } = true;
    public bool ExportTraces { get; set; } = true;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public bool AllowTemporaryDebugLogging { get; set; }
    public DateTimeOffset? DebugLoggingExpiresAtUtc { get; set; }
    public bool EnableOcrFailureInjection { get; set; }
    public int OcrFailureInjectionCount { get; set; } = 3;
}
