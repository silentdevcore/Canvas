using Canvas.Core.Abstractions;

namespace Canvas.Application.UseCases;

public sealed class ApplyHeaderFooterUseCase
{
    private readonly IHeaderFooterService _service;

    public ApplyHeaderFooterUseCase(IHeaderFooterService service)
    {
        _service = service;
    }

    public void Execute(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _service.Apply(documentModel, options);
    }
}
