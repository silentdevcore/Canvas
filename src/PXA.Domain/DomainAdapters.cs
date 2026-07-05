using System.Text.Json;
using PXA.Domain.Entities;
using PXA.Domain.Repositories;
using PXA.Domain.ValueObjects;

namespace PXA.Domain;

public static class DomainAdapters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Canvas.Domain.Entities.DesignTemplate ToCanvas(this DesignTemplate template) =>
        Convert<DesignTemplate, Canvas.Domain.Entities.DesignTemplate>(template);

    public static DesignTemplate ToPxa(this Canvas.Domain.Entities.DesignTemplate template) =>
        Convert<Canvas.Domain.Entities.DesignTemplate, DesignTemplate>(template);

    public static Canvas.Domain.Entities.TemplateMetadata ToCanvas(this TemplateMetadata metadata) =>
        Convert<TemplateMetadata, Canvas.Domain.Entities.TemplateMetadata>(metadata);

    public static TemplateMetadata ToPxa(this Canvas.Domain.Entities.TemplateMetadata metadata) =>
        Convert<Canvas.Domain.Entities.TemplateMetadata, TemplateMetadata>(metadata);

    public static Canvas.Domain.ValueObjects.PageSettings ToCanvas(this PageSettings settings) =>
        Convert<PageSettings, Canvas.Domain.ValueObjects.PageSettings>(settings);

    public static PageSettings ToPxa(this Canvas.Domain.ValueObjects.PageSettings settings) =>
        Convert<Canvas.Domain.ValueObjects.PageSettings, PageSettings>(settings);

    public static Canvas.Domain.ValueObjects.DesignerElement ToCanvas(this DesignerElement element) =>
        Convert<DesignerElement, Canvas.Domain.ValueObjects.DesignerElement>(element);

    public static DesignerElement ToPxa(this Canvas.Domain.ValueObjects.DesignerElement element) =>
        Convert<Canvas.Domain.ValueObjects.DesignerElement, DesignerElement>(element);

    public static Canvas.Domain.Repositories.ValidationResult ToCanvas(this ValidationResult result) =>
        Convert<ValidationResult, Canvas.Domain.Repositories.ValidationResult>(result);

    public static ValidationResult ToPxa(this Canvas.Domain.Repositories.ValidationResult result) =>
        Convert<Canvas.Domain.Repositories.ValidationResult, ValidationResult>(result);

    public static Canvas.Domain.Repositories.TemplateNameInfo ToCanvas(this TemplateNameInfo info) =>
        Convert<TemplateNameInfo, Canvas.Domain.Repositories.TemplateNameInfo>(info);

    public static TemplateNameInfo ToPxa(this Canvas.Domain.Repositories.TemplateNameInfo info) =>
        Convert<Canvas.Domain.Repositories.TemplateNameInfo, TemplateNameInfo>(info);

    private static TTarget Convert<TSource, TTarget>(TSource source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<TTarget>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Could not convert {typeof(TSource).Name} to {typeof(TTarget).Name}.");
    }
}

public sealed class CanvasTemplateRepositoryAdapter : Canvas.Domain.Repositories.ITemplateRepository
{
    private readonly ITemplateRepository repository;

    public CanvasTemplateRepositoryAdapter(ITemplateRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Canvas.Domain.Entities.DesignTemplate?> FindByIdAsync(string id) =>
        (await repository.FindByIdAsync(id).ConfigureAwait(false))?.ToCanvas();

    public async Task<Canvas.Domain.Entities.DesignTemplate?> FindVersionAsync(string id, string version) =>
        (await repository.FindVersionAsync(id, version).ConfigureAwait(false))?.ToCanvas();

    public Task SaveAsync(Canvas.Domain.Entities.DesignTemplate template) =>
        repository.SaveAsync(template.ToPxa());

    public async Task<Canvas.Domain.Repositories.ValidationResult> ValidateAsync(Canvas.Domain.Entities.DesignTemplate template) =>
        (await repository.ValidateAsync(template.ToPxa()).ConfigureAwait(false)).ToCanvas();

    public async Task<IEnumerable<Canvas.Domain.Repositories.TemplateNameInfo>> GetTemplateNamesAsync() =>
        (await repository.GetTemplateNamesAsync().ConfigureAwait(false)).Select(name => name.ToCanvas());

    public async Task<Canvas.Domain.Entities.DesignTemplate> CreateVersionAsync(string id, string? versionName = null) =>
        (await repository.CreateVersionAsync(id, versionName).ConfigureAwait(false)).ToCanvas();
}

public sealed class PxaTemplateRepositoryAdapter : ITemplateRepository
{
    private readonly Canvas.Domain.Repositories.ITemplateRepository repository;

    public PxaTemplateRepositoryAdapter(Canvas.Domain.Repositories.ITemplateRepository repository)
    {
        this.repository = repository;
    }

    public async Task<DesignTemplate?> FindByIdAsync(string id) =>
        (await repository.FindByIdAsync(id).ConfigureAwait(false))?.ToPxa();

    public async Task<DesignTemplate?> FindVersionAsync(string id, string version) =>
        (await repository.FindVersionAsync(id, version).ConfigureAwait(false))?.ToPxa();

    public Task SaveAsync(DesignTemplate template) =>
        repository.SaveAsync(template.ToCanvas());

    public async Task<ValidationResult> ValidateAsync(DesignTemplate template) =>
        (await repository.ValidateAsync(template.ToCanvas()).ConfigureAwait(false)).ToPxa();

    public async Task<IEnumerable<TemplateNameInfo>> GetTemplateNamesAsync() =>
        (await repository.GetTemplateNamesAsync().ConfigureAwait(false)).Select(name => name.ToPxa());

    public async Task<DesignTemplate> CreateVersionAsync(string id, string? versionName = null) =>
        (await repository.CreateVersionAsync(id, versionName).ConfigureAwait(false)).ToPxa();
}
