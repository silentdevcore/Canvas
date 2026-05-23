namespace Canvas.Domain.ValueObjects;

public class PageSettings
{
    public double Width { get; set; } = 595.276; // A4 width in points (210mm)
    public double Height { get; set; } = 841.89; // A4 height in points (297mm)
    public string Orientation { get; set; } = "portrait";
    public Margins Margins { get; set; } = new();
    public string? BackgroundColor { get; set; }
}

public class Margins
{
    public double Top { get; set; } = 72; // 1 inch in points
    public double Right { get; set; } = 72;
    public double Bottom { get; set; } = 72;
    public double Left { get; set; } = 72;
}