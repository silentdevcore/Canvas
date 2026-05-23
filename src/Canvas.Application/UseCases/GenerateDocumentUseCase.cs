using Canvas.Core.Abstractions;

namespace Canvas.Application.UseCases;

public sealed class GenerateDocumentUseCase
{
    private readonly IDocumentRenderer _renderer;
    private readonly IOutputWriter _outputWriter;

    public GenerateDocumentUseCase(IDocumentRenderer renderer, IOutputWriter outputWriter)
    {
        _renderer = renderer;
        _outputWriter = outputWriter;
    }

    public void Execute(GenerateDocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty.", nameof(request));
        }

        var bytes = _renderer.Render(request.DocumentModel);
        _outputWriter.Write(request.OutputPath, bytes);
    }
}
