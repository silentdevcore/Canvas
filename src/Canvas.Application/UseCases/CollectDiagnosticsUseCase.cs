using Canvas.Core.Abstractions;

namespace Canvas.Application.UseCases;

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
