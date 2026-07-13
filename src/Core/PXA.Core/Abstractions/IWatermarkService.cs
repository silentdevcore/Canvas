namespace PXA.Core.Abstractions;

public interface IWatermarkService
{
    void Apply(object documentModel, string text, object? options = null);
}
