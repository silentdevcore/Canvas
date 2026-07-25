using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using PXA.Application.UseCases;
using PXA.FileImporter;
using PXA.FileImporter.ImageAnalysis;
using PXA.FileImporter.ImageAnalysis.Analysis;
using PXA.Importer.Analysis;
using PXA.Importer.Debugging;

namespace PXA.WebApi.Controllers;

/// <summary>
/// Document-level operations: find-and-replace, clone, and page extraction.
/// All endpoints accept and return <see cref="DesignExportDto"/> JSON payloads.
/// </summary>
[ApiController]
[Route("api/document")]
[Route("api/pxa/document")]
public class DocumentOpsController : ControllerBase
{
    private const long MaxMarkdownUploadBytes = 4L * 1024 * 1024;

    private readonly FindAndReplaceUseCase      _findReplace;
    private readonly CloneTemplateUseCase       _clone;
    private readonly ExtractPagesUseCase        _extractPages;
    private readonly IEnumerable<IFileImporter> _importers;
    private readonly ImageAnalysisFileImporter  _imageAnalysis;

    public DocumentOpsController(
        FindAndReplaceUseCase findReplace,
        CloneTemplateUseCase  clone,
        ExtractPagesUseCase   extractPages,
        IEnumerable<IFileImporter> importers,
        ImageAnalysisFileImporter imageAnalysis)
    {
        _findReplace   = findReplace;
        _clone         = clone;
        _extractPages  = extractPages;
        _importers     = importers;
        _imageAnalysis = imageAnalysis;
    }

    private IFileImporter Importer(string ext) =>
        _importers.FirstOrDefault(i => i.SupportedExtensions.Contains(ext))
        ?? throw new InvalidOperationException($"No importer registered for .{ext} files.");

    /// <summary>
    /// Replaces all occurrences of a search string in every text-bearing element
    /// across all pages and shared elements.
    /// </summary>
    /// <remarks>
    /// Supports plain-text, case-insensitive, whole-word, and regular-expression modes.
    /// Returns the modified design and a replacement count.
    /// </remarks>
    [HttpPost("find-replace")]
    [ProducesResponseType(typeof(FindAndReplaceApiResponse), 200)]
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
            return Ok(new FindAndReplaceApiResponse
            {
                Design = result.Design,
                ReplacementCount = result.ReplacementCount,
                AffectedElementIds = result.AffectedElementIds,
            });
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
    /// Imports a PDF using the PXA.Importer low-level engine (own tokenizer, object graph,
    /// and content stream interpreter). Returns a PXA-compatible design with text, shape, and image
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
            var design = await Importer("pdf").ImportAsync(
                stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse PDF (Engine): {ex.Message}" });
        }
    }

    /// <summary>
    /// Debug: returns element statistics from the PXA.Importer scene graph for one page.
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
            var doc = await new PXA.Importer.PdfImporter().LoadAsync(stream);
            var p   = doc.Pages.ElementAtOrDefault(page - 1);
            if (p is null)
                return NotFound(new { error = $"Page {page} not found.", pageCount = doc.Pages.Count });

            // Flatten scene graph to count all elements recursively
            IEnumerable<PXA.Importer.Graphics.PdfGraphicsElement> Flatten(
                IEnumerable<PXA.Importer.Graphics.PdfGraphicsElement> els)
                => els.SelectMany(e => e is PXA.Importer.Graphics.PdfGroupElement g
                    ? Flatten(g.Children).Prepend(e) : new[] { e });

            var all   = Flatten(p.GraphicsObjects).ToList();
            var scenePage = new SceneGraphEngine().BuildPage(page - 1, p);
            var overlays = new PdfDebugOverlayBuilder().Build(scenePage);
            var texts = all.OfType<PXA.Importer.Graphics.PdfTextElement>().Take(30).Select(t => new {
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
                    text    = all.OfType<PXA.Importer.Graphics.PdfTextElement>().Count(),
                    path    = all.OfType<PXA.Importer.Graphics.PdfPathElement>().Count(),
                    image   = all.OfType<PXA.Importer.Graphics.PdfImageElement>().Count(),
                    shading = all.OfType<PXA.Importer.Graphics.PdfShadingElement>().Count(),
                    group   = all.OfType<PXA.Importer.Graphics.PdfGroupElement>().Count(),
                },
                sceneGraph = new {
                    layerCount = scenePage.Layers.Count,
                    primitiveCount = scenePage.Layers.SelectMany(l => l.Objects).Count(),
                    visualGroupCount = scenePage.VisualGroups.Count,
                    lineCount = scenePage.ReadingOrder?.Lines.Count ?? 0,
                    paragraphCount = scenePage.ReadingOrder?.Paragraphs.Count ?? 0,
                    columnCount = scenePage.ReadingOrder?.Columns.Count ?? 0,
                    layoutNodeCount = CountLayoutNodes(scenePage.Layout),
                    debugOverlayCount = overlays.Count,
                    classifications = scenePage.Layers
                        .SelectMany(l => l.Objects)
                        .SelectMany(FlattenPrimitive)
                        .GroupBy(o => o.Classification.ToString())
                        .ToDictionary(g => g.Key, g => g.Count()),
                    groups = scenePage.VisualGroups.Take(20).Select(g => new {
                        g.Kind,
                        g.Confidence,
                        g.Bounds,
                        objectCount = g.Objects.Count,
                    }),
                    readingOrderSample = scenePage.ReadingOrder?.Lines.Take(20).Select(l => new {
                        l.Order,
                        l.Text,
                        l.Bounds,
                    }),
                },
                sampleTexts = texts,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, type = ex.GetType().Name });
        }
    }

    private static IEnumerable<PrimitiveObject> FlattenPrimitive(PrimitiveObject primitive)
    {
        yield return primitive;
        foreach (var child in primitive.Children.SelectMany(FlattenPrimitive))
        {
            yield return child;
        }
    }

    private static int CountLayoutNodes(SemanticLayoutNode? node)
    {
        return node is null ? 0 : 1 + node.Children.Sum(CountLayoutNodes);
    }

    /// <summary>
    /// Imports a legacy Word 97-2003 .doc file and converts it into a PXA design.
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
            var design = await Importer("doc").ImportAsync(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse DOC: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports an OOXML .docx file and converts it into a PXA design.
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
            var design = await Importer("docx").ImportAsync(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse DOCX: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports a Markdown (.md/.markdown) file and converts it into a PXA design.
    /// Headings, paragraphs (with bold/italic/links), tables, lists, task-list
    /// checkboxes, blockquotes, horizontal rules, code blocks, and images are
    /// each converted into the corresponding PXA element type.
    /// </summary>
    [HttpPost("import-markdown")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxMarkdownUploadBytes)]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(413)]
    public async Task<IActionResult> ImportMarkdown(
        IFormFile? file,
        [FromForm] string? assetBaseUri = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A .md file is required." });

        if (!file.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .md/.markdown (Markdown) files are accepted." });

        if (file.Length > MaxMarkdownUploadBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Markdown file is too large.",
                Detail = $"The maximum supported upload size is {MaxMarkdownUploadBytes / (1024 * 1024)} MiB.",
            });

        Uri? parsedAssetBaseUri = null;
        if (!string.IsNullOrWhiteSpace(assetBaseUri) &&
            (!Uri.TryCreate(assetBaseUri, UriKind.Absolute, out parsedAssetBaseUri) ||
             (parsedAssetBaseUri.Scheme != Uri.UriSchemeHttp &&
              parsedAssetBaseUri.Scheme != Uri.UriSchemeHttps) ||
             !string.IsNullOrEmpty(parsedAssetBaseUri.UserInfo)))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Markdown asset base URI.",
                Detail = "The asset base URI must be an absolute HTTP(S) URL without embedded credentials.",
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var importer = Importer("md");
            var design = importer is MarkdownFileImporter markdownImporter
                ? await markdownImporter.ImportAsync(
                    stream,
                    Path.GetFileNameWithoutExtension(file.FileName),
                    parsedAssetBaseUri,
                    HttpContext.RequestAborted)
                : await importer.ImportAsync(
                    stream,
                    Path.GetFileNameWithoutExtension(file.FileName),
                    HttpContext.RequestAborted);
            return Ok(design);
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Markdown import rejected.",
                Detail = "The document exceeds supported Markdown complexity limits.",
            });
        }
        catch (Exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Could not parse Markdown.",
                Detail = "The uploaded document is invalid or unsupported.",
            });
        }
    }

    /// <summary>
    /// Imports an OpenDocument Text (.odt) file and converts it into a PXA design.
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
            var design = await Importer("odt").ImportAsync(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse ODT: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports a raster image file (PNG, JPG, JPEG, GIF, WebP, BMP, TIFF) and converts it
    /// into a PXA design. A single page is created whose dimensions match the image's
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
            var design = await Importer(Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant())
                .ImportAsync(stream, file.FileName);
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not decode image: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports an SVG file and converts it into a PXA design with full vector fidelity.
    /// Rectangles map to shape elements; text maps to text elements; embedded images map to
    /// image elements; all other vector primitives (path, circle, ellipse, line, etc.) are
    /// preserved as inline SVG data-URI image elements.
    /// Route: POST /api/document/import-svg
    /// </summary>
    [HttpPost("import-svg")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportSvg(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "An SVG file is required." });

        if (!file.ContentType.Contains("svg", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only SVG files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            var design = await Importer("svg").ImportAsync(stream, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse SVG: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports a PowerPoint .pptx file and converts it into a PXA design.
    /// Each slide becomes a page. Text boxes, shapes, and embedded images are extracted
    /// with full fidelity including colors, fonts, and slide backgrounds.
    /// Route: POST /api/document/import-pptx
    /// </summary>
    [HttpPost("import-pptx")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportPptx(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A .pptx file is required." });

        if (!file.FileName.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only .pptx (PowerPoint Open XML) files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            var design = await Importer("pptx").ImportAsync(ms, Path.GetFileNameWithoutExtension(file.FileName));
            return Ok(design);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse PPTX: {ex.Message}" });
        }
    }

    /// <summary>
    /// Imports a raster image using the custom analysis engine (Phases 1–5).
    /// Extracts text (via NCC character recognition), geometric shapes (via Sobel edges),
    /// and colour regions into individual editable PXA elements.
    /// Route: POST /api/document/import-image-analysis
    /// </summary>
    [HttpPost("import-image-analysis")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DesignExportDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportImageAnalysis(
        IFormFile? file,
        [FromForm] double? pageWidthPt  = null,
        [FromForm] double? pageHeightPt = null,
        [FromForm] bool includeDiagnostics = false,
        [FromForm] bool includeDebugOverlay = false,
        [FromForm] bool includeFallbackImageLayer = false,
        [FromForm] double? lowConfidenceThreshold = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "An image file is required." });

        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (!ImageAnalysisFileImporter.SupportedExtensions.Contains(ext))
            return BadRequest(new { error = $"Supported formats: {string.Join(", ", ImageAnalysisFileImporter.SupportedExtensions)}." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _imageAnalysis.ImportWithAnalysisAsync(
                stream,
                Path.GetFileNameWithoutExtension(file.FileName),
                pageWidthPt,
                pageHeightPt,
                new ImageAnalysisOptions
                {
                    IncludeDebugOverlay = includeDebugOverlay,
                    IncludeFallbackImageLayer = includeFallbackImageLayer,
                    LowConfidenceThreshold = lowConfidenceThreshold ?? ImageAnalysisOptions.Default.LowConfidenceThreshold,
                });

            if (!includeDiagnostics && !includeDebugOverlay)
                return Ok(result.Design);

            return Ok(new ImageAnalysisDebugResponse
            {
                Design = result.Design,
                Diagnostics = result.Diagnostics,
                DebugOverlay = result.DebugOverlayPng is null
                    ? null
                    : $"data:image/png;base64,{Convert.ToBase64String(result.DebugOverlayPng)}",
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Image analysis failed: {ex.Message}" });
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

            var signedBytes = PXA.Infrastructure.Word.DigitalSigningService.SignDocx(docxStream, certMs.ToArray(), password);
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

public sealed class FindAndReplaceApiResponse
{
    public required DesignExportDto Design { get; set; }
    public int ReplacementCount { get; set; }
    public List<string> AffectedElementIds { get; set; } = [];
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

public sealed class ImageAnalysisDebugResponse
{
    public required DesignExportDto Design { get; set; }
    public required ImageAnalysisDiagnostics Diagnostics { get; set; }
    public string? DebugOverlay { get; set; }
}
