namespace PVAD.Migration.Pdf;

/// <summary>
/// Synthetic source fixtures for exercising provider-specific PDF Trust migration.
/// They describe representative API shapes and are not executable vendor SDK tutorials.
/// </summary>
public static class PdfTrustMigrationExamples
{
    private static readonly IReadOnlyDictionary<string, PdfTrustMigrationExample> Examples =
        new[]
        {
            Example(PdfMigrationProviderKeys.Nutrient, "Nutrient SDK",
                "SignPDF", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "ApplyRedaction", "ConvertToPDFA", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.IText7, "iText 7",
                "SignDetached", "TSAClient", "LtvVerification", "OpenEncrypted",
                "PdfCleanUpTool", "PdfADocument", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.Apryse, "Apryse",
                "SignOnNextSave", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "Redactor", "PDFACompliance", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.AsposePdf, "Aspose.PDF",
                "PdfFileSignature", "TimestampSettings", "ValidateSignature", "OpenEncrypted",
                "ApplyRedactions", "PdfAConversion", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.SyncfusionPdf, "Syncfusion PDF",
                "PdfSignature", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "ApplyRedactions", "PdfConformanceLevel", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.DevExpressPdf, "DevExpress PDF",
                "PdfDocumentSigner", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "ApplyRedactions", "PdfACompatibility", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.FoxitPdf, "Foxit PDF SDK",
                "Sign", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "ApplyRedactions", "PDFCompliance", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.DsPdf, "DsPdf",
                "Sign", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "ApplyRedactions", "SaveAsPdfA", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.GemBoxPdf, "GemBox.Pdf",
                "PdfSignature", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "ApplyRedactions", "PdfConformanceLevel", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.SpirePdf, "Spire.PDF",
                "PdfSignature", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "ApplyRedactions", "PdfConformanceLevel", "PdfUaValidation"),
            Example(PdfMigrationProviderKeys.PdfTools, "Pdftools",
                "Sign", "Timestamp", "ValidateSignature", "OpenEncrypted",
                "Redact", "Conformance", "PdfUaValidation")
        }.ToDictionary(static example => example.ProviderKey, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PdfTrustMigrationExample> All { get; } = Examples.Values
        .OrderBy(static example => example.ProviderKey, StringComparer.Ordinal)
        .ToArray();

    public static PdfTrustMigrationExample Get(string providerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);
        return Examples.TryGetValue(providerKey, out var example)
            ? example
            : throw new KeyNotFoundException($"No PDF Trust migration example exists for provider '{providerKey}'.");
    }

    private static PdfTrustMigrationExample Example(
        string providerKey,
        string displayName,
        string signing,
        string timestamp,
        string validation,
        string encryptedInput,
        string redaction,
        string archival,
        string accessibility)
    {
        var className = new string(displayName.Where(char.IsLetterOrDigit).ToArray());
        var source = $$"""
            // Synthetic PVAD migration fixture for {{displayName}}. This is not vendor SDK sample code.
            public static class {{className}}PdfTrustMigrationFixture
            {
                public static void Process(
                    dynamic document,
                    object certificateReference,
                    object timestampAuthorityReference,
                    object passwordReference)
                {
                    document.{{encryptedInput}}(passwordReference);
                    document.{{signing}}(certificateReference);
                    document.{{timestamp}}(timestampAuthorityReference);
                    document.{{validation}}();
                    document.{{redaction}}();
                    document.{{archival}}(PdfTrustProfile.PdfA2B);
                    document.{{accessibility}}(PdfTrustProfile.PdfUa1);
                }
            }
            """;

        return new PdfTrustMigrationExample(providerKey, displayName, source);
    }
}

public sealed record PdfTrustMigrationExample(
    string ProviderKey,
    string DisplayName,
    string SourceCode);
