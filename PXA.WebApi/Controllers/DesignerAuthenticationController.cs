using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Application.Identity;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/auth/designer-handoff")]
public sealed class DesignerAuthenticationController(
    DesignerAuthorizationCodeService authorizationCodes) : ControllerBase
{
    [Authorize]
    [HttpPost]
    [PxaValidateAntiforgery]
    public async Task<IActionResult> Create(
        CreateDesignerHandoffRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authorizationCodes.CreateAsync(User, request, cancellationToken);
        if (result.Success)
            return Ok(new DesignerHandoffResponse(result.RedirectUrl!));
        return Problem(
            statusCode: result.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status400BadRequest,
            title: result.Forbidden ? "Designer access denied" : "Invalid Designer handoff",
            detail: result.Reason,
            extensions: new Dictionary<string, object?> { ["code"] = result.Code });
    }

    [AllowAnonymous]
    [HttpPost("exchange")]
    [PxaValidateAntiforgery]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Exchange(
        ExchangeDesignerHandoffRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authorizationCodes.ExchangeAsync(
            request,
            Request.Headers.Origin.ToString(),
            HttpContext,
            cancellationToken);
        if (!result.Success)
        {
            return Problem(
                statusCode: result.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status400BadRequest,
                title: result.Forbidden ? "Designer access denied" : "Invalid Designer handoff",
                detail: result.Reason,
                extensions: new Dictionary<string, object?> { ["code"] = result.Code });
        }

        await HttpContext.SignInAsync(
            PxaAuthenticationSchemes.DesignerCookie,
            result.Principal!,
            new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = false,
                ExpiresUtc = result.ExpiresAt,
            });
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return Ok(new DesignerExchangeResponse(result.ReturnPath!));
    }
}

public sealed record DesignerHandoffResponse(string RedirectUrl);
public sealed record DesignerExchangeResponse(string ReturnPath);
