using PVAD.Core.Abstractions;

namespace PVAD.Infrastructure.Pdf;

/// <summary>Describes supported native PDF rendering features without executing them.</summary>
/// <remarks>Capability flags are technical metadata, not customer entitlements or a native license.</remarks>
public sealed class PdfRendererCapabilities : IRendererCapabilities
{
    /// <summary>Gets the stable renderer key, <c>pdf</c>.</summary>
    public string RendererKey => "pdf";

    /// <summary>Gets support for PDF outline bookmarks.</summary>
    public bool SupportsBookmarks => true;

    /// <summary>Gets support for generated table-of-contents pages.</summary>
    public bool SupportsTableOfContents => true;

    /// <summary>Gets support for named page destinations.</summary>
    public bool SupportsNamedDestinations => true;

    /// <summary>Gets support for links within the document.</summary>
    public bool SupportsInternalLinks => true;

    /// <summary>Gets support for links to external addresses.</summary>
    public bool SupportsExternalLinks => true;

    /// <summary>Gets support for image opacity.</summary>
    public bool SupportsImageOpacity => true;

    /// <summary>Gets support for page rotation.</summary>
    public bool SupportsPageRotation => true;

    /// <summary>Gets support for PDF page-boundary settings.</summary>
    public bool SupportsPageBoundaries => true;

    /// <summary>Gets support for text watermarks.</summary>
    public bool SupportsWatermarks => true;

    /// <summary>Gets support for headers and footers.</summary>
    public bool SupportsHeaderFooter => true;

    /// <summary>Gets support for section-aware page numbering.</summary>
    public bool SupportsSectionNumbering => true;

    /// <summary>Gets support for native text decorations.</summary>
    public bool SupportsAdvancedTextDecorations => true;

    /// <summary>Gets support for compressed PDF output.</summary>
    public bool SupportsCompression => true;
}
