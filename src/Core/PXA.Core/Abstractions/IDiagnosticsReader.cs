namespace PXA.Core.Abstractions;

public interface IDiagnosticsReader
{
    object? Read(object documentModel);
}
