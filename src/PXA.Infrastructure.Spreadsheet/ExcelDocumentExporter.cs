using PXA.Core.Contracts;

namespace PXA.Infrastructure.Spreadsheet;

public sealed class ExcelDocumentExporter
{
    private readonly Canvas.Infrastructure.Spreadsheet.ExcelDocumentExporter inner = new();

    public string FormatKey => inner.FormatKey;

    public string MimeType => inner.MimeType;

    public string FileExtension => inner.FileExtension;

    public ExporterCapabilities Capabilities => ExporterCapabilities.FromCanvas(inner.Capabilities);

    public byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
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
