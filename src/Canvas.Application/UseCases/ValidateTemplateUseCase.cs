using Canvas.Domain.Entities;
using Canvas.Domain.Repositories;
using Canvas.Domain.ValueObjects;

namespace Canvas.Application.UseCases;

public class ValidateTemplateUseCase
{
    private readonly ITemplateRepository _templateRepository;

    public ValidateTemplateUseCase(ITemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;
    }

    public async Task<ValidationResult> ExecuteAsync(ValidateTemplateRequest request)
    {
        var template = new DesignTemplate
        {
            Id = request.Id ?? Guid.NewGuid().ToString(),
            Name = request.Name ?? "Validation Template",
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
                CreatedBy = "validator",
                UpdatedBy = "validator",
                Locale = "en-US",
                Currency = "USD",
                Timezone = "UTC"
            }
        };

        return await _templateRepository.ValidateAsync(template);
    }
}

public class ValidateTemplateRequest
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public PageSettings? PageSettings { get; set; }
    public List<DesignerElement>? Elements { get; set; }
    public Dictionary<string, object>? SamplePayload { get; set; }
    public TemplateMetadata? TemplateMetadata { get; set; }
}