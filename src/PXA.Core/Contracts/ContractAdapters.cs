using System.Text.Json;

namespace PXA.Core.Contracts;

/// <summary>
/// JSON-compatible adapters between PXA contracts and the legacy Canvas contracts.
/// </summary>
public static class ContractAdapters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Canvas.Core.Contracts.DesignExportDto ToCanvas(this DesignExportDto design) =>
        Convert<DesignExportDto, Canvas.Core.Contracts.DesignExportDto>(design);

    public static DesignExportDto ToPxa(this Canvas.Core.Contracts.DesignExportDto design) =>
        Convert<Canvas.Core.Contracts.DesignExportDto, DesignExportDto>(design);

    public static Canvas.Core.Contracts.SpreadsheetDto ToCanvas(this SpreadsheetDto workbook) =>
        Convert<SpreadsheetDto, Canvas.Core.Contracts.SpreadsheetDto>(workbook);

    public static SpreadsheetDto ToPxa(this Canvas.Core.Contracts.SpreadsheetDto workbook) =>
        Convert<Canvas.Core.Contracts.SpreadsheetDto, SpreadsheetDto>(workbook);

    public static Canvas.Core.Contracts.ExportOptions ToCanvas(this ExportOptions options) => new(
        options.Dpi,
        options.Quality,
        options.CancellationToken,
        options.WordFidelityV2);

    public static ExportOptions ToPxa(this Canvas.Core.Contracts.ExportOptions options) => new(
        options.Dpi,
        options.Quality,
        options.CancellationToken,
        options.WordFidelityV2);

    public static Canvas.Core.Primitives.PdfPoint ToCanvas(this PXA.Core.Primitives.PdfPoint point) =>
        new(point.X, point.Y);

    public static PXA.Core.Primitives.PdfPoint ToPxa(this Canvas.Core.Primitives.PdfPoint point) =>
        new(point.X, point.Y);

    public static Canvas.Core.Primitives.PdfTextAlignment ToCanvas(this PXA.Core.Primitives.PdfTextAlignment alignment) =>
        Enum.Parse<Canvas.Core.Primitives.PdfTextAlignment>(alignment.ToString());

    public static PXA.Core.Primitives.PdfTextAlignment ToPxa(this Canvas.Core.Primitives.PdfTextAlignment alignment) =>
        Enum.Parse<PXA.Core.Primitives.PdfTextAlignment>(alignment.ToString());

    public static Canvas.Core.Primitives.PdfVerticalAlignment ToCanvas(this PXA.Core.Primitives.PdfVerticalAlignment alignment) =>
        Enum.Parse<Canvas.Core.Primitives.PdfVerticalAlignment>(alignment.ToString());

    public static PXA.Core.Primitives.PdfVerticalAlignment ToPxa(this Canvas.Core.Primitives.PdfVerticalAlignment alignment) =>
        Enum.Parse<PXA.Core.Primitives.PdfVerticalAlignment>(alignment.ToString());

    public static Canvas.Core.Capabilities.RendererFeature ToCanvas(this PXA.Core.Capabilities.RendererFeature feature) =>
        Enum.Parse<Canvas.Core.Capabilities.RendererFeature>(feature.ToString());

    public static PXA.Core.Capabilities.RendererFeature ToPxa(this Canvas.Core.Capabilities.RendererFeature feature) =>
        Enum.Parse<PXA.Core.Capabilities.RendererFeature>(feature.ToString());

    public static Canvas.Core.Capabilities.UnsupportedFeatureFallbackMode ToCanvas(
        this PXA.Core.Capabilities.UnsupportedFeatureFallbackMode mode) =>
        Enum.Parse<Canvas.Core.Capabilities.UnsupportedFeatureFallbackMode>(mode.ToString());

    public static PXA.Core.Capabilities.UnsupportedFeatureFallbackMode ToPxa(
        this Canvas.Core.Capabilities.UnsupportedFeatureFallbackMode mode) =>
        Enum.Parse<PXA.Core.Capabilities.UnsupportedFeatureFallbackMode>(mode.ToString());

    public static PXA.Core.Abstractions.ExporterCapabilities ToPxa(
        this Canvas.Core.Abstractions.IExporterCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return new PXA.Core.Abstractions.ExporterCapabilities(
            capabilities.SupportsMultiPage,
            capabilities.SupportsImages,
            capabilities.SupportsRichText,
            capabilities.SupportsFormFields);
    }

    public static Canvas.Core.Abstractions.ExporterCapabilities ToCanvas(
        this PXA.Core.Abstractions.IExporterCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return new Canvas.Core.Abstractions.ExporterCapabilities(
            capabilities.SupportsMultiPage,
            capabilities.SupportsImages,
            capabilities.SupportsRichText,
            capabilities.SupportsFormFields);
    }

    private static TTarget Convert<TSource, TTarget>(TSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<TTarget>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to convert {typeof(TSource).Name} to {typeof(TTarget).Name}.");
    }
}
