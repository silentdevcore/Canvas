using Microsoft.AspNetCore.Authentication;

namespace PVAD.WebApi.Security;

public sealed class PvadMigratingTicketDataFormat(
    ISecureDataFormat<AuthenticationTicket> current,
    ISecureDataFormat<AuthenticationTicket> legacy) : ISecureDataFormat<AuthenticationTicket>
{
    private const string RenewalMarker = ".pvad.legacy-designer-ticket";

    public static bool ConsumeLegacyMarker(AuthenticationProperties properties) => properties.Items.Remove(RenewalMarker);

    public string Protect(AuthenticationTicket data) => Protect(data, null);

    public string Protect(AuthenticationTicket data, string? purpose)
    {
        if (data.AuthenticationScheme is not (PvadAuthenticationSchemes.DesignerCookie or PvadAuthenticationSchemes.LegacyDesignerCookie))
            throw new ArgumentException("Only Designer tickets can be written with this format.", nameof(data));
        var properties = data.Properties.Clone();
        properties.Items.Remove(RenewalMarker);
        return current.Protect(new AuthenticationTicket(data.Principal, properties,
            PvadAuthenticationSchemes.DesignerCookie), purpose);
    }

    public AuthenticationTicket? Unprotect(string? protectedText) => Unprotect(protectedText, null);

    public AuthenticationTicket? Unprotect(string? protectedText, string? purpose)
    {
        var ticket = current.Unprotect(protectedText, purpose);
        if (ticket is not null)
            return ticket.AuthenticationScheme == PvadAuthenticationSchemes.DesignerCookie ? ticket : null;

        ticket = legacy.Unprotect(protectedText, purpose);
        if (ticket?.AuthenticationScheme != PvadAuthenticationSchemes.LegacyDesignerCookie) return null;
        ticket.Properties.Items[RenewalMarker] = "true";
        return new AuthenticationTicket(ticket.Principal, ticket.Properties, PvadAuthenticationSchemes.DesignerCookie);
    }
}
