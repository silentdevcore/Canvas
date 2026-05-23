namespace Canvas.Core.Abstractions;

public interface ITableOfContentsService
{
    void Apply(object documentModel, object? options = null);
}
