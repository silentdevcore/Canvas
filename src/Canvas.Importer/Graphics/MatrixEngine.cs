namespace Canvas.Importer.Graphics;

public static class MatrixEngine
{
    public static PdfMatrix Translate(double x, double y) => new(1, 0, 0, 1, x, y);

    public static PdfMatrix Scale(double x, double y) => new(x, 0, 0, y, 0, 0);

    public static PdfMatrix Rotate(double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new PdfMatrix(cos, sin, -sin, cos, 0, 0);
    }

    public static PdfMatrix Skew(double xDegrees, double yDegrees)
    {
        return new PdfMatrix(
            1,
            Math.Tan(yDegrees * Math.PI / 180d),
            Math.Tan(xDegrees * Math.PI / 180d),
            1,
            0,
            0);
    }

    public static PdfMatrix Multiply(PdfMatrix left, PdfMatrix right) => left.Multiply(right);

    public static PdfPoint TransformPoint(PdfPoint point, PdfMatrix matrix)
    {
        return new PdfPoint(
            point.X * matrix.A + point.Y * matrix.C + matrix.E,
            point.X * matrix.B + point.Y * matrix.D + matrix.F);
    }

    public static PdfVector TransformVector(PdfVector vector, PdfMatrix matrix)
    {
        return new PdfVector(
            vector.X * matrix.A + vector.Y * matrix.C,
            vector.X * matrix.B + vector.Y * matrix.D);
    }

    public static PdfRectangle TransformBounds(PdfRectangle bounds, PdfMatrix matrix)
    {
        var p1 = TransformPoint(new PdfPoint(bounds.Left, bounds.Bottom), matrix);
        var p2 = TransformPoint(new PdfPoint(bounds.Right, bounds.Bottom), matrix);
        var p3 = TransformPoint(new PdfPoint(bounds.Right, bounds.Top), matrix);
        var p4 = TransformPoint(new PdfPoint(bounds.Left, bounds.Top), matrix);

        var left = Math.Min(Math.Min(p1.X, p2.X), Math.Min(p3.X, p4.X));
        var right = Math.Max(Math.Max(p1.X, p2.X), Math.Max(p3.X, p4.X));
        var bottom = Math.Min(Math.Min(p1.Y, p2.Y), Math.Min(p3.Y, p4.Y));
        var top = Math.Max(Math.Max(p1.Y, p2.Y), Math.Max(p3.Y, p4.Y));
        return new PdfRectangle(left, bottom, right - left, top - bottom);
    }

    public static PdfMatrix Invert(PdfMatrix matrix)
    {
        var determinant = matrix.A * matrix.D - matrix.B * matrix.C;
        if (Math.Abs(determinant) <= double.Epsilon)
        {
            return PdfMatrix.Identity;
        }

        var inverse = 1d / determinant;
        return new PdfMatrix(
            matrix.D * inverse,
            -matrix.B * inverse,
            -matrix.C * inverse,
            matrix.A * inverse,
            (matrix.C * matrix.F - matrix.D * matrix.E) * inverse,
            (matrix.B * matrix.E - matrix.A * matrix.F) * inverse);
    }

    public static PdfPoint WorldToLocal(PdfPoint point, PdfMatrix worldMatrix) => TransformPoint(point, Invert(worldMatrix));

    public static double ExtractRotationDegrees(PdfMatrix matrix) => NormalizeDegrees(matrix.RotationDegrees);

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }
}
