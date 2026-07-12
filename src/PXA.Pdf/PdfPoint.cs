namespace PXA.Pdf;

public readonly struct PdfPoint
{
    public PdfPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }
}
