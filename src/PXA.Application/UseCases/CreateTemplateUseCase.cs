using PXA.Domain.Entities;
using PXA.Domain.Repositories;
using PXA.Domain.ValueObjects;

namespace PXA.Application.UseCases;

public class CreateTemplateUseCase
{
    private readonly ITemplateRepository _templateRepository;

    public CreateTemplateUseCase(ITemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;
    }

    public async Task<DesignTemplate> ExecuteAsync(CreateTemplateRequest request)
    {
        var template = new DesignTemplate
        {
            Id = request.Id ?? Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PageSettings = request.PageSettings ?? new PageSettings
            {
                Width = 595.276,
                Height = 841.89,
                Orientation = "portrait",
                BackgroundColor = "#ffffff",
                Margins = new Margins { Top = 72, Right = 72, Bottom = 72, Left = 72 }
            },
            Elements = request.Elements ?? new List<DesignerElement>(),
            Metadata = request.TemplateMetadata ?? new TemplateMetadata
            {
                Version = "1.0.0",
                SchemaVersion = "1.0.0",
                CreatedBy = request.CreatedBy ?? "system",
                UpdatedBy = request.CreatedBy ?? "system",
                Locale = "en-US",
                Currency = "USD",
                Timezone = "UTC"
            }
        };

        await _templateRepository.SaveAsync(template);
        return template;
    }
}

public class CreateTemplateRequest
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public PageSettings? PageSettings { get; set; }
    public List<DesignerElement>? Elements { get; set; }
    public Dictionary<string, object>? SamplePayload { get; set; }
    public TemplateMetadata? TemplateMetadata { get; set; }
}