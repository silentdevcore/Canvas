using System.Text.Json;
using PVAD.Domain.Entities;
using PVAD.Importer.Trust;
using PVAD.WebApi.Services.Pdf;
using PVAD.WebApi.Services.Storage;

namespace PVAD.WebApi.Services.Jobs;

public sealed class PdfTrustInspectionJobExecutor(
    PvadStoredObjectService storedObjects,
    IPdfTrustInspector inspector,
    IPvadPdfTrustPolicyProvider trustPolicyProvider,
    IPvadPdfTrustAuditWriter auditWriter)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PdfTrustInspectionJobResult> ExecuteAsync(
        PvadBackgroundJob job,
        CancellationToken cancellationToken)
    {
        if (job.Type != PvadJobQueue.PdfTrustInspectionType || job.InputObjectId is not { } inputObjectId)
            throw new PvadPermanentJobException("The PDF Trust inspection job is invalid.");
        if (job.ResultObjectId is { } existingResultId)
        {
            var existingResult = await storedObjects.OpenAsync(
                existingResultId,
                job.OrganizationId,
                cancellationToken);
            if (existingResult is not null)
            {
                await existingResult.Value.Content.DisposeAsync();
                return new PdfTrustInspectionJobResult(
                    existingResult.Value.Metadata,
                    job.DiagnosticsJson ?? "[]");
            }

            job.ResultObjectId = null;
            job.DiagnosticsJson = null;
        }

        var input = await storedObjects.OpenAsync(inputObjectId, job.OrganizationId, cancellationToken)
            ?? throw new PvadPermanentJobException("The PDF Trust inspection input is unavailable.");
        await using var inputContent = input.Content;
        var report = await inspector.InspectAsync(
            inputContent,
            new PdfTrustInspectionOptions
            {
                MaximumFileSizeBytes = PdfTrustInspectionOptions.DefaultMaximumFileSizeBytes,
                SignatureValidationPolicy = trustPolicyProvider.Policy,
            },
            cancellationToken);
        var diagnosticCodes = report.Diagnostics.Select(value => value.Code).ToArray();
        await using var output = new MemoryStream(
            JsonSerializer.SerializeToUtf8Bytes(report, JsonOptions),
            writable: false);
        var result = await storedObjects.StoreAsync(
            job.OrganizationId,
            job.CreatedByUserId,
            "job-result",
            "application/json",
            "pdf-trust-report.json",
            output,
            cancellationToken);
        var diagnosticsJson = JsonSerializer.Serialize(diagnosticCodes, JsonOptions);
        job.ResultObjectId = result.Id;
        job.DiagnosticsJson = diagnosticsJson;
        try
        {
            await auditWriter.WriteForTenantAsync(
                job.OrganizationId,
                job.CreatedByUserId,
                new PvadPdfTrustAuditEntry(
                    "pdf.trust.inspected",
                    "configured-validation-policy",
                    report.Sha256,
                    null,
                    report.Status.ToString().ToLowerInvariant(),
                    diagnosticCodes,
                    BackgroundJobId: job.Id),
                cancellationToken);
        }
        catch
        {
            job.ResultObjectId = null;
            job.DiagnosticsJson = null;
            await storedObjects.DeleteAsync(result.Id, job.OrganizationId, CancellationToken.None);
            throw;
        }
        return new PdfTrustInspectionJobResult(result, diagnosticsJson);
    }
}

public sealed record PdfTrustInspectionJobResult(PvadStoredObject Result, string DiagnosticsJson);
