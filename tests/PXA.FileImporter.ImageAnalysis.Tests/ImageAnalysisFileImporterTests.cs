using PXA.FileImporter.ImageAnalysis;
using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis.Tests;

public sealed class ImageAnalysisFileImporterTests
{
    [Fact]
    public void ImportWithAnalysis_ReturnsPxaDiagnosticsAndDesign()
    {
        using var bitmap = MakeBitmap();

        var result = ImageAnalysisFileImporter.ImportWithAnalysis(
            bitmap,
            "analysis",
            options: new ImageAnalysisOptions
            {
                IncludeDebugOverlay = true,
                IncludeFallbackImageLayer = true,
            });

        Assert.Equal("analysis", result.Design.Name);
        Assert.Equal(160, result.Diagnostics.SourceWidthPx);
        Assert.Equal(90, result.Diagnostics.SourceHeightPx);
        Assert.True(result.Diagnostics.WorkingWidthPx > 0);
        Assert.NotNull(result.DebugOverlayPng);
        Assert.NotEmpty(result.DebugOverlayPng);
    }

    [Fact]
    public async Task ImportAsync_ImplementsCommonFileImporterContract()
    {
        await using var stream = new MemoryStream(MakePng());

        PXA.FileImporter.IFileImporter importer = new ImageAnalysisFileImporter();
        var design = await importer.ImportAsync(stream, "analysis.png");

        Assert.Contains("png", importer.SupportedExtensions);
        Assert.Equal("analysis.png", design.Name);
        Assert.NotEmpty(design.Pages);
    }

    private static SKBitmap MakeBitmap()
    {
        var bitmap = new SKBitmap(160, 90);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black };
        canvas.DrawRect(20, 20, 60, 30, paint);
        return bitmap;
    }

    private static byte[] MakePng()
    {
        using var bitmap = MakeBitmap();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
