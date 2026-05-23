namespace Canvas.Pdf;

internal sealed class PdfBookmark
{
    public PdfBookmark(string title, int pageNumber, int level = 1)
    {
        Title = title;
        PageNumber = pageNumber;
        Level = level;
    }

    public string Title { get; }

    public int PageNumber { get; set; }

    public int Level { get; set; }
}
