namespace PVAD.WebApi.Security;

public sealed class PvadAccountClosureOptions
{
    public const string SectionName = "AccountClosure";

    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
}
