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

    public double ScaleX => Math.Sqrt(A * A + B * B);

    public double ScaleY => Math.Sqrt(C * C + D * D);

    public double RotationRadians => Math.Atan2(B, A);

    public double RotationDegrees => RotationRadians * 180d / Math.PI;
}

public readonly record struct PdfPoint(double X, double Y);

public readonly record struct PdfVector(double X, double Y)
{
    public double Length => Math.Sqrt(X * X + Y * Y);

    public PdfVector Normalize()
    {
        var length = Length;
        return length <= double.Epsilon ? new PdfVector(0, 0) : new PdfVector(X / length, Y / length);
    }
}

public readonly record struct PdfRectangle(double X, double Y, double Width, double Height)
{
    public double Left => Math.Min(X, X + Width);

    public double Right => Math.Max(X, X + Width);

    public double Bottom => Math.Min(Y, Y + Height);

    public double Top => Math.Max(Y, Y + Height);

    public double CenterX => (Left + Right) / 2d;

    public double CenterY => (Bottom + Top) / 2d;

    public bool IsEmpty => Width == 0 && Height == 0;

    public bool Intersects(PdfRectangle other)
    {
        return Left <= other.Right && Right >= other.Left && Bottom <= other.Top && Top >= other.Bottom;
    }

    public PdfRectangle Inflate(double dx, double dy)
    {
        return new PdfRectangle(Left - dx, Bottom - dy, (Right - Left) + dx * 2d, (Top - Bottom) + dy * 2d);
    }

    public PdfRectangle Union(PdfRectangle other)
    {
        var left = Math.Min(Left, other.Left);
        var bottom = Math.Min(Bottom, other.Bottom);
        var right = Math.Max(Right, other.Right);
        var top = Math.Max(Top, other.Top);
        return new PdfRectangle(left, bottom, right - left, top - bottom);
    }

    public PdfRectangle? Intersect(PdfRectangle other)
    {
        var left = Math.Max(Left, other.Left);
        var bottom = Math.Max(Bottom, other.Bottom);
        var right = Math.Min(Right, other.Right);
        var top = Math.Min(Top, other.Top);
        return right < left || top < bottom ? null : new PdfRectangle(left, bottom, right - left, top - bottom);
    }
}

public readonly record struct TextGeometry(PdfRectangle Bounds, double RotationDegrees, PdfVector Baseline);

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
