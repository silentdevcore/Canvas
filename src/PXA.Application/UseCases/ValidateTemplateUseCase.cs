using Canvas.Domain.Repositories;
using Canvas.Domain.ValueObjects;
using CanvasDomain = Canvas.Domain.Entities;
using CanvasUseCases = Canvas.Application.UseCases;

namespace PXA.Application.UseCases;

public sealed class ValidateTemplateRequest
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public PageSettings? PageSettings { get; set; }
    public List<DesignerElement>? Elements { get; set; }
    public Dictionary<string, object>? SamplePayload { get; set; }
    public TemplateMetadata? TemplateMetadata { get; set; }
}

public sealed class TemplateMetadata
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

public sealed class FormattingProfile
{
    public string? DateFormat { get; set; }
    public string? TimeFormat { get; set; }
    public string? NumberFormat { get; set; }
    public string? CurrencyFormat { get; set; }
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
        inner = new CanvasUseCases.ValidateTemplateUseCase(templateRepository);
    }

    public async Task<ValidationResult> ExecuteAsync(ValidateTemplateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await inner.ExecuteAsync(new CanvasUseCases.ValidateTemplateRequest
        {
            Id = request.Id,
            Name = request.Name,
            Description = request.Description,
            PageSettings = request.PageSettings,
            Elements = request.Elements,
            SamplePayload = request.SamplePayload,
            TemplateMetadata = request.TemplateMetadata is null ? null : ToCanvas(request.TemplateMetadata),
        });

        return new ValidationResult
        {
            IsValid = result.IsValid,
            Errors = result.Errors,
            Warnings = result.Warnings,
        };
    }

    private static CanvasDomain.TemplateMetadata ToCanvas(TemplateMetadata metadata) => new()
    {
        Version = metadata.Version,
        SchemaVersion = metadata.SchemaVersion,
        CreatedBy = metadata.CreatedBy,
        UpdatedBy = metadata.UpdatedBy,
        Locale = metadata.Locale,
        Currency = metadata.Currency,
        Timezone = metadata.Timezone,
        FormattingProfile = metadata.FormattingProfile is null ? null : new CanvasDomain.FormattingProfile
        {
            DateFormat = metadata.FormattingProfile.DateFormat,
            TimeFormat = metadata.FormattingProfile.TimeFormat,
            NumberFormat = metadata.FormattingProfile.NumberFormat,
            CurrencyFormat = metadata.FormattingProfile.CurrencyFormat,
        },
        MigrationHints = metadata.MigrationHints,
        IsPublic = metadata.IsPublic,
        IsArchived = metadata.IsArchived,
    };
}
