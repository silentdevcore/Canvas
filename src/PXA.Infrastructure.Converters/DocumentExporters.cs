using PXA.Core.Contracts;

namespace PXA.Infrastructure.Converters;

public abstract class DocumentExporter
{
    public abstract string FormatKey { get; }
    public abstract string MimeType { get; }
    public abstract string FileExtension { get; }
    public abstract ExporterCapabilities Capabilities { get; }
    public abstract byte[] Export(DesignExportDto design);

    public virtual byte[] Export(DesignExportDto design, ExportOptions? options) => Export(design);

    protected static ExporterCapabilities FromCanvas(Canvas.Core.Abstractions.IExporterCapabilities capabilities) => new(
        capabilities.SupportsMultiPage,
        capabilities.SupportsImages,
        capabilities.SupportsRichText,
        capabilities.SupportsFormFields);
}

public sealed record ExporterCapabilities(
    bool SupportsMultiPage = true,
    bool SupportsImages = true,
    bool SupportsRichText = true,
    bool SupportsFormFields = true);

public sealed class HtmlDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.HtmlDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
}

public sealed class MarkdownDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.MarkdownDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
}

public sealed class CsvDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.CsvDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
}

public sealed class XmlDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.XmlDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
}

public sealed class SvgDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.SvgDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
}

public sealed class ImageDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.ImageDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
    public override byte[] Export(DesignExportDto design, ExportOptions? options) =>
        inner.Export(design.ToCanvas(), options?.ToCanvas());
}

public sealed class JpegDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.JpegDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
    public override byte[] Export(DesignExportDto design, ExportOptions? options) =>
        inner.Export(design.ToCanvas(), options?.ToCanvas());
}

public sealed class TiffDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.TiffDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
    public override byte[] Export(DesignExportDto design, ExportOptions? options) =>
        inner.Export(design.ToCanvas(), options?.ToCanvas());
}

public sealed class OdtDocumentExporter : DocumentExporter
{
    private readonly Canvas.Infrastructure.Converters.OdtDocumentExporter inner = new();
    public override string FormatKey => inner.FormatKey;
    public override string MimeType => inner.MimeType;
    public override string FileExtension => inner.FileExtension;
    public override ExporterCapabilities Capabilities => FromCanvas(inner.Capabilities);
    public override byte[] Export(DesignExportDto design) => inner.Export(design.ToCanvas());
    public override byte[] Export(DesignExportDto design, ExportOptions? options) =>
        inner.Export(design.ToCanvas(), options?.ToCanvas());
}
