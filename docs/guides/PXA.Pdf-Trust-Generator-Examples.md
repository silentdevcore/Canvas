# PXA PDF Trust Generator Examples

These examples separate document generation from operations that require final PDF bytes. Do not
store certificate passwords, private keys, or signing credentials in `DesignExportDto`.

## AES-256 Password Encryption

```csharp
var document = PXA.Generator.Pdf.CreateDocument();
document.AddPage().DrawTextFromTop("Confidential", 36, 36, 14);

var bytes = document.ToBytes(new PdfSaveOptions
{
    Encryption = new PdfEncryptionOptions
    {
        Algorithm = PdfEncryptionAlgorithm.Aes256,
        UserPassword = "reader-password",
        OwnerPassword = "distinct-owner-password",
        Permissions = PdfPermissions.Print | PdfPermissions.ExtractForAccessibility
    }
});
```

`Aes128` is available only for compatibility. `Rc4_128` is an explicit legacy choice and should
not be used for new documents.

## X.509 Recipient Encryption

```csharp
var bytes = document.ToBytes(new PdfSaveOptions
{
    CertificateEncryption = new PdfCertificateEncryptionOptions
    {
        Recipients = [recipientCertificate]
    }
});
```

## PDF/A-2b and PDF/A-3b

```csharp
var pdfA2b = document.ToBytes(new PdfSaveOptions
{
    ComplianceProfile = PdfComplianceProfile.PdfA2B
});

var pdfA3b = document.ToBytes(new PdfSaveOptions
{
    ComplianceProfile = PdfComplianceProfile.PdfA3B
});
```

PDF/A output cannot be encrypted. Generation fails closed when the selected profile cannot be
validated.

## PDF/UA-1

```csharp
document.Info.Title = "Accessible report";
document.SetLanguage("en-US");

var pdfUa = document.ToBytes(new PdfSaveOptions
{
    AccessibilityProfile = PdfAccessibilityProfile.PdfUa1
});
```

Add semantic roles, alternate text, reading order, table structure, and form descriptions before
saving. PDF/UA generation fails when required semantics are missing and cannot be combined with
encryption in the PXA Designer contract.

## PAdES B, T, LT and LTA

Signing is a post-processing operation over final PDF bytes. Use `PdfPadesSigningService` with an
`IPdfSigningKey` supplied by a protected key provider. Select `PdfPadesProfile.BaselineB`,
`BaselineT`, `BaselineLT`, or `BaselineLTA`; timestamped and long-term profiles require the
corresponding timestamp and revocation-material providers. Always inspect the returned bytes with
`PdfTrustInspector` before distribution.

## Redaction and Sanitization

Redaction also operates on existing final PDF bytes. Run `PdfRedactionPreflight` first, then apply
only removable regions through `PdfRedactionApplier` with an explicit
`PdfRedactionSanitizationPolicy`. A visual rectangle is not a redaction. Distribute output only
after the apply result reports successful verification.

## Tenant-Scoped Trust Inspection Jobs

The PXA Viewer submits inspection work to
`POST /api/pxa/v1/pdf/trust/inspect/jobs`. The endpoint accepts one bounded PDF upload
and returns `202 Accepted` with a tenant-scoped job ID and status URL. Poll the status
URL and download the JSON report only after the job reaches `Completed`.

```text
POST /api/pxa/v1/pdf/trust/inspect/jobs
GET  /api/pxa/v1/jobs/{jobId}
GET  /api/pxa/v1/jobs/{jobId}/result
POST /api/pxa/v1/jobs/{jobId}/cancel
```

Inspection inputs and reports use transient object storage. Their content expires
after one hour by default and is purged after the first successful result download;
terminal job metadata is retained separately for operational diagnosis. Every
completed inspection is correlated with an immutable operation record and append-only
audit event by document hash and job ID. Another organization cannot read the job,
input, report, or audit evidence. `POST /api/pxa/v1/pdf/trust/inspect` remains a
protected synchronous compatibility endpoint for existing integrations.

## One Final-Byte Trust Pipeline

Use `IPdfTrustPostProcessingPipeline` when multiple operations must be applied safely. Destructive
redaction and sanitization always run before signing, and the exact returned bytes are inspected
last. The serializable plan carries stable provider and key IDs; the trusted host supplies the
resolved key and timestamp services through the runtime-only context.

```csharp
var result = await pipeline.ProcessAsync(
    generatedPdfBytes,
    new PdfTrustPostProcessingOptions
    {
        Redaction = new PdfTrustRedactionOptions
        {
            Regions = redactionRegions,
            Sanitization = new PdfRedactionSanitizationPolicy(),
            Reason = "Approved disclosure copy"
        },
        Signing = new PdfTrustSigningOptions
        {
            CertificateSource = new PdfSigningCertificateSource
            {
                Kind = PdfSigningCertificateSourceKind.OrganizationKeyProvider,
                ProviderId = "company-hsm",
                KeyId = "document-signing-key"
            },
            Profile = PdfPadesProfile.BaselineLT
        },
        Validation = new PdfTrustPipelineValidationPolicy
        {
            RequireValidStructure = true,
            RequireValidSignatures = true,
            RejectRiskyContent = true
        }
    },
    new PdfTrustPipelineContext
    {
        DocumentSigningKey = resolvedSigningKey,
        DocumentTimestampAuthority = timestampAuthority,
        LongTermValidationProvider = revocationMaterialProvider,
        RedactionAuditSigningKey = resolvedAuditKey
    },
    cancellationToken);
```

Never serialize `PdfTrustPipelineContext`. Resolve it after authentication, tenant isolation,
permission checks, and entitlement checks inside the trusted application host.

## Designer and Code Workspace

`DesignExportDto.PageSettings` carries PDF/A, PDF/UA and encryption settings through JSON, C#
Model, C# PDF Builder, and FromBase64String representations. The semantic C# PDF Builder returns
the canonical design to the trusted host, which applies the same save options and validation as a
normal Designer export. Cryptographic operations are never simulated in Live Preview.
