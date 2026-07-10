using PXA.Core.Contracts;
using PXA.Infrastructure.Word;

namespace PXA.Generator;

/// <summary>
/// Additive Power Dox Automation facade for Word document generation.
/// </summary>
public static class Word
{
    /// <summary>
    /// Exports a PXA design document to DOCX bytes using the current Word exporter.
    /// </summary>
    public static byte[] Export(DesignExportDto design, ExportOptions? options = null) =>
        new WordDocumentExporter().Export(design, options);
}
