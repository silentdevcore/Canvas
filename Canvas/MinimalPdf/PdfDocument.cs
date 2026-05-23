using Canvas.MinimalPdf.Writer;

namespace Canvas.MinimalPdf;

public sealed class PdfDocument
{
    private readonly List<PdfPage> _pages = new();

    public IReadOnlyList<PdfPage> Pages => _pages;

    public PdfPage AddPage(double width = PdfPageSize.A4Width, double height = PdfPageSize.A4Height)
    {
        var page = new PdfPage(width, height);
        _pages.Add(page);
        return page;
    }

    public void Save(string path)
    {
        if (_pages.Count == 0)
        {
            AddPage();
        }

        var bytes = PdfWriter.Write(this);
        File.WriteAllBytes(path, bytes);
    }
}
