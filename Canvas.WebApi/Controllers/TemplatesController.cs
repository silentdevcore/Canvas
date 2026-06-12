using System.Reflection;
using Canvas.Application.UseCases;
using Canvas.Domain.Entities;
using Canvas.Domain.Repositories;
using Canvas.WebApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Canvas.WebApi.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplatesController : ControllerBase
{
    private readonly RenderTemplateUseCase _renderTemplateUseCase;
    private readonly CreateTemplateUseCase _createTemplateUseCase;
    private readonly UpdateTemplateUseCase _updateTemplateUseCase;
    private readonly GetTemplateUseCase _getTemplateUseCase;
    private readonly ValidateTemplateUseCase _validateTemplateUseCase;
    private readonly Canvas.Pdf.PdfFontLoader? _fontLoader;

    public TemplatesController(
        RenderTemplateUseCase renderTemplateUseCase,
        CreateTemplateUseCase createTemplateUseCase,
        UpdateTemplateUseCase updateTemplateUseCase,
        GetTemplateUseCase getTemplateUseCase,
        ValidateTemplateUseCase validateTemplateUseCase,
        Canvas.Pdf.PdfFontLoader? fontLoader = null)
    {
        _renderTemplateUseCase = renderTemplateUseCase;
        _createTemplateUseCase = createTemplateUseCase;
        _updateTemplateUseCase = updateTemplateUseCase;
        _getTemplateUseCase = getTemplateUseCase;
        _validateTemplateUseCase = validateTemplateUseCase;
        _fontLoader = fontLoader;
    }

    /// <summary>
    /// Renders a template with the provided data payload to generate a PDF.
    /// </summary>
    /// <param name="templateId">The ID of the template to render</param>
    /// <param name="payload">The data payload to merge into the template</param>
    /// <param name="templateVersion">Optional template version</param>
    /// <returns>The generated PDF file</returns>
    [HttpPost("render")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RenderTemplate([FromQuery] string templateId, [FromBody] object payload, [FromQuery] string? templateVersion = null)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            return BadRequest("Template ID is required");
        }

        if (payload == null)
        {
            return BadRequest("Payload is required");
        }

        try
        {
            // Generate a temporary file path for the PDF output
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"template_{Guid.NewGuid()}.pdf");

            // Create the render request
            var renderRequest = new RenderTemplateRequest
            {
                TemplateId = templateId,
                Payload = payload,
                OutputPath = tempFilePath,
                TemplateVersion = templateVersion
            };

            // Execute the render use case
            await _renderTemplateUseCase.ExecuteAsync(renderRequest);

            // Read the generated PDF file
            var pdfBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);

            // Clean up the temporary file
            try
            {
                System.IO.File.Delete(tempFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            // Return the PDF file
            return File(pdfBytes, "application/pdf", $"template_{templateId}.pdf");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("validation failed"))
        {
            return BadRequest(new { error = "Template validation failed", details = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Renders a template asynchronously for large payloads or batch processing.
    /// Returns a job ID that can be used to check status and download the result.
    /// </summary>
    /// <param name="request">The async render request</param>
    /// <returns>Job ID for tracking the async render operation</returns>
    [HttpPost("render/async")]
    [ProducesResponseType(typeof(RenderJobResponse), 202)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RenderTemplateAsync([FromBody] RenderTemplateRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body is required");
        }

        // For now, implement synchronous rendering
        // In production, this would queue the job and return immediately
        try
        {
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"template_{Guid.NewGuid()}.pdf");

            var renderRequest = new RenderTemplateRequest
            {
                TemplateId = request.TemplateId,
                Payload = request.Payload,
                OutputPath = tempFilePath,
                TemplateVersion = request.TemplateVersion
            };

            await _renderTemplateUseCase.ExecuteAsync(renderRequest);

            var pdfBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);

            try
            {
                System.IO.File.Delete(tempFilePath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            return File(pdfBytes, "application/pdf", $"template_{request.TemplateId}.pdf");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("validation failed"))
        {
            return BadRequest(new { error = "Template validation failed", details = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new template.
    /// </summary>
    /// <param name="request">The template creation request</param>
    /// <returns>The created template</returns>
    [HttpPost]
    [ProducesResponseType(typeof(DesignTemplate), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Name))
        {
            return BadRequest("Template name is required");
        }

        try
        {
            var template = await _createTemplateUseCase.ExecuteAsync(request);
            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    /// <param name="id">The template ID</param>
    /// <param name="version">Optional version</param>
    /// <returns>The template</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DesignTemplate), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetTemplate(string id, [FromQuery] string? version = null)
    {
        try
        {
            var request = new GetTemplateRequest { Id = id, Version = version };
            var template = await _getTemplateUseCase.ExecuteAsync(request);
            return Ok(template);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    /// <param name="id">The template ID</param>
    /// <param name="request">The update request</param>
    /// <returns>The updated template</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(DesignTemplate), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateTemplate(string id, [FromBody] UpdateTemplateRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body is required");
        }

        request.Id = id;

        try
        {
            var template = await _updateTemplateUseCase.ExecuteAsync(request);
            return Ok(template);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Validates a template without saving it.
    /// </summary>
    /// <param name="request">The validation request</param>
    /// <returns>Validation results</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ValidateTemplate([FromBody] ValidateTemplateRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body is required");
        }

        try
        {
            var result = await _validateTemplateUseCase.ExecuteAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets a list of all template names and IDs.
    /// </summary>
    /// <returns>List of template names</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TemplateNameInfo>), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetTemplateNames()
    {
        try
        {
            var templates = await _getTemplateUseCase.GetTemplateNamesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Renders a design directly from the UIDesigner JSON export without a pre-stored template.
    /// Accepts the full design payload (pages + elements + page settings) and returns a PDF.
    /// </summary>
    [HttpPost("render-design")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public IActionResult RenderDesign([FromBody] DesignExportDto design)
    {
        if (design is null || design.Pages.Count == 0)
            return BadRequest(new { error = "Design must have at least one page." });

        try
        {
            var document = DesignJsonMapper.MapToPdfDocument(design, _fontLoader);
            var bytes = document.ToBytes();
            var filename = (design.Name ?? "document").ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("/", "-") + ".pdf";
            return File(bytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message;
            var trace = ex.StackTrace?.Split('\n').Take(5).ToArray();
            return StatusCode(500, new { error = "Render failed", details = ex.Message, inner, trace });
        }
    }

    /// <summary>
    /// Executes a C# script that builds a PdfDocument using the Canvas.Pdf API and returns the PDF.
    /// The script must evaluate to a PdfDocument instance as its last expression.
    /// </summary>
    [HttpPost("csharp-code-to-pdf")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CsharpCodeToPdf([FromBody] CsharpToJsonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code is required." });

        try
        {
            var platformRefs = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
                .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(System.IO.File.Exists)
                .Select(p => MetadataReference.CreateFromFile(p))
                .Cast<MetadataReference>()
                .ToList();

            var options = ScriptOptions.Default
                .WithReferences(platformRefs)
                .AddReferences(
                    Assembly.GetExecutingAssembly(),
                    typeof(Canvas.Pdf.PdfDocument).Assembly)
                .WithImports(
                    "Canvas.Pdf",
                    "Canvas.WebApi.Infrastructure",
                    "System",
                    "System.IO",
                    "System.Collections.Generic")
                .WithEmitDebugInformation(true);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            // Evaluate as object to avoid cross-context type identity issues.
            // Roslyn loads assemblies in its own context; casting directly to PdfDocument
            // fails at runtime even though the type name matches.
            var result = await CSharpScript.EvaluateAsync<object>(request.Code.Trim(), options, cancellationToken: cts.Token);

            if (result is null)
                return BadRequest(new { error = "Script must return a PdfDocument instance as the last expression." });

            // Call ToBytes() via reflection to side-step the type identity problem.
            // Use LINQ search because ToBytes(PdfSaveOptions? options = null) has one optional param
            // and GetMethod("ToBytes", Type.EmptyTypes) won't find it; reflection ignores defaults.
            var toBytesMethod = result.GetType()
                .GetMethods()
                .FirstOrDefault(m => m.Name == "ToBytes" && m.ReturnType == typeof(byte[]));

            if (toBytesMethod is null)
                return BadRequest(new { error = $"Script returned '{result.GetType().Name}' — expected a PdfDocument instance." });

            var invokeArgs = toBytesMethod.GetParameters().Select(_ => (object?)null).ToArray();
            var bytes = (byte[])toBytesMethod.Invoke(result, invokeArgs)!;
            return File(bytes, "application/pdf", "preview.pdf");
        }
        catch (CompilationErrorException ex)
        {
            var errors = ex.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();
            return BadRequest(new { error = "Compilation error", details = errors });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, new { error = "Script timed out after 15 seconds." });
        }
        catch (Exception ex)
        {
            var line = ExtractScriptLineNumber(ex.StackTrace);
            var details = line.HasValue ? $"{ex.Message}\n(script line {line})" : ex.Message;
            return StatusCode(500, new { error = "Execution failed", details });
        }
    }

    /// <summary>
    /// Converts a C# expression (returning DesignExportDto) to its JSON representation.
    /// The code must be a single expression that evaluates to a DesignExportDto object.
    /// Example: new DesignExportDto { Name = "Hello", Pages = [ new PageDto { ... } ] }
    /// </summary>
    [HttpPost("csharp-to-json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CsharpToJson([FromBody] CsharpToJsonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code is required." });

        try
        {
            // Load all trusted platform assemblies so Roslyn can resolve every BCL type
            var platformRefs = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
                .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(System.IO.File.Exists)
                .Select(p => MetadataReference.CreateFromFile(p))
                .Cast<MetadataReference>()
                .ToList();

            var options = ScriptOptions.Default
                .WithReferences(platformRefs)
                .AddReferences(Assembly.GetExecutingAssembly())
                .WithImports(
                    "Canvas.WebApi.Infrastructure",
                    "System",
                    "System.Collections.Generic")
                .WithEmitDebugInformation(true);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var result = await CSharpScript.EvaluateAsync<DesignExportDto>(
                request.Code.Trim(), options, cancellationToken: cts.Token);

            if (result is null)
                return BadRequest(new { error = "Expression must return a DesignExportDto instance." });

            return Ok(result);
        }
        catch (CompilationErrorException ex)
        {
            var errors = ex.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();
            return BadRequest(new { error = "Compilation error", details = errors });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, new { error = "Script timed out after 15 seconds." });
        }
        catch (Exception ex)
        {
            var line = ExtractScriptLineNumber(ex.StackTrace);
            var details = line.HasValue ? $"{ex.Message}\n(script line {line})" : ex.Message;
            return StatusCode(500, new { error = "Execution failed", details });
        }
    }

    /// <summary>
    /// Executes a C# script returning PdfDocument and converts the resulting element tree back to the JSON design format.
    /// Useful for round-tripping: Code → JSON → live preview.
    /// </summary>
    [HttpPost("csharp-code-to-json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CsharpCodeToJson([FromBody] CsharpToJsonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { error = "Code is required." });

        try
        {
            var platformRefs = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
                .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(System.IO.File.Exists)
                .Select(p => MetadataReference.CreateFromFile(p))
                .Cast<MetadataReference>()
                .ToList();

            var options = ScriptOptions.Default
                .WithReferences(platformRefs)
                .AddReferences(Assembly.GetExecutingAssembly(), typeof(Canvas.Pdf.PdfDocument).Assembly)
                .WithImports("Canvas.Pdf", "System", "System.Collections.Generic")
                .WithEmitDebugInformation(true);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var result = await CSharpScript.EvaluateAsync<object>(request.Code.Trim(), options, cancellationToken: cts.Token);

            if (result is null)
                return BadRequest(new { error = "Script must return a PdfDocument instance as the last expression." });

            // Pages is a public property
            var pagesEnum = result.GetType()
                .GetProperty("Pages", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(result) as System.Collections.IEnumerable;

            if (pagesEnum is null)
                return BadRequest(new { error = "Could not extract pages from the PdfDocument." });

            var jsonPages   = new List<object>();
            var firstPageW  = 595.0;
            var firstPageH  = 842.0;
            var pageIdx     = 0;

            foreach (var pageObj in pagesEnum)
            {
                var pt     = pageObj.GetType();
                var pageW  = pt.GetProperty("Width") ?.GetValue(pageObj) is double pw ? pw : 595.0;
                var pageH  = pt.GetProperty("Height")?.GetValue(pageObj) is double ph ? ph : 842.0;
                if (pageIdx == 0) { firstPageW = pageW; firstPageH = pageH; }

                // Elements is internal — NonPublic binding is required
                var elementsEnum = pt
                    .GetProperty("Elements", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.GetValue(pageObj) as System.Collections.IEnumerable;

                var jsonElements = new List<object>();
                var elIdx = 0;

                if (elementsEnum is not null)
                {
                    foreach (var elObj in elementsEnum)
                    {
                        var et   = elObj.GetType();
                        var name = et.Name;

                        object? P(string prop) => et.GetProperty(prop)?.GetValue(elObj);
                        double  D(string prop, double fb = 0) => P(prop) is double d ? d : fb;
                        string  Hex(string prop) => ColorToHex(P(prop));
                        double  Lw(string prop)
                        {
                            var ss = P(prop);
                            return ss?.GetType().GetProperty("LineWidth")?.GetValue(ss) is double lw ? lw : 1.0;
                        }

                        var id = $"el-{pageIdx}-{elIdx++}";

                        var textContent = P("Text")?.ToString() ?? "";
                        var fontSize    = D("FontSize", 12);

                        object? jsonEl = name switch
                        {
                            "TextElement" => (object)new
                            {
                                id, type = "text",
                                content = textContent,
                                x = D("X"),
                                y = pageH - D("Y") - fontSize * 0.72,
                                width  = Math.Max(textContent.Length * fontSize * 0.55, 50),
                                height = fontSize * 1.4,
                                style  = new Dictionary<string, object>
                                {
                                    ["fontSize"] = fontSize,
                                    ["color"]    = Hex("FillColor"),
                                }
                            },
                            "RectangleElement" => new
                            {
                                id, type = "rect",
                                x = D("X"), y = pageH - D("Y") - D("Height"),
                                width = D("Width"), height = D("Height"),
                                style = new Dictionary<string, object>
                                {
                                    ["backgroundColor"] = Hex("FillColor"),
                                    ["borderColor"]     = Hex("StrokeColor"),
                                    ["borderWidth"]     = (object)Lw("StrokeStyle"),
                                }
                            },
                            "RoundedRectangleElement" => new
                            {
                                id, type = "rect",
                                x = D("X"), y = pageH - D("Y") - D("Height"),
                                width = D("Width"), height = D("Height"),
                                style = new Dictionary<string, object>
                                {
                                    ["backgroundColor"] = Hex("FillColor"),
                                    ["borderColor"]     = Hex("StrokeColor"),
                                    ["borderWidth"]     = (object)Lw("StrokeStyle"),
                                    ["borderRadius"]    = (object)D("CornerRadius"),
                                }
                            },
                            "LineElement" => new
                            {
                                id, type = "line",
                                x = D("X1"), y = pageH - D("Y1"),
                                width  = Math.Abs(D("X2") - D("X1")),
                                height = Math.Max(Math.Abs(D("Y2") - D("Y1")), Lw("StrokeStyle")),
                                style  = new Dictionary<string, object>
                                {
                                    ["color"]       = Hex("StrokeColor"),
                                    ["strokeWidth"] = (object)Lw("StrokeStyle"),
                                }
                            },
                            "CircleElement" => new
                            {
                                id, type = "circle",
                                x = D("CenterX") - D("Radius"), y = pageH - D("CenterY") - D("Radius"),
                                width = D("Radius") * 2, height = D("Radius") * 2,
                                style = new Dictionary<string, object>
                                {
                                    ["backgroundColor"] = Hex("FillColor"),
                                    ["borderColor"]     = Hex("StrokeColor"),
                                    ["borderWidth"]     = (object)Lw("StrokeStyle"),
                                }
                            },
                            "ImageElement" => new
                            {
                                id, type = "image",
                                x = D("X"),
                                y = pageH - D("Y") - D("Height"),
                                width  = D("Width"),
                                height = D("Height"),
                                style  = new Dictionary<string, object>()
                            },
                            _ => null
                        };

                        if (jsonEl is not null) jsonElements.Add(jsonEl);
                    }
                }

                jsonPages.Add(new { id = $"page-{pageIdx + 1}", elements = jsonElements });
                pageIdx++;
            }

            return Ok(new
            {
                name = "Imported from C# Code",
                pageSettings = new { width = firstPageW, height = firstPageH },
                pages = jsonPages
            });
        }
        catch (CompilationErrorException ex)
        {
            var errors = ex.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();
            return BadRequest(new { error = "Compilation error", details = errors });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, new { error = "Script timed out after 15 seconds." });
        }
        catch (Exception ex)
        {
            var line = ExtractScriptLineNumber(ex.StackTrace);
            var details = line.HasValue ? $"{ex.Message}\n(script line {line})" : ex.Message;
            return StatusCode(500, new { error = "Execution failed", details });
        }
    }

    private static int? ExtractScriptLineNumber(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(
            stackTrace, @"Submission#\d+.*:line (\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var n) ? n : null;
    }

    private static string ColorToHex(object? colorObj)
    {
        if (colorObj is null) return "#000000";
        var t = colorObj.GetType();
        // PdfColor: Red, Green, Blue
        var r = t.GetProperty("Red")  ?.GetValue(colorObj) as double?;
        var g = t.GetProperty("Green")?.GetValue(colorObj) as double?;
        var b = t.GetProperty("Blue") ?.GetValue(colorObj) as double?;
        if (r.HasValue && g.HasValue && b.HasValue)
            return $"#{(int)Math.Round(r.Value * 255):X2}{(int)Math.Round(g.Value * 255):X2}{(int)Math.Round(b.Value * 255):X2}";
        // PdfGrayColor: Gray
        var gray = t.GetProperty("Gray")?.GetValue(colorObj) as double?;
        if (gray.HasValue) { var gv = (int)Math.Round(gray.Value * 255); return $"#{gv:X2}{gv:X2}{gv:X2}"; }
        return "#000000";
    }
}

public class CsharpToJsonRequest
{
    public string Code { get; set; } = "";
}

public class RenderJobResponse
{
    public required string JobId { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DownloadUrl { get; set; }
}
