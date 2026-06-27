using System.Globalization;

namespace Canvas.Pdf;

/// <summary>A grayscale color with a single 0–1 component (0 = black, 1 = white).</summary>
public readonly struct PdfGrayColor : IPdfColor
{
    public PdfGrayColor(double gray)
    {
        ValidateComponent(gray, nameof(gray));
        Gray = gray;
    }

    public double Gray { get; }

    public string ToFillColorOperator()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} g", Format(Gray));
    }

    public string ToStrokeColorOperator()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0} G", Format(Gray));
    }

    private static void ValidateComponent(double value, string name)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(name, "Gray component must be in the range [0, 1].");
        }
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
