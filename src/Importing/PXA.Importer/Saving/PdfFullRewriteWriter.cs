using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Xref;

namespace PXA.Importer.Saving;

public sealed record PdfFullRewriteOptions
{
    /// <summary>
    /// A full rewrite changes every byte offset, so it necessarily breaks existing
    /// signatures. The caller must say so explicitly rather than discovering it afterwards.
    /// </summary>
    public bool AllowSignatureInvalidation { get; init; }

    /// <summary>Header version to emit. Defaults to the source version.</summary>
    public string? PdfVersion { get; init; }

    public PdfParseLimits Limits { get; init; } = new();
}

public sealed record PdfFullRewriteResult(
    byte[] Bytes,
    int ObjectCount,
    long CrossReferenceOffset,
    IReadOnlyList<PdfObjectId> InvalidatedSignatures,
    IReadOnlyList<PdfObjectId> RemovedObjects);

/// <summary>
/// Writes a document as a single fresh revision.
///
/// Unlike an incremental update, which appends and therefore keeps every earlier revision
/// in the file, this produces a file containing exactly what a reader currently resolves
/// and nothing else. That is what makes it the required save strategy whenever earlier
/// revisions must not survive.
/// </summary>
public sealed class PdfFullRewriteWriter
{
    public PdfFullRewriteResult Rewrite(ReadOnlyMemory<byte> source, PdfFullRewriteOptions? options = null)
    {
        options ??= new PdfFullRewriteOptions();
        if (source.IsEmpty)
        {
            throw new InvalidDataException("A full rewrite requires a non-empty source document.");
        }

        var context = new PdfParseContext(source, new PdfImporterOptions { Limits = options.Limits });
        var table = new PdfCrossReferenceParser().Parse(context);
        if (table.Diagnostics.Count > 0)
        {
            throw new InvalidDataException($"The source PDF cross-reference data is invalid: {table.Diagnostics[0].Code}.");
        }

        if (table.Trailer?["Encrypt"] is not null)
        {
            throw new NotSupportedException(
                "Rewriting encrypted PDFs requires encrypted object serialization and is not enabled yet.");
        }

        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        if (context.Diagnostics.Count > 0)
        {
            throw new InvalidDataException($"The source PDF could not be parsed in full: {context.Diagnostics[0].Code}.");
        }

        if (graph.Trailer?["Root"] is not PdfReference rootReference)
        {
            throw new InvalidDataException("The source PDF trailer does not contain an indirect /Root reference.");
        }

        var signatures = FindSignatures(graph);
        if (signatures.Count > 0 && !options.AllowSignatureInvalidation)
        {
            throw new InvalidOperationException(
                "A full rewrite invalidates the existing signatures in this document. " +
                "Set AllowSignatureInvalidation to proceed.");
        }

        // Only objects reachable from the trailer survive. Keeping every current xref entry
        // would retain orphaned attachments, metadata and image streams after a sanitizing
        // edit merely removed their references. Unknown reachable objects remain byte-faithful.
        var reachable = CollectReachableObjects(graph);

        // Cross-reference stream and object stream containers are structural artefacts of the
        // source layout. Their contents are re-emitted as plain indirect objects, so carrying
        // the containers forward would preserve stale duplicates of that same content.
        var removed = new List<PdfObjectId>();
        var retained = new List<PdfIndirectObject>();
        foreach (var indirectObject in graph.Objects.Values.OrderBy(
            static value => value.Id, PdfObjectSerializer.PdfObjectIdComparer.Instance))
        {
            if (!reachable.Contains(indirectObject.Id) || IsStructuralArtefact(indirectObject.Value))
            {
                removed.Add(indirectObject.Id);
                continue;
            }

            retained.Add(indirectObject);
        }

        if (retained.Count == 0)
        {
            throw new InvalidDataException("The source PDF does not contain any objects to rewrite.");
        }

        // Deterministic renumbering: sorted source order maps onto 1..N with generation 0, so
        // the same input always produces the same output bytes.
        var mapping = new Dictionary<PdfObjectId, PdfObjectId>(retained.Count);
        for (var index = 0; index < retained.Count; index++)
        {
            mapping[retained[index].Id] = new PdfObjectId(index + 1, 0);
        }

        var version = options.PdfVersion ?? ReadPdfVersion(source.Span) ?? "1.7";
        using var stream = new MemoryStream(source.Length + 4096);
        PdfObjectSerializer.WriteAscii(stream, $"%PDF-{version}\n");
        // Marks the file as binary for tools that sniff the first bytes.
        stream.Write([(byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);

        var offsets = new SortedDictionary<PdfObjectId, long>(PdfObjectSerializer.PdfObjectIdComparer.Instance);
        foreach (var indirectObject in retained)
        {
            var id = mapping[indirectObject.Id];
            offsets[id] = stream.Position;
            PdfObjectSerializer.WriteIndirectObject(stream, id, Remap(indirectObject.Value, mapping, depth: 0));
        }

        var crossReferenceOffset = stream.Position;
        if (crossReferenceOffset > 9_999_999_999L)
        {
            throw new NotSupportedException("Classic cross-reference output supports offsets up to ten decimal digits.");
        }

        PdfObjectSerializer.WriteAscii(stream, "xref\n");
        PdfObjectSerializer.WriteAscii(stream, $"0 {retained.Count + 1}\n");
        PdfObjectSerializer.WriteAscii(stream, "0000000000 65535 f \n");
        foreach (var entry in offsets)
        {
            PdfObjectSerializer.WriteAscii(stream, $"{entry.Value:0000000000} 00000 n \n");
        }

        var documentId = CreateDocumentId(stream.GetBuffer().AsSpan(0, checked((int)crossReferenceOffset)));
        var trailer = new PdfDictionary
        {
            ["Size"] = new PdfInteger(retained.Count + 1),
            ["Root"] = new PdfReference(mapping[rootReference.Id]),
            ["ID"] = new PdfArray([
                new PdfString(documentId, IsHex: false),
                new PdfString(documentId, IsHex: false)
            ])
        };
        if (graph.Trailer["Info"] is PdfReference infoReference && mapping.TryGetValue(infoReference.Id, out var mappedInfo))
        {
            trailer["Info"] = new PdfReference(mappedInfo);
        }

        PdfObjectSerializer.WriteAscii(stream, "trailer\n");
        PdfObjectSerializer.WriteObject(stream, trailer);
        PdfObjectSerializer.WriteAscii(stream, "\nstartxref\n");
        PdfObjectSerializer.WriteAscii(stream, crossReferenceOffset.ToString(CultureInfo.InvariantCulture));
        PdfObjectSerializer.WriteAscii(stream, "\n%%EOF\n");

        var result = stream.ToArray();
        ValidateRewrite(result, retained.Count, options.Limits);
        return new PdfFullRewriteResult(result, retained.Count, crossReferenceOffset, signatures, removed);
    }

    private static void ValidateRewrite(byte[] result, int expectedObjectCount, PdfParseLimits limits)
    {
        var context = new PdfParseContext(result, new PdfImporterOptions { Limits = limits });
        var table = new PdfCrossReferenceParser().Parse(context);
        if (table.Diagnostics.Count > 0)
        {
            throw new InvalidDataException($"Post-save validation rejected the rewritten PDF: {table.Diagnostics[0].Code}.");
        }

        if (table.Revisions.Count != 1)
        {
            throw new InvalidDataException(
                $"A full rewrite must produce exactly one revision but produced {table.Revisions.Count}.");
        }

        var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
        if (context.Diagnostics.Count > 0)
        {
            throw new InvalidDataException($"Post-save validation could not parse the rewritten PDF: {context.Diagnostics[0].Code}.");
        }

        if (graph.Objects.Count != expectedObjectCount)
        {
            throw new InvalidDataException(
                $"Post-save validation resolved {graph.Objects.Count} objects but expected {expectedObjectCount}.");
        }

        if (graph.Trailer?["Root"] is not PdfReference root || graph.Resolve(root.Id)?.Value is not PdfDictionary)
        {
            throw new InvalidDataException("Post-save validation could not resolve the rewritten document catalog.");
        }
    }

    private static IReadOnlyList<PdfObjectId> FindSignatures(PdfObjectGraph graph) =>
        graph.Objects.Values
            .Where(static value => value.Value is PdfDictionary dictionary &&
                (dictionary["Type"] is PdfName { Value: "Sig" or "DocTimeStamp" } ||
                    dictionary["ByteRange"] is not null && dictionary["Contents"] is not null))
            .Select(static value => value.Id)
            .OrderBy(static id => id.Number)
            .ToArray();

    private static bool IsStructuralArtefact(PdfObject value) =>
        value is PdfStreamObject stream &&
        stream.Dictionary["Type"] is PdfName { Value: "XRef" or "ObjStm" };

    private static HashSet<PdfObjectId> CollectReachableObjects(PdfObjectGraph graph)
    {
        var reachable = new HashSet<PdfObjectId>();
        var visitedDirectObjects = new HashSet<PdfObject>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<PdfObject>();

        if (graph.Trailer is not null)
        {
            foreach (var key in new[] { "Root", "Info", "Encrypt" })
            {
                if (graph.Trailer[key] is { } value)
                {
                    pending.Push(value);
                }
            }
        }

        while (pending.Count > 0)
        {
            var value = pending.Pop();
            if (value is PdfReference reference)
            {
                if (!reachable.Add(reference.Id))
                {
                    continue;
                }

                if (graph.Resolve(reference.Id)?.Value is { } resolved)
                {
                    pending.Push(resolved);
                }

                continue;
            }

            if (!visitedDirectObjects.Add(value))
            {
                continue;
            }

            switch (value)
            {
                case PdfArray array:
                    foreach (var item in array.Items)
                    {
                        pending.Push(item);
                    }
                    break;
                case PdfDictionary dictionary:
                    foreach (var item in dictionary.Values.Values)
                    {
                        pending.Push(item);
                    }
                    break;
                case PdfStreamObject stream:
                    pending.Push(stream.Dictionary);
                    break;
            }
        }

        return reachable;
    }

    /// <summary>
    /// Derived from the written body rather than randomly, so that rewriting the same
    /// document twice produces identical bytes and the output can be compared in tests and
    /// in audit evidence.
    /// </summary>
    private static byte[] CreateDocumentId(ReadOnlySpan<byte> body) => SHA256.HashData(body)[..16];

    private static PdfObject Remap(
        PdfObject value,
        IReadOnlyDictionary<PdfObjectId, PdfObjectId> mapping,
        int depth)
    {
        if (depth > 256)
        {
            throw new InvalidDataException("The source PDF nests objects too deeply to rewrite.");
        }

        switch (value)
        {
            case PdfReference reference:
                // A reference to an object the reader cannot resolve becomes null, which is
                // what a reader already treats it as.
                return mapping.TryGetValue(reference.Id, out var mapped)
                    ? new PdfReference(mapped)
                    : PdfNull.Value;
            case PdfArray array:
                return new PdfArray(array.Items.Select(item => Remap(item, mapping, depth + 1)));
            case PdfDictionary dictionary:
                var values = new Dictionary<string, PdfObject>(dictionary.Values.Count, StringComparer.Ordinal);
                foreach (var entry in dictionary.Values)
                {
                    values[entry.Key] = Remap(entry.Value, mapping, depth + 1);
                }

                return new PdfDictionary(values);
            case PdfStreamObject streamObject:
                return new PdfStreamObject(
                    (PdfDictionary)Remap(streamObject.Dictionary, mapping, depth + 1),
                    streamObject.EncodedBytes);
            default:
                return value;
        }
    }

    private static string? ReadPdfVersion(ReadOnlySpan<byte> source)
    {
        var header = "%PDF-"u8;
        if (!source.StartsWith(header))
        {
            return null;
        }

        var rest = source[header.Length..];
        var length = 0;
        while (length < rest.Length && length < 8 && (char.IsAsciiDigit((char)rest[length]) || rest[length] == (byte)'.'))
        {
            length++;
        }

        return length == 0 ? null : Encoding.ASCII.GetString(rest[..length]);
    }
}
