namespace PXA.Infrastructure.Word;

/// <summary>
/// Power Dox Automation facade for signing DOCX packages.
/// </summary>
public static class DigitalSigningService
{
    public static byte[] SignDocx(Stream docxStream, byte[] pfxBytes, string? pfxPassword = null) =>
        Canvas.Infrastructure.Word.DigitalSigningService.SignDocx(docxStream, pfxBytes, pfxPassword);
}
