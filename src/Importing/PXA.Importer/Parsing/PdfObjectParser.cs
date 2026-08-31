using PXA.Importer.Objects;
using PXA.Importer.Streams;
using PXA.Importer.Tokenizer;
using PXA.Importer.Xref;

namespace PXA.Importer.Parsing;

public sealed class PdfObjectParser
{
    private readonly PdfParseContext _context;
    private readonly PdfCrossReferenceTable _xref;
    private readonly PdfStreamDecoderRegistry _streamDecoders;
    private readonly HashSet<PdfObjectId> _resolving = [];
    private long _activeBaseOffset;
    private int _parsedObjectCount;

    public PdfObjectParser(PdfParseContext context, PdfCrossReferenceTable xref)
    {
        _context = context;
        _xref = xref;
        _streamDecoders = new PdfStreamDecoderRegistry(context.Limits);
    }

    public PdfObjectGraph ParseDocumentGraph(bool parseCompressedObjects = true)
    {
        var graph = new PdfObjectGraph { Trailer = _xref.Trailer };

        foreach (var entry in _xref.Entries.Values)
        {
            _context.CancellationToken.ThrowIfCancellationRequested();
            if (entry.IsFree || entry.Kind == PdfCrossReferenceEntryKind.Compressed || entry.Offset < 0 || entry.Offset >= _context.Source.Length)
            {
                continue;
            }

            if (_parsedObjectCount >= _context.Limits.MaxIndirectObjectCount)
            {
                _context.AddDiagnostic(new PdfParseDiagnostic(
                    "PXAPDFPARSE005",
                    $"The document exceeds the maximum of {_context.Limits.MaxIndirectObjectCount} indirect objects; the remaining objects were not parsed."));
                break;
            }

            var indirectObject = TryParseIndirectObject(entry);
            if (indirectObject is not null)
            {
                _parsedObjectCount++;
                graph.Add(indirectObject);
                _context.ObjectCache[indirectObject.Id] = indirectObject;
            }
        }

        if (parseCompressedObjects)
        {
            ParseCompressedObjects(graph);
        }

        return graph;
    }

    public PdfObject ParseObjectAt(int offset, long baseOffset)
    {
        _activeBaseOffset = baseOffset;
        var reader = new PdfTokenReader(_context.Source.Span[offset..]);
        return ParseObject(ref reader, depth: 0);
    }

    public PdfIndirectObject? TryParseIndirectObjectAt(long offset)
    {
        return TryParseIndirectObject(new PdfCrossReferenceEntry(
            new PdfObjectId(0, 0),
            offset,
            Generation: 0,
            IsFree: false,
            Revision: 0));
    }

    private PdfIndirectObject? TryParseIndirectObject(PdfCrossReferenceEntry entry)
    {
        var span = _context.Source.Span[(int)entry.Offset..];
        var reader = new PdfTokenReader(span);
        _activeBaseOffset = entry.Offset;
        var number = reader.Read();
        var generation = reader.Read();
        var objKeyword = reader.Read();

        if (number.Kind != PdfTokenKind.Number || generation.Kind != PdfTokenKind.Number || objKeyword.Text != "obj")
        {
            return null;
        }

        if (!int.TryParse(number.Text, out var objectNumber) ||
            !int.TryParse(generation.Text, out var objectGeneration))
        {
            return null;
        }

        PdfObject value;
        try
        {
            value = ParseObject(ref reader, depth: 0);
        }
        catch (PdfParseLimitExceededException exception)
        {
            _context.AddDiagnostic(new PdfParseDiagnostic(
                exception.Code,
                exception.Message,
                objectNumber,
                objectGeneration));
            return null;
        }

        var id = new PdfObjectId(objectNumber, objectGeneration);
        var length = FindEndObjectLength(entry.Offset, reader.Position);
        return new PdfIndirectObject(id, value, new PdfSourceSpan(entry.Offset, length));
    }

    public PdfObject ParseObject(ref PdfTokenizer tokenizer)
    {
        var reader = new PdfTokenReader(_context.Source.Span[checked((int)_activeBaseOffset)..]);
        reader.Seek(tokenizer.Position);
        var parsed = ParseObject(ref reader, depth: 0);
        tokenizer.Seek(reader.Position);
        return parsed;
    }

    internal PdfObject ParseObject(ref PdfTokenReader reader) => ParseObject(ref reader, depth: 0);

    private PdfObject ParseObject(ref PdfTokenReader reader, int depth)
    {
        if (depth > _context.Limits.MaxNestingDepth)
        {
            throw new PdfParseLimitExceededException(
                "PXAPDFPARSE001",
                $"The object exceeds the maximum nesting depth of {_context.Limits.MaxNestingDepth}.");
        }

        var token = reader.Read();
        return token.Kind switch
        {
            PdfTokenKind.Name => new PdfName(PdfNameCodec.Decode(token.Text[1..])) { SourceSpan = Span(token) },
            PdfTokenKind.LiteralString => new PdfString(token.Bytes[1..^1], IsHex: false) { SourceSpan = Span(token) },
            PdfTokenKind.HexString => new PdfString(token.Bytes[1..^1], IsHex: true) { SourceSpan = Span(token) },
            PdfTokenKind.ArrayStart => ParseArray(ref reader, _activeBaseOffset + token.Offset, depth),
            PdfTokenKind.DictionaryStart => ParseDictionaryOrStream(ref reader, _activeBaseOffset + token.Offset, depth),
            PdfTokenKind.Number => ParseNumberOrReference(ref reader, token),
            PdfTokenKind.Keyword when token.Text == "true" => new PdfBoolean(true) { SourceSpan = Span(token) },
            PdfTokenKind.Keyword when token.Text == "false" => new PdfBoolean(false) { SourceSpan = Span(token) },
            PdfTokenKind.Keyword when token.Text == "null" => PdfNull.Value with { SourceSpan = Span(token) },
            _ => new PdfName(token.Text) { SourceSpan = Span(token) }
        };
    }

    private PdfArray ParseArray(ref PdfTokenReader reader, long offset, int depth)
    {
        var values = new List<PdfObject>();
        while (true)
        {
            var next = reader.Peek();
            if (next.Kind is PdfTokenKind.ArrayEnd or PdfTokenKind.EndOfFile)
            {
                reader.Read();
                break;
            }

            if (values.Count >= _context.Limits.MaxArrayItemCount)
            {
                throw new PdfParseLimitExceededException(
                    "PXAPDFPARSE002",
                    $"An array exceeds the maximum of {_context.Limits.MaxArrayItemCount} elements.");
            }

            values.Add(ParseObject(ref reader, depth + 1));
        }

        return new PdfArray(values) { SourceSpan = new PdfSourceSpan(offset, checked((int)(_activeBaseOffset + reader.Position - offset))) };
    }

    private PdfObject ParseDictionaryOrStream(ref PdfTokenReader reader, long offset, int depth)
    {
        var dictionary = new PdfDictionary();
        while (true)
        {
            var key = reader.Read();
            if (key.Kind is PdfTokenKind.DictionaryEnd or PdfTokenKind.EndOfFile)
            {
                break;
            }

            if (key.Kind != PdfTokenKind.Name)
            {
                continue;
            }

            if (dictionary.Values.Count >= _context.Limits.MaxDictionaryEntryCount)
            {
                throw new PdfParseLimitExceededException(
                    "PXAPDFPARSE003",
                    $"A dictionary exceeds the maximum of {_context.Limits.MaxDictionaryEntryCount} entries.");
            }

            dictionary[PdfNameCodec.Decode(key.Text[1..])] = ParseObject(ref reader, depth + 1);
        }

        var maybeStream = reader.Peek();
        if (maybeStream.Text != "stream")
        {
            dictionary.SourceSpan = new PdfSourceSpan(offset, checked((int)(_activeBaseOffset + reader.Position - offset)));
            return dictionary;
        }

        reader.Read();
        var length = ResolveInteger(dictionary["Length"]);
        var streamStart = checked((int)(_activeBaseOffset + reader.Position));
        if (streamStart < _context.Source.Length && _context.Source.Span[streamStart] == (byte)'\r')
        {
            streamStart++;
        }

        if (streamStart < _context.Source.Length && _context.Source.Span[streamStart] == (byte)'\n')
        {
            streamStart++;
        }

        var encoded = length > 0 && streamStart + length <= _context.Source.Length
            ? _context.Source.Slice(streamStart, checked((int)length))
            : ReadUntilEndStream(streamStart);

        var sourceLength = FindEndStreamLength(offset, streamStart, encoded.Length);
        return new PdfStreamObject(dictionary, encoded) { SourceSpan = new PdfSourceSpan(offset, sourceLength) };
    }

    private PdfObject ParseNumberOrReference(ref PdfTokenReader reader, PdfToken first)
    {
        var second = reader.Peek();
        if (second.Kind == PdfTokenKind.Number)
        {
            reader.Read();
            var third = reader.Peek();
            if (third.Text == "R" &&
                int.TryParse(first.Text, out var referenceNumber) &&
                int.TryParse(second.Text, out var referenceGeneration))
            {
                reader.Read();
                return new PdfReference(new PdfObjectId(referenceNumber, referenceGeneration))
                {
                    SourceSpan = new PdfSourceSpan(_activeBaseOffset + first.Offset, (int)(third.Offset - first.Offset) + third.Length)
                };
            }

            reader.Seek((int)second.Offset);
        }

        if (long.TryParse(first.Text, out var integer))
        {
            return new PdfInteger(integer) { SourceSpan = Span(first) };
        }

        return double.TryParse(first.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var real)
            ? new PdfNumber(real) { SourceSpan = Span(first) }
            : new PdfInteger(0) { SourceSpan = Span(first) };
    }

    private ReadOnlyMemory<byte> ReadUntilEndStream(int streamStart)
    {
        var marker = "endstream"u8;
        var source = _context.Source.Span[streamStart..];
        var index = source.IndexOf(marker);
        return index < 0 ? _context.Source[streamStart..] : _context.Source.Slice(streamStart, index);
    }

    private long ResolveInteger(PdfObject? value)
    {
        switch (value)
        {
            case PdfInteger integer:
                return integer.Value;
            case PdfNumber number:
                return (long)number.Value;
            case PdfReference reference:
                return ResolveInteger(ResolveReference(reference.Id));
            default:
                return 0;
        }
    }

    private PdfObject? ResolveReference(PdfObjectId id)
    {
        if (_context.ObjectCache.TryGetValue(id, out var cached))
        {
            return cached.Value;
        }

        if (!_xref.Entries.TryGetValue(id, out var entry) || entry.IsFree)
        {
            return null;
        }

        // The cache is only populated after a successful parse, so a cyclic /Length chain
        // would otherwise re-enter this method forever.
        if (!_resolving.Add(id))
        {
            _context.AddDiagnostic(new PdfParseDiagnostic(
                "PXAPDFPARSE006",
                "An indirect reference chain is cyclic and was not resolved.",
                id.Number,
                id.Generation));
            return null;
        }

        if (_resolving.Count > _context.Limits.MaxReferenceResolutionDepth)
        {
            _context.AddDiagnostic(new PdfParseDiagnostic(
                "PXAPDFPARSE007",
                $"An indirect reference chain exceeds the maximum resolution depth of {_context.Limits.MaxReferenceResolutionDepth}.",
                id.Number,
                id.Generation));
            _resolving.Remove(id);
            return null;
        }

        var previousBaseOffset = _activeBaseOffset;
        PdfIndirectObject? parsed;
        try
        {
            parsed = TryParseIndirectObject(entry);
        }
        finally
        {
            _resolving.Remove(id);
            _activeBaseOffset = previousBaseOffset;
        }

        if (parsed is null)
        {
            return null;
        }

        _context.ObjectCache[id] = parsed;
        return parsed.Value;
    }

    internal void ParseCompressedObjects(PdfObjectGraph graph)
    {
        foreach (var entry in _xref.Entries.Values.Where(entry => entry.Kind == PdfCrossReferenceEntryKind.Compressed))
        {
            _context.CancellationToken.ThrowIfCancellationRequested();
            var objectStreamId = new PdfObjectId(entry.ObjectStreamNumber, 0);
            if (graph.Resolve(entry.Id) is not null)
            {
                continue;
            }

            var objectStream = graph.Resolve(objectStreamId)?.Value as PdfStreamObject ?? ResolveReference(objectStreamId) as PdfStreamObject;
            if (objectStream is null || objectStream.Dictionary["Type"] is not PdfName { Value: "ObjStm" })
            {
                continue;
            }

            var parsedObjects = ParseObjectStream(objectStream);
            if (!parsedObjects.TryGetValue(entry.Id.Number, out var value))
            {
                continue;
            }

            var indirectObject = new PdfIndirectObject(entry.Id, value, value.SourceSpan);
            graph.Add(indirectObject);
            _context.ObjectCache[entry.Id] = indirectObject;
        }
    }

    private Dictionary<int, PdfObject> ParseObjectStream(PdfStreamObject stream)
    {
        if (!TryResolveInteger(stream.Dictionary["N"], out var objectCount) ||
            !TryResolveInteger(stream.Dictionary["First"], out var firstObjectOffset) ||
            objectCount <= 0 ||
            firstObjectOffset < 0)
        {
            return [];
        }

        if (objectCount > _context.Limits.MaxObjectStreamEntryCount)
        {
            _context.AddDiagnostic(new PdfParseDiagnostic(
                "PXAPDFPARSE004",
                $"An object stream declares {objectCount} entries, above the maximum of {_context.Limits.MaxObjectStreamEntryCount}."));
            return [];
        }

        ReadOnlyMemory<byte> bytes;
        try
        {
            bytes = stream.IsDecoded ? stream.DecodedBytes : _streamDecoders.Decode(stream);
        }
        catch (PdfParseLimitExceededException exception)
        {
            _context.AddDiagnostic(new PdfParseDiagnostic(exception.Code, exception.Message));
            return [];
        }

        stream.SetDecodedBytes(bytes);

        var headerReader = new PdfTokenReader(bytes.Span);
        var objectOffsets = new List<(int ObjectNumber, int Offset)>(checked((int)objectCount));
        for (var i = 0; i < objectCount; i++)
        {
            var objectNumber = headerReader.Read();
            var offset = headerReader.Read();
            if (!int.TryParse(objectNumber.Text, out var parsedObjectNumber) ||
                !int.TryParse(offset.Text, out var parsedOffset))
            {
                break;
            }

            objectOffsets.Add((parsedObjectNumber, parsedOffset));
        }

        var objects = new Dictionary<int, PdfObject>();
        foreach (var (objectNumber, relativeOffset) in objectOffsets)
        {
            var objectOffset = checked((int)firstObjectOffset + relativeOffset);
            if (objectOffset < 0 || objectOffset >= bytes.Length)
            {
                continue;
            }

            var objectContext = new PdfParseContext(bytes, _context.Options, _context.CancellationToken);
            var objectParser = new PdfObjectParser(objectContext, _xref);
            try
            {
                objects[objectNumber] = objectParser.ParseObjectAt(objectOffset, baseOffset: 0);
            }
            catch (PdfParseLimitExceededException exception)
            {
                _context.AddDiagnostic(new PdfParseDiagnostic(exception.Code, exception.Message, objectNumber));
            }

            foreach (var diagnostic in objectContext.Diagnostics)
            {
                _context.AddDiagnostic(diagnostic);
            }
        }

        return objects;
    }

    private bool TryResolveInteger(PdfObject? value, out long integer)
    {
        integer = ResolveInteger(value);
        return integer != 0 || value is PdfInteger { Value: 0 } or PdfNumber { Value: 0 };
    }

    private int FindEndStreamLength(long objectOffset, int streamStart, int encodedLength)
    {
        var searchStart = Math.Min(streamStart + encodedLength, _context.Source.Length);
        var marker = "endstream"u8;
        var remaining = _context.Source.Span[searchStart..];
        var markerIndex = remaining.IndexOf(marker);

        if (markerIndex < 0)
        {
            return checked((int)(streamStart + encodedLength - objectOffset));
        }

        return checked((int)(searchStart + markerIndex + marker.Length - objectOffset));
    }

    private int FindEndObjectLength(long objectOffset, int parsedLength)
    {
        var searchStart = Math.Min(checked((int)objectOffset + parsedLength), _context.Source.Length);
        var marker = "endobj"u8;
        var remaining = _context.Source.Span[searchStart..];
        var markerIndex = remaining.IndexOf(marker);

        if (markerIndex < 0)
        {
            return parsedLength;
        }

        return checked((int)(searchStart + markerIndex + marker.Length - objectOffset));
    }

    private PdfSourceSpan Span(PdfToken token) => new(_activeBaseOffset + token.Offset, token.Length);
}
