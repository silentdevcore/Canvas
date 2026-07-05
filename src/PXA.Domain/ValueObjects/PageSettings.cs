namespace PXA.Domain.ValueObjects;

public class PageSettings
{
    public double Width { get; set; } = 595.276;
    public double Height { get; set; } = 841.89;
    public string Orientation { get; set; } = "portrait";
    public Margins Margins { get; set; } = new();
    public string? BackgroundColor { get; set; }
}

public class Margins
{
    public double Top { get; set; } = 72;
    public double Right { get; set; } = 72;
    public double Bottom { get; set; } = 72;
    public double Left { get; set; } = 72;
}
