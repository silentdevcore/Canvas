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
                var commandOperands = TakeOperands(operands, descriptor.OperandCount);
                commands.Add(new PdfContentCommand(descriptor, commandOperands, new PdfSourceSpan(token.Offset, token.Length), sequence++));
                operands.Clear();
                continue;
            }

            operands.Add(ParseOperand(token, ref tokenizer));
        }

        return commands;
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

    private static PdfSourceSpan Span(PdfToken token) => new(token.Offset, token.Length);
}
