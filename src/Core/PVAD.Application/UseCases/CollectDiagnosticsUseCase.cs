using PVAD.Core.Abstractions;

namespace PVAD.Application.UseCases;

public sealed class CollectDiagnosticsUseCase
{
    private readonly IDiagnosticsReader _diagnosticsReader;

    public CollectDiagnosticsUseCase(IDiagnosticsReader diagnosticsReader)
    {
        _diagnosticsReader = diagnosticsReader;
    }

    public object? Execute(object documentModel)
    {
        ArgumentNullException.ThrowIfNull(documentModel);

        return _diagnosticsReader.Read(documentModel);
    }
}
