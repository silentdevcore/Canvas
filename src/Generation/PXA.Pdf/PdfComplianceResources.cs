using PXA.Pdf.Serialization;

namespace PXA.Pdf;

/// <summary>Deterministic resources shared by PDF compliance generation and conversion.</summary>
internal static class PdfComplianceResources
{
    /// <summary>Creates the compact ICC v2 sRGB profile used by PXA PDF/A-2b output.</summary>
    internal static byte[] CreateSrgbIccProfile() => PdfSrgbIccProfile.Create();
}
