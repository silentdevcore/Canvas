using System.Globalization;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Xref;

namespace PXA.Importer.Saving;

public sealed record PdfIncrementalObjectUpdate(PdfObjectId Id, PdfObject Value);

public sealed record PdfIncrementalUpdateResult(
    byte[] Bytes,
    long PreviousCrossReferenceOffset,
    long CrossReferenceOffset,
    int RevisionCount,
    IReadOnlyList<PdfObjectId> ChangedObjects);

public sealed class PdfIncrementalUpdateWriter
{
    public PdfIncrementalUpdateResult Append(
        ReadOnlyMemory<byte> source,
        IEnumerable<PdfIncrementalObjectUpdate> objectUpdates)
    {
        ArgumentNullException.ThrowIfNull(objectUpdates);
        if (source.IsEmpty)
        {
            throw new InvalidDataException("An incremental PDF update requires a non-empty source document.");
        }

        var updates = objectUpdates
            .Select((update, index) => (Update: update, Index: index))
            .GroupBy(static item => item.Update.Id)
            .Select(static group => group.MaxBy(static item => item.Index).Update)
            .OrderBy(static update => update.Id.Number)
            .ThenBy(static update => update.Id.Generation)
            .ToArray();
        if (updates.Length == 0)
        {
            throw new ArgumentException("At least one PDF object update is required.", nameof(objectUpdates));
        }

        if (updates.Any(static update => update.Id.Number <= 0 || update.Id.Generation < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(objectUpdates), "PDF object numbers must be positive and generations cannot be negative.");
        }

        var sourceTable = ParseAndValidate(source, expectedRevisionCount: null, expectedObjects: null);
        var trailer = sourceTable.Trailer
            ?? throw new InvalidDataException("The PDF does not contain a readable trailer.");
        if (trailer["Root"] is null)
        {
            throw new InvalidDataException("The PDF trailer is missing /Root.");
        }

        if (trailer["Encrypt"] is not null)
        {
            throw new NotSupportedException("Incremental updates of encrypted PDFs require encrypted object serialization and are not enabled yet.");
        }

        var latestRevision = sourceTable.Revisions.LastOrDefault()
            ?? throw new InvalidDataException("The PDF does not contain a trustworthy startxref revision.");
        using var stream = new MemoryStream(source.Length + 4096);
        stream.Write(source.Span);
        if (source.Span[^1] != (byte)'\n')
        {
            PdfObjectSerializer.WriteAscii(stream, "\n");
        }

        var offsets = new SortedDictionary<PdfObjectId, long>(PdfObjectSerializer.PdfObjectIdComparer.Instance);
        foreach (var update in updates)
        {
            offsets[update.Id] = stream.Position;
            PdfObjectSerializer.WriteIndirectObject(stream, update.Id, update.Value);
        }

        var crossReferenceOffset = stream.Position;
        if (crossReferenceOffset > 9_999_999_999L || offsets.Values.Any(static offset => offset > 9_999_999_999L))
        {
            throw new NotSupportedException("Classic cross-reference output supports offsets up to ten decimal digits.");
        }

        PdfObjectSerializer.WriteAscii(stream, "xref\n");
        foreach (var subsection in PdfObjectSerializer.GroupSubsections(offsets))
        {
            PdfObjectSerializer.WriteAscii(stream, $"{subsection.Start} {subsection.Entries.Count}\n");
            foreach (var entry in subsection.Entries)
            {
                PdfObjectSerializer.WriteAscii(stream, $"{entry.Offset:0000000000} {entry.Generation:00000} n \n");
            }
        }

        var size = Math.Max(ReadInteger(trailer["Size"]), offsets.Keys.Max(static id => (long)id.Number) + 1);
        var incrementalTrailer = new PdfDictionary
        {
            ["Size"] = new PdfInteger(size),
            ["Root"] = trailer["Root"],
            ["Prev"] = new PdfInteger(latestRevision.CrossReferenceOffset)
        };
        if (trailer["Info"] is { } info)
        {
            incrementalTrailer["Info"] = info;
        }

        if (trailer["ID"] is { } id)
        {
            incrementalTrailer["ID"] = id;
        }

        PdfObjectSerializer.WriteAscii(stream, "trailer\n");
        PdfObjectSerializer.WriteObject(stream, incrementalTrailer);
        PdfObjectSerializer.WriteAscii(stream, "\nstartxref\n");
        PdfObjectSerializer.WriteAscii(stream, crossReferenceOffset.ToString(CultureInfo.InvariantCulture));
        PdfObjectSerializer.WriteAscii(stream, "\n%%EOF\n");

        var result = stream.ToArray();
        var resultTable = ParseAndValidate(
            result,
            sourceTable.Revisions.Count + 1,
            updates.Select(static update => update.Id).ToArray());
        if (resultTable.Revisions[^1].CrossReferenceOffset != crossReferenceOffset)
        {
            throw new InvalidDataException("Post-save validation resolved an unexpected final cross-reference offset.");
        }

        return new PdfIncrementalUpdateResult(
            result,
            latestRevision.CrossReferenceOffset,
            crossReferenceOffset,
            resultTable.Revisions.Count,
            updates.Select(static update => update.Id).ToArray());
    }

    private static PdfCrossReferenceTable ParseAndValidate(
        ReadOnlyMemory<byte> source,
        int? expectedRevisionCount,
        IReadOnlyCollection<PdfObjectId>? expectedObjects)
    {
        var context = new PdfParseContext(source, new PdfImporterOptions());
        var table = new PdfCrossReferenceParser().Parse(context);
        if (table.Diagnostics.Count > 0)
        {
            throw new InvalidDataException($"The PDF revision chain is invalid: {table.Diagnostics[0].Code}.");
        }

        if (expectedRevisionCount is not null && table.Revisions.Count != expectedRevisionCount)
        {
            throw new InvalidDataException(
                $"Post-save validation expected {expectedRevisionCount} revisions but found {table.Revisions.Count}.");
        }

        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        if (graph.Trailer?["Root"] is null)
        {
            throw new InvalidDataException("Post-save validation could not resolve the document trailer and catalog root.");
        }

        if (expectedObjects is not null)
        {
            var missingObject = expectedObjects.FirstOrDefault(id => graph.Resolve(id) is null);
            if (missingObject != default)
            {
                throw new InvalidDataException(
                    $"Post-save validation could not resolve object {missingObject.Number} {missingObject.Generation}.");
            }
        }

        return table;
    }

    private static long ReadInteger(PdfObject? value) => value switch
    {
        PdfInteger integer => integer.Value,
        PdfNumber number when number.Value >= 0 && number.Value <= long.MaxValue => (long)number.Value,
        _ => 0
    };
}
