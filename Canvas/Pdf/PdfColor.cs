using System.Globalization;

namespace Canvas.Pdf;

public readonly struct PdfColor : IPdfColor
{
    public PdfColor(double red, double green, double blue)
    {
        ValidateComponent(red, nameof(red));
        ValidateComponent(green, nameof(green));
        ValidateComponent(blue, nameof(blue));

        Red = red;
        Green = green;
        Blue = blue;
    }

    public double Red { get; }

    public double Green { get; }

    public double Blue { get; }

    public static PdfColor Black { get; } = new(0, 0, 0);

    public static PdfColor White { get; } = new(1, 1, 1);

    public static PdfColor Gray { get; } = new(0.5, 0.5, 0.5);

    public static PdfColor RedColor { get; } = new(1, 0, 0);

    public static PdfColor GreenColor { get; } = new(0, 1, 0);

    public static PdfColor BlueColor { get; } = new(0, 0, 1);

    public static PdfColor FromRgb(int red, int green, int blue)
    {
        ValidateRgbByte(red, nameof(red));
        ValidateRgbByte(green, nameof(green));
        ValidateRgbByte(blue, nameof(blue));

        return new PdfColor(red / 255d, green / 255d, blue / 255d);
    }

    public string ToFillColorOperator()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} rg", Format(Red), Format(Green), Format(Blue));
    }

    public string ToStrokeColorOperator()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} RG", Format(Red), Format(Green), Format(Blue));
    }

    private static void ValidateComponent(double value, string name)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(name, "Color components must be in the range [0, 1].");
        }
    }

    private static void ValidateRgbByte(int value, string name)
    {
        if (value is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(name, "RGB byte components must be in the range [0, 255].");
        }
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
