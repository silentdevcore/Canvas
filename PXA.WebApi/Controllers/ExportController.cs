using System.IO.Compression;
using PXA.Pdf;
using PXA.Application.UseCases;
using PXA.WebApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using PxaDesignExportDto = PXA.Core.Contracts.DesignExportDto;
using PxaExportOptions = PXA.Core.Contracts.ExportOptions;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/export")]
[Route("api/pxa/export")]
public class ExportController : ControllerBase
{
    private readonly ExportDocumentUseCase _useCase;
    private readonly PdfFontLoader? _fontLoader;

    public ExportController(ExportDocumentUseCase useCase, PdfFontLoader? fontLoader = null)
    {
        _useCase = useCase;
        _fontLoader = fontLoader;
    }

    /// <summary>
    /// Exports a design to the specified format and returns the file.
    /// For PDF format, an optional <c>language</c> query parameter applies localized property values.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(415)]
    public IActionResult Export(
        [FromQuery] string format,
        [FromQuery] float? dpi,
        [FromQuery] int? quality,
        [FromQuery] string? language,
        [FromBody] PxaDesignExportDto design)
    {
        if (string.IsNullOrWhiteSpace(format))
            return BadRequest(new { error = "Query parameter 'format' is required." });

        if (design is null)
            return BadRequest(new { error = "Request body is required." });

        // For PDF: query-param language takes precedence; fall back to targetLanguage in the JSON body.
        var effectiveLang = language ?? design.PageSettings?.TargetLanguage;
        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(effectiveLang))
        {
            var doc = DesignJsonMapper.MapToPdfDocument(design, _fontLoader, effectiveLang);
            var bytes = doc.ToBytes(DesignJsonMapper.BuildSaveOptions(design));
            var safeName = SanitizeFileName(design.Name);
            return File(bytes, "application/pdf", $"{safeName}-{effectiveLang}.pdf");
        }

        var options = (dpi.HasValue || quality.HasValue) ? new PxaExportOptions(dpi, quality) : null;

        try
        {
            var result = _useCase.Execute(new ExportDocumentRequest(design, format, options));
            return File(result.Data, result.MimeType, result.FileName);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(415, new
            {
                error = ex.Message,
                supportedFormats = _useCase.GetSupportedFormats()
                    .Select(f => f.Key)
                    .Order()
                    .ToList()
            });
        }
    }

    /// <summary>
    /// Exports one PDF per active language and returns a ZIP archive.
    /// Each file is named <c>{documentName}-{lang}.pdf</c>.
    /// </summary>
    [HttpPost("multilanguage")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult ExportMultiLanguage([FromBody] PxaDesignExportDto design)
    {
        if (design is null)
            return BadRequest(new { error = "Request body is required." });

        var langs = design.PageSettings?.ActiveLanguages;
        if (langs is null || langs.Count == 0)
            return BadRequest(new { error = "No active languages configured on the design." });

        var safeName = SanitizeFileName(design.Name);

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var lang in langs)
            {
                var doc = DesignJsonMapper.MapToPdfDocument(design, _fontLoader, lang);
                var pdfBytes = doc.ToBytes(DesignJsonMapper.BuildSaveOptions(design));
                var entry = archive.CreateEntry($"{safeName}-{lang}.pdf", CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(pdfBytes);
            }
        }

        zipStream.Position = 0;
        return File(zipStream.ToArray(), "application/zip", $"{safeName}-multilanguage.zip");
    }

    private static string SanitizeFileName(string? name)
    {
        var n = string.IsNullOrWhiteSpace(name) ? "document" : name;
        foreach (var c in Path.GetInvalidFileNameChars())
            n = n.Replace(c, '_');
        return n.Trim();
    }

    /// <summary>
    /// Returns a list of all supported export formats.
    /// </summary>
    [HttpGet("formats")]
    [ProducesResponseType(typeof(IEnumerable<object>), 200)]
    public IActionResult GetFormats()
    {
        var formats = _useCase.GetSupportedFormats()
            .OrderBy(f => f.Key)
            .Select(f => new
            {
                key               = f.Key,
                mimeType          = f.MimeType,
                extension         = f.Extension,
                supportsMultiPage  = f.Capabilities.SupportsMultiPage,
                supportsImages     = f.Capabilities.SupportsImages,
                supportsRichText   = f.Capabilities.SupportsRichText,
                supportsFormFields = f.Capabilities.SupportsFormFields,
            });

        return Ok(formats);
    }
}
