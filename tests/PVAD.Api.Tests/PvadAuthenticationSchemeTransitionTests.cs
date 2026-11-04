using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadAuthenticationSchemeTransitionTests
{
    [Fact]
    public void New_scheme_names_are_canonical_and_historical_aliases_are_distinct()
    {
        Assert.Equal("PVAD.Combined", PvadAuthenticationSchemes.Combined);
        Assert.Equal("PVAD.ApiKey", PvadAuthenticationSchemes.ApiKey);
        Assert.Equal("PVAD.McpDelegation", PvadAuthenticationSchemes.McpDelegation);
        Assert.Equal("PVAD.Designer.Cookie", PvadAuthenticationSchemes.DesignerCookie);
        Assert.NotEqual(PvadAuthenticationSchemes.DesignerCookie, PvadAuthenticationSchemes.LegacyDesignerCookie);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ticket_reader_accepts_only_the_matching_purpose_and_normalizes_the_ticket_scheme(bool legacy)
    {
        var provider = new EphemeralDataProtectionProvider();
        var current = Format(provider, PvadAuthenticationSchemes.DesignerCookie);
        var old = Format(provider, PvadAuthenticationSchemes.LegacyDesignerCookie);
        var account = Format(provider, IdentityConstants.ApplicationScheme);
        var reader = new PvadMigratingTicketDataFormat(current, old);
        var ticket = Ticket(legacy ? PvadAuthenticationSchemes.LegacyDesignerCookie : PvadAuthenticationSchemes.DesignerCookie);
        var encoded = (legacy ? old : current).Protect(ticket, "synthetic-binding");

        var decoded = Assert.IsType<AuthenticationTicket>(reader.Unprotect(encoded, "synthetic-binding"));
        Assert.Equal(PvadAuthenticationSchemes.DesignerCookie, decoded.AuthenticationScheme);
        Assert.Equal(ticket.Properties.ExpiresUtc, decoded.Properties.ExpiresUtc);
        Assert.Equal(legacy, PvadMigratingTicketDataFormat.ConsumeLegacyMarker(decoded.Properties));
        Assert.False(PvadMigratingTicketDataFormat.ConsumeLegacyMarker(decoded.Properties));
        var rewritten = reader.Protect(decoded, "synthetic-binding");
        Assert.NotNull(current.Unprotect(rewritten, "synthetic-binding"));
        Assert.Null(old.Unprotect(rewritten, "synthetic-binding"));
        Assert.Null(account.Unprotect(rewritten, "synthetic-binding"));
        Assert.Null(reader.Unprotect(encoded, "wrong-binding"));
        var middle = encoded.Length / 2;
        var tampered = encoded[..middle] + (encoded[middle] == 'A' ? 'B' : 'A') + encoded[(middle + 1)..];
        Assert.Null(reader.Unprotect(tampered, "synthetic-binding"));
        Assert.Null(reader.Unprotect("invalid-ticket"));
        Assert.Null(reader.Unprotect(account.Protect(Ticket(IdentityConstants.ApplicationScheme))));
        Assert.Null(reader.Unprotect(old.Protect(Ticket(IdentityConstants.ApplicationScheme))));
        Assert.Null(reader.Unprotect(current.Protect(Ticket(IdentityConstants.ApplicationScheme))));
        Assert.Throws<ArgumentException>(() => reader.Protect(Ticket(IdentityConstants.ApplicationScheme)));
    }

    [Fact]
    public void Existing_keyring_reads_legacy_tickets_after_restart_without_changing_the_application_discriminator()
    {
        var directory = Directory.CreateTempSubdirectory("pvad-auth-keyring-");
        try
        {
            var issuer = DataProtectionProvider.Create(directory, options => options.SetApplicationName(PvadDataProtection.ApplicationName));
            var original = Format(issuer, PvadAuthenticationSchemes.LegacyDesignerCookie)
                .Protect(Ticket(PvadAuthenticationSchemes.LegacyDesignerCookie));
            var restarted = DataProtectionProvider.Create(directory, options => options.SetApplicationName(PvadDataProtection.ApplicationName));
            var current = Format(restarted, PvadAuthenticationSchemes.DesignerCookie);
            var reader = new PvadMigratingTicketDataFormat(current, Format(restarted, PvadAuthenticationSchemes.LegacyDesignerCookie));
            var decoded = Assert.IsType<AuthenticationTicket>(reader.Unprotect(original));
            var rewritten = reader.Protect(decoded);
            Assert.NotNull(current.Unprotect(rewritten));
            Assert.False(PvadMigratingTicketDataFormat.ConsumeLegacyMarker(current.Unprotect(rewritten)!.Properties));
            var nextRestart = DataProtectionProvider.Create(directory, options => options.SetApplicationName(PvadDataProtection.ApplicationName));
            Assert.NotNull(Format(nextRestart, PvadAuthenticationSchemes.DesignerCookie).Unprotect(rewritten));
            var unrelated = DataProtectionProvider.Create(directory, options => options.SetApplicationName("UnrelatedSyntheticApplication"));
            Assert.Null(new PvadMigratingTicketDataFormat(Format(unrelated, PvadAuthenticationSchemes.DesignerCookie),
                Format(unrelated, PvadAuthenticationSchemes.LegacyDesignerCookie)).Unprotect(original));
        }
        finally { directory.Delete(recursive: true); }
    }

    internal static TicketDataFormat Format(IDataProtectionProvider provider, string scheme) => new(provider.CreateProtector(
        "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", scheme, "v2"));

    private static AuthenticationTicket Ticket(string scheme) => new(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "synthetic-user")], "synthetic")),
        new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, scheme);
}
