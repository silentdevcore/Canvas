using Microsoft.AspNetCore.Diagnostics;
using PVAD.Licensing;

namespace PVAD.WebApi.Infrastructure;

public sealed class PvadNativeLicenseExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not PvadLicenseException license) return false;
        if (license.Code == "PVAD_ACCOUNT_CAPABILITY_DENIED")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(PvadApiProblems.Create(context, 403,
                "Account capability denied", "This operation is not included in this account's rights.", license.Code),
                options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            return true;
        }
        // This is a host license failure, not a user's role or subscription denial.
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        var problem = PvadApiProblems.Create(context, context.Response.StatusCode,
            "Native processing license unavailable", "Contact the server operator to configure or renew the native product license.", license.Code);
        await context.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
