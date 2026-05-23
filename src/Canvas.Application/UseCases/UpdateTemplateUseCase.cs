using Canvas.Domain.Entities;
using Canvas.Domain.Repositories;
using Canvas.Domain.ValueObjects;

namespace Canvas.Application.UseCases;

public class UpdateTemplateUseCase
{
    private readonly ITemplateRepository _templateRepository;

    public UpdateTemplateUseCase(ITemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;
    }

    public async Task<DesignTemplate> ExecuteAsync(UpdateTemplateRequest request)
    {
        var existingTemplate = await _templateRepository.FindByIdAsync(request.Id);
        if (existingTemplate == null)
        {
            throw new InvalidOperationException($"Template with ID '{request.Id}' not found");
        }

        // Create new version if requested
        var templateToUpdate = request.CreateNewVersion
            ? await _templateRepository.CreateVersionAsync(request.Id, request.VersionName)
            : existingTemplate;

        // Update template properties
        templateToUpdate.Name = request.Name ?? templateToUpdate.Name;
        templateToUpdate.Description = request.Description ?? templateToUpdate.Description;
        templateToUpdate.UpdatedAt = DateTime.UtcNow;

        if (request.PageSettings != null)
        {
            templateToUpdate.PageSettings = request.PageSettings;
        }

        if (request.Elements != null)
        {
            templateToUpdate.Elements = request.Elements;
        }

        if (request.TemplateMetadata != null)
        {
            templateToUpdate.Metadata = request.TemplateMetadata;
            if (templateToUpdate.Metadata != null)
            {
                templateToUpdate.Metadata.UpdatedBy = request.UpdatedBy ?? "system";
            }
        }

        await _templateRepository.SaveAsync(templateToUpdate);
        return templateToUpdate;
    }
}

public class UpdateTemplateRequest
{
    public required string Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UpdatedBy { get; set; }
    public bool CreateNewVersion { get; set; } = false;
    public string? VersionName { get; set; }
    public PageSettings? PageSettings { get; set; }
    public List<DesignerElement>? Elements { get; set; }
    public Dictionary<string, object>? SamplePayload { get; set; }
    public TemplateMetadata? TemplateMetadata { get; set; }
}