namespace Canvas.MinimalPdf.Writer;

internal sealed class PdfIndirectObject
{
    public PdfIndirectObject(int number, string body)
    {
        Number = number;
        Body = body;
    }

    public int Number { get; }

    public string Body { get; }
}
