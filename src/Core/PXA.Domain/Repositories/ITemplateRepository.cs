using PXA.Domain.Entities;

namespace PXA.Domain.Repositories;

public interface ITemplateRepository
{
    Task<DesignTemplate?> FindByIdAsync(string id);

    Task<DesignTemplate?> FindVersionAsync(string id, string version);

    Task SaveAsync(DesignTemplate template);

    Task<ValidationResult> ValidateAsync(DesignTemplate template);

    Task<IEnumerable<TemplateNameInfo>> GetTemplateNamesAsync();

    Task<DesignTemplate> CreateVersionAsync(string id, string? versionName = null);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class TemplateNameInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}

public sealed class TemplateConcurrencyException : Exception
{
    public TemplateConcurrencyException(string templateId, long expectedRevision, long currentRevision)
        : base($"Template '{templateId}' changed from revision {expectedRevision} to {currentRevision}.")
    {
        TemplateId = templateId;
        ExpectedRevision = expectedRevision;
        CurrentRevision = currentRevision;
    }

    public string TemplateId { get; }
    public long ExpectedRevision { get; }
    public long CurrentRevision { get; }
}
