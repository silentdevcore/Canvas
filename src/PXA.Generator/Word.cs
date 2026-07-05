using Canvas.Core.Primitives;
using Canvas.Infrastructure.Word;
using PXA.Core.Contracts;

namespace PXA.Generator;

/// <summary>
/// Additive Power Dox Automation facade for Word document generation.
/// </summary>
public static class Word
{
    /// <summary>
    /// Exports a Canvas design document to DOCX bytes using the current Word exporter.
    /// </summary>
    public static byte[] Export(DesignExportDto design, ExportOptions? options = null) =>
        new WordDocumentExporter().Export(design.ToCanvas(), options?.ToCanvas());
}
