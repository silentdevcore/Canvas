using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/admin/mail")]
public sealed class AdminMailController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;

    public AdminMailController(PxaDbContext dbContext, IPxaTenantContext tenantContext)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
    }

    [HttpGet]
    [Authorize(Policy = PxaPermissions.MailRead)]
    public async Task<ActionResult<AdminMailPage>> GetMessages(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Problem(statusCode: 403, title: "Organization context required");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.MailOutboxMessages.AsNoTracking()
            .Where(message => message.OrganizationId == organizationId);
        if (Enum.TryParse<MailDeliveryStatus>(status, true, out var parsedStatus))
            query = query.Where(message => message.Status == parsedStatus);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(message => message.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(message => new AdminMailResponse(
                message.Id,
                message.RecipientEmail,
                message.TemplateKey,
                message.Status.ToString(),
                message.Attempts,
                message.ProviderMessageId,
                message.FailureReason,
                message.CreatedAt,
                message.DeliveredAt))
            .ToListAsync(cancellationToken);
        return Ok(new AdminMailPage(items, page, pageSize, total));
    }
}

public sealed record AdminMailPage(IReadOnlyList<AdminMailResponse> Items, int Page, int PageSize, int Total);
public sealed record AdminMailResponse(
    Guid Id,
    string RecipientEmail,
    string TemplateKey,
    string Status,
    int Attempts,
    string? ProviderMessageId,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt);
