using PXA.Importer.Objects;
using PXA.Importer.Trust;

namespace PXA.Importer.Saving;

public sealed record PdfSaveRequest
{
    public required PdfSaveStrategy Strategy { get; init; }

    /// <summary>Objects to append. Required for an incremental update, ignored by a full rewrite.</summary>
    public IReadOnlyList<PdfIncrementalObjectUpdate> Updates { get; init; } = [];

    public PdfFullRewriteOptions? FullRewriteOptions { get; init; }
}

public sealed record PdfSaveResult(
    byte[] Bytes,
    PdfSaveStrategy Strategy,
    int RevisionCount,
    IReadOnlyList<PdfObjectId> ChangedObjects,
    IReadOnlyList<PdfObjectId> InvalidatedSignatures);

/// <summary>
/// Selects between the two save strategies explicitly, so that a caller states which
/// guarantee it needs rather than inheriting whichever writer happened to be reachable.
/// Both paths reparse and validate their output before returning it.
/// </summary>
public sealed class PdfDocumentSaver
{
    private readonly PdfIncrementalUpdateWriter _incrementalWriter = new();
    private readonly PdfFullRewriteWriter _fullRewriteWriter = new();

    public PdfSaveResult Save(ReadOnlyMemory<byte> source, PdfSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Strategy)
        {
            case PdfSaveStrategy.IncrementalUpdate:
                if (request.Updates.Count == 0)
                {
                    throw new ArgumentException(
                        "An incremental update requires at least one object update.",
                        nameof(request));
                }

                var appended = _incrementalWriter.Append(source, request.Updates);
                return new PdfSaveResult(
                    appended.Bytes,
                    PdfSaveStrategy.IncrementalUpdate,
                    appended.RevisionCount,
                    appended.ChangedObjects,
                    []);

            case PdfSaveStrategy.FullRewrite:
                var rewritten = _fullRewriteWriter.Rewrite(source, request.FullRewriteOptions);
                return new PdfSaveResult(
                    rewritten.Bytes,
                    PdfSaveStrategy.FullRewrite,
                    1,
                    [],
                    rewritten.InvalidatedSignatures);

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Strategy, "Unknown PDF save strategy.");
        }
    }
}
