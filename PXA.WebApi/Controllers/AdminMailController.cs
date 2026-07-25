using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Mail;
using System.Text.Json;

namespace PXA.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/pxa/v1/admin/mail")]
public sealed class AdminMailController : ControllerBase
{
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;
    private readonly PxaMailOptions mailOptions;

    public AdminMailController(
        PxaDbContext dbContext,
        IPxaTenantContext tenantContext,
        IOptions<PxaMailOptions> mailOptions)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
        this.mailOptions = mailOptions.Value;
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
        var rows = await query.OrderByDescending(message => message.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(message => new
            {
                message.Id,
                message.RecipientEmail,
                message.TemplateKey,
                message.Status,
                message.Attempts,
                message.ProviderMessageId,
                message.FailureReason,
                message.CreatedAt,
                message.DeliveredAt,
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(message => new AdminMailResponse(
                message.Id,
                MaskRecipientEmail(message.RecipientEmail),
                message.TemplateKey,
                message.Status.ToString(),
                message.Attempts,
                message.ProviderMessageId,
                message.FailureReason,
                message.CreatedAt,
                message.DeliveredAt))
            .ToList();
        return Ok(new AdminMailPage(items, page, pageSize, total));
    }

    [HttpGet("status")]
    [Authorize(Policy = PxaPermissions.MailRead)]
    public async Task<ActionResult<AdminMailStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();

        var counts = await dbContext.MailOutboxMessages.AsNoTracking()
            .Where(message => message.OrganizationId == organizationId)
            .GroupBy(message => message.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status.ToString(), item => item.Count, cancellationToken);
        return Ok(new AdminMailStatusResponse(
            mailOptions.Transport,
            mailOptions.IsDeliveryEnabled,
            counts.GetValueOrDefault(nameof(MailDeliveryStatus.Pending)),
            counts.GetValueOrDefault(nameof(MailDeliveryStatus.Failed)),
            counts.GetValueOrDefault(nameof(MailDeliveryStatus.DeadLetter))));
    }

    [HttpPost("{messageId:guid}/retry")]
    [Authorize(Policy = PxaPermissions.MailManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("mail.retry")]
    public async Task<IActionResult> Retry(Guid messageId, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is null)
            return MissingOrganization();
        var message = await FindMessage(messageId, cancellationToken);
        if (message is null)
            return NotFound();
        if (message.Status is not (MailDeliveryStatus.Failed or MailDeliveryStatus.DeadLetter))
            return ConflictProblem("Only failed or dead-letter messages can be retried.");

        message.Status = MailDeliveryStatus.Pending;
        message.Attempts = 0;
        message.ScheduledAt = DateTimeOffset.UtcNow;
        message.LastAttemptAt = null;
        message.FailureReason = null;
        message.ProviderMessageId = null;
        message.UpdatedAt = DateTimeOffset.UtcNow;
        AddAuditEvent(message, "mail.retry");
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{messageId:guid}/cancel")]
    [Authorize(Policy = PxaPermissions.MailManage)]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("mail.cancel")]
    public async Task<IActionResult> Cancel(Guid messageId, CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is null)
            return MissingOrganization();
        var message = await FindMessage(messageId, cancellationToken);
        if (message is null)
            return NotFound();
        if (message.Status is not (MailDeliveryStatus.Pending or MailDeliveryStatus.Scheduled or
            MailDeliveryStatus.Failed or MailDeliveryStatus.DeadLetter))
        {
            return ConflictProblem("This message can no longer be cancelled.");
        }

        message.Status = MailDeliveryStatus.Cancelled;
        message.UpdatedAt = DateTimeOffset.UtcNow;
        AddAuditEvent(message, "mail.cancel");
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Task<MailOutboxMessage?> FindMessage(Guid messageId, CancellationToken cancellationToken) =>
        tenantContext.OrganizationId is { } organizationId
            ? dbContext.MailOutboxMessages.SingleOrDefaultAsync(
                message => message.Id == messageId && message.OrganizationId == organizationId,
                cancellationToken)
            : Task.FromResult<MailOutboxMessage?>(null);

    private void AddAuditEvent(MailOutboxMessage message, string action)
    {
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = message.OrganizationId,
            ActorUserId = tenantContext.UserId,
            Action = action,
            TargetType = "mail-outbox-message",
            TargetId = message.Id.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(new { message.TemplateKey, message.Status }),
        });
    }

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Organization context required");

    private ObjectResult ConflictProblem(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Mail operation rejected",
        detail: detail);

    internal static string MaskRecipientEmail(string recipientEmail)
    {
        var separator = recipientEmail.LastIndexOf('@');
        if (separator <= 0 || separator == recipientEmail.Length - 1)
            return "***";

        var local = recipientEmail[..separator];
        var domain = recipientEmail[(separator + 1)..];
        var domainSeparator = domain.LastIndexOf('.');
        var domainName = domainSeparator > 0 ? domain[..domainSeparator] : domain;
        var suffix = domainSeparator > 0 ? domain[domainSeparator..] : string.Empty;
        return $"{local[0]}***@{domainName[0]}***{suffix}";
    }
}

public sealed record AdminMailPage(IReadOnlyList<AdminMailResponse> Items, int Page, int PageSize, int Total);
public sealed record AdminMailStatusResponse(
    string Transport,
    bool DeliveryEnabled,
    int Pending,
    int Failed,
    int DeadLetter);
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
