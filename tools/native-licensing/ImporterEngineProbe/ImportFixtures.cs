using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using P = DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

internal static class ImportFixtures
{
    internal static Dictionary<string, byte[]> Create() => new()
    {
        ["pdf"] = Pdf(), ["doc"] = Doc(), ["docx"] = Docx(), ["pptx"] = Pptx(),
        ["md"] = Encoding.UTF8.GetBytes("# Imported content\n\n![asset](https://example.com/synthetic.png)"),
        ["svg"] = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><text x='10' y='40'>Imported content</text></svg>"),
        ["image"] = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="),
        ["odt"] = Odt(),
    };

    private static byte[] Pdf()
    {
        const string content = "BT /F1 12 Tf 30 140 Td (Imported content) Tj ET\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 180] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream",
        };
        using var stream = new MemoryStream();
        void Write(string text) => stream.Write(Encoding.ASCII.GetBytes(text));
        Write("%PDF-1.7\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(stream.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = stream.Position;
        Write("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    // Bounded synthetic CFB/FIB text-fallback fixture, not a Word interoperability corpus.
    private static byte[] Doc()
    {
        var bytes = new byte[2048];
        Convert.FromHexString("D0CF11E0A1B11AE1").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(30), 9);
        Write(44, 1); Write(48, 1);
        for (var i = 0; i < 109; i++) Write(76 + i * 4, -1);
        Write(76, 0);
        for (var i = 0; i < 128; i++) Write(512 + i * 4, -1);
        Write(512, -3); Write(516, -2); Write(520, -2);
        var name = Encoding.Unicode.GetBytes("WordDocument\0");
        name.CopyTo(bytes, 1024);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1024 + 64), (ushort)name.Length);
        Write(1024 + 116, 2); Write(1024 + 120, 512);
        Encoding.ASCII.GetBytes("Imported content").CopyTo(bytes, 1536 + 128);
        return bytes;
        void Write(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    }

    private static byte[] Docx()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            document.AddMainDocumentPart().Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("Imported content")))));
        return stream.ToArray();
    }

    private static byte[] Odt()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(zip.CreateEntry("content.xml").Open(), Encoding.UTF8))
            writer.Write("<office:document-content xmlns:office='urn:oasis:names:tc:opendocument:xmlns:office:1.0' xmlns:text='urn:oasis:names:tc:opendocument:xmlns:text:1.0'><office:body><office:text><text:p>Imported content</text:p></office:text></office:body></office:document-content>");
        return stream.ToArray();
    }

    private static byte[] Pptx()
    {
        using var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, DocumentFormat.OpenXml.PresentationDocumentType.Presentation))
        {
            var part = document.AddPresentationPart();
            part.Presentation = new P.Presentation(new P.SlideIdList(), new P.SlideSize { Cx = 9144000, Cy = 6858000 });
            var slide = part.AddNewPart<SlidePart>("rId1");
            slide.Slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree(new P.Shape(
                new P.NonVisualShapeProperties(new P.NonVisualDrawingProperties { Id = 2, Name = "Text" }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = 914400, Y = 914400 }, new A.Extents { Cx = 3657600, Cy = 914400 })),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text("Imported content"))))))));
            part.Presentation.SlideIdList!.Append(new P.SlideId { Id = 256, RelationshipId = "rId1" });
        }
        return stream.ToArray();
    }
}
