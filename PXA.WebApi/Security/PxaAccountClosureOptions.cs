namespace PXA.WebApi.Security;

public sealed class PxaAccountClosureOptions
{
    public const string SectionName = "AccountClosure";

    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
}
