using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PXA.WebApi.Controllers;

[ApiController]
[Route("api/pxa/v1/version")]
public sealed class VersionController : ControllerBase
{
    private const string ApiContractVersion = "v1";

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    public ActionResult<VersionResponse> Get()
    {
        var assembly = typeof(VersionController).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        var productVersion = informationalVersion.Split('+', 2)[0];

        return Ok(new VersionResponse(
            Product: "PXA",
            ProductVersion: productVersion,
            InformationalVersion: informationalVersion,
            CommitId: Environment.GetEnvironmentVariable("PXA_BUILD_COMMIT") ?? "unknown",
            BuildTime: Environment.GetEnvironmentVariable("PXA_BUILD_TIME"),
            ApiContractVersion: ApiContractVersion));
    }
}

public sealed record VersionResponse(
    string Product,
    string ProductVersion,
    string InformationalVersion,
    string CommitId,
    string? BuildTime,
    string ApiContractVersion);
