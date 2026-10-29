namespace PVAD.WebApi.Services.Jobs;

public sealed class PvadJobOptions
{
    public const string SectionName = "Jobs";

    public int PollIntervalSeconds { get; set; } = 2;
    public int LeaseMinutes { get; set; } = 10;
    public int MaximumAttempts { get; set; } = 3;
    public int ResultRetentionDays { get; set; } = 7;
    public int TransientRetentionHours { get; set; } = 24;
    public int PdfTrustInspectionRetentionHours { get; set; } = 1;
    public int TerminalMetadataRetentionDays { get; set; } = 30;
    public int CleanupIntervalMinutes { get; set; } = 60;
    public int CleanupBatchSize { get; set; } = 100;
    public int MetricsIntervalSeconds { get; set; } = 15;
}
