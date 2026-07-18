using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Services.Licensing;

namespace PXA.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/pxa/v1/licensing")]
public sealed class LicensePublicKeyController : ControllerBase
{
    [HttpGet("public-key")]
    public ActionResult<LicensePublicKeyResponse> GetPublicKey([FromServices] IPxaLicenseSigningService signingService) =>
        Ok(new LicensePublicKeyResponse(signingService.KeyId, "ECDSA_P256_SHA256", signingService.PublicKeyPem));
}

public sealed record LicensePublicKeyResponse(string KeyId, string Algorithm, string PublicKeyPem);
