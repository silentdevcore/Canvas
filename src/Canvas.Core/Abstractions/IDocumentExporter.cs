using Canvas.Core.Contracts;

namespace Canvas.Core.Abstractions;

public interface IDocumentExporter
{
    string FormatKey { get; }
    string MimeType { get; }
    string FileExtension { get; }
    IExporterCapabilities Capabilities => new ExporterCapabilities();
    byte[] Export(DesignExportDto design);
    byte[] Export(DesignExportDto design, ExportOptions? options) => Export(design);
}
