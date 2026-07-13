namespace PXA.Importer.Tokenizer;

public enum PdfTokenKind
{
    EndOfFile,
    Number,
    Name,
    LiteralString,
    HexString,
    ArrayStart,
    ArrayEnd,
    DictionaryStart,
    DictionaryEnd,
    Keyword,
    Comment
}

public readonly record struct PdfToken(PdfTokenKind Kind, ReadOnlyMemory<byte> Bytes, long Offset)
{
    public int Length => Bytes.Length;
    public string Text => System.Text.Encoding.ASCII.GetString(Bytes.Span);
}
