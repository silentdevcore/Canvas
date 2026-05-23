using System.Globalization;

namespace Canvas.Pdf;

public readonly struct PdfCmykColor : IPdfColor
{
    public PdfCmykColor(double cyan, double magenta, double yellow, double black)
    {
        ValidateComponent(cyan, nameof(cyan));
        ValidateComponent(magenta, nameof(magenta));
        ValidateComponent(yellow, nameof(yellow));
        ValidateComponent(black, nameof(black));

        Cyan = cyan;
        Magenta = magenta;
        Yellow = yellow;
        Black = black;
    }

    public double Cyan { get; }

    public double Magenta { get; }

    public double Yellow { get; }

    public double Black { get; }

    public string ToFillColorOperator()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} k", Format(Cyan), Format(Magenta), Format(Yellow), Format(Black));
    }

    public string ToStrokeColorOperator()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} K", Format(Cyan), Format(Magenta), Format(Yellow), Format(Black));
    }

    private static void ValidateComponent(double value, string name)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(name, "CMYK components must be in the range [0, 1].");
        }
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
