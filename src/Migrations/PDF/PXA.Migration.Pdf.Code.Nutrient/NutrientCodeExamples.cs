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
        document.DrawImage("invoice-logo.png", 460, 36, 84, 42);
        document.AddLinkToWeb("https://example.invalid", 48, 240, 180, 20);

        document.NewPage(PdfPageSizes.PdfPageSizeA4);
        document.RotatePage(90);
        document.AddTextFormField("customerName", 48, 72, 240, 28, "Example Customer");
        document.AddCheckBoxFormField("approved", 48, 120, 18, 18, true);
        document.AddStickyNoteAnnotation(320, 72, 24, 24, "Review this page");

        document.OcrPage("eng");
        document.Encrypt("owner-password", "user-password");
        document.ConvertToPDFA(PdfConformance.PDF_A_2b);
        document.TwainAcquire();
        document.ApplyRedaction();
        document.SignPDF("certificate.pfx", "secret", "approval");
        document.SaveToFile("nutrient-comprehensive.pdf");
        """;
}
