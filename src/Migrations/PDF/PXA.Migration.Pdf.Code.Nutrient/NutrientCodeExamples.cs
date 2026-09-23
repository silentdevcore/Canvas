namespace PXA.Migration.Pdf.Code.Nutrient;

public static class NutrientCodeExamples
{
    public const string ComprehensiveGdPictureCSharp = """
        using GdPicture14;

        using var document = new GdPicturePDF();
        document.SetLicenseNumber("SYNTHETIC-LICENSE-MUST-BE-REMOVED");
        document.NewPDF(PdfConformance.PDF);
        document.SetMeasurementUnit(PdfMeasurementUnit.PdfMeasurementUnitPoint);
        document.SetOrigin(PdfOrigin.PdfOriginTopLeft);

        document.NewPage(PdfPageSizes.PdfPageSizeA4);
        document.SelectPage(1);
        var headingFont = document.AddStandardFont(PdfStandardFont.PdfStandardFontHelveticaBold);
        document.SetTextSize(18);
        document.DrawText(headingFont, 48, 64, "PXA Nutrient migration example");
        document.DrawRectangle(48, 90, 500, 120, false, true);
        var logo = document.AddJpegImageFromFile("invoice-logo.jpg");
        document.DrawImage(logo, 460, 36, 84, 42);
        var linkId = document.AddLinkToWebAnnotation(48, 240, 180, 20, "https://example.invalid", false, 0, 0, 255);

        document.NewPage(PdfPageSizes.PdfPageSizeA4);
        document.RotatePage(90);
        var customerNameId = document.AddTextFormField(48, 72, 240, 28, "customerName", "Example Customer", false, headingFont, 10, 0, 0, 0);
        var approvedId = document.AddCheckBoxFormField(48, 120, 18, 18, "approved", PdfCheckBoxStyle.PdfCheckBoxStyleCheck, true, 0, 0, 0);
        var noteId = document.AddStickyNoteAnnotation(PdfStickyNoteAnnotationIcon.PdfStickyNoteAnnotationIconNote,
            320, 72, "PXA", "Review", "Review this page", 1, false, 255, 255, 0, 340, 72, 180, 100);

        document.OcrPage("eng");
        document.Encrypt("owner-password", "user-password");
        document.ConvertToPDFA(PdfConformance.PDF_A_2b);
        document.TwainAcquire();
        document.ApplyRedaction();
        document.SignPDF("certificate.pfx", "secret", "approval");
        document.SaveToFile("nutrient-comprehensive.pdf");
        """;
}
