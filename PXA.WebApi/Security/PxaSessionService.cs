using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Security;

public sealed class PxaSessionService(PxaDbContext dbContext)
{
    public async Task<UserSession> CreateAsync(
        Guid userId,
        Guid? organizationId,
        DateTimeOffset expiresAt,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new UserSession
        {
            UserId = userId,
            OrganizationId = organizationId,
            IpAddressHash = HashIpAddress(httpContext.Connection.RemoteIpAddress?.ToString()),
            UserAgent = ReduceUserAgent(httpContext.Request.Headers.UserAgent.ToString()),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = expiresAt,
        };
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task RevokeCurrentAsync(
        ClaimsPrincipal principal,
        Guid? revokedByUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetSessionId(principal, out var sessionId))
            return;

        var session = await dbContext.UserSessions.SingleOrDefaultAsync(
            value => value.Id == sessionId && value.RevokedAt == null,
            cancellationToken);
        if (session is null)
            return;

        session.RevokedAt = DateTimeOffset.UtcNow;
        session.RevokedByUserId = revokedByUserId;
        session.RevocationReason = reason;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static bool TryGetSessionId(ClaimsPrincipal principal, out Guid sessionId) =>
        Guid.TryParse(principal.FindFirstValue(PxaClaimTypes.Session), out sessionId);

    public static string ReduceUserAgent(string value)
    {
        var normalized = string.Join(' ', value.Split(
            ['\r', '\n', '\t', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(normalized))
            return "Unknown client";
        return normalized[..Math.Min(normalized.Length, 200)];
    }

    private static string HashIpAddress(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? "unknown"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
