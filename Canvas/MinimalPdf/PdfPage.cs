using Canvas.MinimalPdf.Rendering;

namespace Canvas.MinimalPdf;

public sealed class PdfPage
{
    private readonly DrawingContext _drawingContext = new();

    internal PdfPage(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }

    internal DrawingContext DrawingContext => _drawingContext;

    public void DrawText(string text, double x, double y, double fontSize = 14)
    {
        _drawingContext.DrawText(text, x, y, fontSize);
    }
}
