using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Generator;
using PXA.Importer.EInvoicing;
using PXA.Pdf;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/pdf/e-invoices")]
[EnableRateLimiting("designer-code")]
public sealed class PdfEInvoicesController(
    IPxaEInvoiceValidationService? validationService = null,
    PdfFontLoader? fontLoader = null) : ControllerBase
{
    private const long MaximumPdfBytes = 100L * 1024 * 1024;
    private const long MaximumXmlBytes = EInvoice.MaximumXmlBytes;
    private const int MaximumJsonBytes = 2 * 1024 * 1024;
    private const int MaximumPdfInvoiceLines = 2_000;
    private const int MaximumGeneratedPdfBytes = 50 * 1024 * 1024;

    [HttpPost("generate")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesGenerate)]
    [Consumes("application/json")]
    [Produces("application/xml")]
    [RequestSizeLimit(MaximumJsonBytes)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public ActionResult Generate([FromBody] PdfEInvoiceGenerationRequest? request)
    {
        if (!TryCreateGenerationInput(request, out var invoice, out var options, out var problem))
            return problem!;

        var result = EInvoice.GenerateXml(invoice!, options!);
        if (!result.IsGenerated)
            return GenerationProblem("Electronic-invoice XML could not be generated", result.Diagnostics);

        return File(
            result.Xml.ToArray(),
            "application/xml",
            EInvoice.GetDefaultFileName(options!.Standard));
    }

    [HttpPost("create")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesCreate)]
    [Consumes("application/json")]
    [Produces("application/pdf")]
    [RequestSizeLimit(MaximumJsonBytes)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public ActionResult Create([FromBody] PdfEInvoiceGenerationRequest? request)
    {
        if (!TryCreateGenerationInput(request, out var invoice, out var options, out var problem))
            return problem!;
        if (invoice!.Lines is { Count: > MaximumPdfInvoiceLines })
        {
            return InvoiceProblem(
                StatusCodes.Status422UnprocessableEntity,
                "PXAEINVAPI011",
                "The invoice contains too many lines for synchronous PDF creation",
                $"A PDF invoice may contain up to {MaximumPdfInvoiceLines} lines.");
        }

        var result = EInvoice.CreatePdf(invoice, options!, fontLoader);
        if (!result.IsGenerated)
            return GenerationProblem("The PDF/A-3 electronic invoice could not be created", result.Diagnostics);
        if (result.Pdf.Length > MaximumGeneratedPdfBytes)
        {
            return InvoiceProblem(
                StatusCodes.Status422UnprocessableEntity,
                "PXAEINVAPI012",
                "The generated PDF is too large",
                $"Generated PDF invoices may contain up to {MaximumGeneratedPdfBytes} bytes.");
        }

        return File(
            result.Pdf.ToArray(),
            "application/pdf",
            SafeInvoicePdfName(invoice.InvoiceNumber));
    }

    [HttpPost("validate")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesValidate)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumXmlBytes + 64 * 1024)]
    [ProducesResponseType(typeof(PxaEInvoiceValidationReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PxaEInvoiceValidationReport>> Validate(
        IFormFile? file,
        [FromForm] string? standard,
        [FromForm] string? version,
        [FromForm] string? profile,
        CancellationToken cancellationToken)
    {
        if (ValidateXmlUpload(file) is { } uploadProblem)
        {
            return uploadProblem;
        }
        if (!TryReadStandard(standard, out var parsedStandard))
        {
            return InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI002",
                "The electronic-invoice standard is not supported",
                "Use FacturX, ZUGFeRD, or XRechnung.");
        }

        var bytes = await ReadBoundedAsync(file!, MaximumXmlBytes, cancellationToken);
        return Ok((validationService ?? new StructuralPxaEInvoiceValidationService()).Validate(bytes, new PxaEInvoiceOptions
        {
            Standard = parsedStandard,
            Version = version?.Trim() ?? string.Empty,
            Profile = profile?.Trim() ?? string.Empty,
            FileName = file!.FileName
        }, cancellationToken));
    }

    [HttpPost("inspect")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPdfBytes + 64 * 1024)]
    [ProducesResponseType(typeof(IReadOnlyList<PdfEInvoiceAttachmentSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PdfEInvoiceAttachmentSummary>>> Inspect(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (ValidatePdfUpload(file) is { } uploadProblem)
        {
            return uploadProblem;
        }

        try
        {
            var pdf = await ReadBoundedAsync(file!, MaximumPdfBytes, cancellationToken);
            await using var stream = new MemoryStream(pdf, writable: false);
            var attachments = await new PdfAssociatedFileExtractor().ExtractAsync(
                stream,
                new PdfAssociatedFileExtractionOptions { MaximumPdfSizeBytes = MaximumPdfBytes },
                cancellationToken);
            var visibleText = await PxaEInvoicePdfConsistencyInspector.ExtractVisibleTextAsync(
                pdf,
                cancellationToken);
            var summaries = new List<PdfEInvoiceAttachmentSummary>(attachments.Count);
            foreach (var attachment in attachments)
                summaries.Add(InspectAttachment(attachment, visibleText, cancellationToken));
            return Ok(summaries);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI003",
                "The PDF attachments could not be inspected",
                exception.Message);
        }
    }

    [HttpPost("extract")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesRead)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPdfBytes + 64 * 1024)]
    [Produces("application/xml", "application/octet-stream")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Extract(
        IFormFile? file,
        [FromForm] string? attachmentName,
        CancellationToken cancellationToken)
    {
        if (ValidatePdfUpload(file) is { } uploadProblem)
        {
            return uploadProblem;
        }
        if (string.IsNullOrWhiteSpace(attachmentName))
        {
            return InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI004",
                "An attachment name is required",
                "Inspect the PDF first and submit one exact associated-file name.");
        }

        try
        {
            await using var stream = file!.OpenReadStream();
            var attachments = await new PdfAssociatedFileExtractor().ExtractAsync(
                stream,
                new PdfAssociatedFileExtractionOptions { MaximumPdfSizeBytes = MaximumPdfBytes },
                cancellationToken);
            var attachment = attachments.SingleOrDefault(item =>
                string.Equals(item.FileName, attachmentName, StringComparison.Ordinal));
            if (attachment is null)
            {
                return InvoiceProblem(
                    StatusCodes.Status404NotFound,
                    "PXAEINVAPI005",
                    "The associated file was not found",
                    "Use an exact filename returned by the inspect operation.");
            }
            if (!attachment.SizeMatches || !attachment.ChecksumMatches)
            {
                return InvoiceProblem(
                    StatusCodes.Status400BadRequest,
                    "PXAEINVAPI006",
                    "The associated file failed integrity checks",
                    "Its declared size or PDF checksum does not match the extracted bytes.");
            }

            return File(
                attachment.Content.ToArray(),
                attachment.MimeType ?? "application/octet-stream",
                SafeDownloadName(attachment.FileName));
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI003",
                "The PDF attachments could not be extracted",
                exception.Message);
        }
    }

    private ActionResult? ValidateXmlUpload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return InvoiceProblem(StatusCodes.Status400BadRequest, "PXAEINVAPI001", "Invoice XML is required", "Upload a non-empty XML file in the 'file' field.");
        if (file.Length > MaximumXmlBytes)
            return InvoiceProblem(StatusCodes.Status413PayloadTooLarge, "PXAEINVAPI007", "Invoice XML is too large", $"Invoice XML may contain up to {MaximumXmlBytes} bytes.");
        return null;
    }

    private ActionResult? ValidatePdfUpload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return InvoiceProblem(StatusCodes.Status400BadRequest, "PXAEINVAPI008", "A PDF is required", "Upload a non-empty PDF in the 'file' field.");
        if (file.Length > MaximumPdfBytes)
            return InvoiceProblem(StatusCodes.Status413PayloadTooLarge, "PXAEINVAPI009", "The PDF is too large", $"PDF files may contain up to {MaximumPdfBytes} bytes.");
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) &&
            !file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            return InvoiceProblem(StatusCodes.Status400BadRequest, "PXAEINVAPI010", "The upload is not a PDF", "Use a .pdf file or the application/pdf content type.");
        return null;
    }

    private ObjectResult InvoiceProblem(int status, string code, string title, string detail) =>
        Problem(statusCode: status, title: title, detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private ObjectResult GenerationProblem(
        string title,
        IReadOnlyList<PxaEInvoiceDiagnostic> diagnostics) =>
        Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: title,
            detail: diagnostics.FirstOrDefault(static item => item.Severity == "error")?.Message ??
                "The supplied invoice data could not be processed.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = diagnostics.FirstOrDefault(static item => item.Severity == "error")?.Code ?? "PXAEINVAPI013",
                ["diagnostics"] = diagnostics
            });

    private bool TryCreateGenerationInput(
        PdfEInvoiceGenerationRequest? request,
        out PxaEInvoiceDocument? invoice,
        out PxaEInvoiceOptions? options,
        out ActionResult? problem)
    {
        invoice = request?.Invoice;
        options = null;
        problem = null;
        if (request is null || invoice is null)
        {
            problem = InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI014",
                "Invoice data is required",
                "Submit standard, version, profile, and a typed invoice document.");
            return false;
        }
        if (!TryReadStandard(request.Standard, out var standard))
        {
            problem = InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI002",
                "The electronic-invoice standard is not supported",
                "Use FacturX, ZUGFeRD, or XRechnung.");
            return false;
        }

        options = new PxaEInvoiceOptions
        {
            Standard = standard,
            Version = request.Version?.Trim() ?? string.Empty,
            Profile = request.Profile?.Trim() ?? string.Empty
        };
        return true;
    }

    private static async Task<byte[]> ReadBoundedAsync(IFormFile file, long maximumBytes, CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();
        using var target = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) return target.ToArray();
            if (target.Length + read > maximumBytes) throw new InvalidDataException("The upload exceeded its size limit.");
            target.Write(buffer, 0, read);
        }
    }

    private static bool TryReadStandard(string? value, out PxaEInvoiceStandard standard)
    {
        var normalized = value?.Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        if (string.Equals(normalized, "ZUGFeRD", StringComparison.OrdinalIgnoreCase))
        {
            standard = PxaEInvoiceStandard.Zugferd;
            return true;
        }
        return Enum.TryParse(normalized, ignoreCase: true, out standard) && Enum.IsDefined(standard);
    }

    private static bool IsXml(PdfExtractedAssociatedFile attachment) =>
        attachment.MimeType is "application/xml" or "text/xml" ||
        attachment.FileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private PdfEInvoiceAttachmentSummary InspectAttachment(
        PdfExtractedAssociatedFile attachment,
        string? visibleText,
        CancellationToken cancellationToken)
    {
        var isXml = IsXml(attachment);
        PxaEInvoiceRecognitionReport? recognition = null;
        PxaEInvoiceValidationReport? validation = null;
        PxaEInvoiceStandard? standard = null;
        if (isXml && attachment.SizeMatches && attachment.ChecksumMatches)
        {
            recognition = EInvoice.Recognize(attachment.Content);
            standard = InferStandard(attachment.FileName, recognition);
            if (standard is not null && recognition.IsRecognized && recognition.Profile is not null)
            {
                validation = (validationService ?? new StructuralPxaEInvoiceValidationService()).Validate(
                    attachment.Content,
                    new PxaEInvoiceOptions
                    {
                        Standard = standard.Value,
                        Version = CurrentVersion(standard.Value),
                        Profile = recognition.Profile,
                        FileName = attachment.FileName
                    },
                    cancellationToken);
                if (standard == PxaEInvoiceStandard.FacturX &&
                    validation.ConformanceStatus == "notEvaluated")
                {
                    validation = (validationService ?? new StructuralPxaEInvoiceValidationService()).Validate(
                        attachment.Content,
                        new PxaEInvoiceOptions
                        {
                            Standard = PxaEInvoiceStandard.Zugferd,
                            Version = EInvoice.CurrentZugferdVersion,
                            Profile = recognition.Profile,
                            FileName = attachment.FileName
                        },
                        cancellationToken);
                }
            }
        }

        return new PdfEInvoiceAttachmentSummary(
            attachment.FileName,
            attachment.MimeType,
            attachment.Relationship,
            attachment.Description,
            attachment.Content.Length,
            attachment.SizeMatches,
            attachment.ChecksumMatches,
            isXml,
            standard switch
            {
                PxaEInvoiceStandard.FacturX or PxaEInvoiceStandard.Zugferd => "Factur-X / ZUGFeRD",
                PxaEInvoiceStandard.XRechnung => "XRechnung",
                _ => null
            },
            recognition?.Syntax,
            recognition?.GuidelineIdentifier,
            recognition?.Profile,
            validation?.IsStructurallyValid ?? recognition?.IsRecognized ?? false,
            validation?.ConformanceStatus ?? "notEvaluated",
            validation?.ValidatorArtifactSetId,
            validation?.ValidatorArtifactSetSha256,
            validation?.Diagnostics ?? recognition?.Diagnostics ?? [],
            recognition?.SemanticData,
            PxaEInvoicePdfConsistencyInspector.Inspect(visibleText, recognition?.SemanticData));
    }

    private static PxaEInvoiceStandard? InferStandard(
        string fileName,
        PxaEInvoiceRecognitionReport recognition)
    {
        if (!recognition.IsRecognized)
            return null;
        if (recognition.GuidelineIdentifier?.Contains("xrechnung", StringComparison.OrdinalIgnoreCase) == true)
            return PxaEInvoiceStandard.XRechnung;
        if (string.Equals(fileName, "factur-x.xml", StringComparison.OrdinalIgnoreCase))
            return PxaEInvoiceStandard.FacturX;
        if (string.Equals(fileName, "zugferd-invoice.xml", StringComparison.OrdinalIgnoreCase))
            return null;
        return null;
    }

    private static string CurrentVersion(PxaEInvoiceStandard standard) => standard switch
    {
        PxaEInvoiceStandard.FacturX => EInvoice.CurrentFacturXVersion,
        PxaEInvoiceStandard.Zugferd => EInvoice.CurrentZugferdVersion,
        PxaEInvoiceStandard.XRechnung => EInvoice.CurrentXRechnungVersion,
        _ => throw new ArgumentOutOfRangeException(nameof(standard))
    };

    private static string SafeDownloadName(string value)
    {
        var name = Path.GetFileName(value);
        return string.IsNullOrWhiteSpace(name) ? "invoice.xml" : name;
    }

    private static string SafeInvoicePdfName(string value)
    {
        const string invalidCharacters = "<>:\"/\\|?*";
        var sanitized = new string(value
            .Trim()
            .Select(character => char.IsControl(character) || invalidCharacters.Contains(character)
                ? '-'
                : character)
            .ToArray())
            .Trim(' ', '.', '-');
        if (sanitized.Length > 120)
            sanitized = sanitized[..120].TrimEnd(' ', '.', '-');
        return (string.IsNullOrWhiteSpace(sanitized) ? "invoice" : sanitized) + ".pdf";
    }
}

public sealed record PdfEInvoiceAttachmentSummary(
    string FileName,
    string? MimeType,
    string Relationship,
    string? Description,
    int SizeBytes,
    bool SizeMatches,
    bool ChecksumMatches,
    bool IsXml,
    string? Standard,
    string? Syntax,
    string? GuidelineIdentifier,
    string? Profile,
    bool IsStructurallyValid,
    string ConformanceStatus,
    string? ValidatorArtifactSetId,
    string? ValidatorArtifactSetSha256,
    IReadOnlyList<PxaEInvoiceDiagnostic> Diagnostics,
    PxaEInvoiceSemanticData? SemanticData,
    PxaEInvoicePdfConsistencyReport PdfConsistency);

public sealed record PdfEInvoiceGenerationRequest(
    string? Standard,
    string? Version,
    string? Profile,
    PxaEInvoiceDocument? Invoice);
