using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace PVAD.WebApi.Security;

public static class PvadAuthenticationCompatibility
{
    public static AuthenticationBuilder AddPvadAuthenticationCompatibility(this AuthenticationBuilder builder)
    {
        foreach (var (legacy, current) in new[]
        {
            (PvadAuthenticationSchemes.LegacyCombined, PvadAuthenticationSchemes.Combined),
            (PvadAuthenticationSchemes.LegacyApiKey, PvadAuthenticationSchemes.ApiKey),
            (PvadAuthenticationSchemes.LegacyMcpDelegation, PvadAuthenticationSchemes.McpDelegation),
            (PvadAuthenticationSchemes.LegacyDesignerCookie, PvadAuthenticationSchemes.DesignerCookie),
        })
            builder.AddPolicyScheme(legacy, legacy, options => options.ForwardDefault = current);

        // Register after AddCookie so the framework has initialized the current ticket format.
        builder.Services.AddOptions<CookieAuthenticationOptions>(PvadAuthenticationSchemes.DesignerCookie)
            .PostConfigure<IDataProtectionProvider>((options, provider) =>
            {
                var oldFormat = new TicketDataFormat((options.DataProtectionProvider ?? provider).CreateProtector(
                    "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware",
                    PvadAuthenticationSchemes.LegacyDesignerCookie, "v2"));
                options.TicketDataFormat = new PvadMigratingTicketDataFormat(options.TicketDataFormat, oldFormat);
            });
        return builder;
    }
}
