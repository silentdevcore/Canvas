using System.Collections.Frozen;

namespace PVAD.Licensing;

internal enum PvadLicenseAccountKind { IndividualDeveloper, Company }

// Only Allowed is a positive eligibility decision. Approval, undecided and deferred
// states must never be converted into a grant by truthiness or a role assignment.
internal enum PvadAccountLicenseDecision { Denied, Allowed, RequiresAdminApproval, Undecided, Deferred }

/// <summary>
/// One account-level capability, evaluated independently of broad product grants.
/// Entry-point integration must additionally check the actual native product rights.
/// </summary>
internal sealed record PvadAccountCapability(
    string Id,
    PvadAccountLicenseDecision Individual,
    PvadAccountLicenseDecision Company = PvadAccountLicenseDecision.Allowed);

/// <summary>
/// Central free-phase eligibility policy, separate from member roles and verified
/// native license admission. Used by scoped issuance, verification and trusted host policy.
/// A policy allowance alone never authorizes processing or signs a license.
/// </summary>
internal static class PvadAccountLicensePolicy
{
    internal static bool TryAccountKind(string? value, out PvadLicenseAccountKind account)
    {
        account = value == "company" ? PvadLicenseAccountKind.Company : PvadLicenseAccountKind.IndividualDeveloper;
        return value is "company" or "individualDeveloper";
    }
    private const PvadAccountLicenseDecision Allow = PvadAccountLicenseDecision.Allowed;
    private const PvadAccountLicenseDecision Deny = PvadAccountLicenseDecision.Denied;
    private const PvadAccountLicenseDecision Approval = PvadAccountLicenseDecision.RequiresAdminApproval;
    private const PvadAccountLicenseDecision Undecided = PvadAccountLicenseDecision.Undecided;
    private const PvadAccountLicenseDecision Deferred = PvadAccountLicenseDecision.Deferred;

    // Stable capability IDs describe policy, not the availability of an importer or
    // exporter. In particular, HTML/XML import is approved but not implemented.
    internal static IReadOnlyList<PvadAccountCapability> Capabilities { get; } = Array.AsReadOnly<PvadAccountCapability>(
    [
        new("document.create", Allow),
        new("document.edit", Allow),
        new("document.print", Allow),
        new("template.create", Allow),
        new("template.version", Allow),
        new("template.publish", Allow),
        new("code.edit", Allow),
        new("code.execute", Allow),
        new("api.access", Allow),
        new("service-account.manage", Allow),

        new("import.pdf", Deny),
        new("import.doc", Allow),
        new("import.docx", Allow),
        new("import.xlsx", Allow),
        new("import.xls", Deferred, Deferred),
        new("import.csv", Allow),
        new("import.tsv", Allow),
        new("import.odt", Allow),
        new("import.pptx", Allow),
        new("import.md", Allow),
        new("import.html", Allow),
        new("import.xml", Allow),
        new("import.svg", Deny),
        new("import.image", Deny),
        new("image.analysis", Deny),
        new("image.ocr", Deny),

        new("export.pdf", Allow),
        new("export.docx", Allow),
        new("export.xlsx", Allow),
        new("export.xls", Deferred, Deferred),
        new("export.odt", Allow),
        new("export.pptx", Deny),
        new("export.html", Deny),
        new("export.xml", Deny),
        new("export.json", Undecided),
        new("export.md", Deny),
        new("export.csv", Deny),
        new("export.tsv", Deny),
        new("export.svg", Deny),
        new("export.png", Deny),
        new("export.jpeg", Deny),
        new("export.tiff", Deny),

        new("pdf.signature.sign", Deny),
        new("pdf.redaction.apply", Deny),
        new("pdf.compliance.convert", Deny),
        new("pdf.e-invoice.create", Deny),
        new("pdf.e-invoice.validate", Deny),
        // These inspection/read operations were not decided by denying mutation.
        new("pdf.trust.inspect", Undecided),
        new("pdf.signature.validate", Undecided),
        new("pdf.redaction.inspect", Undecided),
        new("pdf.compliance.validate", Undecided),
        new("pdf.e-invoice.read", Undecided),
        new("pdf.e-invoice.attach", Undecided),

        new("migration.pdf.code", Approval, Approval),
        new("migration.pdf.viewer", Approval, Approval),
        new("migration.pdf.designer", Approval, Approval),
        new("migration.spreadsheet.code", Approval, Approval),
        new("migration.report.designer", Approval, Approval),
    ]);

    private static readonly FrozenDictionary<string, PvadAccountCapability> ById =
        Capabilities.ToFrozenDictionary(capability => capability.Id, StringComparer.Ordinal);

    internal static PvadAccountLicenseDecision Evaluate(PvadLicenseAccountKind account, string? capability)
    {
        if (capability is null || !ById.TryGetValue(capability, out var definition))
            return Deny;

        return account switch
        {
            PvadLicenseAccountKind.IndividualDeveloper => definition.Individual,
            PvadLicenseAccountKind.Company => definition.Company,
            _ => Deny,
        };
    }

    // Callers must first identify the actual processing format. This normalizes
    // format identifiers/extensions only; it does not validate uploaded file bytes.
    internal static PvadAccountLicenseDecision EvaluateImport(PvadLicenseAccountKind account, string? format)
        => Evaluate(account, ImportCapability(format));

    internal static string ImportCapability(string? format)
    {
        var normalized = NormalizeFormat(format);
        if (normalized is "png" or "jpeg" or "gif" or "webp" or "bmp" or "tiff")
            normalized = "image";
        return "import." + normalized;
    }

    internal static PvadAccountLicenseDecision EvaluateExport(PvadLicenseAccountKind account, string? format) =>
        Evaluate(account, ExportCapability(format));

    internal static string ExportCapability(string? format) => "export." + NormalizeFormat(format);

    private static string NormalizeFormat(string? format) => format?.Trim().TrimStart('.').ToLowerInvariant() switch
    {
        "markdown" => "md",
        "jpg" => "jpeg",
        "tif" => "tiff",
        "htm" => "html",
        "word" => "docx",
        "excel" => "xlsx",
        var normalized => normalized ?? string.Empty,
    };
}
