using PXA.Importer.Document;
using PXA.Importer.Graphics;
using PXA.Importer.Objects;

namespace PXA.Importer.Generation;

public interface IPdfGeneratorBridge
{
    Task RegenerateAsync(PdfDocumentModel document, Stream output, CancellationToken cancellationToken = default);
    object MapObject(PdfObject parsedObject);
    object MapGraphicsElement(PdfGraphicsElement element);
    object MapResources(PdfDictionary resources);
}

public sealed class PdfGeneratorBridgePipeline
{
    private readonly IPdfGeneratorBridge _bridge;

    public PdfGeneratorBridgePipeline(IPdfGeneratorBridge bridge)
    {
        _bridge = bridge;
    }

    public Task SaveAsync(PdfDocumentModel document, Stream output, CancellationToken cancellationToken = default)
    {
        return _bridge.RegenerateAsync(document, output, cancellationToken);
    }
}
