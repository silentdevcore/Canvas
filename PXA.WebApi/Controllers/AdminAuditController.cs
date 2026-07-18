using System.Globalization;
using System.Text;
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
[Authorize(Policy = PxaPermissions.AuditRead)]
[Route("api/pxa/v1/admin/audit")]
public sealed class AdminAuditController : ControllerBase
{
    private const int MaximumExportRows = 50_000;
    private readonly PxaDbContext dbContext;
    private readonly IPxaTenantContext tenantContext;

    public AdminAuditController(PxaDbContext dbContext, IPxaTenantContext tenantContext)
    {
        this.dbContext = dbContext;
        this.tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<AdminAuditPage>> GetEvents(
        [FromQuery] AuditFilter filter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();
        if (!TryValidateFilter(filter, out var validationError))
            return ValidationProblem(validationError!);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var tenantQuery = BuildTenantQuery(organizationId);
        var query = ApplyFilter(tenantQuery, filter);
        query = string.Equals(filter.Direction, "asc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(value => value.CreatedAt).ThenBy(value => value.Id)
            : query.OrderByDescending(value => value.CreatedAt).ThenByDescending(value => value.Id);
        var total = await query.CountAsync(cancellationToken);
        var records = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var actions = await tenantQuery.Select(value => value.Action).Distinct().OrderBy(value => value)
            .ToArrayAsync(cancellationToken);
        var targetTypes = await tenantQuery.Select(value => value.TargetType).Distinct().OrderBy(value => value)
            .ToArrayAsync(cancellationToken);
        var outcomes = await tenantQuery.Select(value => value.Outcome).Distinct().OrderBy(value => value)
            .ToArrayAsync(cancellationToken);
        var canExport = await IsEnterpriseAsync(organizationId, cancellationToken);
        return Ok(new AdminAuditPage(
            records.Select(ToResponse).ToArray(), page, pageSize, total,
            actions, targetTypes, outcomes, canExport));
    }

    [HttpGet("{eventId:guid}")]
    public async Task<ActionResult<AdminAuditEventResponse>> GetEvent(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId)
            return MissingOrganization();
        var record = await BuildTenantQuery(organizationId)
            .SingleOrDefaultAsync(value => value.Id == eventId, cancellationToken);
        return record is null ? NotFound() : Ok(ToResponse(record));
    }

    [HttpPost("export")]
    [PxaValidateAntiforgery]
    [PxaAuditedMutation("audit.export")]
    public async Task<IActionResult> Export(
        AuditExportRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantContext.OrganizationId is not { } organizationId || tenantContext.UserId is not { } actorUserId)
            return MissingOrganization();
        var filter = request.Filter ?? new AuditFilter();
        if (!TryValidateFilter(filter, out var validationError))
            return ValidationProblem(validationError!);
        var format = request.Format?.Trim().ToLowerInvariant();
        if (format is not ("csv" or "json"))
            return ValidationProblem("Export format must be csv or json.");
        if (!await IsEnterpriseAsync(organizationId, cancellationToken))
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "PXA_AUDIT_EXPORT_REQUIRES_ENTERPRISE",
                detail: "Audit export is available for Enterprise subscriptions.");

        var query = ApplyFilter(BuildTenantQuery(organizationId), filter)
            .OrderByDescending(value => value.CreatedAt).ThenByDescending(value => value.Id);
        var count = await query.CountAsync(cancellationToken);
        if (count > MaximumExportRows)
            return Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Audit export is too large",
                detail: $"Narrow the filters to at most {MaximumExportRows:N0} events.");
        var records = (await query.ToListAsync(cancellationToken)).Select(ToResponse).ToArray();
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            Action = "audit.export",
            TargetType = "audit-log",
            TargetId = organizationId.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(new { Format = format, Rows = records.Length, Filter = filter }),
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return format == "json"
            ? File(JsonSerializer.SerializeToUtf8Bytes(records, new JsonSerializerOptions { WriteIndented = true }),
                "application/json", $"pxa-audit-{timestamp}.json")
            : File(BuildCsv(records), "text/csv; charset=utf-8", $"pxa-audit-{timestamp}.csv");
    }

    private IQueryable<AuditRecord> BuildTenantQuery(Guid organizationId) =>
        from audit in dbContext.AuditEvents.AsNoTracking()
        join user in dbContext.Users.AsNoTracking() on audit.ActorUserId equals user.Id into users
        from actor in users.DefaultIfEmpty()
        where audit.OrganizationId == organizationId
        select new AuditRecord
        {
            Id = audit.Id,
            Action = audit.Action,
            TargetType = audit.TargetType,
            TargetId = audit.TargetId,
            Outcome = audit.Outcome,
            DetailsJson = audit.DetailsJson,
            ActorUserId = audit.ActorUserId,
            ActorName = actor == null ? null : actor.DisplayName,
            ActorEmail = actor == null ? null : actor.Email,
            CreatedAt = audit.CreatedAt,
        };

    private static IQueryable<AuditRecord> ApplyFilter(IQueryable<AuditRecord> query, AuditFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(value =>
                EF.Functions.ILike(value.Action, pattern) ||
                EF.Functions.ILike(value.TargetType, pattern) ||
                EF.Functions.ILike(value.TargetId, pattern) ||
                EF.Functions.ILike(value.ActorName ?? string.Empty, pattern) ||
                EF.Functions.ILike(value.ActorEmail ?? string.Empty, pattern));
        }
        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(value => value.Action == filter.Action.Trim());
        if (!string.IsNullOrWhiteSpace(filter.TargetType))
            query = query.Where(value => value.TargetType == filter.TargetType.Trim());
        if (!string.IsNullOrWhiteSpace(filter.Outcome))
            query = query.Where(value => value.Outcome == filter.Outcome.Trim());
        if (filter.ActorUserId is { } actorUserId)
            query = query.Where(value => value.ActorUserId == actorUserId);
        if (filter.From is { } from)
            query = query.Where(value => value.CreatedAt >= from);
        if (filter.To is { } to)
            query = query.Where(value => value.CreatedAt <= to);
        return query;
    }

    private async Task<bool> IsEnterpriseAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await dbContext.OrganizationSubscriptions.AsNoTracking().AnyAsync(value =>
            value.OrganizationId == organizationId &&
            value.Edition == SubscriptionEdition.Enterprise &&
            value.Status != SubscriptionStatus.Cancelled &&
            value.Status != SubscriptionStatus.Expired,
            cancellationToken);

    private static bool TryValidateFilter(AuditFilter filter, out string? error)
    {
        error = null;
        if (filter.From is { } from && filter.To is { } to && from > to)
            error = "The start timestamp must be before the end timestamp.";
        else if (filter.Search?.Length > 200 || filter.Action?.Length > 160 ||
                 filter.TargetType?.Length > 100 || filter.Outcome?.Length > 32)
            error = "One or more audit filters exceed their allowed length.";
        else if (!string.IsNullOrWhiteSpace(filter.Direction) &&
                 !new[] { "asc", "desc" }.Contains(filter.Direction, StringComparer.OrdinalIgnoreCase))
            error = "Sort direction must be asc or desc.";
        return error is null;
    }

    private static AdminAuditEventResponse ToResponse(AuditRecord record) => new(
        record.Id,
        record.Action,
        record.TargetType,
        record.TargetId,
        record.Outcome,
        ParseDetails(record.DetailsJson),
        record.ActorUserId,
        record.ActorName ?? "System",
        record.ActorEmail,
        record.CreatedAt);

    private static JsonElement? ParseDetails(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return null;
        try
        {
            return JsonDocument.Parse(detailsJson).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { unavailable = true });
        }
    }

    private static byte[] BuildCsv(IEnumerable<AdminAuditEventResponse> records)
    {
        var csv = new StringBuilder("\uFEFFTime,Actor,Email,Action,Target type,Target ID,Outcome,Details\r\n");
        foreach (var record in records)
        {
            var fields = new[]
            {
                record.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                record.ActorName,
                record.ActorEmail ?? string.Empty,
                record.Action,
                record.TargetType,
                record.TargetId,
                record.Outcome,
                record.Details?.GetRawText() ?? string.Empty,
            };
            csv.AppendJoin(',', fields.Select(CsvCell)).Append("\r\n");
        }
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static string CsvCell(string value)
    {
        var safe = value.Length > 0 && "=+-@".Contains(value[0]) ? $"'{value}" : value;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }

    private ObjectResult MissingOrganization() => Problem(
        statusCode: StatusCodes.Status403Forbidden, title: "Organization context required");

    private BadRequestObjectResult ValidationProblem(string detail) => BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid audit request",
        Detail = detail,
    });

    private sealed class AuditRecord
    {
        public Guid Id { get; init; }
        public required string Action { get; init; }
        public required string TargetType { get; init; }
        public required string TargetId { get; init; }
        public required string Outcome { get; init; }
        public string? DetailsJson { get; init; }
        public Guid? ActorUserId { get; init; }
        public string? ActorName { get; init; }
        public string? ActorEmail { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}

public sealed class AuditFilter
{
    public string? Search { get; init; }
    public string? Action { get; init; }
    public string? TargetType { get; init; }
    public string? Outcome { get; init; }
    public Guid? ActorUserId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Direction { get; init; }
}

public sealed record AuditExportRequest(string? Format, AuditFilter? Filter);
public sealed record AdminAuditPage(
    IReadOnlyList<AdminAuditEventResponse> Items,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> TargetTypes,
    IReadOnlyList<string> Outcomes,
    bool CanExport);
public sealed record AdminAuditEventResponse(
    Guid Id,
    string Action,
    string TargetType,
    string TargetId,
    string Outcome,
    JsonElement? Details,
    Guid? ActorUserId,
    string ActorName,
    string? ActorEmail,
    DateTimeOffset CreatedAt);
