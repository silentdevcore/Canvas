using System.Globalization;
using System.Text;
using Canvas.MinimalPdf.Rendering;

namespace Canvas.MinimalPdf.Writer;

internal static class PdfWriter
{
    public static byte[] Write(PdfDocument document)
    {
        var objects = BuildObjects(document.Pages);

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.4\n");

        var offsets = new List<long>(objects.Count);
        foreach (var obj in objects)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{obj.Number} 0 obj\n");
            WriteAscii(stream, obj.Body);
            WriteAscii(stream, "\nendobj\n");
        }

        var xrefStart = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");

        foreach (var offset in offsets)
        {
            WriteAscii(stream, $"{offset:D10} 00000 n \n");
        }

        WriteAscii(stream, "trailer\n");
        WriteAscii(stream, $"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        WriteAscii(stream, "startxref\n");
        WriteAscii(stream, xrefStart.ToString(CultureInfo.InvariantCulture));
        WriteAscii(stream, "\n%%EOF\n");

        return stream.ToArray();
    }

    private static List<PdfIndirectObject> BuildObjects(IReadOnlyList<PdfPage> pages)
    {
        var objects = new List<PdfIndirectObject>();

        const int catalogObjectNumber = 1;
        const int pagesObjectNumber = 2;
        const int fontObjectNumber = 3;

        var pageObjectNumbers = new List<int>();
        var contentObjectNumbers = new List<int>();

        var nextObjectNumber = 4;
        foreach (var _ in pages)
        {
            pageObjectNumbers.Add(nextObjectNumber++);
            contentObjectNumbers.Add(nextObjectNumber++);
        }

        objects.Add(new PdfIndirectObject(catalogObjectNumber, "<< /Type /Catalog /Pages 2 0 R >>"));

        var kids = string.Join(' ', pageObjectNumbers.Select(number => $"{number} 0 R"));
        objects.Add(new PdfIndirectObject(pagesObjectNumber, $"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>"));

        objects.Add(new PdfIndirectObject(fontObjectNumber, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            var pageObjectNumber = pageObjectNumbers[i];
            var contentObjectNumber = contentObjectNumbers[i];

            var pageBody = string.Create(CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent {pagesObjectNumber} 0 R /MediaBox [0 0 {page.Width:0.###} {page.Height:0.###}] /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>");

            objects.Add(new PdfIndirectObject(pageObjectNumber, pageBody));

            var pageStream = PdfPageRenderer.Render(page.DrawingContext);
            var length = Encoding.ASCII.GetByteCount(pageStream);
            var contentBody = $"<< /Length {length} >>\nstream\n{pageStream}endstream";

            objects.Add(new PdfIndirectObject(contentObjectNumber, contentBody));
        }

        return objects.OrderBy(o => o.Number).ToList();
    }

    private static void WriteAscii(Stream stream, string text)
    {
        var buffer = Encoding.ASCII.GetBytes(text);
        stream.Write(buffer, 0, buffer.Length);
    }
}
