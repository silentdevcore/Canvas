namespace PXA.Migration.Abstractions;

/// <summary>
/// Electronic-invoice capabilities a source SDK may express in customer code, and which PXA
/// therefore has to answer for during a migration.
/// </summary>
public enum PdfEInvoiceCapability
{
    /// <summary>Producing Factur-X invoice XML or a Factur-X hybrid PDF.</summary>
    FacturX,

    /// <summary>Producing ZUGFeRD invoice XML or a ZUGFeRD hybrid PDF.</summary>
    Zugferd,

    /// <summary>Producing XRechnung invoice XML.</summary>
    XRechnung,

    /// <summary>Embedding a file as a PDF/A-3 associated file, the hybrid-invoice mechanism.</summary>
    PdfA3Attachment,

    /// <summary>Reading an embedded invoice back out of a PDF.</summary>
    InvoiceExtraction,

    /// <summary>Validating invoice XML against a standard's schema or rules.</summary>
    InvoiceValidation
}

/// <summary>
/// One provider capability mapping. <see cref="Disposition"/> is deliberately the same three-state
/// vocabulary the trust migrations use, so the unified catalog reports one kind of answer.
/// </summary>
public sealed record PdfEInvoiceProviderCapability
{
    public required PdfEInvoiceCapability Capability { get; init; }

    public required PdfTrustMigrationDisposition Disposition { get; init; }

    /// <summary>
    /// API identifiers in the source SDK that indicate this capability. The mapping is driven by
    /// what a customer's code actually contains, not by a claim about the vendor's product line.
    /// </summary>
    public IReadOnlyList<string> DetectedApiMarkers { get; init; } = [];

    /// <summary>The PXA API a detected marker maps onto, or null when there is no equivalent.</summary>
    public string? PxaEquivalent { get; init; }

    /// <summary>Why the disposition is what it is. Required, so no mapping is a silent TODO.</summary>
    public required string Rationale { get; init; }

    public required string DocumentationPath { get; init; }
}

public sealed record PdfEInvoiceProviderProfile
{
    public required string ProviderKey { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<PdfEInvoiceProviderCapability> Capabilities { get; init; }

    public PdfEInvoiceProviderCapability Capability(PdfEInvoiceCapability capability) =>
        Capabilities.Single(item => item.Capability == capability);
}

/// <summary>
/// One electronic-invoice capability found in a customer's source, and what PXA does with it.
/// </summary>
/// <remarks>
/// A finding is produced whenever source code touches an invoice capability - including when the
/// provider has no such API and the answer is <see cref="PdfTrustMigrationDisposition.Unsupported"/>.
/// That is the point: a capability a migration cannot carry across has to be reported, never
/// silently dropped.
/// </remarks>
public sealed record PdfEInvoiceMigrationFinding
{
    public required string ProviderKey { get; init; }

    public required PdfEInvoiceCapability Capability { get; init; }

    public required PdfTrustMigrationDisposition Disposition { get; init; }

    /// <summary>The stable <c>CANMIGEINV...</c> identifier for this capability.</summary>
    public required string Code { get; init; }

    /// <summary>The API identifiers actually present in the analyzed source, in source order.</summary>
    public required IReadOnlyList<string> MatchedMarkers { get; init; }

    public string? PxaEquivalent { get; init; }

    public required string Rationale { get; init; }

    public required string DocumentationPath { get; init; }
}
