using Canvas.Importer.Objects;
using Canvas.Importer.Tokenizer;

namespace Canvas.Importer.Content;

public sealed class PdfContentStreamParser
{
    public IReadOnlyList<PdfContentCommand> Parse(ReadOnlyMemory<byte> contentBytes)
    {
        var tokenizer = new PdfTokenizer(contentBytes.Span);
        var operands = new List<PdfObject>(8);
        var commands = new List<PdfContentCommand>();
        var sequence = 0;

        while (true)
        {
            var token = tokenizer.ReadToken();
            if (token.Kind == PdfTokenKind.EndOfFile)
            {
                break;
            }

            if (token.Kind == PdfTokenKind.Comment)
            {
                continue;
            }

            if (token.Kind == PdfTokenKind.Keyword && PdfOperatorRegistry.TryGet(token.Text, out var descriptor))
            {
                if (descriptor.Name == "BI")
                {
                    commands.Add(ParseInlineImageCommand(token, ref tokenizer, contentBytes, sequence++));
                    operands.Clear();
                    continue;
                }

                var commandOperands = TakeOperands(operands, descriptor.OperandCount);
                commands.Add(new PdfContentCommand(descriptor, commandOperands, new PdfSourceSpan(token.Offset, token.Length), sequence++));
                operands.Clear();
                continue;
            }

            operands.Add(ParseOperand(token, ref tokenizer));
        }

        return commands;
    }

    private static PdfContentCommand ParseInlineImageCommand(PdfToken startToken, ref PdfTokenizer tokenizer, ReadOnlyMemory<byte> contentBytes, int sequence)
    {
        var dictionary = new PdfDictionary();

        while (true)
        {
            var token = tokenizer.ReadToken();
            if (token.Kind == PdfTokenKind.EndOfFile)
            {
                return CreateInlineImageCommand(startToken, dictionary, ReadOnlyMemory<byte>.Empty, sequence);
            }

            if (token.Kind == PdfTokenKind.Comment)
            {
                continue;
            }

            if (token.Kind == PdfTokenKind.Keyword && token.Text == "ID")
            {
                break;
            }

            if (token.Kind == PdfTokenKind.Name)
            {
                dictionary[token.Text[1..]] = ParseOperand(tokenizer.ReadToken(), ref tokenizer);
            }
        }

        var dataStart = SkipInlineImageDataPrefix(contentBytes.Span, tokenizer.Position);
        var dataEnd = FindInlineImageDataEnd(contentBytes.Span, dataStart, out var resumePosition);
        tokenizer.Seek(resumePosition);

        return CreateInlineImageCommand(startToken, dictionary, contentBytes.Slice(dataStart, dataEnd - dataStart), sequence);
    }

    private static IReadOnlyList<PdfObject> TakeOperands(List<PdfObject> operands, int? count)
    {
        if (count is null || count.Value >= operands.Count)
        {
            return [.. operands];
        }

        return operands.Skip(operands.Count - count.Value).ToArray();
    }

    private static PdfObject ParseOperand(PdfToken token, ref PdfTokenizer tokenizer)
    {
        return token.Kind switch
        {
            PdfTokenKind.Name => new PdfName(token.Text[1..]) { SourceSpan = Span(token) },
            PdfTokenKind.Number when long.TryParse(token.Text, out var integer) => new PdfInteger(integer) { SourceSpan = Span(token) },
            PdfTokenKind.Number when double.TryParse(token.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) => new PdfNumber(number) { SourceSpan = Span(token) },
            PdfTokenKind.LiteralString => new PdfString(token.Bytes[1..^1], false) { SourceSpan = Span(token) },
            PdfTokenKind.HexString => new PdfString(token.Bytes[1..^1], true) { SourceSpan = Span(token) },
            PdfTokenKind.ArrayStart => ParseArray(ref tokenizer, token),
            PdfTokenKind.DictionaryStart => ParseDictionary(ref tokenizer, token),
            _ => new PdfName(token.Text) { SourceSpan = Span(token) }
        };
    }

    private static PdfContentCommand CreateInlineImageCommand(PdfToken startToken, PdfDictionary dictionary, ReadOnlyMemory<byte> inlineBytes, int sequence)
    {
        PdfOperatorRegistry.TryGet("BI", out var descriptor);
        return new PdfContentCommand(
            descriptor,
            [new PdfStreamObject(dictionary, inlineBytes)],
            new PdfSourceSpan(startToken.Offset, startToken.Length),
            sequence);
    }

    private static PdfArray ParseArray(ref PdfTokenizer tokenizer, PdfToken start)
    {
        var array = new PdfArray { SourceSpan = Span(start) };
        while (true)
        {
            var token = tokenizer.ReadToken();
            if (token.Kind is PdfTokenKind.ArrayEnd or PdfTokenKind.EndOfFile)
            {
                return array;
            }

            array.Add(ParseOperand(token, ref tokenizer));
        }
    }

    private static PdfDictionary ParseDictionary(ref PdfTokenizer tokenizer, PdfToken start)
    {
        var dictionary = new PdfDictionary { SourceSpan = Span(start) };
        while (true)
        {
            var key = tokenizer.ReadToken();
            if (key.Kind is PdfTokenKind.DictionaryEnd or PdfTokenKind.EndOfFile)
            {
                return dictionary;
            }

            if (key.Kind == PdfTokenKind.Name)
            {
                dictionary[key.Text[1..]] = ParseOperand(tokenizer.ReadToken(), ref tokenizer);
            }
        }
    }

    private static int SkipInlineImageDataPrefix(ReadOnlySpan<byte> bytes, int position)
    {
        if (position >= bytes.Length)
        {
            return position;
        }

        return bytes[position] switch
        {
            (byte)'\r' when position + 1 < bytes.Length && bytes[position + 1] == (byte)'\n' => position + 2,
            0 or 9 or 10 or 12 or 13 or 32 => position + 1,
            _ => position
        };
    }

    private static int FindInlineImageDataEnd(ReadOnlySpan<byte> bytes, int dataStart, out int resumePosition)
    {
        for (var index = dataStart; index + 1 < bytes.Length; index++)
        {
            if (bytes[index] != (byte)'E' || bytes[index + 1] != (byte)'I')
            {
                continue;
            }

            var hasLeadingBoundary = index == dataStart || IsInlineImageBoundary(bytes[index - 1]);
            var trailingIndex = index + 2;
            var hasTrailingBoundary = trailingIndex >= bytes.Length || IsInlineImageBoundary(bytes[trailingIndex]);
            if (!hasLeadingBoundary || !hasTrailingBoundary)
            {
                continue;
            }

            resumePosition = trailingIndex;
            return TrimInlineImageDataEnd(bytes, dataStart, index);
        }

        resumePosition = bytes.Length;
        return bytes.Length;
    }

    private static bool IsInlineImageBoundary(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private static int TrimInlineImageDataEnd(ReadOnlySpan<byte> bytes, int dataStart, int dataEnd)
    {
        while (dataEnd > dataStart && IsInlineImageBoundary(bytes[dataEnd - 1]))
        {
            dataEnd--;
        }

        return dataEnd;
    }

    private static PdfSourceSpan Span(PdfToken token) => new(token.Offset, token.Length);
}
