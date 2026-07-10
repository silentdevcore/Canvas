using PXA.Domain.Entities;
using PXA.Domain.Repositories;

namespace PXA.Application.UseCases;

public class GetTemplateUseCase
{
    private readonly ITemplateRepository _templateRepository;

    public GetTemplateUseCase(ITemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;
    }

    public async Task<DesignTemplate> ExecuteAsync(GetTemplateRequest request)
    {
        DesignTemplate? template;

        if (!string.IsNullOrEmpty(request.Version))
        {
            template = await _templateRepository.FindVersionAsync(request.Id, request.Version);
        }
        else
        {
            template = await _templateRepository.FindByIdAsync(request.Id);
        }

        if (template == null)
        {
            throw new InvalidOperationException($"Template '{request.Id}'{(request.Version != null ? $" version '{request.Version}'" : "")} not found");
        }

        return template;
    }

    public async Task<IEnumerable<TemplateNameInfo>> GetTemplateNamesAsync()
    {
        return await _templateRepository.GetTemplateNamesAsync();
    }
}

public class GetTemplateRequest
{
    public required string Id { get; set; }
    public string? Version { get; set; }
}