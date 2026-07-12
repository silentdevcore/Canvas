using PXA.Migration.JasperReports;
using PXA.Core.Contracts;

namespace PXA.Migration.Report;

public sealed class JasperReportsMigration : IReportMigration
{
    private readonly JrxmlToDesignConverter inner = new();

    public static bool LooksLike(string source) => JrxmlToDesignConverter.LooksLikeJrxml(source);

    public ReportMigrationResult Convert(string source)
        => Convert(source, subreportSources: null);

    public ReportMigrationResult Convert(string source, IReadOnlyDictionary<string, string>? subreportSources)
    {
        var result = inner.Convert(source, subreportSources);
        return new ReportMigrationResult
        {
            Design = result.Design,
            Diagnostics = result.Diagnostics,
        };
    }
}
