using Canvas.Application.UseCases;
using Canvas.Core.Contracts;
using Canvas.Infrastructure.Converters;
using Canvas.Infrastructure.Word;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Canvas.WebApi.Controllers;

/// <summary>
/// Document-level operations: find-and-replace, clone, and page extraction.
/// All endpoints accept and return <see cref="DesignExportDto"/> JSON payloads.
/// </summary>
[ApiController]
[Route("api/document")]
public class DocumentOpsController : ControllerBase
{
    private readonly FindAndReplaceUseCase _findReplace;
    private readonly CloneTemplateUseCase  _clone;
    private readonly ExtractPagesUseCase   _extractPages;

    public DocumentOpsController(
        FindAndReplaceUseCase findReplace,
        CloneTemplateUseCase  clone,
        ExtractPagesUseCase   extractPages)
    {
        _findReplace  = findReplace;
        _clone        = clone;
        _extractPages = extractPages;
    }

    /// <summary>
    /// Replaces all occurrences of a search string in every text-bearing element
    /// across all pages and shared elements.
    /// </summary>
    /// <remarks>
    /// Supports plain-text, case-insensitive, whole-word, and regular-expression modes.
    /// Returns the modified design and a replacement count.
    /// </remarks>
    [HttpPost("find-replace")]
    [ProducesResponseType(typeof(FindAndReplaceResult), 200)]
    [ProducesResponseType(400)]
    public IActionResult FindAndReplace([FromBody] FindAndReplaceApiRequest body)
    {
        if (body?.Design is null)
            return BadRequest(new { error = "Design is required." });
        if (string.IsNullOrWhiteSpace(body.Find))
            return BadRequest(new { error = "find is required." });

        try
        {
            var result = _findReplace.Execute(new FindAndReplaceRequest
            {
                Design        = body.Design,
                Find          = body.Find,
                Replace       = body.Replace ?? "",
                CaseSensitive = body.CaseSensitive,
                WholeWord     = body.WholeWord,
                UseRegex      = body.UseRegex,
            });
            return Ok(result);
        }
        catch (System.Text.RegularExpressions.RegexParseException ex)
        {
            return BadRequest(new { error = $"Invalid regex: {ex.Message}" });
        }
    }

    /// <summary>
    /// Creates a deep clone of the supplied design with a new ID and optional new name.
    /// </summary>
    [HttpPost("clone")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult Clone([FromBody] CloneApiRequest body)
    {
        if (body?.Design is null)
            return BadRequest(new { error = "Design is required." });

        var clone = _clone.Execute(new CloneDesignRequest
        {
            Design  = body.Design,
            NewName = body.NewName,
        });
        return Ok(clone);
    }

    /// <summary>
    /// Extracts a subset of pages (1-based numbers) from a design into a new design document.
    /// </summary>
    [HttpPost("extract-pages")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public IActionResult ExtractPages([FromBody] ExtractPagesApiRequest body)
    {
        if (body?.Design is null)
            return BadRequest(new { error = "Design is required." });
        if (body.PageNumbers is null || body.PageNumbers.Count == 0)
            return BadRequest(new { error = "At least one pageNumber is required." });

        try
        {
            var result = _extractPages.Execute(new ExtractPagesRequest
            {
                Design      = body.Design,
                PageNumbers = body.PageNumbers,
                NewName     = body.NewName,
            });
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Imports a PDF file and converts it into a Canvas design.
    /// Each PDF page becomes a Canvas page; text words and images are extracted
    /// with their approximate positions and sizes.
    /// </summary>
    [HttpPost("import-pdf")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportPdf(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });

        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            var design = PdfImporter.Import(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse PDF: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports a PDF via PdfToSvg.NET (SVG intermediate) for comparison with the PdfPig importer.
    /// Route: POST /api/document/import-pdf-svg
    /// </summary>
    [HttpPost("import-pdf-svg")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportPdfSvg(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });

        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            var design = SvgPdfImporter.Import(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse PDF (SVG): {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports a PDF using the Canvas.Importer low-level engine (own tokenizer, object graph,
    /// and content stream interpreter). Returns a Canvas design with text, shape, and image
    /// elements derived from the PDF's raw graphics scene graph.
    /// Route: POST /api/document/import-pdf-engine
    /// </summary>
    [HttpPost("import-pdf-engine")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportPdfEngine(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });

        if (!file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only PDF files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            var design = await CanvasImporterPdfImporter.ImportAsync(
                stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse PDF (Engine): {ex.Message}" });
        }
    }

    /// <summary>
    /// Debug: returns element statistics from the Canvas.Importer scene graph for one page.
    /// Query param: page=1 (1-based).
    /// </summary>
    [HttpPost("debug-pdf-engine")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DebugPdfEngine(IFormFile? file, [FromQuery] int page = 1)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });
        try
        {
            await using var stream = file.OpenReadStream();
            var doc = await new Canvas.Importer.PdfImporter().LoadAsync(stream);
            var p   = doc.Pages.ElementAtOrDefault(page - 1);
            if (p is null)
                return NotFound(new { error = $"Page {page} not found.", pageCount = doc.Pages.Count });

            // Flatten scene graph to count all elements recursively
            IEnumerable<Canvas.Importer.Graphics.PdfGraphicsElement> Flatten(
                IEnumerable<Canvas.Importer.Graphics.PdfGraphicsElement> els)
                => els.SelectMany(e => e is Canvas.Importer.Graphics.PdfGroupElement g
                    ? Flatten(g.Children).Prepend(e) : new[] { e });

            var all   = Flatten(p.GraphicsObjects).ToList();
            var texts = all.OfType<Canvas.Importer.Graphics.PdfTextElement>().Take(30).Select(t => new {
                text = t.Text,
                fontSize = t.FontSize,
                font = t.FontResourceName,
                x = Math.Round(t.Transform.E, 1),
                y = Math.Round(t.Transform.F, 1),
            }).ToList();

            return Ok(new {
                pageCount  = doc.Pages.Count,
                mediaBox   = p.MediaBox,
                totalElements = all.Count,
                byType = new {
                    text    = all.OfType<Canvas.Importer.Graphics.PdfTextElement>().Count(),
                    path    = all.OfType<Canvas.Importer.Graphics.PdfPathElement>().Count(),
                    image   = all.OfType<Canvas.Importer.Graphics.PdfImageElement>().Count(),
                    shading = all.OfType<Canvas.Importer.Graphics.PdfShadingElement>().Count(),
                    group   = all.OfType<Canvas.Importer.Graphics.PdfGroupElement>().Count(),
                },
                sampleTexts = texts,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Returns the raw SVG string for one page of a PDF (via PdfToSvg.NET).
    /// Use this to inspect the SVG structure when debugging the SVG importer.
    /// Query param: page=1 (1-based).
    /// </summary>
    [HttpPost("debug-pdf-svg")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DebugPdfSvg(IFormFile? file, [FromQuery] int page = 1)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A PDF file is required." });
        try
        {
            await using var stream = file.OpenReadStream();
            using var pdf = PdfToSvg.PdfDocument.Open(stream);
            int pageCount = pdf.Pages.Count;
            var p = pdf.Pages.ElementAtOrDefault(page - 1);
            if (p is null)
                return NotFound(new { error = $"Page {page} not found.", pageCount });

            string svg = p.ToSvgString();

            // Count element types so we can see what PdfToSvg actually extracted
            var doc  = System.Xml.Linq.XDocument.Parse(svg);
            var root = doc.Root!;
            var stats = new[] { "rect","path","line","circle","ellipse","image","text","tspan","g","use" }
                .ToDictionary(tag => tag, tag => root.Descendants()
                    .Count(e => e.Name.LocalName == tag));

            // Return SVG + debug stats as a JSON envelope so the caller sees both
            return Ok(new
            {
                pageCount,
                svgLength   = svg.Length,
                elementStats = stats,
                svg          = svg,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    /// <summary>
    /// Imports a legacy Word 97-2003 .doc file and converts it into a Canvas design.
    /// Paragraphs are extracted with basic font metadata and stacked as Text elements.
    /// </summary>
    [HttpPost("import-doc")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportDoc(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A .doc file is required." });

        if (!file.FileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .doc (Word 97-2003) files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            var design = DocImporter.Import(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse DOC: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports an OOXML .docx file and converts it into a Canvas design.
    /// Paragraphs map to Text elements with typography; inline images are extracted
    /// as base64 data-URIs; tables become Table elements.
    /// </summary>
    [HttpPost("import-docx")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportDocx(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A .docx file is required." });

        if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .docx (Word Open XML) files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            var design = DocxImporter.Import(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse DOCX: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports an OpenDocument Text (.odt) file and converts it into a Canvas design.
    /// Paragraphs and headings are extracted with style metadata; draw:frame images
    /// are converted to Image elements.
    /// </summary>
    [HttpPost("import-odt")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportOdt(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "An .odt file is required." });

        if (!file.FileName.EndsWith(".odt", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .odt (OpenDocument Text) files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            var design = OdtImporter.Import(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse ODT: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports a raster image file (PNG, JPG, JPEG, GIF, WebP, BMP, TIFF) and converts it
    /// into a Canvas design. A single page is created whose dimensions match the image's
    /// native pixel size, with one full-page Image element containing the image as a
    /// base64 data-URI.
    /// </summary>
    [HttpPost("import-image")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportImage(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "An image file is required." });

        try
        {
            await using var stream = file.OpenReadStream();
            var design = ImageImporter.Import(stream, file.FileName);
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not decode image: {ex.Message}" });
        }
    }

    /// <summary>
    /// Applies an X.509 digital signature (OOXML XML-DSig) to a DOCX file.
    /// Accepts a multipart form with a <c>docx</c> file and a <c>certificate</c>
    /// PFX/P12 file. An optional <c>password</c> field unlocks the PFX.
    /// Returns the signed DOCX as application/octet-stream.
    /// </summary>
    [HttpPost("sign-docx")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SignDocx(
        IFormFile? docx,
        IFormFile? certificate,
        [Microsoft.AspNetCore.Mvc.FromForm] string? password)
    {
        if (docx is null || docx.Length == 0)
            return BadRequest(new { error = "A .docx file is required." });
        if (certificate is null || certificate.Length == 0)
            return BadRequest(new { error = "A PFX/P12 certificate file is required." });
        if (!docx.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .docx files can be signed." });

        try
        {
            await using var docxStream = docx.OpenReadStream();
            await using var certStream = certificate.OpenReadStream();
            using var certMs = new MemoryStream();
            await certStream.CopyToAsync(certMs);

            var signedBytes = DigitalSigningService.SignDocx(docxStream, certMs.ToArray(), password);
            var outName     = Path.GetFileNameWithoutExtension(docx.FileName) + "_signed.docx";
            return File(signedBytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                outName);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            return BadRequest(new { error = $"Certificate error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Signing failed: {ex.Message}" });
        }
    }
}

// ── Request models ───────────────────────────────────────────────────────────

public sealed class FindAndReplaceApiRequest
{
    public required DesignExportDto Design { get; set; }
    public required string Find { get; set; }
    public string? Replace { get; set; }
    public bool CaseSensitive { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }
}

public sealed class CloneApiRequest
{
    public required DesignExportDto Design { get; set; }
    public string? NewName { get; set; }
}

public sealed class ExtractPagesApiRequest
{
    public required DesignExportDto Design { get; set; }
    public required List<int> PageNumbers { get; set; }
    public string? NewName { get; set; }
}
