using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/admin/service-accounts")]
public sealed class AdminServiceAccountsController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;

    public AdminServiceAccountsController(PxaDbContext dbContext, IPxaTenantContext tenantContext)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
    }

    [HttpGet]
    [Authorize(Policy = PxaPermissions.ServiceAccountsRead)]
    public async Task<ActionResult<IReadOnlyList<ServiceAccountResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();
        var accounts = await dbContext.ServiceAccounts.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId)
            .OrderBy(value => value.Name)
            .Select(value => new
            {
                value.Id,
                value.Name,
                value.IsActive,
                value.CreatedAt,
                value.RevokedAt,
            })
            .ToListAsync(cancellationToken);
        var accountIds = accounts.Select(value => value.Id).ToArray();
        var keys = await dbContext.ApiKeys.AsNoTracking()
            .Where(value => accountIds.Contains(value.ServiceAccountId))
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new ApiKeyResponse(
                value.Id, value.ServiceAccountId, value.Name, value.Prefix, value.ExpiresAt,
                value.LastUsedAt, value.CreatedAt, value.RevokedAt))
            .ToListAsync(cancellationToken);
        return Ok(accounts.Select(value => new ServiceAccountResponse(
            value.Id, value.Name, value.IsActive, value.CreatedAt, value.RevokedAt,
            keys.Where(key => key.ServiceAccountId == value.Id).ToArray())).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = PxaPermissions.ServiceAccountsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("service_accounts.create")]
    public async Task<ActionResult<ServiceAccountResponse>> Create(
        CreateServiceAccountRequest request,
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
        AddAudit(account, "service_accounts.create");
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new ServiceAccountResponse(
            account.Id, account.Name, account.IsActive, account.CreatedAt, account.RevokedAt, []));
    }

    [HttpPost("{accountId:guid}/keys")]
    [Authorize(Policy = PxaPermissions.ServiceAccountsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("api_keys.create")]
    public async Task<ActionResult<CreateApiKeyResponse>> CreateKey(
        Guid accountId,
        CreateApiKeyRequest request,
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
        AddAudit(account, "api_keys.create", new { apiKey.Id, apiKey.Name, apiKey.Prefix, apiKey.ExpiresAt });
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new CreateApiKeyResponse(
            apiKey.Id, account.Id, apiKey.Name, apiKey.Prefix, generated.Secret,
            apiKey.ExpiresAt, apiKey.CreatedAt));
    }

    [HttpPost("{accountId:guid}/keys/{keyId:guid}/revoke")]
    [Authorize(Policy = PxaPermissions.ServiceAccountsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("api_keys.revoke")]
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
                "api_keys.revoke", new { key.Id, key.Name, key.Prefix });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }

    [HttpPost("{accountId:guid}/revoke")]
    [Authorize(Policy = PxaPermissions.ServiceAccountsManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("service_accounts.revoke")]
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
            await dbContext.ApiKeys.Where(value => value.ServiceAccountId == account.Id && value.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.RevokedAt, now), cancellationToken);
            AddAudit(account, "service_accounts.revoke");
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

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden, title: "Organization context required");

    private ObjectResult ValidationProblem(string detail) => Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "Invalid service-account request", detail: detail);

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict, title: "Service-account operation rejected", detail: detail);
}

public sealed record CreateServiceAccountRequest(string? Name);
public sealed record CreateApiKeyRequest(string? Name, DateTimeOffset? ExpiresAt);
public sealed record ServiceAccountResponse(
    Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt,
    IReadOnlyList<ApiKeyResponse> Keys);
public sealed record ApiKeyResponse(
    Guid Id, Guid ServiceAccountId, string Name, string Prefix, DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
public sealed record CreateApiKeyResponse(
    Guid Id, Guid ServiceAccountId, string Name, string Prefix, string Secret,
    DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt);
