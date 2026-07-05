using PXA.Core.Contracts;

namespace PXA.Infrastructure.Word;

/// <summary>
/// Power Dox Automation facade for DOCX export.
/// </summary>
public sealed class WordDocumentExporter
{
    private readonly Canvas.Infrastructure.Word.WordDocumentExporter inner = new();

    public string FormatKey => inner.FormatKey;

    public string MimeType => inner.MimeType;

    public string FileExtension => inner.FileExtension;

    public ExporterCapabilities Capabilities => ExporterCapabilities.FromCanvas(inner.Capabilities);

    public byte[] Export(DesignExportDto design) => Export(design, null);

    public byte[] Export(DesignExportDto design, ExportOptions? options) =>
        inner.Export(design.ToCanvas(), options?.ToCanvas());
}

public sealed record ExporterCapabilities(
    bool SupportsMultiPage = true,
    bool SupportsImages = true,
    bool SupportsRichText = true,
    bool SupportsFormFields = true)
{
    internal static ExporterCapabilities FromCanvas(Canvas.Core.Abstractions.IExporterCapabilities capabilities) => new(
        capabilities.SupportsMultiPage,
        capabilities.SupportsImages,
        capabilities.SupportsRichText,
        capabilities.SupportsFormFields);
}
