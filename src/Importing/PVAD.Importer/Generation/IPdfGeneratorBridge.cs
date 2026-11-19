using PVAD.Importer.Document;
using PVAD.Importer.Graphics;
using PVAD.Importer.Objects;
using PVAD.Licensing;

namespace PVAD.Importer.Generation;

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
        PvadLicense.Require(PvadLicenseProducts.Importer);
        return SaveWithAdmissionAsync(document, output, cancellationToken);
    }

    private async Task SaveWithAdmissionAsync(PdfDocumentModel document, Stream output, CancellationToken cancellationToken)
    {
        using var operation = PvadLicense.BeginOperation(PvadNativeOperation.ImporterRegeneration);
        await SaveAdmittedAsync(document, output, cancellationToken).ConfigureAwait(false);
    }

    internal async Task SaveAdmittedAsync(PdfDocumentModel document, Stream output, CancellationToken cancellationToken = default)
    {
        PvadLicense.DemandContinuation(PvadLicenseProducts.Importer);
        if (_bridge is PvadPdfGeneratorBridge builtin)
            await builtin.RegenerateAdmittedAsync(document, output, cancellationToken).ConfigureAwait(false);
        else
            await _bridge.RegenerateAsync(document, output, cancellationToken).ConfigureAwait(false);
    }
}
