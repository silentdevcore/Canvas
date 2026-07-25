using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Domain.Repositories;
using PXA.Domain.ValueObjects;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Infrastructure;

public sealed class PostgreSqlTemplateRepository(
    PxaDbContext dbContext,
    IPxaTenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor) : ITemplateRepository
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private CancellationToken CancellationToken =>
        httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

    public async Task<DesignTemplate?> FindByIdAsync(string id)
    {
        var organizationId = RequireOrganization();
        var entity = await FindEntity(id, organizationId, tracked: false);
        return entity is null ? null : ToLegacyTemplate(entity.DraftJson, entity);
    }

    public async Task<DesignTemplate?> FindVersionAsync(string id, string version)
    {
        var organizationId = RequireOrganization();
        var template = await FindEntity(id, organizationId, tracked: false);
        if (template is null)
            return null;

        var query = dbContext.DesignerTemplateVersions.AsNoTracking()
            .Where(value => value.TemplateId == template.Id && value.OrganizationId == organizationId);
        DesignerTemplateVersion? snapshot;
        if (long.TryParse(version, out var versionNumber))
            snapshot = await query.SingleOrDefaultAsync(value => value.VersionNumber == versionNumber, CancellationToken);
        else
            snapshot = await query.OrderByDescending(value => value.VersionNumber)
                .FirstOrDefaultAsync(value => value.Label == version, CancellationToken);

        if (snapshot is null)
            return null;
        var result = ToLegacyTemplate(snapshot.DesignJson, template, preserveSnapshotContent: true);
        result.UpdatedAt = snapshot.CreatedAt.UtcDateTime;
        result.Metadata ??= new TemplateMetadata();
        result.Metadata.Version = snapshot.Label ?? snapshot.VersionNumber.ToString();
        return result;
    }

    public async Task SaveAsync(DesignTemplate template)
    {
        var organizationId = RequireOrganization();
        var userId = RequireUser();
        var externalId = NormalizeExternalId(template.Id);
        var entity = await FindEntity(template.Id, organizationId, tracked: true);
        var draftJson = BuildDesignerDocument(template);
        var checksum = Checksum(draftJson);
        var now = DateTimeOffset.UtcNow;

        if (entity is null)
        {
            if (template.Revision > 0)
                throw new InvalidOperationException($"Template '{template.Id}' not found");
            entity = new DesignerTemplate
            {
                ExternalId = externalId,
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
                Name = NormalizeName(template.Name),
                Description = NormalizeDescription(template.Description),
                Tags = NormalizeTags(template.Tags),
                DraftJson = draftJson,
                DraftChecksum = checksum,
                SchemaVersion = NormalizeVersion(template.Metadata?.SchemaVersion, "1.0"),
                DesignerVersion = "legacy-api",
                Status = template.Metadata?.IsArchived == true
                    ? DesignerTemplateStatus.Archived
                    : DesignerTemplateStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now,
            };
            dbContext.DesignerTemplates.Add(entity);
            AddAudit(entity, userId, "designer.templates.legacy-created", new { entity.Revision });
        }
        else
        {
            if (template.Revision != entity.Revision)
                throw new TemplateConcurrencyException(template.Id, template.Revision, entity.Revision);

            var changed = entity.DraftChecksum != checksum ||
                          entity.Name != template.Name.Trim() ||
                          entity.Description != NormalizeDescription(template.Description) ||
                          !entity.Tags.SequenceEqual(NormalizeTags(template.Tags)) ||
                          entity.Status == DesignerTemplateStatus.Archived != (template.Metadata?.IsArchived == true);
            if (changed)
            {
                entity.Name = NormalizeName(template.Name);
                entity.Description = NormalizeDescription(template.Description);
                entity.Tags = NormalizeTags(template.Tags);
                entity.DraftJson = draftJson;
                entity.DraftChecksum = checksum;
                entity.SchemaVersion = NormalizeVersion(template.Metadata?.SchemaVersion, entity.SchemaVersion);
                entity.DesignerVersion = "legacy-api";
                entity.Status = template.Metadata?.IsArchived == true
                    ? DesignerTemplateStatus.Archived
                    : DesignerTemplateStatus.Draft;
                entity.ArchivedAt = entity.Status == DesignerTemplateStatus.Archived ? now : null;
                entity.UpdatedByUserId = userId;
                entity.UpdatedAt = now;
                entity.Revision++;
                AddAudit(entity, userId, "designer.templates.legacy-updated", new { entity.Revision });
            }
        }

        await dbContext.SaveChangesAsync(CancellationToken);
        ApplyServerMetadata(template, entity);
    }

    public Task<ValidationResult> ValidateAsync(DesignTemplate template)
    {
        var result = new ValidationResult { IsValid = true };
        if (string.IsNullOrWhiteSpace(template.Id))
            result.Errors.Add("Template ID is required");
        if (string.IsNullOrWhiteSpace(template.Name))
            result.Errors.Add("Template name is required");
        if (template.Elements is not { Count: > 0 })
            result.Errors.Add("Template must have at least one element");
        result.IsValid = result.Errors.Count == 0;
        return Task.FromResult(result);
    }

    public async Task<IEnumerable<TemplateNameInfo>> GetTemplateNamesAsync()
    {
        var organizationId = RequireOrganization();
        return await dbContext.DesignerTemplates.AsNoTracking()
            .Where(value => value.OrganizationId == organizationId &&
                            value.Status != DesignerTemplateStatus.Archived)
            .OrderBy(value => value.Name)
            .ThenBy(value => value.Id)
            .Select(value => new TemplateNameInfo
            {
                Id = value.ExternalId ?? value.Id.ToString(),
                Name = value.Name,
            })
            .ToArrayAsync(CancellationToken);
    }

    public async Task<DesignTemplate> CreateVersionAsync(string id, string? versionName = null)
    {
        var organizationId = RequireOrganization();
        var userId = RequireUser();
        var template = await FindEntity(id, organizationId, tracked: true)
            ?? throw new InvalidOperationException($"Template '{id}' not found");
        var latest = await dbContext.DesignerTemplateVersions
            .Where(value => value.TemplateId == template.Id)
            .OrderByDescending(value => value.VersionNumber)
            .FirstOrDefaultAsync(CancellationToken);

        DesignerTemplateVersion version;
        if (latest?.Checksum == template.DraftChecksum)
        {
            version = latest;
        }
        else
        {
            version = new DesignerTemplateVersion
            {
                TemplateId = template.Id,
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                VersionNumber = (latest?.VersionNumber ?? 0) + 1,
                Label = NormalizeLabel(versionName),
                DesignJson = template.DraftJson,
                Checksum = template.DraftChecksum,
                SchemaVersion = template.SchemaVersion,
                DesignerVersion = template.DesignerVersion,
            };
            dbContext.DesignerTemplateVersions.Add(version);
            AddAudit(template, userId, "designer.templates.legacy-version-created", new
            {
                version.VersionNumber,
                version.Label,
            });
            await dbContext.SaveChangesAsync(CancellationToken);
        }

        var result = ToLegacyTemplate(template.DraftJson, template);
        result.Metadata ??= new TemplateMetadata();
        result.Metadata.Version = version.Label ?? version.VersionNumber.ToString();
        return result;
    }

    private async Task<DesignerTemplate?> FindEntity(
        string id,
        Guid organizationId,
        bool tracked)
    {
        var query = tracked
            ? dbContext.DesignerTemplates.AsQueryable()
            : dbContext.DesignerTemplates.AsNoTracking();
        query = query.Where(value => value.OrganizationId == organizationId);
        if (Guid.TryParse(id, out var internalId))
            return await query.SingleOrDefaultAsync(
                value => value.Id == internalId || value.ExternalId == id,
                CancellationToken);
        return await query.SingleOrDefaultAsync(value => value.ExternalId == id, CancellationToken);
    }

    private Guid RequireOrganization() =>
        tenantContext.OrganizationId ??
        throw new UnauthorizedAccessException("An active organization is required.");

    private Guid RequireUser() =>
        tenantContext.UserId ??
        throw new UnauthorizedAccessException("An authenticated user is required.");

    private void AddAudit(DesignerTemplate template, Guid userId, string action, object details) =>
        dbContext.AuditEvents.Add(new AuditEvent
        {
            OrganizationId = template.OrganizationId,
            ActorUserId = userId,
            Action = action,
            TargetType = "designer_template",
            TargetId = template.Id.ToString(),
            Outcome = "succeeded",
            DetailsJson = JsonSerializer.Serialize(details, JsonOptions),
        });

    private static string BuildDesignerDocument(DesignTemplate template)
    {
        var elements = new JsonArray(template.Elements.Select(ToDesignerElement).ToArray());
        var document = new JsonObject
        {
            ["template"] = new JsonObject
            {
                ["id"] = template.Id,
                ["name"] = template.Name,
                ["category"] = "custom",
                ["description"] = template.Description ?? string.Empty,
                ["pages"] = new JsonArray(new JsonObject
                {
                    ["id"] = "page-1",
                    ["elements"] = elements,
                }),
                ["sharedElements"] = new JsonArray(),
                ["data"] = new JsonObject(),
            },
            ["pageSettings"] = JsonSerializer.SerializeToNode(template.PageSettings, JsonOptions),
            ["jsonData"] = new JsonObject(),
            ["documentMode"] = "pdf",
            ["currentPageIndex"] = 0,
            ["legacyTemplate"] = ToLegacySnapshotNode(template),
        };
        return document.ToJsonString(JsonOptions);
    }

    private static JsonNode? ToLegacySnapshotNode(DesignTemplate template)
    {
        var node = JsonSerializer.SerializeToNode(template, JsonOptions);
        if (node is not JsonObject value)
            return node;
        value.Remove("revision");
        value.Remove("createdAt");
        value.Remove("updatedAt");
        if (value["metadata"] is JsonObject metadata)
        {
            metadata.Remove("createdBy");
            metadata.Remove("updatedBy");
        }
        return value;
    }

    private static JsonObject ToDesignerElement(DesignerElement element)
    {
        var result = new JsonObject
        {
            ["id"] = element.Id,
            ["type"] = ToDesignerElementType(element.Type),
            ["x"] = element.X ?? 0,
            ["y"] = element.Y ?? 0,
            ["width"] = element.Width ?? 100,
            ["height"] = element.Height ?? 20,
            ["locked"] = element.Locked,
            ["legacy"] = JsonSerializer.SerializeToNode(element, JsonOptions),
        };
        var text = element.Props.FirstOrDefault(value =>
            string.Equals(value.Key, "text", StringComparison.OrdinalIgnoreCase)).Value?.ToString();
        if (text is not null)
            result["content"] = text;
        if (element.Props.Count > 0)
            result["style"] = JsonSerializer.SerializeToNode(element.Props, JsonOptions);
        return result;
    }

    private static string ToDesignerElementType(ElementType type) => type switch
    {
        ElementType.Rectangle => "rect",
        ElementType.RichText => "richtext",
        ElementType.TextField => "field",
        ElementType.CheckMark => "checkmark",
        ElementType.PageBoundary => "pageboundary",
        ElementType.PageNumber => "pagenumber",
        ElementType.ContentControl => "contentcontrol",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static DesignTemplate ToLegacyTemplate(
        string json,
        DesignerTemplate entity,
        bool preserveSnapshotContent = false)
    {
        using var document = JsonDocument.Parse(json);
        DesignTemplate? result = null;
        if (document.RootElement.TryGetProperty("legacyTemplate", out var legacy))
            result = legacy.Deserialize<DesignTemplate>(JsonOptions);
        if (result is null)
            result = ConvertDesignerDocument(document.RootElement, entity);
        ApplyServerMetadata(result, entity, preserveSnapshotContent);
        return result;
    }

    private static DesignTemplate ConvertDesignerDocument(JsonElement root, DesignerTemplate entity)
    {
        var templateNode = root.TryGetProperty("template", out var template) ? template : default;
        var pageSettings = root.TryGetProperty("pageSettings", out var settings)
            ? settings.Deserialize<PageSettings>(JsonOptions)
            : null;
        var elements = new List<DesignerElement>();
        if (templateNode.ValueKind == JsonValueKind.Object &&
            templateNode.TryGetProperty("pages", out var pages))
        {
            foreach (var page in pages.EnumerateArray())
            {
                if (!page.TryGetProperty("elements", out var pageElements))
                    continue;
                foreach (var element in pageElements.EnumerateArray())
                {
                    if (element.TryGetProperty("legacy", out var legacy))
                    {
                        var value = legacy.Deserialize<DesignerElement>(JsonOptions);
                        if (value is not null)
                            elements.Add(value);
                        continue;
                    }
                    var converted = ConvertDesignerElement(element);
                    if (converted is not null)
                        elements.Add(converted);
                }
            }
        }
        return new DesignTemplate
        {
            Id = entity.ExternalId ?? entity.Id.ToString(),
            Name = entity.Name,
            Description = entity.Description,
            Elements = elements,
            PageSettings = pageSettings ?? new PageSettings(),
            Tags = entity.Tags.ToList(),
            CreatedAt = entity.CreatedAt.UtcDateTime,
            UpdatedAt = entity.UpdatedAt.UtcDateTime,
        };
    }

    private static DesignerElement? ConvertDesignerElement(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var idValue) ||
            !element.TryGetProperty("type", out var typeValue))
            return null;
        var id = idValue.GetString();
        var type = FromDesignerElementType(typeValue.GetString());
        if (string.IsNullOrWhiteSpace(id) || type is null)
            return null;

        var props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (element.TryGetProperty("style", out var style) &&
            style.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in style.EnumerateObject())
                props[property.Name] = property.Value.Clone();
        }
        if (element.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String)
            props["text"] = content.GetString() ?? string.Empty;

        return new DesignerElement
        {
            Id = id,
            Type = type.Value,
            Props = props,
            X = ReadDouble(element, "x"),
            Y = ReadDouble(element, "y"),
            Width = ReadDouble(element, "width"),
            Height = ReadDouble(element, "height"),
            Locked = element.TryGetProperty("locked", out var locked) &&
                     locked.ValueKind is JsonValueKind.True,
        };
    }

    private static ElementType? FromDesignerElementType(string? value) => value switch
    {
        "rect" or "shape" => ElementType.Rectangle,
        "richtext" => ElementType.RichText,
        "field" or "textarea" => ElementType.TextField,
        "checkmark" => ElementType.CheckMark,
        "pageboundary" => ElementType.PageBoundary,
        "pagenumber" => ElementType.PageNumber,
        "contentcontrol" => ElementType.ContentControl,
        null => null,
        _ when Enum.TryParse<ElementType>(value, true, out var parsed) => parsed,
        _ => null,
    };

    private static double? ReadDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetDouble(out var number)
            ? number
            : null;

    private static void ApplyServerMetadata(
        DesignTemplate template,
        DesignerTemplate entity,
        bool preserveSnapshotContent = false)
    {
        template.Id = entity.ExternalId ?? entity.Id.ToString();
        template.Revision = entity.Revision;
        if (!preserveSnapshotContent)
        {
            template.Name = entity.Name;
            template.Description = entity.Description;
            template.Tags = entity.Tags.ToList();
            template.CreatedAt = entity.CreatedAt.UtcDateTime;
            template.UpdatedAt = entity.UpdatedAt.UtcDateTime;
        }
        template.Metadata ??= new TemplateMetadata();
        template.Metadata.SchemaVersion = entity.SchemaVersion;
        template.Metadata.CreatedBy = entity.CreatedByUserId.ToString();
        template.Metadata.UpdatedBy = entity.UpdatedByUserId.ToString();
        template.Metadata.IsArchived = entity.Status == DesignerTemplateStatus.Archived;
    }

    private static string NormalizeExternalId(string id)
    {
        var value = id.Trim();
        if (value.Length is 0 or > 200)
            throw new ArgumentException("Template ID must contain between 1 and 200 characters.", nameof(id));
        return value;
    }

    private static string NormalizeName(string name)
    {
        var value = name.Trim();
        if (value.Length is 0 or > 200)
            throw new ArgumentException("Template name must contain between 1 and 200 characters.", nameof(name));
        return value;
    }

    private static string? NormalizeDescription(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized[..Math.Min(2000, normalized.Length)];
    }

    private static string[] NormalizeTags(IEnumerable<string>? values) =>
        (values ?? [])
        .Select(value => value.Trim().ToLowerInvariant())
        .Where(value => value.Length is > 0 and <= 80)
        .Distinct(StringComparer.Ordinal)
        .Take(50)
        .ToArray();

    private static string NormalizeVersion(string? value, string fallback)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized)
            ? fallback
            : normalized[..Math.Min(normalized.Length, 32)];
    }

    private static string? NormalizeLabel(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized)
            ? null
            : normalized[..Math.Min(normalized.Length, 200)];
    }

    private static string Checksum(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
