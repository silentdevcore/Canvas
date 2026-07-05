using PXA.Domain;
using PXA.Domain.Repositories;
using PXA.Domain.ValueObjects;
using CanvasUseCases = Canvas.Application.UseCases;
using PxaDomain = PXA.Domain.Entities;

namespace PXA.Application.UseCases;

public sealed class ValidateTemplateRequest
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public PageSettings? PageSettings { get; set; }
    public List<DesignerElement>? Elements { get; set; }
    public Dictionary<string, object>? SamplePayload { get; set; }
    public PxaDomain.TemplateMetadata? TemplateMetadata { get; set; }
}

public sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// PXA-facing facade for template validation.
/// </summary>
public sealed class ValidateTemplateUseCase
{
    private readonly CanvasUseCases.ValidateTemplateUseCase inner;

    public ValidateTemplateUseCase(ITemplateRepository templateRepository)
    {
        inner = new CanvasUseCases.ValidateTemplateUseCase(new CanvasTemplateRepositoryAdapter(templateRepository));
    }

    public async Task<ValidationResult> ExecuteAsync(ValidateTemplateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await inner.ExecuteAsync(new CanvasUseCases.ValidateTemplateRequest
        {
            Id = request.Id,
            Name = request.Name,
            Description = request.Description,
            PageSettings = request.PageSettings?.ToCanvas(),
            Elements = request.Elements?.Select(element => element.ToCanvas()).ToList(),
            SamplePayload = request.SamplePayload,
            TemplateMetadata = request.TemplateMetadata?.ToCanvas(),
        });

        return new ValidationResult
        {
            IsValid = result.IsValid,
            Errors = result.Errors,
            Warnings = result.Warnings,
        };
    }
}
