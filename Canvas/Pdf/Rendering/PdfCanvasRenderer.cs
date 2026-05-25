using System.Globalization;
using System.Text;
using Canvas.Pdf.Layout;

namespace Canvas.Pdf.Rendering;

internal static class PdfCanvasRenderer
{
    public static string RenderPage(
        PdfPage page,
        IReadOnlyDictionary<PdfStandardFont, string> fontResourceNames,
        IReadOnlyDictionary<string, string> imageResourceNames,
        IReadOnlyDictionary<double, string>? opacityResourceNames = null,
        IReadOnlyDictionary<PdfEmbeddedFont, string>? embeddedFontResourceNames = null)
    {
        var sb = new StringBuilder();

        foreach (var element in page.Elements)
        {
            sb.Append("q\n");

            switch (element)
            {
                case TextElement textElement:
                    RenderText(sb, textElement, fontResourceNames, embeddedFontResourceNames);
                    break;

                case LineElement lineElement:
                    RenderLine(sb, lineElement);
                    break;

                case RectangleElement rectangleElement:
                    RenderRectangle(sb, rectangleElement);
                    break;

                case RoundedRectangleElement roundedRectangleElement:
                    RenderRoundedRectangle(sb, roundedRectangleElement);
                    break;

                case CircleElement circleElement:
                    RenderCircle(sb, circleElement);
                    break;

                case PolygonElement polygonElement:
                    RenderPolygon(sb, polygonElement);
                    break;

                case BezierCurveElement bezierCurveElement:
                    RenderBezierCurve(sb, bezierCurveElement);
                    break;

                case ImageElement imageElement:
                    RenderImage(sb, imageElement, imageResourceNames, opacityResourceNames);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported page element: {element.GetType().Name}");
            }

            sb.Append("Q\n");
        }

        return sb.ToString();
    }

    private static void RenderText(
        StringBuilder sb,
        TextElement element,
        IReadOnlyDictionary<PdfStandardFont, string> fontResourceNames,
        IReadOnlyDictionary<PdfEmbeddedFont, string>? embeddedFontResourceNames)
    {
        string fontResourceName;
        bool useEmbedded;
        if (element.EmbeddedFont is not null
            && embeddedFontResourceNames is not null
            && embeddedFontResourceNames.TryGetValue(element.EmbeddedFont, out var embeddedName))
        {
            fontResourceName = embeddedName;
            useEmbedded = true;
        }
        else
        {
            fontResourceName = fontResourceNames[element.Font];
            useEmbedded = false;
        }

        sb.Append("BT\n");
        sb.AppendFormat(CultureInfo.InvariantCulture, "/{0} {1} Tf\n", fontResourceName, FormatNumber(element.FontSize));
        sb.Append((element.FillColor ?? PdfColor.Black).ToFillColorOperator());
        sb.Append('\n');
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} Tw\n", FormatNumber(element.WordSpacing));
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} Tc\n", FormatNumber(element.CharacterSpacing));
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} Tz\n", FormatNumber(element.HorizontalScalingPercent));

        if (Math.Abs(element.RotationDegrees) < 0.0001)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} Td\n", FormatNumber(element.X), FormatNumber(element.Y));
        }
        else
        {
            var radians = element.RotationDegrees * (Math.PI / 180.0);
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} {4} {5} Tm\n",
                FormatNumber(cos),
                FormatNumber(sin),
                FormatNumber(-sin),
                FormatNumber(cos),
                FormatNumber(element.X),
                FormatNumber(element.Y));
        }

        if (useEmbedded)
        {
            var displayText = element.TextDirection == "rtl"
                ? ReverseForRtl(element.Text)
                : element.Text;
            sb.Append(EncodeAsHexUtf16Be(displayText));
            sb.Append(" Tj\n");
        }
        else
        {
            sb.Append('(');
            sb.Append(EscapeLiteralString(element.Text));
            sb.Append(") Tj\n");
        }

        double textWidth = useEmbedded && element.EmbeddedFont is not null
            ? element.EmbeddedFont.MeasureWidth(element.Text, element.FontSize)
            : EstimateTextWidth(element.Text, element.FontSize, element.Font);

        var scaledTextWidth = textWidth * (element.HorizontalScalingPercent / 100.0);

        if (element.Underline)
        {
            var underlineY = element.Y - (element.FontSize * 0.12);
            sb.Append("ET\n");
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} w\n", FormatNumber(Math.Max(0.5, element.FontSize * 0.06)));
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatNumber(element.X), FormatNumber(underlineY));
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatNumber(element.X + scaledTextWidth), FormatNumber(underlineY));
            sb.Append("S\nBT\n");
        }

        if (element.Strikethrough)
        {
            var strikeY = element.Y + (element.FontSize * 0.3);
            sb.Append("ET\n");
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} w\n", FormatNumber(Math.Max(0.5, element.FontSize * 0.06)));
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatNumber(element.X), FormatNumber(strikeY));
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatNumber(element.X + scaledTextWidth), FormatNumber(strikeY));
            sb.Append("S\nBT\n");
        }

        sb.Append("ET\n");
    }

    private static void RenderImage(
        StringBuilder sb,
        ImageElement element,
        IReadOnlyDictionary<string, string> imageResourceNames,
        IReadOnlyDictionary<double, string>? opacityResourceNames)
    {
        var imageName = imageResourceNames[element.CacheKey];

        if (element.Opacity < 1 && opacityResourceNames is not null && opacityResourceNames.TryGetValue(element.Opacity, out var gsName))
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "/{0} gs\n", gsName);
        }

        if (element.ClipToBounds)
        {
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} re W n\n",
                FormatNumber(element.ClipX ?? element.X),
                FormatNumber(element.ClipY ?? element.Y),
                FormatNumber(element.ClipWidth ?? element.Width),
                FormatNumber(element.ClipHeight ?? element.Height));
        }

        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0} 0 0 {1} {2} {3} cm\n",
            FormatNumber(element.Width),
            FormatNumber(element.Height),
            FormatNumber(element.X),
            FormatNumber(element.Y));
        sb.Append('/');
        sb.Append(imageName);
        sb.Append(" Do\n");
    }

    private static void RenderLine(StringBuilder sb, LineElement element)
    {
        sb.Append(element.StrokeColor.ToStrokeColorOperator());
        sb.Append('\n');
        AppendStrokeStyle(sb, element.StrokeStyle);
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatNumber(element.X1), FormatNumber(element.Y1));
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatNumber(element.X2), FormatNumber(element.Y2));
        sb.Append("S\n");
    }

    private static void RenderRectangle(StringBuilder sb, RectangleElement element)
    {
        sb.Append(element.StrokeColor.ToStrokeColorOperator());
        sb.Append('\n');

        if (element.Fill)
        {
            sb.Append(element.FillColor.ToFillColorOperator());
            sb.Append('\n');
        }

        AppendStrokeStyle(sb, element.StrokeStyle);
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0} {1} {2} {3} re\n",
            FormatNumber(element.X),
            FormatNumber(element.Y),
            FormatNumber(element.Width),
            FormatNumber(element.Height));

        var paintOperator = element switch
        {
            { Stroke: true, Fill: true } => "B",
            { Stroke: true, Fill: false } => "S",
            { Stroke: false, Fill: true } => "f",
            _ => "n"
        };

        sb.Append(paintOperator);
        sb.Append('\n');
    }

    private static void RenderRoundedRectangle(StringBuilder sb, RoundedRectangleElement element)
    {
        sb.Append(element.StrokeColor.ToStrokeColorOperator());
        sb.Append('\n');

        if (element.Fill)
        {
            sb.Append(element.FillColor.ToFillColorOperator());
            sb.Append('\n');
        }

        AppendStrokeStyle(sb, element.StrokeStyle);

        if (element.CornerRadius == 0)
        {
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} re\n",
                FormatNumber(element.X),
                FormatNumber(element.Y),
                FormatNumber(element.Width),
                FormatNumber(element.Height));
        }
        else
        {
            var kappa = 0.552284749831;
            var r = element.CornerRadius;
            var c = r * kappa;
            var x = element.X;
            var y = element.Y;
            var w = element.Width;
            var h = element.Height;

            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatNumber(x + r), FormatNumber(y));
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatNumber(x + w - r), FormatNumber(y));
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} {4} {5} c\n",
                FormatNumber(x + w - r + c), FormatNumber(y),
                FormatNumber(x + w), FormatNumber(y + r - c),
                FormatNumber(x + w), FormatNumber(y + r));
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatNumber(x + w), FormatNumber(y + h - r));
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} {4} {5} c\n",
                FormatNumber(x + w), FormatNumber(y + h - r + c),
                FormatNumber(x + w - r + c), FormatNumber(y + h),
                FormatNumber(x + w - r), FormatNumber(y + h));
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatNumber(x + r), FormatNumber(y + h));
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} {4} {5} c\n",
                FormatNumber(x + r - c), FormatNumber(y + h),
                FormatNumber(x), FormatNumber(y + h - r + c),
                FormatNumber(x), FormatNumber(y + h - r));
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatNumber(x), FormatNumber(y + r));
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} {4} {5} c\n",
                FormatNumber(x), FormatNumber(y + r - c),
                FormatNumber(x + r - c), FormatNumber(y),
                FormatNumber(x + r), FormatNumber(y));
        }

        AppendPaintOperator(sb, element.Stroke, element.Fill);
    }

    private static void RenderBezierCurve(StringBuilder sb, BezierCurveElement element)
    {
        sb.Append(element.StrokeColor.ToStrokeColorOperator());
        sb.Append('\n');
        AppendStrokeStyle(sb, element.StrokeStyle);
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatNumber(element.Start.X), FormatNumber(element.Start.Y));
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0} {1} {2} {3} {4} {5} c\n",
            FormatNumber(element.Control1.X), FormatNumber(element.Control1.Y),
            FormatNumber(element.Control2.X), FormatNumber(element.Control2.Y),
            FormatNumber(element.End.X), FormatNumber(element.End.Y));
        sb.Append("S\n");
    }

    private static void RenderPolygon(StringBuilder sb, PolygonElement element)
    {
        sb.Append(element.StrokeColor.ToStrokeColorOperator());
        sb.Append('\n');

        if (element.Fill)
        {
            sb.Append(element.FillColor.ToFillColorOperator());
            sb.Append('\n');
        }

        AppendStrokeStyle(sb, element.StrokeStyle);

        var first = element.Points[0];
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatNumber(first.X), FormatNumber(first.Y));

        for (var i = 1; i < element.Points.Count; i++)
        {
            var point = element.Points[i];
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatNumber(point.X), FormatNumber(point.Y));
        }

        sb.Append("h\n");
        AppendPaintOperator(sb, element.Stroke, element.Fill);
    }

    private static void RenderCircle(StringBuilder sb, CircleElement element)
    {
        sb.Append(element.StrokeColor.ToStrokeColorOperator());
        sb.Append('\n');

        if (element.Fill)
        {
            sb.Append(element.FillColor.ToFillColorOperator());
            sb.Append('\n');
        }

        AppendStrokeStyle(sb, element.StrokeStyle);

        var kappa = 0.552284749831;
        var r = element.Radius;
        var c = r * kappa;
        var cx = element.CenterX;
        var cy = element.CenterY;

        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatNumber(cx + r), FormatNumber(cy));
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0} {1} {2} {3} {4} {5} c\n",
            FormatNumber(cx + r), FormatNumber(cy + c),
            FormatNumber(cx + c), FormatNumber(cy + r),
            FormatNumber(cx), FormatNumber(cy + r));
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0} {1} {2} {3} {4} {5} c\n",
            FormatNumber(cx - c), FormatNumber(cy + r),
            FormatNumber(cx - r), FormatNumber(cy + c),
            FormatNumber(cx - r), FormatNumber(cy));
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0} {1} {2} {3} {4} {5} c\n",
            FormatNumber(cx - r), FormatNumber(cy - c),
            FormatNumber(cx - c), FormatNumber(cy - r),
            FormatNumber(cx), FormatNumber(cy - r));
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0} {1} {2} {3} {4} {5} c\n",
            FormatNumber(cx + c), FormatNumber(cy - r),
            FormatNumber(cx + r), FormatNumber(cy - c),
            FormatNumber(cx + r), FormatNumber(cy));

        AppendPaintOperator(sb, element.Stroke, element.Fill);
    }

    private static void AppendStrokeStyle(StringBuilder sb, PdfStrokeStyle strokeStyle)
    {
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} w\n", FormatNumber(strokeStyle.LineWidth));
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} J\n", (int)strokeStyle.LineCap);
        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} j\n", (int)strokeStyle.LineJoin);

        if (strokeStyle.DashArray is { Count: > 0 })
        {
            sb.Append('[');

            for (var i = 0; i < strokeStyle.DashArray.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(FormatNumber(strokeStyle.DashArray[i]));
            }

            sb.Append("] ");
            sb.Append(FormatNumber(strokeStyle.DashPhase));
            sb.Append(" d\n");
        }
    }

    private static void AppendPaintOperator(StringBuilder sb, bool stroke, bool fill)
    {
        var paintOperator = (stroke, fill) switch
        {
            (true, true) => "B",
            (true, false) => "S",
            (false, true) => "f",
            _ => "n"
        };

        sb.Append(paintOperator);
        sb.Append('\n');
    }

    private static string EncodeAsHexUtf16Be(string text) => PdfTextEncoding.EncodeAsHexUtf16Be(text);

    private static string ReverseForRtl(string text) => PdfTextEncoding.ReverseForRtl(text);

    private static string EscapeLiteralString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static double EstimateTextWidth(string text, double fontSize, PdfStandardFont font)
    {
        var avgCharacterWidthFactor = font switch
        {
            PdfStandardFont.Courier or PdfStandardFont.CourierBold or PdfStandardFont.CourierOblique or PdfStandardFont.CourierBoldOblique => 0.6,
            PdfStandardFont.TimesRoman or PdfStandardFont.TimesBold or PdfStandardFont.TimesItalic or PdfStandardFont.TimesBoldItalic => 0.5,
            _ => 0.52
        };

        return text.Length * fontSize * avgCharacterWidthFactor;
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
