using PVAD.Importer.Objects;
using PVAD.Importer.Trust;

namespace PVAD.Importer.Saving;

public sealed record PdfSaveRequest
{
    public required PdfSaveStrategy Strategy { get; init; }

    /// <summary>
    /// Objects to change. Incremental saves append them; full rewrites apply them to the
    /// resolved graph before serializing one fresh revision.
    /// </summary>
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
                var rewritten = request.Updates.Count == 0
                    ? _fullRewriteWriter.Rewrite(source, request.FullRewriteOptions)
                    : _fullRewriteWriter.RewriteWithUpdates(source, request.Updates, request.FullRewriteOptions);
                return new PdfSaveResult(
                    rewritten.Bytes,
                    PdfSaveStrategy.FullRewrite,
                    1,
                    request.Updates.Select(static update => update.Id).Distinct().ToArray(),
                    rewritten.InvalidatedSignatures);

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Strategy, "Unknown PDF save strategy.");
        }
    }
}
