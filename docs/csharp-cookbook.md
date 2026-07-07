# PXA PDF C# Cookbook

Task-oriented recipes for the imperative PXA-compatible PDF API. Every snippet is self-contained — add
`using Canvas.Pdf;` plus `using PXA.Generator;`, create a `PdfDocument` with `Pdf.CreateDocument()`, and
call `ToBytes()` to get the PDF bytes.

> **Coordinates** are in points (1/72"). The origin is the **bottom-left** of the page; `x` increases to
> the right and `y` increases upward. Many `Draw*` methods have a `*FromTop` variant that measures `y`
> from the top instead.

## Getting started

```csharp
using Canvas.Pdf;
using PXA.Generator;

var document = Pdf.CreateDocument();
document.Info.Title = "Hello";
document.Info.Author = "Power Dox Automation";

var page = document.AddPage();                 // A4 portrait by default
page.DrawText("Hello world", x: 40, y: 800, fontSize: 18);

byte[] pdf = document.ToBytes();
File.WriteAllBytes("hello.pdf", pdf);
```

Page sizes and orientation:

```csharp
document.AddPage();                                 // A4
document.AddPage(PdfPagePreset.Letter);             // Letter
document.AddPage(PdfPagePreset.A4, landscape: true);// A4 landscape
document.AddPageRotated(90);                         // rotated view
document.AddPage(width: 400, height: 600);          // custom size (points)
```

## Text

```csharp
// Simple
page.DrawText("Plain text", x: 40, y: 780, fontSize: 12);

// Styled
page.DrawText("Heading", x: 40, y: 750, new PdfDrawTextOptions {
    FontSize = 20,
    FontFamily = PdfFontFamily.Helvetica,
    Bold = true,
    FillColor = new PdfColor(0.1, 0.2, 0.5),
    Underline = true,
});
```

## Paragraphs (wrapping)

`DrawParagraph` wraps to `maxWidth` and returns the measured layout so you can flow content downward.

```csharp
var layout = page.DrawParagraph(
    "A longer block of text that wraps within the given width and reports how tall it ended up.",
    x: 40, y: 720, maxWidth: 380,
    new PdfParagraphOptions { FontSize = 12, Alignment = PdfTextAlignment.Justify });

double nextY = 720 - layout.Height - 12;  // continue below the paragraph
```

## Shapes & lines

```csharp
page.DrawLine(x1: 40, y1: 700, x2: 340, y2: 700, lineWidth: 1);

page.DrawRectangle(x: 40, y: 640, width: 200, height: 48,
    lineWidth: 1.5, fill: true,
    strokeColor: new PdfColor(0.1, 0.2, 0.5),
    fillColor: new PdfColor(0.93, 0.95, 1));

page.DrawCircle(centerX: 300, centerY: 660, radius: 24,
    fill: true, fillColor: new PdfCmykColor(0.2, 0, 0, 0));

page.DrawPolygon(new[] {
    new PdfPoint(360, 690), new PdfPoint(420, 670),
    new PdfPoint(400, 630), new PdfPoint(350, 640),
}, fill: true, fillColor: new PdfGrayColor(0.9));
```

## Images

```csharp
page.DrawImage("logo.png", x: 40, y: 560, width: 120, height: 60);   // from a file
page.DrawImage(imageBytes, x: 200, y: 560, width: 120, height: 60);  // from bytes
```

## Tables

```csharp
page.DrawSimpleTable(
    x: 40, y: 540, width: 360,
    rows: new[] {
        new[] { "Item",   "Qty", "Amount" },
        new[] { "Coffee", "2",   "€ 7.00" },
        new[] { "Tea",    "5",   "€ 9.00" },
    },
    new PdfTableOptions { /* header styling, borders, column widths */ });
```

## Links & navigation

```csharp
page.DrawText("Visit site", x: 40, y: 500, fontSize: 11);
page.AddWebLink(x: 40, y: 496, width: 80, height: 16, url: "https://example.com");

page.AddPageLink(x: 140, y: 496, width: 80, height: 16, targetPageNumber: 2);

document.AddBookmark("Chapter 1", pageNumber: 1, level: 1);   // outline entry
document.AddNamedDestination("intro", pageNumber: 1);
```

## Form fields

```csharp
page.AddTextField(fieldName: "name", x: 40, y: 460, width: 220, height: 24);
page.AddMultilineTextField(fieldName: "notes", x: 40, y: 400, width: 220, height: 50);
page.AddCheckBox(fieldName: "agree", x: 40, y: 380, size: 16, isChecked: true);
page.AddComboBox(fieldName: "country", x: 40, y: 350, width: 160, height: 22,
    options: new[] { "DE", "FR", "US" }, selectedIndex: 0);
```

## Page numbers, headers & footers

```csharp
document.AddPageNumbers(new PdfPageNumberOptions { /* format, prefix, position */ });
document.AddHeadersAndFooters(new PdfHeaderFooterOptions { /* header/footer text */ });
```

## Watermark

```csharp
document.AddTextWatermark("DRAFT", new PdfWatermarkOptions {
    RotationDegrees = -45,
    Opacity = 0.15,
});
```

## Table of contents

Add bookmarks/sections, then generate the TOC pages:

```csharp
document.AddSection("Introduction", startPageNumber: 1);
document.AddSection("Details", startPageNumber: 3);
var tocPages = document.AddTableOfContents(new PdfTableOfContentsOptions { /* title, leader dots */ });
```

## Encryption

```csharp
var bytes = document.ToBytes(new PdfSaveOptions {
    Encryption = new PdfEncryptionOptions {
        UserPassword = "open-me",
        OwnerPassword = "owner",
        Algorithm = PdfEncryptionAlgorithm.Aes128,
        Permissions = PdfPermissions.Print,
    },
});
```

## Metadata

```csharp
document.Info.Title = "Quarterly report";
document.Info.Author = "Finance";
document.Info.Subject = "Q3";
document.Info.Keywords = "report,q3";
document.Info.CustomProperties["Department"] = "Finance";
```

---

See the [API Reference](api/) for every type, overload, and option. A complete runnable program lives in
`samples/PXA.Demo/Program.cs`.
