using System.Globalization;
using System.Text;
using Canvas.MinimalPdf.Layout;

namespace Canvas.MinimalPdf.Rendering;

internal static class PdfPageRenderer
{
    public static string Render(DrawingContext drawingContext)
    {
        var sb = new StringBuilder();

        foreach (var element in drawingContext.Elements)
        {
            if (element is TextElement text)
            {
                // Each text operation is emitted as a separate text object for simplicity.
                sb.Append("BT\n");
                sb.Append("/F1 ").Append(Format(text.FontSize)).Append(" Tf\n");
                sb.Append(Format(text.X)).Append(' ').Append(Format(text.Y)).Append(" Td\n");
                sb.Append('(').Append(EscapeText(text.Text)).Append(") Tj\n");
                sb.Append("ET\n");
            }
        }

        return sb.ToString();
    }

    private static string EscapeText(string text)
    {
        var sb = new StringBuilder(text.Length);

        foreach (var ch in text)
        {
            if (ch == '(' || ch == ')' || ch == '\\')
            {
                sb.Append('\\');
                sb.Append(ch);
                continue;
            }

            sb.Append(ch <= 127 ? ch : '?');
        }

        return sb.ToString();
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
