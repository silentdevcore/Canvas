using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Services.Mail;

public sealed class IdentityActionTokenService
{
    public const string InvitationPurpose = "user-invitation";
    public const string PasswordResetPurpose = "password-reset";

    private readonly PxaDbContext dbContext;

    public IdentityActionTokenService(PxaDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IssuedActionToken> IssueAsync(
        Guid userId,
        Guid? organizationId,
        string recipientEmail,
        string purpose,
        object metadata,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existing = await dbContext.IdentityActionTokens
            .Where(token => token.UserId == userId &&
                            token.Purpose == purpose &&
                            token.UsedAt == null &&
                            token.SupersededAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in existing)
            token.SupersededAt = now;

        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var actionToken = new IdentityActionToken
        {
            UserId = userId,
            OrganizationId = organizationId,
            RecipientEmail = recipientEmail,
            Purpose = purpose,
            TokenHash = Hash(rawToken),
            MetadataJson = JsonSerializer.Serialize(metadata),
            ExpiresAt = now.Add(lifetime),
        };
        dbContext.IdentityActionTokens.Add(actionToken);
        return new IssuedActionToken(actionToken, rawToken);
    }

    public Task<IdentityActionToken?> FindValidAsync(
        string rawToken,
        string purpose,
        CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var now = DateTimeOffset.UtcNow;
        return dbContext.IdentityActionTokens.SingleOrDefaultAsync(token =>
            token.TokenHash == hash &&
            token.Purpose == purpose &&
            token.UsedAt == null &&
            token.SupersededAt == null &&
            token.ExpiresAt > now,
            cancellationToken);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed record IssuedActionToken(IdentityActionToken Entity, string RawToken);
