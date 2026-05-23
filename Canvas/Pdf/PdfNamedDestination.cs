namespace Canvas.Pdf;

internal sealed class PdfNamedDestination
{
    public PdfNamedDestination(string name, int pageNumber, double? y)
    {
        Name = name;
        PageNumber = pageNumber;
        Y = y;
    }

    public string Name { get; }

    public int PageNumber { get; set; }

    public double? Y { get; set; }
}
