using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PVAD.WebApi.Services.Licensing;

namespace PVAD.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/pvad/v1/licensing")]
public sealed class LicensePublicKeyController : ControllerBase
{
    [HttpGet("public-key")]
    public ActionResult<LicensePublicKeyResponse> GetPublicKey([FromServices] IPvadLicenseSignatureVerifier signatureVerifier) =>
        Ok(new LicensePublicKeyResponse(
            signatureVerifier.KeyId,
            "ECDSA_P256_SHA256",
            signatureVerifier.PublicKeyPem));
}

public sealed record LicensePublicKeyResponse(string KeyId, string Algorithm, string PublicKeyPem);
