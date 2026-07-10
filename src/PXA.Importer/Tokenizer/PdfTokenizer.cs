namespace PXA.Importer.Tokenizer;

public ref struct PdfTokenizer
{
    private readonly ReadOnlySpan<byte> _source;
    private int _position;

    public PdfTokenizer(ReadOnlySpan<byte> source)
    {
        _source = source;
        _position = 0;
    }

    public int Position => _position;

    public void Seek(int position)
    {
        _position = Math.Clamp(position, 0, _source.Length);
    }

    public PdfToken ReadToken()
    {
        SkipWhiteSpace();
        if (_position >= _source.Length)
        {
            return new PdfToken(PdfTokenKind.EndOfFile, ReadOnlyMemory<byte>.Empty, _position);
        }

        var start = _position;
        var current = _source[_position];

        if (current == (byte)'%')
        {
            return ReadUntilLineEnd(PdfTokenKind.Comment, start);
        }

        if (current == (byte)'/' )
        {
            _position++;
            while (_position < _source.Length && !IsDelimiter(_source[_position]) && !IsWhiteSpace(_source[_position]))
            {
                _position++;
            }

            return Create(PdfTokenKind.Name, start, _position);
        }

        if (current == (byte)'(')
        {
            return ReadLiteralString(start);
        }

        if (current == (byte)'<')
        {
            if (Peek(1) == (byte)'<')
            {
                _position += 2;
                return Create(PdfTokenKind.DictionaryStart, start, _position);
            }

            return ReadHexString(start);
        }

        if (current == (byte)'>' && Peek(1) == (byte)'>')
        {
            _position += 2;
            return Create(PdfTokenKind.DictionaryEnd, start, _position);
        }

        if (current == (byte)'[')
        {
            _position++;
            return Create(PdfTokenKind.ArrayStart, start, _position);
        }

        if (current == (byte)']')
        {
            _position++;
            return Create(PdfTokenKind.ArrayEnd, start, _position);
        }

        if (IsNumberStart(current))
        {
            return ReadNumber(start);
        }

        while (_position < _source.Length && !IsDelimiter(_source[_position]) && !IsWhiteSpace(_source[_position]))
        {
            _position++;
        }

        return Create(PdfTokenKind.Keyword, start, _position);
    }

    private PdfToken ReadLiteralString(int start)
    {
        _position++;
        var depth = 1;
        while (_position < _source.Length && depth > 0)
        {
            var current = _source[_position++];
            if (current == (byte)'\\' && _position < _source.Length)
            {
                _position++;
                continue;
            }

            if (current == (byte)'(')
            {
                depth++;
            }
            else if (current == (byte)')')
            {
                depth--;
            }
        }

        return Create(PdfTokenKind.LiteralString, start, _position);
    }

    private PdfToken ReadHexString(int start)
    {
        _position++;
        while (_position < _source.Length && _source[_position] != (byte)'>')
        {
            _position++;
        }

        if (_position < _source.Length)
        {
            _position++;
        }

        return Create(PdfTokenKind.HexString, start, _position);
    }

    private PdfToken ReadNumber(int start)
    {
        _position++;
        while (_position < _source.Length && IsNumberBody(_source[_position]))
        {
            _position++;
        }

        return Create(PdfTokenKind.Number, start, _position);
    }

    private PdfToken ReadUntilLineEnd(PdfTokenKind kind, int start)
    {
        while (_position < _source.Length && _source[_position] is not (byte)'\r' and not (byte)'\n')
        {
            _position++;
        }

        return Create(kind, start, _position);
    }

    private void SkipWhiteSpace()
    {
        while (_position < _source.Length && IsWhiteSpace(_source[_position]))
        {
            _position++;
        }
    }

    private byte Peek(int distance)
    {
        var index = _position + distance;
        return index < _source.Length ? _source[index] : (byte)0;
    }

    private PdfToken Create(PdfTokenKind kind, int start, int end)
    {
        var bytes = _source[start..end].ToArray();
        return new PdfToken(kind, bytes, start);
    }

    private static bool IsNumberStart(byte value) => value is (byte)'+' or (byte)'-' or (byte)'.' || char.IsDigit((char)value);

    private static bool IsNumberBody(byte value) => value is (byte)'+' or (byte)'-' or (byte)'.' || char.IsDigit((char)value);

    private static bool IsWhiteSpace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private static bool IsDelimiter(byte value) => value is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';
}
