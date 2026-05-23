namespace Canvas.Pdf.Compatibility;

internal static class CorePrimitiveAdapters
{
    public static Canvas.Core.Primitives.PdfPoint ToCore(this PdfPoint value)
    {
        return new Canvas.Core.Primitives.PdfPoint(value.X, value.Y);
    }

    public static PdfPoint ToPdf(this Canvas.Core.Primitives.PdfPoint value)
    {
        return new PdfPoint(value.X, value.Y);
    }

    public static Canvas.Core.Primitives.PdfTextAlignment ToCore(this PdfTextAlignment value)
    {
        return value switch
        {
            PdfTextAlignment.Left => Canvas.Core.Primitives.PdfTextAlignment.Left,
            PdfTextAlignment.Center => Canvas.Core.Primitives.PdfTextAlignment.Center,
            PdfTextAlignment.Right => Canvas.Core.Primitives.PdfTextAlignment.Right,
            PdfTextAlignment.Justify => Canvas.Core.Primitives.PdfTextAlignment.Justify,
            _ => Canvas.Core.Primitives.PdfTextAlignment.Left
        };
    }

    public static PdfTextAlignment ToPdf(this Canvas.Core.Primitives.PdfTextAlignment value)
    {
        return value switch
        {
            Canvas.Core.Primitives.PdfTextAlignment.Left => PdfTextAlignment.Left,
            Canvas.Core.Primitives.PdfTextAlignment.Center => PdfTextAlignment.Center,
            Canvas.Core.Primitives.PdfTextAlignment.Right => PdfTextAlignment.Right,
            Canvas.Core.Primitives.PdfTextAlignment.Justify => PdfTextAlignment.Justify,
            _ => PdfTextAlignment.Left
        };
    }

    public static Canvas.Core.Primitives.PdfVerticalAlignment ToCore(this PdfVerticalAlignment value)
    {
        return value switch
        {
            PdfVerticalAlignment.Top => Canvas.Core.Primitives.PdfVerticalAlignment.Top,
            PdfVerticalAlignment.Middle => Canvas.Core.Primitives.PdfVerticalAlignment.Middle,
            PdfVerticalAlignment.Bottom => Canvas.Core.Primitives.PdfVerticalAlignment.Bottom,
            _ => Canvas.Core.Primitives.PdfVerticalAlignment.Top
        };
    }

    public static PdfVerticalAlignment ToPdf(this Canvas.Core.Primitives.PdfVerticalAlignment value)
    {
        return value switch
        {
            Canvas.Core.Primitives.PdfVerticalAlignment.Top => PdfVerticalAlignment.Top,
            Canvas.Core.Primitives.PdfVerticalAlignment.Middle => PdfVerticalAlignment.Middle,
            Canvas.Core.Primitives.PdfVerticalAlignment.Bottom => PdfVerticalAlignment.Bottom,
            _ => PdfVerticalAlignment.Top
        };
    }
}
