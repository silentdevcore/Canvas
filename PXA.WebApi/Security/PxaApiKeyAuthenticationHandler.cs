using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Security;

public sealed class PxaApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly PxaDbContext dbContext;

    public PxaApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        PxaDbContext dbContext)
        : base(options, logger, encoder)
    {
        this.dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var secret = ReadSecret(Request);
        if (secret is null)
            return AuthenticateResult.NoResult();
        if (!secret.StartsWith("pxa_", StringComparison.Ordinal) || secret.Length > 256)
            return AuthenticateResult.Fail("Invalid PXA API key.");

        var now = DateTimeOffset.UtcNow;
        var hash = PxaApiKeySecret.Hash(secret);
        var apiKey = await dbContext.ApiKeys
            .Join(dbContext.ServiceAccounts,
                key => key.ServiceAccountId,
                account => account.Id,
                (key, account) => new { Key = key, Account = account })
            .Join(dbContext.Organizations,
                value => value.Key.OrganizationId,
                organization => organization.Id,
                (value, organization) => new { value.Key, value.Account, Organization = organization })
            .SingleOrDefaultAsync(value => value.Key.SecretHash == hash, Context.RequestAborted);
        if (apiKey is null || apiKey.Key.RevokedAt is not null || !apiKey.Account.IsActive ||
            apiKey.Organization.Status != OrganizationStatus.Active ||
            apiKey.Key.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return AuthenticateResult.Fail("Invalid or inactive PXA API key.");
        }

        apiKey.Key.LastUsedAt = now;
        await dbContext.SaveChangesAsync(Context.RequestAborted);
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, apiKey.Account.Name),
            new Claim(PxaClaimTypes.ActiveOrganization, apiKey.Key.OrganizationId.ToString()),
            new Claim(PxaClaimTypes.ServiceAccount, apiKey.Account.Id.ToString()),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private static string? ReadSecret(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-PXA-API-Key", out var header) && header.Count == 1)
            return header[0];
        var authorization = request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer pxa_", StringComparison.Ordinal)
            ? authorization["Bearer ".Length..]
            : null;
    }
}
