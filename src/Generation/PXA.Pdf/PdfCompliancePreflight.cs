using PXA.Pdf.Layout;

namespace PXA.Pdf;

/// <summary>Validates generator input before a PDF/A conformance claim is emitted.</summary>
public static class PdfCompliancePreflight
{
    public const string ValidatorName = "PXA PDF/A-2b generator preflight 1.0";

    public static PdfComplianceValidationReport Validate(PdfDocument document, PdfSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<PdfComplianceDiagnostic>();
        if (options.ComplianceProfile == PdfComplianceProfile.None)
        {
            return new PdfComplianceValidationReport(
                PdfComplianceProfile.None,
                true,
                ValidatorName,
                diagnostics);
        }

        if (options.ComplianceProfile != PdfComplianceProfile.PdfA2B)
        {
            diagnostics.Add(new PdfComplianceDiagnostic(
                "PXAPDFA001",
                PdfComplianceDiagnosticSeverity.Error,
                $"Compliance profile '{options.ComplianceProfile}' is not supported."));
        }

        if (options.Encryption is not null || options.CertificateEncryption is not null)
        {
            diagnostics.Add(new PdfComplianceDiagnostic(
                "PXAPDFA002",
                PdfComplianceDiagnosticSeverity.Error,
                "PDF/A documents cannot be encrypted."));
        }

        if (document.Info.CustomProperties.Count > 0)
        {
            diagnostics.Add(new PdfComplianceDiagnostic(
                "PXAPDFA006",
                PdfComplianceDiagnosticSeverity.Error,
                "Custom document properties require a declared XMP extension schema and are not supported by the initial PXA PDF/A-2b writer."));
        }

        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            if (page.Elements.OfType<TextElement>().Any(static text => text.EmbeddedFont is null))
            {
                diagnostics.Add(new PdfComplianceDiagnostic(
                    "PXAPDFA003",
                    PdfComplianceDiagnosticSeverity.Error,
                    "Every font used by visible text must be embedded. Configure PdfFontLoader and specify a language for text drawing.",
                    pageIndex + 1));
            }

            if (page.ComboBoxAnnotations.Count > 0
                || page.MultilineTextFields.Count > 0
                || page.TextFields.Count > 0
                || page.CheckBoxAnnotations.Count > 0)
            {
                diagnostics.Add(new PdfComplianceDiagnostic(
                    "PXAPDFA004",
                    PdfComplianceDiagnosticSeverity.Error,
                    "Interactive form fields are not supported by the initial PXA PDF/A-2b writer because their appearance fonts are not embedded.",
                    pageIndex + 1));
            }

            if (page.ReviewAnnotations.Count > 0)
            {
                diagnostics.Add(new PdfComplianceDiagnostic(
                    "PXAPDFA005",
                    PdfComplianceDiagnosticSeverity.Error,
                    "Review annotations are not supported by the initial PXA PDF/A-2b writer.",
                    pageIndex + 1));
            }
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(new PdfComplianceDiagnostic(
                "PXAPDFA000",
                PdfComplianceDiagnosticSeverity.Information,
                "The document satisfies the PXA generator preflight for PDF/A-2b."));
        }

        return new PdfComplianceValidationReport(
            options.ComplianceProfile,
            diagnostics.All(static diagnostic => diagnostic.Severity != PdfComplianceDiagnosticSeverity.Error),
            ValidatorName,
            diagnostics);
    }
}
