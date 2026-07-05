using Canvas.Domain.Entities;
using Canvas.Domain.Repositories;
using Canvas.Domain.ValueObjects;
using PXA.Application.UseCases;

namespace PXA.Application.Tests;

public sealed class ValidateTemplateUseCaseTests
{
    [Fact]
    public async Task ValidateTemplate_DelegatesToCanvasRepositoryAndReturnsPxaResult()
    {
        var repository = new FakeTemplateRepository(new Canvas.Domain.Repositories.ValidationResult
        {
            IsValid = false,
            Errors = ["missing-name"],
            Warnings = ["sample-warning"],
        });
        var useCase = new ValidateTemplateUseCase(repository);

        var result = await useCase.ExecuteAsync(new ValidateTemplateRequest
        {
            Id = "template-1",
            Name = "Template",
            Elements = [],
            PageSettings = new PageSettings
            {
                Width = 595,
                Height = 842,
                Orientation = "portrait",
                Margins = new Margins(),
            },
            TemplateMetadata = new PXA.Application.UseCases.TemplateMetadata
            {
                Version = "2.0",
                Locale = "de-DE",
                FormattingProfile = new PXA.Application.UseCases.FormattingProfile
                {
                    DateFormat = "dd.MM.yyyy",
                },
            },
        });

        Assert.False(result.IsValid);
        Assert.Equal(["missing-name"], result.Errors);
        Assert.Equal("template-1", repository.LastValidated!.Id);
        Assert.Equal("2.0", repository.LastValidated.Metadata!.Version);
        Assert.Equal("dd.MM.yyyy", repository.LastValidated.Metadata.FormattingProfile!.DateFormat);
    }

    private sealed class FakeTemplateRepository : ITemplateRepository
    {
        private readonly Canvas.Domain.Repositories.ValidationResult result;

        public FakeTemplateRepository(Canvas.Domain.Repositories.ValidationResult result)
        {
            this.result = result;
        }

        public DesignTemplate? LastValidated { get; private set; }

        public Task<DesignTemplate?> FindByIdAsync(string id) => Task.FromResult<DesignTemplate?>(null);

        public Task<DesignTemplate?> FindVersionAsync(string id, string version) => Task.FromResult<DesignTemplate?>(null);

        public Task SaveAsync(DesignTemplate template) => Task.CompletedTask;

        public Task<Canvas.Domain.Repositories.ValidationResult> ValidateAsync(DesignTemplate template)
        {
            LastValidated = template;
            return Task.FromResult(result);
        }

        public Task<IEnumerable<TemplateNameInfo>> GetTemplateNamesAsync() =>
            Task.FromResult<IEnumerable<TemplateNameInfo>>([]);

        public Task<DesignTemplate> CreateVersionAsync(string id, string? versionName = null) =>
            throw new NotSupportedException();
    }
}
