using Canvas.Importer.Tokenizer;

namespace Canvas.Importer.Parsing;

internal ref struct PdfTokenReader
{
    private PdfTokenizer _tokenizer;
    private PdfToken _lookahead;
    private bool _hasLookahead;

    public PdfTokenReader(ReadOnlySpan<byte> source)
    {
        _tokenizer = new PdfTokenizer(source);
        _lookahead = default;
        _hasLookahead = false;
    }

    public int Position => _hasLookahead ? (int)_lookahead.Offset : _tokenizer.Position;

    public PdfToken Read()
    {
        if (!_hasLookahead)
        {
            return _tokenizer.ReadToken();
        }

        var token = _lookahead;
        _lookahead = default;
        _hasLookahead = false;
        return token;
    }

    public PdfToken Peek()
    {
        if (_hasLookahead)
        {
            return _lookahead;
        }

        _lookahead = _tokenizer.ReadToken();
        _hasLookahead = true;
        return _lookahead;
    }

    public void Seek(int position)
    {
        _tokenizer.Seek(position);
        _lookahead = default;
        _hasLookahead = false;
    }
}
