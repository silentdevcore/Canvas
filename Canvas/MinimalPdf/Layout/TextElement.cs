namespace Canvas.MinimalPdf.Layout;

internal sealed class TextElement : IPageElement
{
    public TextElement(string text, double x, double y, double fontSize)
    {
        Text = text;
        X = x;
        Y = y;
        FontSize = fontSize;
    }

    public string Text { get; }

    public double X { get; }

    public double Y { get; }

    public double FontSize { get; }
}
