namespace PVAD.Core.Abstractions;

public interface IDiagnosticsReader
{
    object? Read(object documentModel);
}
