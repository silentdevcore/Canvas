using PXA.Core.Contracts;
using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis;

/// <summary>
/// Power Dox Automation facade for the editable image analysis importer.
/// </summary>
public sealed class ImageAnalysisFileImporter : PXA.FileImporter.IFileImporter
{
    private readonly Canvas.FileImporter.ImageAnalysis.ImageAnalysisFileImporter inner = new();

    public IReadOnlyList<string> SupportedExtensions =>
        Canvas.FileImporter.ImageAnalysis.ImageAnalysisFileImporter.SupportedExtensions;

    public async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null)
    {
        var result = await ImportWithAnalysisAsync(stream, name, options: ImageAnalysisOptions.Default);
        return result.Design;
    }

    public async Task<ImageAnalysisImportResult> ImportWithAnalysisAsync(
        Stream stream,
        string? name = null,
        double? targetWidthPt = null,
        double? targetHeightPt = null,
        ImageAnalysisOptions? options = null)
    {
        var result = await inner.ImportWithAnalysisAsync(
            stream,
            name,
            targetWidthPt,
            targetHeightPt,
            (options ?? ImageAnalysisOptions.Default).ToCanvasOptions());

        return ImageAnalysisImportResult.FromCanvas(result);
    }

    public static DesignExportDto Import(
        SKBitmap source,
        string name,
        double? targetWidthPt = null,
        double? targetHeightPt = null) =>
        ImportWithAnalysis(source, name, targetWidthPt, targetHeightPt).Design;

    public static ImageAnalysisImportResult ImportWithAnalysis(
        SKBitmap source,
        string name,
        double? targetWidthPt = null,
        double? targetHeightPt = null,
        ImageAnalysisOptions? options = null)
    {
        var result = Canvas.FileImporter.ImageAnalysis.ImageAnalysisFileImporter.ImportWithAnalysis(
            source,
            name,
            targetWidthPt,
            targetHeightPt,
            (options ?? ImageAnalysisOptions.Default).ToCanvasOptions());

        return ImageAnalysisImportResult.FromCanvas(result);
    }
}
