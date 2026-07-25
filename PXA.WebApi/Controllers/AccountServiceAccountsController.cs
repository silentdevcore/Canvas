using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/account/service-accounts")]
public sealed class AccountServiceAccountsController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly OrganizationNotificationService notifications;

    public AccountServiceAccountsController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        OrganizationNotificationService notifications)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.notifications = notifications;
    }

    [HttpGet]
    [Authorize(Policy = PxaAccountPermissions.ServiceAccountsRead)]
    public async Task<ActionResult<IReadOnlyList<AccountServiceAccountResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();
        var accounts = await dbContext.ServiceAccounts.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId)
            .OrderBy(value => value.Name)
            .Select(value => new { value.Id, value.Name, value.IsActive, value.CreatedAt, value.RevokedAt })
            .ToListAsync(cancellationToken);
        var accountIds = accounts.Select(value => value.Id).ToArray();
        var keys = await dbContext.ApiKeys.AsNoTracking()
            .Where(value => accountIds.Contains(value.ServiceAccountId))
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new AccountApiKeyResponse(
                value.Id, value.ServiceAccountId, value.Name, value.Prefix, value.ExpiresAt,
                value.LastUsedAt, value.CreatedAt, value.RevokedAt))
            .ToListAsync(cancellationToken);
        return Ok(accounts.Select(value => new AccountServiceAccountResponse(
            value.Id, value.Name, value.IsActive, value.CreatedAt, value.RevokedAt,
            keys.Where(key => key.ServiceAccountId == value.Id).ToArray())).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = PxaAccountPermissions.ServiceAccountsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.serviceaccounts.created")]
    [EnableRateLimiting("account-service-accounts")]
    public async Task<ActionResult<AccountServiceAccountResponse>> Create(
        CreateAccountServiceAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160)
            return ValidationProblem("A service-account name between 1 and 160 characters is required.");
        if (await dbContext.ServiceAccounts.AnyAsync(
                value => value.OrganizationId == organizationId && value.Name == name, cancellationToken))
            return ConflictProblem("A service account with this name already exists.");

        var account = new ServiceAccount
        {
            OrganizationId = organizationId,
            Name = name,
            CreatedByUserId = tenantContext.UserId,
        };
        dbContext.ServiceAccounts.Add(account);
        AddAudit(account, "account.serviceaccounts.created");
        await QueueSecurityNotification(
            account,
            $"account-service-account-created:{account.Id}",
            $"Service account \"{account.Name}\" was created.",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new AccountServiceAccountResponse(
            account.Id, account.Name, account.IsActive, account.CreatedAt, account.RevokedAt, []));
    }

    [HttpPost("{accountId:guid}/keys")]
    [Authorize(Policy = PxaAccountPermissions.ServiceAccountsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.serviceaccounts.key-created")]
    [EnableRateLimiting("account-service-accounts")]
    public async Task<ActionResult<CreateAccountApiKeyResponse>> CreateKey(
        Guid accountId,
        CreateAccountApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var account = await FindAccount(accountId, cancellationToken);
        if (account is null)
            return NotFound();
        if (!account.IsActive)
            return ConflictProblem("Keys cannot be created for an inactive service account.");
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160)
            return ValidationProblem("A key name between 1 and 160 characters is required.");
        if (request.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
            return ValidationProblem("The API-key expiry must be in the future.");

        var generated = PxaApiKeySecret.Create();
        var apiKey = new ApiKey
        {
            OrganizationId = account.OrganizationId,
            ServiceAccountId = account.Id,
            Name = name,
            Prefix = generated.Prefix,
            SecretHash = generated.Hash,
            ExpiresAt = request.ExpiresAt,
        };
        dbContext.ApiKeys.Add(apiKey);
        AddAudit(account, "account.serviceaccounts.key-created", new { apiKey.Id, apiKey.Name, apiKey.Prefix, apiKey.ExpiresAt });
        await QueueSecurityNotification(
            account,
            $"account-api-key-created:{apiKey.Id}",
            $"API key \"{apiKey.Name}\" was created for service account \"{account.Name}\".",
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new CreateAccountApiKeyResponse(
            apiKey.Id, account.Id, apiKey.Name, apiKey.Prefix, generated.Secret,
            apiKey.ExpiresAt, apiKey.CreatedAt));
    }

    [HttpPost("{accountId:guid}/keys/{keyId:guid}/revoke")]
    [Authorize(Policy = PxaAccountPermissions.ServiceAccountsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.serviceaccounts.key-revoked")]
    public async Task<IActionResult> RevokeKey(
        Guid accountId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();
        var key = await dbContext.ApiKeys.SingleOrDefaultAsync(value =>
            value.Id == keyId && value.ServiceAccountId == accountId && value.OrganizationId == organizationId,
            cancellationToken);
        if (key is null)
            return NotFound();
        if (key.RevokedAt is null)
        {
            key.RevokedAt = DateTimeOffset.UtcNow;
            AddAudit(new ServiceAccount { Id = accountId, OrganizationId = organizationId, Name = string.Empty },
                "account.serviceaccounts.key-revoked", new { key.Id, key.Name, key.Prefix });
            await notifications.QueueAdministratorsAsync(
                organizationId,
                "security.organization-changed",
                $"account-api-key-revoked:{key.Id}:{key.RevokedAt.Value.UtcDateTime.Ticks}",
                new Dictionary<string, string>
                {
                    ["summary"] = $"API key \"{key.Name}\" was revoked.",
                },
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("{accountId:guid}/revoke")]
    [Authorize(Policy = PxaAccountPermissions.ServiceAccountsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("account.serviceaccounts.revoked")]
    public async Task<IActionResult> Revoke(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await FindAccount(accountId, cancellationToken);
        if (account is null)
            return NotFound();
        if (account.IsActive)
        {
            var now = DateTimeOffset.UtcNow;
            account.IsActive = false;
            account.RevokedAt = now;
            account.UpdatedAt = now;
            var activeKeys = await dbContext.ApiKeys
                .Where(value => value.ServiceAccountId == account.Id && value.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var key in activeKeys)
                key.RevokedAt = now;
            AddAudit(account, "account.serviceaccounts.revoked");
            await QueueSecurityNotification(
                account,
                $"account-service-account-revoked:{account.Id}:{now.UtcDateTime.Ticks}",
                $"Service account \"{account.Name}\" was revoked.",
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    private Task<ServiceAccount?> FindAccount(Guid id, CancellationToken cancellationToken) =>
        tenantContext.OrganizationId is { } organizationId
            ? dbContext.ServiceAccounts.SingleOrDefaultAsync(
                value => value.Id == id && value.OrganizationId == organizationId, cancellationToken)
            : Task.FromResult<ServiceAccount?>(null);

    private void AddAudit(ServiceAccount account, string action, object? details = null) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = account.OrganizationId,
            ActorUserId = tenantContext.UserId,
            Action = action,
            TargetType = "service-account",
            TargetId = account.Id.ToString(),
            Outcome = "succeeded",
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details),
        });

    private Task<int> QueueSecurityNotification(
        ServiceAccount account,
        string eventKey,
        string summary,
        CancellationToken cancellationToken) =>
        notifications.QueueAdministratorsAsync(
            account.OrganizationId,
            "security.organization-changed",
            eventKey,
            new Dictionary<string, string> { ["summary"] = summary },
            cancellationToken);

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden, title: "Organization context required");

    private ObjectResult ValidationProblem(string detail) => Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "Invalid service-account request", detail: detail);

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict, title: "Service-account operation rejected", detail: detail);
}

public sealed record CreateAccountServiceAccountRequest(string? Name);
public sealed record CreateAccountApiKeyRequest(string? Name, DateTimeOffset? ExpiresAt);
public sealed record AccountServiceAccountResponse(
    Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt,
    IReadOnlyList<AccountApiKeyResponse> Keys);
public sealed record AccountApiKeyResponse(
    Guid Id, Guid ServiceAccountId, string Name, string Prefix, DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
public sealed record CreateAccountApiKeyResponse(
    Guid Id, Guid ServiceAccountId, string Name, string Prefix, string Secret,
    DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt);
