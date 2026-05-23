using Canvas.Domain.Entities;
using Canvas.Domain.ValueObjects;

namespace Canvas.Domain.Repositories;

public interface ITemplateRepository
{
    /// <summary>
    /// Finds a template by its ID.
    /// </summary>
    /// <param name="id">The template ID</param>
    /// <returns>The template if found, null otherwise</returns>
    Task<DesignTemplate?> FindByIdAsync(string id);

    /// <summary>
    /// Finds a specific version of a template.
    /// </summary>
    /// <param name="id">The template ID</param>
    /// <param name="version">The version to find</param>
    /// <returns>The template version if found, null otherwise</returns>
    Task<DesignTemplate?> FindVersionAsync(string id, string version);

    /// <summary>
    /// Saves a template.
    /// </summary>
    /// <param name="template">The template to save</param>
    Task SaveAsync(DesignTemplate template);

    /// <summary>
    /// Validates a template.
    /// </summary>
    /// <param name="template">The template to validate</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateAsync(DesignTemplate template);

    /// <summary>
    /// Gets all template names.
    /// </summary>
    /// <returns>List of template names with IDs</returns>
    Task<IEnumerable<TemplateNameInfo>> GetTemplateNamesAsync();

    /// <summary>
    /// Creates a new version of a template.
    /// </summary>
    /// <param name="id">The template ID</param>
    /// <param name="versionName">Optional version name</param>
    /// <returns>The new template version</returns>
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