namespace PVAD.Core.Contracts;

public sealed record PdfTrustValidationIssue(string Code, string Message);

/// <summary>
/// Validates document-level PDF trust settings before they reach a renderer or code worker.
/// Cryptographic credentials are deliberately not part of the design contract.
/// </summary>
public static class PdfTrustSettingsValidator
{
    public static IReadOnlyList<PdfTrustValidationIssue> Validate(PageSettingsDto? settings)
    {
        if (settings is null)
            return [];

        var issues = new List<PdfTrustValidationIssue>();
        var compliance = settings.ComplianceProfile?.Trim() ?? "None";
        var accessibility = settings.AccessibilityProfile?.Trim() ?? "None";
        var encryption = settings.Encryption;

        if (compliance is not ("None" or "PdfA2B" or "PdfA3B"))
            issues.Add(new("PXAPDFTRUST201", $"Unsupported PDF compliance profile '{compliance}'."));
        if (accessibility is not ("None" or "PdfUa1"))
            issues.Add(new("PXAPDFTRUST202", $"Unsupported PDF accessibility profile '{accessibility}'."));

        if (encryption?.Enabled == true)
        {
            if (compliance is "PdfA2B" or "PdfA3B")
                issues.Add(new("PXAPDFTRUST203", "PDF/A output and PDF encryption cannot be enabled together."));
            if (accessibility == "PdfUa1")
                issues.Add(new("PXAPDFTRUST204", "PDF/UA output and PDF encryption cannot be enabled together."));
            var algorithm = encryption.Algorithm?.Trim().ToUpperInvariant();
            if (algorithm is not ("AES256" or "AES128" or "RC4_128"))
                issues.Add(new("PXAPDFTRUST205", $"Unsupported PDF encryption algorithm '{encryption.Algorithm}'."));

            var permissions = encryption.Permissions ?? new PdfEncryptionPermissionsDto();
            var restricted = !permissions.Print || !permissions.Modify || !permissions.Copy ||
                !permissions.Annotate || !permissions.FillForms || !permissions.ExtractAccessibility ||
                !permissions.Assemble || !permissions.PrintHighResolution;
            if (algorithm != "RC4_128" && restricted &&
                (string.IsNullOrEmpty(encryption.OwnerPassword) ||
                 string.Equals(encryption.OwnerPassword, encryption.UserPassword, StringComparison.Ordinal)))
            {
                issues.Add(new("PXAPDFTRUST206", "Restricted AES permissions require a distinct owner password."));
            }
        }

        return issues;
    }

    public static void ThrowIfInvalid(PageSettingsDto? settings)
    {
        var issue = Validate(settings).FirstOrDefault();
        if (issue is not null)
            throw new InvalidDataException($"{issue.Code}: {issue.Message}");
    }
}
