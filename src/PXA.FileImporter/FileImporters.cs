using PXA.Core.Contracts;

namespace PXA.FileImporter;

/// <summary>
/// Power Dox Automation facade for legacy Word 97-2003 document import.
/// </summary>
public sealed class DocFileImporter : IFileImporter
{
    private readonly Canvas.FileImporter.Doc.DocFileImporter inner = new();

    public IReadOnlyList<string> SupportedExtensions => inner.SupportedExtensions;

    public async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        (await inner.ImportAsync(stream, name)).ToPxa();
}

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
/// Power Dox Automation facade for raster image import.
/// </summary>
public sealed class ImageFileImporter : IFileImporter
{
    private readonly Canvas.FileImporter.Image.ImageFileImporter inner = new();

    public IReadOnlyList<string> SupportedExtensions => inner.SupportedExtensions;

    public async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        (await inner.ImportAsync(stream, name)).ToPxa();
}

/// <summary>
/// Power Dox Automation facade for ODT import.
/// </summary>
public sealed class OdtFileImporter : IFileImporter
{
    private readonly Canvas.FileImporter.Odt.OdtFileImporter inner = new();

    public IReadOnlyList<string> SupportedExtensions => inner.SupportedExtensions;

    public async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        (await inner.ImportAsync(stream, name)).ToPxa();
}

/// <summary>
/// Power Dox Automation facade for PDF-to-design import.
/// </summary>
public sealed class PdfFileImporter : IFileImporter
{
    private readonly Canvas.FileImporter.Pdf.PdfFileImporter inner = new();

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

/// <summary>
/// Power Dox Automation facade for SVG import.
/// </summary>
public sealed class SvgFileImporter : IFileImporter
{
    private readonly Canvas.FileImporter.Svg.SvgFileImporter inner = new();

    public IReadOnlyList<string> SupportedExtensions => inner.SupportedExtensions;

    public async Task<DesignExportDto> ImportAsync(Stream stream, string? name = null) =>
        (await inner.ImportAsync(stream, name)).ToPxa();
}
