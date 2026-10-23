using System.Text;
using PVAD.Core.Contracts;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Report;

namespace PVAD.WebApi.Services;

public sealed class ReportMigrationService
{
    public ReportMigrationConversion Convert(ReportMigrationInput input)
    {
        var source = input.Source;
        Dictionary<string, string>? packageResources = null;

        if (!string.IsNullOrWhiteSpace(input.SourceBase64))
        {
            byte[] bytes;
            try
            {
                bytes = System.Convert.FromBase64String(input.SourceBase64);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("The report binary source is not valid base64.", nameof(input), exception);
            }

            if (PVAD.Migration.Abstractions.ReportPackageExtractor.IsZip(bytes))
            {
                try
                {
                    var extracted = PVAD.Migration.Abstractions.ReportPackageExtractor.Extract(bytes, LooksLikeAnyReport);
                    source = extracted.Report
                        ?? throw new ArgumentException("The report package contains no recognizable report document.", nameof(input));
                    packageResources = extracted.Resources;
                }
                catch (InvalidDataException exception)
                {
                    throw new ArgumentException("The report binary source is not a valid report package.", nameof(input), exception);
                }
            }
            else
            {
                source = Encoding.UTF8.GetString(bytes);
            }
        }

        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Report source text or binary source is required.", nameof(input));

        var provider = string.Equals(input.Provider, "auto", StringComparison.OrdinalIgnoreCase)
            ? DetectProvider(source)
            : input.Provider;
        var resources = MergeResources(input.ResourceXml, input.Resources, packageResources);
        var result = ConvertWithProvider(provider, source, resources);
        return new ReportMigrationConversion(provider, result.Design, result.Diagnostics);
    }

    public static string DetectProvider(string source)
    {
        if (RdlReportMigration.LooksLike(source)) return ReportMigrationProviderKeys.Rdl;
        if (RpxReportMigration.LooksLike(source)) return ReportMigrationProviderKeys.Rpx;
        if (FastReportMigration.LooksLike(source)) return ReportMigrationProviderKeys.FastReport;
        if (TelerikReportMigration.LooksLike(source)) return ReportMigrationProviderKeys.Telerik;
        if (JasperReportsMigration.LooksLike(source)) return ReportMigrationProviderKeys.JasperReports;
        if (ActiveReportsJsMigration.LooksLike(source)) return ReportMigrationProviderKeys.ActiveReportsJs;
        if (StimulsoftReportMigration.LooksLike(source)) return ReportMigrationProviderKeys.Stimulsoft;
        return ReportMigrationProviderKeys.DevExpressReport;
    }

    private static ReportMigrationResult ConvertWithProvider(
        string provider,
        string source,
        IReadOnlyDictionary<string, string>? resources) => provider.ToLowerInvariant() switch
    {
        ReportMigrationProviderKeys.Rdl => new RdlReportMigration().Convert(source),
        ReportMigrationProviderKeys.Rpx => new RpxReportMigration().Convert(source, resources),
        ReportMigrationProviderKeys.FastReport => new FastReportMigration().Convert(source),
        ReportMigrationProviderKeys.Telerik => new TelerikReportMigration().Convert(source),
        ReportMigrationProviderKeys.JasperReports => new JasperReportsMigration().Convert(source, resources),
        ReportMigrationProviderKeys.ActiveReportsJs => new ActiveReportsJsMigration().Convert(source),
        ReportMigrationProviderKeys.Stimulsoft => new StimulsoftReportMigration().Convert(source),
        ReportMigrationProviderKeys.DevExpressReport => new DevExpressReportMigration().Convert(source, resources),
        _ => throw new ArgumentException($"Unknown report migration provider '{provider}'.", nameof(provider))
    };

    private static bool LooksLikeAnyReport(string source) =>
        RdlReportMigration.LooksLike(source)
        || RpxReportMigration.LooksLike(source)
        || FastReportMigration.LooksLike(source)
        || TelerikReportMigration.LooksLike(source)
        || JasperReportsMigration.LooksLike(source)
        || ActiveReportsJsMigration.LooksLike(source)
        || StimulsoftReportMigration.LooksLike(source);

    private static IReadOnlyDictionary<string, string>? MergeResources(
        string? resourceXml,
        IReadOnlyDictionary<string, string>? resources,
        IReadOnlyDictionary<string, string>? packageResources)
    {
        if (string.IsNullOrWhiteSpace(resourceXml) && resources is null && packageResources is null)
            return null;

        var merged = string.IsNullOrWhiteSpace(resourceXml)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : DevExpressReportResourceParser.ParseResx(resourceXml);

        if (packageResources is not null)
            foreach (var (key, value) in packageResources) merged[key] = value;
        if (resources is not null)
            foreach (var (key, value) in resources) merged[key] = value;

        return merged;
    }
}

public sealed record ReportMigrationInput(
    string Provider,
    string? Source,
    IReadOnlyDictionary<string, string>? Resources = null,
    string? ResourceXml = null,
    string? SourceBase64 = null);

public sealed record ReportMigrationConversion(
    string Provider,
    DesignExportDto Design,
    IReadOnlyList<PVAD.Migration.Abstractions.MigrationDiagnostic> Diagnostics);
