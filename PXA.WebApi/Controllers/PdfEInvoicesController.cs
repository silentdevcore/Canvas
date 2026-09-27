using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.Generator;
using PXA.Importer.EInvoicing;
using PXA.Importer.Trust;
using PXA.Pdf;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Pdf;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/pdf/e-invoices")]
[EnableRateLimiting("designer-code")]
public sealed class PdfEInvoicesController(
    IPxaEInvoiceValidationService? validationService = null,
    PdfFontLoader? fontLoader = null,
    IPdfEInvoiceAttacher? attacher = null,
    IPxaEInvoiceValidationEvidenceService? evidence = null) : ControllerBase
{
    private const int MaximumEvidencePageSize = 200;

    private const long MaximumPdfBytes = 100L * 1024 * 1024;
    private const long MaximumXmlBytes = EInvoice.MaximumXmlBytes;
    private const int MaximumJsonBytes = 2 * 1024 * 1024;
    private const int MaximumPdfInvoiceLines = 2_000;
    private const int MaximumGeneratedPdfBytes = 50 * 1024 * 1024;
    private const long MaximumAttachedPdfBytes = 120L * 1024 * 1024;

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
        var options = new PxaEInvoiceOptions
        {
            Standard = parsedStandard,
            Version = version?.Trim() ?? string.Empty,
            Profile = profile?.Trim() ?? string.Empty,
            FileName = file!.FileName
        };
        var report = Validator().Validate(bytes, options, cancellationToken);
        await RecordEvidenceAsync("validate", bytes, null, options, report, null, cancellationToken);
        return Ok(report);
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

    [HttpPost("attach/preflight")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesAttach)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumPdfBytes + MaximumXmlBytes + 64 * 1024)]
    [ProducesResponseType(typeof(PdfEInvoiceAttachmentPreflightResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PdfEInvoiceAttachmentPreflightResponse>> AttachPreflight(
        IFormFile? file,
        IFormFile? invoice,
        [FromForm] string? standard,
        [FromForm] string? version,
        [FromForm] string? profile,
        [FromForm] bool allowArchiveConversion,
        [FromForm] bool allowSignatureInvalidation,
        [FromForm] bool replaceExistingInvoice,
        [FromForm] bool preserveUnrecognized,
        [FromForm] string? attachmentFileName,
        CancellationToken cancellationToken)
    {
        var input = await ReadAttachmentInputAsync(
            file, invoice, standard, version, profile, preserveUnrecognized, attachmentFileName, cancellationToken);
        if (input.Problem is { } problem)
            return problem;

        try
        {
            await using var stream = new MemoryStream(input.Pdf!, writable: false);
            var report = await Attacher().PreflightAsync(
                stream,
                input.Payload!,
                AttachmentOptions(allowArchiveConversion, allowSignatureInvalidation, replaceExistingInvoice),
                cancellationToken);
            return Ok(new PdfEInvoiceAttachmentPreflightResponse(
                report,
                input.Validation!,
                await InspectConsistencyAsync(input.Pdf!, input.Recognition, cancellationToken),
                input.Mode,
                input.Payload!.FileName,
                IsEditable: input.Mode == PdfEInvoiceAttachmentMode.HybridInvoice));
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or ArgumentException)
        {
            return AttachmentInputProblem(exception);
        }
    }

    [HttpPost("attach")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesAttach)]
    [Consumes("multipart/form-data")]
    [Produces("application/pdf")]
    [RequestSizeLimit(MaximumPdfBytes + MaximumXmlBytes + 64 * 1024)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Attach(
        IFormFile? file,
        IFormFile? invoice,
        [FromForm] string? standard,
        [FromForm] string? version,
        [FromForm] string? profile,
        [FromForm] bool allowArchiveConversion,
        [FromForm] bool allowSignatureInvalidation,
        [FromForm] bool replaceExistingInvoice,
        [FromForm] bool allowInconsistentPdf,
        [FromForm] bool preserveUnrecognized,
        [FromForm] string? attachmentFileName,
        CancellationToken cancellationToken)
    {
        var input = await ReadAttachmentInputAsync(
            file, invoice, standard, version, profile, preserveUnrecognized, attachmentFileName, cancellationToken);
        if (input.Problem is { } problem)
            return problem;

        // A hybrid invoice whose embedded values are absent from the visible page is the one
        // failure a reader cannot detect, so it is refused unless the caller accepts it.
        // Preserved unknown XML is exempt: PXA makes no claim about what it contains, so
        // there is nothing to compare the visible page against.
        var consistency = await InspectConsistencyAsync(input.Pdf!, input.Recognition, cancellationToken);
        if (input.Mode == PdfEInvoiceAttachmentMode.HybridInvoice &&
            consistency.Status == "reviewRequired" && !allowInconsistentPdf)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The invoice values were not found in the visible PDF",
                detail: "The embedded invoice would contradict the visible document. Review it, or approve the attachment explicitly with allowInconsistentPdf.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "PXAEINVAPI021",
                    ["consistency"] = consistency
                });
        }

        try
        {
            await using var stream = new MemoryStream(input.Pdf!, writable: false);
            var result = await Attacher().AttachAsync(
                stream,
                input.Payload!,
                AttachmentOptions(allowArchiveConversion, allowSignatureInvalidation, replaceExistingInvoice),
                cancellationToken);

            Response.Headers["PXA-Source-SHA256"] = result.SourceSha256;
            Response.Headers["PXA-Result-SHA256"] = result.ResultSha256;
            Response.Headers["PXA-Invoice-SHA256"] = result.AttachmentSha256;
            Response.Headers["PXA-Invoice-Attachment"] = result.FileName;
            Response.Headers["PXA-Attachment-Strategy"] = result.Strategy.ToString();
            Response.Headers["PXA-Pages-Unchanged"] = result.PagesUnchanged ? "true" : "false";
            Response.Headers["PXA-Compliance-Profile"] = result.Preflight.TargetProfile;
            Response.Headers["PXA-Invoice-Consistency"] = consistency.Status;
            Response.Headers["PXA-Invoice-Conformance"] = input.Validation!.ConformanceStatus;
            Response.Headers["PXA-Invoice-Mode"] = input.Mode.ToString();
            Response.Headers["PXA-Invoice-Recognition"] =
                input.Mode == PdfEInvoiceAttachmentMode.HybridInvoice ? "recognized" : "unrecognized";
            Response.Headers["PXA-Invoice-Editable"] =
                input.Mode == PdfEInvoiceAttachmentMode.HybridInvoice ? "true" : "false";

            // Only a recognized invoice produces a validation verdict worth retaining;
            // preserved unknown XML has no verdict to prove later.
            if (input.Mode == PdfEInvoiceAttachmentMode.HybridInvoice)
            {
                var recorded = await RecordEvidenceAsync(
                    "attach",
                    input.Payload!.Xml,
                    result.ResultSha256,
                    input.Options!,
                    input.Validation!,
                    null,
                    cancellationToken);
                if (recorded is not null)
                    Response.Headers["PXA-Validation-Record"] = recorded.Id.ToString();
            }

            return File(result.Bytes, "application/pdf", HybridInvoiceFileName(file!.FileName));
        }
        catch (PdfEInvoiceAttachmentBlockedException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The invoice attachment was blocked",
                detail: exception.Message,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "PXAEINVAPI022",
                    ["preflight"] = exception.Preflight,
                    ["consistency"] = consistency
                });
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or ArgumentException)
        {
            return AttachmentInputProblem(exception);
        }
    }

    [HttpGet("validations")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesRead)]
    [ProducesResponseType(typeof(IReadOnlyList<PxaEInvoiceValidationEvidence>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<PxaEInvoiceValidationEvidence>>> Validations(
        [FromQuery] string? documentSha256,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (evidence is null)
            return EvidenceUnavailable();

        try
        {
            return Ok(await evidence.GetHistoryAsync(
                string.IsNullOrWhiteSpace(documentSha256) ? null : documentSha256.Trim(),
                limit ?? 50,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI030",
                "The validation history filter is not valid",
                exception.Message);
        }
    }

    [HttpGet("validations/{id:guid}")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesRead)]
    [ProducesResponseType(typeof(PxaEInvoiceValidationEvidence), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PxaEInvoiceValidationEvidence>> ValidationRecord(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (evidence is null)
            return EvidenceUnavailable();

        var record = await evidence.GetAsync(id, cancellationToken);
        return record is null
            ? InvoiceProblem(
                StatusCodes.Status404NotFound,
                "PXAEINVAPI031",
                "The validation record was not found",
                "It does not exist in this organization, or its retention window has closed.")
            : Ok(record);
    }

    /// <summary>
    /// Downloads the tenant's retained validation evidence as one machine-readable document.
    /// It carries the artifact-set identity of every verdict, so a third party can tell which
    /// pinned official artifacts produced it.
    /// </summary>
    [HttpGet("validations/export")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesRead)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportValidations(
        [FromQuery] string? documentSha256,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (evidence is null)
            return EvidenceUnavailable();

        IReadOnlyList<PxaEInvoiceValidationEvidence> records;
        try
        {
            records = await evidence.GetHistoryAsync(
                string.IsNullOrWhiteSpace(documentSha256) ? null : documentSha256.Trim(),
                limit ?? MaximumEvidencePageSize,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI030",
                "The validation history filter is not valid",
                exception.Message);
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new PxaEInvoiceValidationEvidenceExport(
                "pxa.e-invoice.validation-evidence/1",
                DateTimeOffset.UtcNow,
                records.Count,
                records),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        return File(payload, "application/json", "pxa-e-invoice-validation-evidence.json");
    }

    /// <summary>
    /// Re-runs validation for a stored record using whichever artifact set is configured now,
    /// and stores the new verdict alongside the old one instead of replacing it. The uploaded
    /// document must be the same bytes, proven by its checksum.
    /// </summary>
    [HttpPost("validations/{id:guid}/revalidate")]
    [Authorize(Policy = PxaDesignerPermissions.PdfEInvoicesValidate)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumXmlBytes + 64 * 1024)]
    [ProducesResponseType(typeof(PxaEInvoiceRevalidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PxaEInvoiceRevalidationResponse>> Revalidate(
        Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (evidence is null)
            return EvidenceUnavailable();
        if (ValidateXmlUpload(file) is { } uploadProblem)
            return uploadProblem;

        var previous = await evidence.GetAsync(id, cancellationToken);
        if (previous is null)
        {
            return InvoiceProblem(
                StatusCodes.Status404NotFound,
                "PXAEINVAPI031",
                "The validation record was not found",
                "It does not exist in this organization, or its retention window has closed.");
        }

        var bytes = await ReadBoundedAsync(file!, MaximumXmlBytes, cancellationToken);
        var checksum = Sha256(bytes);
        if (!string.Equals(checksum, previous.DocumentSha256, StringComparison.Ordinal))
        {
            return InvoiceProblem(
                StatusCodes.Status409Conflict,
                "PXAEINVAPI032",
                "The uploaded document is not the one that was validated",
                "Revalidation compares verdicts for identical bytes; upload the document whose SHA-256 matches the stored record.");
        }
        if (!TryReadStandard(previous.Standard, out var parsedStandard))
        {
            return InvoiceProblem(
                StatusCodes.Status409Conflict,
                "PXAEINVAPI033",
                "The stored record cannot be revalidated",
                $"Standard '{previous.Standard}' is no longer supported by this build.");
        }

        var options = new PxaEInvoiceOptions
        {
            Standard = parsedStandard,
            Version = previous.StandardVersion,
            Profile = previous.Profile
        };
        var report = Validator().Validate(bytes, options, cancellationToken);
        var current = await RecordEvidenceAsync(
            "revalidate", bytes, previous.ContainerSha256, options, report, previous.Id, cancellationToken);
        return Ok(new PxaEInvoiceRevalidationResponse(
            previous,
            current!,
            !string.Equals(previous.ConformanceStatus, current!.ConformanceStatus, StringComparison.Ordinal) ||
                previous.IsStructurallyValid != current.IsStructurallyValid,
            !string.Equals(
                previous.ValidatorArtifactSetSha256,
                current.ValidatorArtifactSetSha256,
                StringComparison.Ordinal)));
    }

    private IPxaEInvoiceValidationService Validator() =>
        validationService ?? new StructuralPxaEInvoiceValidationService();

    private IPdfEInvoiceAttacher Attacher() =>
        attacher ?? new PdfEInvoiceAttacher(new PdfA2bConverter(new PdfTrustInspector()));

    private ObjectResult EvidenceUnavailable() => InvoiceProblem(
        StatusCodes.Status503ServiceUnavailable,
        "PXAEINVAPI034",
        "Historical validation evidence is not available",
        "This deployment has no evidence store configured.");

    /// <summary>
    /// Records one verdict. A failure is not swallowed: if PXA cannot retain the evidence, it
    /// must not report the validation as having happened.
    /// </summary>
    private async Task<PxaEInvoiceValidationEvidence?> RecordEvidenceAsync(
        string operation,
        ReadOnlyMemory<byte> document,
        string? containerSha256,
        PxaEInvoiceOptions options,
        PxaEInvoiceValidationReport report,
        Guid? revalidates,
        CancellationToken cancellationToken)
    {
        if (evidence is null)
            return null;

        return await evidence.RecordAsync(
            new PxaEInvoiceValidationEvidenceEntry(
                operation,
                Sha256(document.Span),
                containerSha256,
                options,
                report,
                revalidates),
            cancellationToken);
    }

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(value));

    private static PdfEInvoiceAttachmentOptions AttachmentOptions(
        bool allowArchiveConversion,
        bool allowSignatureInvalidation,
        bool replaceExistingInvoice) => new()
    {
        MaximumPdfSizeBytes = MaximumPdfBytes,
        MaximumOutputSizeBytes = MaximumAttachedPdfBytes,
        AllowArchiveConversion = allowArchiveConversion,
        AllowSignatureInvalidation = allowSignatureInvalidation,
        ReplaceExistingInvoiceAttachment = replaceExistingInvoice
    };

    /// <summary>
    /// Reads and validates both uploads before any PDF is touched. The invoice must pass
    /// structural validation; a configured conformance validator is applied on top, and its
    /// verdict is reported separately instead of being folded into one boolean.
    /// </summary>
    private async Task<AttachmentInput> ReadAttachmentInputAsync(
        IFormFile? file,
        IFormFile? invoice,
        string? standard,
        string? version,
        string? profile,
        bool preserveUnrecognized,
        string? attachmentFileName,
        CancellationToken cancellationToken)
    {
        if (ValidatePdfUpload(file) is { } pdfProblem)
            return new(Problem: pdfProblem);
        if (ValidateXmlUpload(invoice) is { } xmlProblem)
            return new(Problem: xmlProblem);
        if (preserveUnrecognized)
        {
            return await ReadPreservationInputAsync(file!, invoice!, attachmentFileName, cancellationToken);
        }
        if (!TryReadStandard(standard, out var parsedStandard))
        {
            return new(Problem: InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI002",
                "The electronic-invoice standard is not supported",
                "Use FacturX, ZUGFeRD, or XRechnung."));
        }

        var xml = await ReadBoundedAsync(invoice!, MaximumXmlBytes, cancellationToken);
        var options = new PxaEInvoiceOptions
        {
            Standard = parsedStandard,
            Version = version?.Trim() ?? string.Empty,
            Profile = profile?.Trim() ?? string.Empty
        };
        var validation = (validationService ?? new StructuralPxaEInvoiceValidationService())
            .Validate(xml, options, cancellationToken);
        if (!validation.IsStructurallyValid)
        {
            return new(Problem: Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The invoice XML cannot be embedded",
                detail: validation.Diagnostics.FirstOrDefault(static item => item.Severity == "error")?.Message ??
                    "The invoice XML failed structural validation.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "PXAEINVAPI020",
                    ["diagnostics"] = validation.Diagnostics
                }));
        }

        PxaEInvoiceAttachmentDescriptor descriptor;
        try
        {
            descriptor = EInvoice.DescribeAttachment(options);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return new(Problem: InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI023",
                "The invoice attachment could not be described",
                exception.Message));
        }

        return new(
            Pdf: await ReadBoundedAsync(file!, MaximumPdfBytes, cancellationToken),
            Payload: new PdfEInvoiceAttachmentPayload
            {
                Xml = xml,
                FileName = descriptor.FileName,
                MimeType = descriptor.MimeType,
                Relationship = descriptor.Relationship,
                Description = descriptor.Description,
                XmpMetadata = descriptor.XmpMetadata
            },
            Validation: validation,
            Recognition: EInvoice.Recognize(xml),
            Options: options);
    }

    /// <summary>
    /// Stores XML that PXA does not recognize, byte for byte, when the caller asks for it
    /// explicitly. The same bounds as a recognized invoice apply - size, safe filename, MIME
    /// type, no DTDs or external entities, checksum - but nothing about the result claims a
    /// standard: no reserved filename, no Factur-X extension schema, and the /AFRelationship
    /// stays /Unspecified rather than /Alternative.
    /// </summary>
    private async Task<AttachmentInput> ReadPreservationInputAsync(
        IFormFile file,
        IFormFile invoice,
        string? attachmentFileName,
        CancellationToken cancellationToken)
    {
        var fileName = string.IsNullOrWhiteSpace(attachmentFileName)
            ? DefaultPreservedFileName
            : attachmentFileName.Trim();
        if (!IsSafePreservedFileName(fileName))
        {
            return new(Problem: InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI025",
                "The attachment filename is not accepted",
                "Use a plain ASCII .xml filename of up to 120 characters without path separators."));
        }
        if (ReservedInvoiceFileNames.Contains(fileName))
        {
            return new(Problem: InvoiceProblem(
                StatusCodes.Status400BadRequest,
                "PXAEINVAPI026",
                "A reserved invoice filename cannot be used for unrecognized XML",
                $"'{fileName}' is reserved for a maintained standard. Storing unrecognized XML under it would assert a conformance PXA has not established."));
        }

        var xml = await ReadBoundedAsync(invoice, MaximumXmlBytes, cancellationToken);
        var recognition = EInvoice.Recognize(xml);
        if (recognition.IsRecognized)
        {
            return new(Problem: InvoiceProblem(
                StatusCodes.Status409Conflict,
                "PXAEINVAPI028",
                "The XML is already a recognized electronic invoice",
                "Use the normal invoice attachment mode with its standard, version, and profile instead of preserving it as unknown XML."));
        }
        if (recognition.Diagnostics.Any(static item =>
                item.Severity == "error" && UnsafeXmlCodes.Contains(item.Code)))
        {
            return new(Problem: Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The XML cannot be preserved",
                detail: "Preservation still requires well-formed XML within the size limit and without DTDs or external entities.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "PXAEINVAPI027",
                    ["diagnostics"] = recognition.Diagnostics
                }));
        }

        return new(
            Pdf: await ReadBoundedAsync(file, MaximumPdfBytes, cancellationToken),
            Payload: new PdfEInvoiceAttachmentPayload
            {
                Xml = xml,
                FileName = fileName,
                MimeType = "application/xml",
                Relationship = PdfAssociatedFileRelationship.Unspecified,
                Description =
                    "Unrecognized XML preserved byte for byte. PXA does not recognize its syntax, " +
                    "does not treat it as editable invoice data, and makes no conformance claim.",
                XmpMetadata = null
            },
            // Honest report: not a structurally valid invoice, no conformance evaluated.
            Validation: new PxaEInvoiceValidationReport(
                false,
                false,
                recognition.Syntax,
                recognition.GuidelineIdentifier,
                recognition.Diagnostics),
            // Deliberately null: semantic values must not be derived from unrecognized XML.
            Recognition: null,
            Mode: PdfEInvoiceAttachmentMode.PreservedUnknownXml);
    }

    private const string DefaultPreservedFileName = "preserved-invoice.xml";

    /// <summary>Filenames the maintained standards reserve for a recognized invoice payload.</summary>
    private static readonly HashSet<string> ReservedInvoiceFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "factur-x.xml", "zugferd-invoice.xml", "xrechnung.xml", "order-x.xml"
    };

    /// <summary>Recognition failures that mean the XML itself is unsafe or unreadable.</summary>
    private static readonly HashSet<string> UnsafeXmlCodes = new(StringComparer.Ordinal)
    {
        "PXAEINV001", "PXAEINV002", "PXAEINV003", "PXAEINV004"
    };

    private static bool IsSafePreservedFileName(string value) =>
        value.Length <= 120 &&
        value.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
        value.Length > 4 &&
        value.All(static character =>
            character is >= ' ' and <= '~' &&
            character is not ('/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'));

    private static async Task<PxaEInvoicePdfConsistencyReport> InspectConsistencyAsync(
        byte[] pdf,
        PxaEInvoiceRecognitionReport? recognition,
        CancellationToken cancellationToken) =>
        PxaEInvoicePdfConsistencyInspector.Inspect(
            await PxaEInvoicePdfConsistencyInspector.ExtractVisibleTextAsync(pdf, cancellationToken),
            recognition?.SemanticData);

    private ObjectResult AttachmentInputProblem(Exception exception) => InvoiceProblem(
        StatusCodes.Status400BadRequest,
        "PXAEINVAPI024",
        "The invoice could not be attached to the PDF",
        exception.Message);

    private static string HybridInvoiceFileName(string sourceName)
    {
        var name = Path.GetFileNameWithoutExtension(SafeDownloadName(sourceName));
        return (string.IsNullOrWhiteSpace(name) ? "invoice" : name) + "-hybrid.pdf";
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
            PxaEInvoicePdfConsistencyInspector.Inspect(visibleText, recognition?.SemanticData),
            !isXml
                ? "notXml"
                : recognition?.IsRecognized == true && standard is not null
                    ? "recognized"
                    : "unrecognized",
            // Only a recognized invoice of a maintained standard can be mapped onto the typed
            // invoice model, so only that one is editable.
            recognition?.IsRecognized == true && standard is not null);
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

    private sealed record AttachmentInput(
        ActionResult? Problem = null,
        byte[]? Pdf = null,
        PdfEInvoiceAttachmentPayload? Payload = null,
        PxaEInvoiceValidationReport? Validation = null,
        PxaEInvoiceRecognitionReport? Recognition = null,
        PdfEInvoiceAttachmentMode Mode = PdfEInvoiceAttachmentMode.HybridInvoice,
        PxaEInvoiceOptions? Options = null);

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
    PxaEInvoicePdfConsistencyReport PdfConsistency,
    /// <summary>"recognized", "unrecognized" or "notXml".</summary>
    string RecognitionStatus = "notXml",
    /// <summary>
    /// False whenever PXA cannot map the payload onto its typed invoice model. Preserved
    /// unknown XML is readable and extractable but is never offered for editing.
    /// </summary>
    bool IsEditable = false);

public sealed record PdfEInvoiceGenerationRequest(
    string? Standard,
    string? Version,
    string? Profile,
    PxaEInvoiceDocument? Invoice);

/// <summary>A downloadable, machine-readable bundle of retained validation evidence.</summary>
public sealed record PxaEInvoiceValidationEvidenceExport(
    string Schema,
    DateTimeOffset GeneratedAt,
    int RecordCount,
    IReadOnlyList<PxaEInvoiceValidationEvidence> Records);

/// <summary>
/// Both verdicts for the same document bytes: the one that was stored and the one the currently
/// configured artifact set produces. Neither replaces the other.
/// </summary>
public sealed record PxaEInvoiceRevalidationResponse(
    PxaEInvoiceValidationEvidence Previous,
    PxaEInvoiceValidationEvidence Current,
    bool VerdictChanged,
    bool ArtifactSetChanged);

/// <summary>What an attachment operation is allowed to assert about the XML it embeds.</summary>
public enum PdfEInvoiceAttachmentMode
{
    /// <summary>A recognized invoice is embedded as the document's alternative representation.</summary>
    HybridInvoice,

    /// <summary>
    /// XML PXA does not recognize is stored byte for byte at the caller's explicit request.
    /// It is never editable invoice data and never a conformance claim.
    /// </summary>
    PreservedUnknownXml
}

/// <summary>
/// What attaching this invoice to this PDF would do. Structural validation, PXA business
/// validation and official conformance stay separate states in <paramref name="Invoice"/>.
/// </summary>
public sealed record PdfEInvoiceAttachmentPreflightResponse(
    PdfEInvoiceAttachmentPreflightReport Attachment,
    PxaEInvoiceValidationReport Invoice,
    PxaEInvoicePdfConsistencyReport Consistency,
    PdfEInvoiceAttachmentMode Mode,
    string AttachmentFileName,
    bool IsEditable);
