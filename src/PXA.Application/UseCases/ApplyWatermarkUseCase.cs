using PXA.Core.Abstractions;

namespace PXA.Application.UseCases;

public sealed class ApplyWatermarkUseCase
{
    private readonly IWatermarkService _service;

    public ApplyWatermarkUseCase(IWatermarkService service)
    {
        _service = service;
    }

    public void Execute(object documentModel, string text, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(documentModel);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Watermark text cannot be null or empty.", nameof(text));
        }

        _service.Apply(documentModel, text, options);
    }
}
