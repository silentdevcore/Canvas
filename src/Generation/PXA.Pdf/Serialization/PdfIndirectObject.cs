namespace PXA.Pdf.Serialization;

internal sealed class PdfIndirectObject
{
    public PdfIndirectObject(int id, string content)
        : this(id, System.Text.Encoding.ASCII.GetBytes(content))
    {
    }

    public PdfIndirectObject(int id, byte[] contentBytes)
    {
        Id = id;
        ContentBytes = contentBytes;
    }

    public int Id { get; }

    public byte[] ContentBytes { get; }
}
