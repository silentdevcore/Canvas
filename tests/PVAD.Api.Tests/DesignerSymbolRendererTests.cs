using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class DesignerSymbolRendererTests
{
    [Fact]
    public void QrCode_is_deterministic_and_shared_with_code_execution()
    {
        const string value = "https://powerdoxautomation.com/parity";
        var canonical = PvadSymbolRenderer.RenderQrCode(value);

        Assert.Equal(canonical, PvadSymbolRenderer.RenderQrCode(value));
        Assert.Equal(canonical, ScriptHelpers.GenerateQrPng(value));
        AssertPng(canonical);
        Assert.Equal(ReadPngWidth(canonical), ReadPngHeight(canonical));
    }

    [Fact]
    public void Barcode_preserves_requested_pixel_dimensions()
    {
        var canonical = PvadSymbolRenderer.RenderBarcode("PXA-2026-0001", "CODE128", 370, 70);

        Assert.Equal(canonical, ScriptHelpers.GenerateBarcodePng("PXA-2026-0001", "CODE128", 370, 70));
        AssertPng(canonical);
        Assert.Equal(370, ReadPngWidth(canonical));
        Assert.Equal(70, ReadPngHeight(canonical));
    }

    [Fact]
    public void Designer_symbol_endpoint_requires_designer_authentication_and_rate_limiting()
    {
        var type = typeof(DesignerSymbolsController);
        var authorize = Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(PvadAuthenticationSchemes.DesignerCookie, authorize.AuthenticationSchemes);
        var rateLimit = Assert.Single(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
            .Cast<EnableRateLimitingAttribute>());
        Assert.Equal("designer-symbols", rateLimit.PolicyName);
    }

    private static void AssertPng(byte[] bytes) =>
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes[..8]);

    private static int ReadPngWidth(byte[] bytes) => ReadBigEndianInt32(bytes, 16);
    private static int ReadPngHeight(byte[] bytes) => ReadBigEndianInt32(bytes, 20);

    private static int ReadBigEndianInt32(byte[] bytes, int offset) =>
        (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
}
