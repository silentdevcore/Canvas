using Canvas.Pdf;
using PXA.Core.Contracts;
using PXA.WebApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using PXA.FileImporter.ImageOcr;
using System.Diagnostics;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/document")]
[Route("api/pxa/document")]
public sealed class ImageConversionController : ControllerBase
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tif",
        ".tiff",
        ".bmp",
        ".webp",
    };

    private readonly ImageToPdfConverter _converter;
    private readonly PdfFontLoader? _fontLoader;
    private readonly ILogger<ImageConversionController>? _logger;

    public ImageConversionController(
        ImageToPdfConverter converter,
        PdfFontLoader? fontLoader = null,
        ILogger<ImageConversionController>? logger = null)
    {
        _converter = converter;
        _fontLoader = fontLoader;
        _logger = logger;
    }

    [HttpPost("convert-image-to-pdf")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(415)]
    public async Task<IActionResult> ConvertImageToPdf(
        IFormFile? file,
        [FromQuery] bool debug = false,
        [FromForm] string? languages = null,
        [FromForm] double? pageWidthPt = null,
        [FromForm] double? pageHeightPt = null,
        [FromForm] bool includeBackgroundImage = true,
        [FromForm] bool includeDiagnostics = false,
        [FromForm] bool includeDebugOverlay = false,
        [FromForm] bool includeOcrPages = false,
        [FromForm] bool enablePreprocessing = false,
        [FromForm] bool preprocessGrayscale = true,
        [FromForm] bool preprocessContrast = true,
        [FromForm] bool preprocessBinarize = false,
        [FromForm] double? lowConfidenceThreshold = null,
        [FromForm] int? maxOcrRuntimeSeconds = null,
        [FromForm] string? layoutMode = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "An image file is required." });

        var extension = Path.GetExtension(file.FileName);
        if (!SupportedExtensions.Contains(extension))
        {
            return StatusCode(415, new
            {
                error = "Unsupported image format.",
                supportedFormats = SupportedExtensions.Order().ToArray(),
            });
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            _logger?.LogInformation(
                "Starting image OCR import for {FileName} ({FileLength} bytes), debug={Debug}, diagnostics={Diagnostics}, overlay={Overlay}, ocrPages={OcrPages}",
                file.FileName,
                file.Length,
                debug,
                includeDiagnostics,
                includeDebugOverlay,
                includeOcrPages);

            await using var stream = file.OpenReadStream();
            var result = await _converter.ConvertAsync(
                stream,
                file.FileName,
                new ImageToPdfConversionOptions
                {
                    Languages = string.IsNullOrWhiteSpace(languages) ? "deu+eng" : languages,
                    PageWidthPt = pageWidthPt,
                    PageHeightPt = pageHeightPt,
                    IncludeBackgroundImage = includeBackgroundImage,
                    IncludeDiagnostics = includeDiagnostics || debug,
                    IncludeDebugOverlay = includeDebugOverlay,
                    EnablePreprocessing = enablePreprocessing,
                    PreprocessGrayscale = preprocessGrayscale,
                    PreprocessContrast = preprocessContrast,
                    PreprocessBinarize = preprocessBinarize,
                    LowConfidenceThreshold = lowConfidenceThreshold ?? 0.50,
                    MaxOcrRuntimeSeconds = Math.Clamp(maxOcrRuntimeSeconds ?? 45, 5, 180),
                    LayoutMode = string.IsNullOrWhiteSpace(layoutMode) ? "structured" : layoutMode,
                },
                cancellationToken);
            stopwatch.Stop();
            _logger?.LogInformation(
                "Finished image OCR import for {FileName} in {ElapsedMs} ms with {WordCount} words and {ElementCount} elements",
                file.FileName,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 0),
                result.Diagnostics.WordCount,
                result.Design.Pages.Sum(page => page.Elements.Count));

            if (debug)
            {
                return Ok(new
                {
                    result.Design,
                    result.Diagnostics,
                    result.Warnings,
                    OcrPages = includeOcrPages ? result.OcrPages : null,
                    DebugOverlay = result.DebugOverlayPng is null
                        ? null
                        : $"data:image/png;base64,{Convert.ToBase64String(result.DebugOverlayPng)}",
                });
            }

            var document = DesignJsonMapper.MapToPdfDocument(result.Design, _fontLoader);
            var pdfBytes = document.ToBytes();
            var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(file.FileName));
            return File(pdfBytes, "application/pdf", $"{safeName}.pdf");
        }
        catch (OcrLanguageDataMissingException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (OcrNativeDependencyMissingException ex)
        {
            return BadRequest(new { error = ex.Message, detail = ex.InnerException?.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static string SanitizeFileName(string? name)
    {
        var safe = string.IsNullOrWhiteSpace(name) ? "image-ocr" : name;
        foreach (var c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        return safe.Trim();
    }
}
