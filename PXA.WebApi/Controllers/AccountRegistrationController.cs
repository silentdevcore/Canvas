using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.WebApi.Application.Identity;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/pxa/v1/auth")]
public sealed class AccountRegistrationController : ControllerBase
{
    private readonly CustomerRegistrationService registrationService;

    public AccountRegistrationController(CustomerRegistrationService registrationService)
    {
        this.registrationService = registrationService;
    }

    [HttpPost("register")]
    [PxaValidateAntiforgery]
    [EnableRateLimiting("registration")]
    public async Task<ActionResult<RegistrationAcceptedResponse>> Register(
        RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await registrationService.RegisterAsync(request, cancellationToken);
        return outcome.Status switch
        {
            CustomerRegistrationStatus.Accepted => Accepted(AcceptedResponse()),
            CustomerRegistrationStatus.SlugConflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Organization unavailable",
                Detail = outcome.Detail,
            }),
            CustomerRegistrationStatus.Unavailable => Problem(statusCode: 503, title: outcome.Detail),
            _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid registration", detail: outcome.Detail),
        };
    }

    [HttpPost("resend-verification")]
    [PxaValidateAntiforgery]
    [EnableRateLimiting("registration")]
    public async Task<ActionResult<RegistrationAcceptedResponse>> ResendVerification(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        await registrationService.ResendVerificationAsync(request.Email, cancellationToken);
        return Accepted(AcceptedResponse());
    }

    [HttpPost("verify-email")]
    [PxaValidateAntiforgery]
    [EnableRateLimiting("identity-action")]
    public async Task<IActionResult> VerifyEmail(
        VerifyRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await registrationService.VerifyEmailAsync(request.Token, cancellationToken);
        return outcome.Status == EmailVerificationStatus.Succeeded
            ? NoContent()
            : InvalidVerification();
    }

    private static RegistrationAcceptedResponse AcceptedResponse() => new(
        "If the registration can be accepted, a verification message will be sent shortly.");

    private ObjectResult InvalidVerification() => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid or expired verification",
        detail: "Request a new registration or verification message.");
}
