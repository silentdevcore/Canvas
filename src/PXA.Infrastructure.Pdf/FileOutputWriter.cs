namespace PXA.Infrastructure.Pdf;

/// <summary>
/// Power Dox Automation facade for writing generated PDF bytes to disk.
/// </summary>
public sealed class FileOutputWriter
{
    private readonly Canvas.Infrastructure.Pdf.FileOutputWriter inner = new();

    public void Write(string path, byte[] data) => inner.Write(path, data);
}
