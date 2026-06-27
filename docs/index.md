# Canvas.Pdf API Reference

`Canvas.Pdf` is the imperative C# API for generating PDFs in code. Create a
[`PdfDocument`](xref:Canvas.Pdf.PdfDocument), add [`PdfPage`](xref:Canvas.Pdf.PdfPage)s, draw on them, and
call `ToBytes()`.

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
document.Info.Title = "Hello";

var page = document.AddPage();                 // A4 by default
page.DrawText("Hello world", x: 40, y: 800, fontSize: 18);
page.DrawRectangle(x: 40, y: 760, width: 200, height: 24, lineWidth: 1);

byte[] pdf = document.ToBytes();
File.WriteAllBytes("hello.pdf", pdf);
```

- **[C# Cookbook](csharp-cookbook.md)** — task-oriented recipes (text, shapes, images, tables, links,
  forms, watermark, TOC, encryption, flow).
- **[API Reference](api/)** — the full generated reference for every public type and member.

> The visual designer and its declarative `DesignExportDto` element model are documented separately in the
> in-app docs (the **Elements Reference** at `/docs` in the designer app).
