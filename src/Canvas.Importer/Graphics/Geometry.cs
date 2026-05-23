namespace Canvas.Importer.Graphics;

public readonly record struct PdfMatrix(double A, double B, double C, double D, double E, double F)
{
    public static PdfMatrix Identity { get; } = new(1, 0, 0, 1, 0, 0);

    public PdfMatrix Multiply(PdfMatrix other)
    {
        return new PdfMatrix(
            A * other.A + B * other.C,
            A * other.B + B * other.D,
            C * other.A + D * other.C,
            C * other.B + D * other.D,
            E * other.A + F * other.C + other.E,
            E * other.B + F * other.D + other.F);
    }
}

public readonly record struct PdfPoint(double X, double Y);

public readonly record struct PdfRectangle(double X, double Y, double Width, double Height);

public readonly record struct PdfColor(double C1, double C2, double C3, double C4, PdfColorSpace ColorSpace)
{
    public static PdfColor Black { get; } = new(0, 0, 0, 1, PdfColorSpace.DeviceGray);
}

public enum PdfColorSpace
{
    DeviceGray,
    DeviceRgb,
    DeviceCmyk,
    Pattern,
    Indexed,
    IccBased,
    Separation,
    DeviceN
}
