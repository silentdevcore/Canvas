using PVAD.Pdf.Serialization;

namespace PVAD.Pdf;

/// <summary>Deterministic resources shared by PDF compliance generation and conversion.</summary>
internal static class PdfComplianceResources
{
    /// <summary>Creates the compact ICC v2 sRGB profile used by PVAD PDF/A-2b output.</summary>
    internal static byte[] CreateSrgbIccProfile() => PdfSrgbIccProfile.Create();
}
