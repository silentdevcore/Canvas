using Canvas.FileImporter.ImageOcr;

namespace Canvas.FileImporter.ImageOcr.Tests;

/// <summary>
/// Regression: light-grey footer text must survive OCR preprocessing. The contrast stretch used to
/// pivot around a fixed mid-grey (128), which pushed text lighter than mid-grey toward white and
/// erased low-contrast footers before Tesseract ran. Preprocessing now pivots around the page's mean
/// luminance, so text darker than its background is preserved regardless of absolute tone.
/// </summary>
public sealed class LightFooterPreprocessingTests
{
    private static readonly string ImagePath =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "RealSamples", "Agency-Invoice.png");

    private static (string tessData, string native)? OcrPaths()
    {
        var tessData = Path.Combine(AppContext.BaseDirectory, "tessdata");
        var native   = Path.Combine(AppContext.BaseDirectory, "native");
        return Directory.Exists(native) && Directory.Exists(tessData) ? (tessData, native) : null;
    }

    [Fact]
    public async Task LightFooterText_SurvivesPreprocessing()
    {
        if (OcrPaths() is not { } paths) return;          // OCR native deps unavailable in this env
        if (!File.Exists(ImagePath)) return;

        var converter = new ImageToPdfConverter(new EmbeddedTesseractOcrEngine(paths.tessData, paths.native));
        using var stream = File.OpenRead(ImagePath);

        ImageToPdfConversionResult result;
        try
        {
            result = await converter.ConvertAsync(stream, "Agency-Invoice.png", new ImageToPdfConversionOptions
            {
                Languages = "eng",
                SourceDpiX = 150,
                SourceDpiY = 150,
                EnablePreprocessing = true,   // the mode that previously erased the footer
                NativeLibraryPath = paths.native,
            });
        }
        catch (OcrNativeDependencyMissingException) { return; }
        catch (DllNotFoundException) { return; }

        var text = string.Join(" ",
            result.Design.Pages[0].Elements.Where(e => e.Type == "text").Select(e => e.Content));

        // The footer's most distinctive tokens — the account number and bank — must be present.
        Assert.Contains("12345678", text);
        Assert.Contains("Barclays", text, StringComparison.OrdinalIgnoreCase);
    }
}
