namespace Canvas.Application.UseCases;

public sealed record GenerateDocumentRequest(
    object DocumentModel,
    string OutputPath);
