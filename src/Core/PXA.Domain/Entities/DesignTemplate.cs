using PXA.Domain.ValueObjects;

namespace PXA.Domain.Entities;

public class DesignTemplate
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required List<DesignerElement> Elements { get; set; } = new();
    public required PageSettings PageSettings { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string>? Tags { get; set; }
    public TemplateMetadata? Metadata { get; set; }
}

public class TemplateMetadata
{
    public string? Version { get; set; }
    public string? SchemaVersion { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? Locale { get; set; }
    public string? Currency { get; set; }
    public string? Timezone { get; set; }
    public FormattingProfile? FormattingProfile { get; set; }
    public Dictionary<string, object>? MigrationHints { get; set; }
    public bool? IsPublic { get; set; }
    public bool? IsArchived { get; set; }
}

public class FormattingProfile
{
    public string? DateFormat { get; set; }
    public string? TimeFormat { get; set; }
    public string? NumberFormat { get; set; }
    public string? CurrencyFormat { get; set; }
}
