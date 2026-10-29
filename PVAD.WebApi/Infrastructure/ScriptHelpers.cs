namespace PVAD.WebApi.Infrastructure;

/// <summary>
/// Public helpers callable from Roslyn scripts (csharp-code-to-pdf endpoint).
/// </summary>
public static class ScriptHelpers
{
    public static byte[] GenerateQrPng(string value, int scale = 10)
        => PvadSymbolRenderer.RenderQrCode(value, scale);

    public static byte[] GenerateBarcodePng(string value, string? format, int width, int height)
        => PvadSymbolRenderer.RenderBarcode(value, format, width, height);
}
