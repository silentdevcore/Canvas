using PXA.Domain;
using PXA.Domain.Entities;
using PXA.Domain.Repositories;
using PXA.Domain.ValueObjects;
using Xunit;

namespace PXA.Domain.Tests;

public class DomainAdapterTests
{
    [Fact]
    public void DesignTemplate_ToCanvasAndBack_PreservesDomainShape()
    {
        var template = CreateTemplate();

        var canvas = template.ToCanvas();
        var pxa = canvas.ToPxa();

        Assert.Equal("template-1", canvas.Id);
        Assert.Equal(Canvas.Domain.ValueObjects.ElementType.Text, canvas.Elements[0].Type);
        Assert.Equal("Inter", canvas.Elements[0].Text?.FontFamily);
        Assert.Equal("invoice", pxa.Tags![0]);
        Assert.Equal(ElementType.Text, pxa.Elements[0].Type);
        Assert.Equal("Inter", pxa.Elements[0].Text?.FontFamily);
        Assert.Equal(36, pxa.PageSettings.Margins.Top);
    }

    [Fact]
    public void ValidationResult_ToCanvasAndBack_PreservesMessages()
    {
        var result = new ValidationResult
        {
            IsValid = false,
            Errors = ["missing-page"],
            Warnings = ["small-font"]
        };

        var canvas = result.ToCanvas();
        var pxa = canvas.ToPxa();

        Assert.False(canvas.IsValid);
        Assert.Equal("missing-page", canvas.Errors[0]);
        Assert.Equal("small-font", pxa.Warnings[0]);
    }

    [Fact]
    public async Task PxaTemplateRepositoryAdapter_WrapsCanvasRepository()
    {
        var canvasRepository = new CanvasRepositoryFake(CreateTemplate().ToCanvas());
        var repository = new PxaTemplateRepositoryAdapter(canvasRepository);

        var found = await repository.FindByIdAsync("template-1");
        await repository.SaveAsync(CreateTemplate());
        var validation = await repository.ValidateAsync(CreateTemplate());
        var names = await repository.GetTemplateNamesAsync();

        Assert.NotNull(found);
        Assert.Equal(ElementType.Text, found.Elements[0].Type);
        Assert.Equal("template-1", canvasRepository.LastSaved?.Id);
        Assert.True(validation.IsValid);
        Assert.Equal("Template One", names.Single().Name);
    }

    [Fact]
    public async Task CanvasTemplateRepositoryAdapter_WrapsPxaRepository()
    {
        var pxaRepository = new PxaRepositoryFake(CreateTemplate());
        var repository = new CanvasTemplateRepositoryAdapter(pxaRepository);

        var found = await repository.FindByIdAsync("template-1");
        await repository.SaveAsync(CreateTemplate().ToCanvas());
        var version = await repository.CreateVersionAsync("template-1", "v2");

        Assert.NotNull(found);
        Assert.Equal(Canvas.Domain.ValueObjects.ElementType.Text, found.Elements[0].Type);
        Assert.Equal("template-1", pxaRepository.LastSaved?.Id);
        Assert.Equal("template-1-v2", version.Id);
    }

    private static DesignTemplate CreateTemplate() => new()
    {
        Id = "template-1",
        Name = "Template One",
        Description = "PXA domain smoke template",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        Tags = ["invoice"],
        Metadata = new TemplateMetadata
        {
            Version = "1.0",
            SchemaVersion = "pxa-template-v1",
            FormattingProfile = new FormattingProfile { DateFormat = "yyyy-MM-dd" }
        },
        PageSettings = new PageSettings
        {
            Width = 612,
            Height = 792,
            Orientation = "portrait",
            Margins = new Margins { Top = 36, Right = 36, Bottom = 36, Left = 36 }
        },
        Elements =
        [
            new DesignerElement
            {
                Id = "text-1",
                Type = ElementType.Text,
                X = 10,
                Y = 20,
                Width = 200,
                Height = 40,
                Props = [],
                Text = new TextConfig
                {
                    FontFamily = "Inter",
                    FontSize = 12,
                    Color = "#111111"
                }
            }
        ]
    };

    private sealed class CanvasRepositoryFake : Canvas.Domain.Repositories.ITemplateRepository
    {
        private readonly Canvas.Domain.Entities.DesignTemplate template;

        public CanvasRepositoryFake(Canvas.Domain.Entities.DesignTemplate template)
        {
            this.template = template;
        }

        public Canvas.Domain.Entities.DesignTemplate? LastSaved { get; private set; }

        public Task<Canvas.Domain.Entities.DesignTemplate?> FindByIdAsync(string id) =>
            Task.FromResult<Canvas.Domain.Entities.DesignTemplate?>(template);

        public Task<Canvas.Domain.Entities.DesignTemplate?> FindVersionAsync(string id, string version) =>
            Task.FromResult<Canvas.Domain.Entities.DesignTemplate?>(template);

        public Task SaveAsync(Canvas.Domain.Entities.DesignTemplate template)
        {
            LastSaved = template;
            return Task.CompletedTask;
        }

        public Task<Canvas.Domain.Repositories.ValidationResult> ValidateAsync(Canvas.Domain.Entities.DesignTemplate template) =>
            Task.FromResult(new Canvas.Domain.Repositories.ValidationResult { IsValid = true });

        public Task<IEnumerable<Canvas.Domain.Repositories.TemplateNameInfo>> GetTemplateNamesAsync() =>
            Task.FromResult<IEnumerable<Canvas.Domain.Repositories.TemplateNameInfo>>(
                [new Canvas.Domain.Repositories.TemplateNameInfo { Id = "template-1", Name = "Template One" }]);

        public Task<Canvas.Domain.Entities.DesignTemplate> CreateVersionAsync(string id, string? versionName = null) =>
            Task.FromResult(template);
    }

    private sealed class PxaRepositoryFake : ITemplateRepository
    {
        private readonly DesignTemplate template;

        public PxaRepositoryFake(DesignTemplate template)
        {
            this.template = template;
        }

        public DesignTemplate? LastSaved { get; private set; }

        public Task<DesignTemplate?> FindByIdAsync(string id) =>
            Task.FromResult<DesignTemplate?>(template);

        public Task<DesignTemplate?> FindVersionAsync(string id, string version) =>
            Task.FromResult<DesignTemplate?>(template);

        public Task SaveAsync(DesignTemplate template)
        {
            LastSaved = template;
            return Task.CompletedTask;
        }

        public Task<ValidationResult> ValidateAsync(DesignTemplate template) =>
            Task.FromResult(new ValidationResult { IsValid = true });

        public Task<IEnumerable<TemplateNameInfo>> GetTemplateNamesAsync() =>
            Task.FromResult<IEnumerable<TemplateNameInfo>>(
                [new TemplateNameInfo { Id = "template-1", Name = "Template One" }]);

        public Task<DesignTemplate> CreateVersionAsync(string id, string? versionName = null)
        {
            var version = CreateTemplate();
            version.Id = $"{id}-{versionName}";
            return Task.FromResult(version);
        }
    }
}
