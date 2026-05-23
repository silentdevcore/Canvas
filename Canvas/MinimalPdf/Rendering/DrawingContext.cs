using Canvas.MinimalPdf.Layout;

namespace Canvas.MinimalPdf.Rendering;

internal sealed class DrawingContext
{
    private readonly List<IPageElement> _elements = new();

    public IReadOnlyList<IPageElement> Elements => _elements;

    public void DrawText(string text, double x, double y, double fontSize)
    {
        _elements.Add(new TextElement(text, x, y, fontSize));
    }
}
