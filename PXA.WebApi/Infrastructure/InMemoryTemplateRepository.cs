using PXA.Domain.Entities;
using PXA.Domain.Repositories;
using PXA.Domain.ValueObjects;

namespace PXA.WebApi.Infrastructure;

public class InMemoryTemplateRepository : ITemplateRepository
{
    private readonly Dictionary<string, DesignTemplate> _templates = new();

    public InMemoryTemplateRepository()
    {
        // Add a sample template for testing
        var sampleTemplate = new DesignTemplate
        {
            Id = "sample-invoice",
            Name = "Sample Invoice Template",
            Description = "A basic invoice template for testing",
            Elements = new List<DesignerElement>
            {
                new DesignerElement
                {
                    Id = "title",
                    Type = ElementType.Text,
                    Props = new Dictionary<string, object> { { "text", "INVOICE" } },
                    X = 50,
                    Y = 50,
                    Width = 200,
                    Height = 30
                },
                new DesignerElement
                {
                    Id = "customer-name",
                    Type = ElementType.Text,
                    Props = new Dictionary<string, object> { { "text", "Customer Name" } },
                    Binding = new BindingConfig
                    {
                        DataPath = "customer.name",
                        FallbackValue = "Customer Name"
                    },
                    X = 50,
                    Y = 100,
                    Width = 200,
                    Height = 20
                }
            },
            PageSettings = new PageSettings(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _templates[sampleTemplate.Id] = sampleTemplate;
    }

    public Task<DesignTemplate?> FindByIdAsync(string id)
    {
        _templates.TryGetValue(id, out var template);
        return Task.FromResult(template);
    }

    public Task<DesignTemplate?> FindVersionAsync(string id, string version)
    {
        // For now, just return the current version
        return FindByIdAsync(id);
    }

    public Task SaveAsync(DesignTemplate template)
    {
        _templates[template.Id] = template;
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateAsync(DesignTemplate template)
    {
        var result = new ValidationResult { IsValid = true };

        // Basic validation
        if (string.IsNullOrEmpty(template.Id))
        {
            result.IsValid = false;
            result.Errors.Add("Template ID is required");
        }

        if (string.IsNullOrEmpty(template.Name))
        {
            result.IsValid = false;
            result.Errors.Add("Template name is required");
        }

        if (template.Elements == null || !template.Elements.Any())
        {
            result.IsValid = false;
            result.Errors.Add("Template must have at least one element");
        }

        return Task.FromResult(result);
    }

    public Task<IEnumerable<TemplateNameInfo>> GetTemplateNamesAsync()
    {
        var names = _templates.Values.Select(t => new TemplateNameInfo
        {
            Id = t.Id,
            Name = t.Name
        });
        return Task.FromResult(names);
    }

    public Task<DesignTemplate> CreateVersionAsync(string id, string? versionName = null)
    {
        throw new NotImplementedException("Versioning not implemented in in-memory repository");
    }
}
