using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;

namespace PVAD.WebApi.Controllers;

[ApiController]
[Authorize(
    AuthenticationSchemes = PvadAuthenticationSchemes.McpDelegation,
    Policy = PvadOAuthPolicies.TemplatesRead)]
[Route("api/pvad/v1/mcp/templates")]
public sealed class McpTemplatesController(
    PvadDbContext dbContext,
    IPvadTenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<McpTemplatePage>> List(
        [FromQuery] string? search = null,
        [FromQuery] string? tag = null,
        [FromQuery] bool archived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.DesignerTemplates.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId &&
                (archived
                    ? value.Status == DesignerTemplateStatus.Archived
                    : value.Status != DesignerTemplateStatus.Archived));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(value =>
                EF.Functions.ILike(value.Name, pattern) ||
                value.Description != null && EF.Functions.ILike(value.Description, pattern));
        }
        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(value => value.Tags.Contains(tag.Trim().ToLowerInvariant()));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenBy(value => value.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(value => new McpTemplateSummary(
                value.Id,
                value.Name,
                value.Description,
                value.Tags,
                value.Status.ToString(),
                value.Revision,
                value.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        return Ok(new McpTemplatePage(items, page, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<McpTemplateDocument>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return Unauthorized();

        var template = await dbContext.DesignerTemplates.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == id && value.OrganizationId == organizationId,
                cancellationToken);
        if (template is null)
            return NotFound();

        using var document = JsonDocument.Parse(template.DraftJson);
        return Ok(new McpTemplateDocument(
            template.Id,
            template.Name,
            template.Description,
            template.Tags,
            template.Status.ToString(),
            template.Revision,
            template.DraftChecksum,
            document.RootElement.Clone(),
            template.UpdatedAt));
    }
}

public sealed record McpTemplatePage(
    IReadOnlyList<McpTemplateSummary> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record McpTemplateSummary(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Tags,
    string Status,
    long Revision,
    DateTimeOffset UpdatedAt);

public sealed record McpTemplateDocument(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Tags,
    string Status,
    long Revision,
    string Checksum,
    JsonElement Design,
    DateTimeOffset UpdatedAt);
