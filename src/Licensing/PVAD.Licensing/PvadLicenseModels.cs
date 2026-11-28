namespace PVAD.Licensing;

/// <summary>Stable native product identifiers, separate from Cloud subscription editions.</summary>
public static class PvadLicenseProducts
{
    public const string Pdf = "pdf";
    public const string Word = "word";
    public const string Spreadsheet = "spreadsheet";
    public const string Importer = "importer";
    public const string Converter = "converter";
    public const string PdfCodeMigration = "migration.pdf.code";
    public const string PdfViewerMigration = "migration.pdf.viewer";
    public const string PdfDesignerMigration = "migration.pdf.designer";
    public const string SpreadsheetCodeMigration = "migration.spreadsheet.code";
    public const string ReportDesignerMigration = "migration.report.designer";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        Pdf, Word, Spreadsheet, Importer, Converter, PdfCodeMigration,
        PdfViewerMigration, PdfDesignerMigration, SpreadsheetCodeMigration, ReportDesignerMigration,
    });

    internal static bool IsMigration(string product) => product.StartsWith("migration.", StringComparison.Ordinal);
}

public enum PvadLicenseModel { Trial, Subscription, Perpetual }
public enum PvadLicenseState { Unlicensed, Active, GracePeriod, NotYetValid, Expired, UpdateNotCovered, Revoked }

/// <summary>A product grant. Migration providers use canonical registry keys, never aliases.</summary>
public sealed record PvadLicenseGrant(string Product, string[] Providers);

/// <summary>Signed account eligibility and explicit capability grants for version-two licenses.</summary>
public sealed record PvadLicenseAccountScope(string AccountType, string[] Capabilities);

/// <summary>Manufacturer-issued native license payload; all timestamps are UTC.</summary>
public sealed record PvadLicensePayload(
    int SchemaVersion,
    Guid LicenseId,
    string CustomerReference,
    PvadLicenseModel Model,
    DateTimeOffset IssuedAt,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil,
    DateTimeOffset? UpdatesUntil,
    bool RedistributionAllowed,
    PvadLicenseGrant[] Grants)
{
    /// <summary>Required for version two; absent from legacy version-one payloads.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PvadLicenseAccountScope? AccountScope { get; init; }
    /// <summary>Signed subscription grace override; absent retains seven legacy days.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? GracePeriodDays { get; init; }
}

/// <summary>The signature covers the exact decoded UTF-8 Payload bytes, not reserialized JSON.</summary>
public sealed record PvadSignedNativeLicense(
    int SchemaVersion,
    string KeyId,
    string Algorithm,
    string Payload,
    string Signature);

/// <summary>A content-free status suitable for application diagnostics; contains no license payload.</summary>
public sealed record PvadLicenseStatus(
    PvadLicenseState State,
    string Code,
    Guid? LicenseId = null,
    PvadLicenseModel? Model = null,
    DateTimeOffset? ValidUntil = null,
    DateTimeOffset? GraceEndsAt = null,
    DateTimeOffset? UpdatesUntil = null)
{
    public bool CanStartOperation => State is PvadLicenseState.Active or PvadLicenseState.GracePeriod;
}

/// <summary>Immutable admission result captured when an operation starts.</summary>
public sealed class PvadLicenseOperation
{
    public Guid LicenseId { get; }
    public string Product { get; }
    public string? Provider { get; }
    public bool IsGracePeriod { get; }

    internal PvadLicenseOperation(Guid licenseId, string product, string? provider, bool isGracePeriod) =>
        (LicenseId, Product, Provider, IsGracePeriod) = (licenseId, product, provider, isGracePeriod);
}

public sealed class PvadLicenseException : Exception
{
    public string Code { get; }

    internal PvadLicenseException(string code, string message) : base(message) => Code = code;
}
