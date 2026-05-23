namespace Canvas.Core.Abstractions;

public interface IPageNumberingService
{
    void Apply(object documentModel, object? options = null);
}
