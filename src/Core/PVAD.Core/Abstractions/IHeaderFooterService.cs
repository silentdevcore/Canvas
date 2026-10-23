namespace PVAD.Core.Abstractions;

public interface IHeaderFooterService
{
    void Apply(object documentModel, object? options = null);
}
