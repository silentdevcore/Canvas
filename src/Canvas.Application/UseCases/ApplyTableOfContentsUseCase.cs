using Canvas.Core.Abstractions;

namespace Canvas.Application.UseCases;

public sealed class ApplyTableOfContentsUseCase
{
    private readonly ITableOfContentsService _service;

    public ApplyTableOfContentsUseCase(ITableOfContentsService service)
    {
        _service = service;
    }

    public void Execute(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _service.Apply(documentModel, options);
    }
}
