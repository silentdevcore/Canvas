using PVAD.Core.Contracts;
using PVAD.Infrastructure.Word;

namespace PVAD.Generator;

/// <summary>
/// Additive PVADE facade for Word document generation.
/// </summary>
public static class Word
{
    /// <summary>
    /// Exports a PVAD design document to DOCX bytes using the current Word exporter.
    /// </summary>
    public static byte[] Export(DesignExportDto design, ExportOptions? options = null) =>
        new WordDocumentExporter().Export(design, options);
}
