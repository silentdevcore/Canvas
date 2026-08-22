using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = PxaAuthenticationSchemes.DesignerCookie)]
[EnableRateLimiting("designer-symbols")]
[Route("api/pxa/v1/designer/symbols")]
public sealed class DesignerSymbolsController : ControllerBase
{
    [HttpPost("render")]
    [Produces("image/png")]
    public ActionResult Render(DesignerSymbolRequest request)
    {
        var value = request.Value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 2048)
            return ValidationProblem("Symbol values must contain between 1 and 2048 characters.");

        try
        {
            var bytes = request.Type?.ToLowerInvariant() switch
            {
                "qrcode" => PxaSymbolRenderer.RenderQrCode(value),
                "barcode" => PxaSymbolRenderer.RenderBarcode(
                    value,
                    request.Format,
                    Math.Clamp(request.Width ?? 320, 1, 4096),
                    Math.Clamp(request.Height ?? 80, 1, 4096)),
                _ => null,
            };
            if (bytes is null)
                return ValidationProblem("Symbol type must be 'qrcode' or 'barcode'.");

            Response.Headers.CacheControl = "private, max-age=3600";
            return File(bytes, "image/png");
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception.Message);
        }
    }
}

public sealed record DesignerSymbolRequest(
    string? Type,
    string? Value,
    string? Format,
    int? Width,
    int? Height);
