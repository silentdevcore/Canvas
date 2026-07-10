using PXA.Core.Contracts;

namespace PXA.FileImporter;

/// <summary>
/// Power Dox Automation facade for DOCX import.
/// </summary>
public sealed class DocxFileImporter : IFileImporter
{
    private readonly Canvas.FileImporter.Docx.DocxFileImporter inner = new();

    public IReadOnlyList<string> SupportedExtensions => inner.SupportedExtensions;

    public async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        (await inner.ImportAsync(stream, name)).ToPxa();
}

/// <summary>
/// Power Dox Automation facade for PPTX import.
/// </summary>
public sealed class PptxFileImporter : IFileImporter
{
    private readonly Canvas.FileImporter.Pptx.PptxFileImporter inner = new();

    public IReadOnlyList<string> SupportedExtensions => inner.SupportedExtensions;

    public async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        (await inner.ImportAsync(stream, name)).ToPxa();
}
