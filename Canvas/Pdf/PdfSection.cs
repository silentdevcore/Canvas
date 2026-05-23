namespace Canvas.Pdf;

public sealed class PdfSection
{
    public PdfSection(string name, int startPageNumber)
    {
        Name = name;
        StartPageNumber = startPageNumber;
    }

    public string Name { get; }

    public int StartPageNumber { get; }
}
