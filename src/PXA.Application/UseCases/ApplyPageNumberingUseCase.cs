using PXA.Core.Abstractions;

namespace PXA.Application.UseCases;

public sealed class ApplyPageNumberingUseCase
{
    private readonly IPageNumberingService _service;

    public ApplyPageNumberingUseCase(IPageNumberingService service)
    {
        _service = service;
    }

    public void Execute(object documentModel, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);
        _service.Apply(documentModel, options);
    }
}
