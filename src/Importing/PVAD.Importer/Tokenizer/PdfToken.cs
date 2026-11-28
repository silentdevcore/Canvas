namespace PVAD.Importer.Tokenizer;

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

    /// <summary>
    /// Returns the bytes between the delimiters of a literal or hexadecimal string token.
    /// A string that reaches the end of input without its closing delimiter is rejected
    /// instead of being truncated.
    /// </summary>
    internal ReadOnlyMemory<byte> StringContent()
    {
        if (!IsTerminatedString())
        {
            throw new FormatException("A PDF string reaches the end of input without its closing delimiter.");
        }

        return Bytes[1..^1];
    }

    /// <summary>
    /// Returns the bytes after the opening delimiter of a string token, without the closing
    /// delimiter when one is present. Content streams are interpreted leniently and are also
    /// scanned by analysis callers, so an unterminated final string keeps every available byte
    /// instead of dropping its last byte or failing on a lone delimiter.
    /// </summary>
    internal ReadOnlyMemory<byte> AvailableStringContent() =>
        IsTerminatedString() ? Bytes[1..^1] : Bytes[Math.Min(1, Bytes.Length)..];

    private bool IsTerminatedString() => Kind == PdfTokenKind.HexString
        ? Bytes.Length >= 2 && Bytes.Span[^1] == (byte)'>'
        : IsBalancedLiteral(Bytes.Span);

    // Mirrors the tokenizer: escaped bytes are skipped and nesting must close exactly at the last byte.
    private static bool IsBalancedLiteral(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2 || bytes[0] != (byte)'(')
        {
            return false;
        }

        var depth = 1;
        for (var index = 1; index < bytes.Length; index++)
        {
            switch (bytes[index])
            {
                case (byte)'\\':
                    index++;
                    break;
                case (byte)'(':
                    depth++;
                    break;
                case (byte)')' when --depth == 0:
                    return index == bytes.Length - 1;
            }
        }

        return false;
    }
}
