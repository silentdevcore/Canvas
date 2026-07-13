# PXA Migration: Spire.PDF

## V1 Pilot Analysis

- [x] Spire.PDF is a good fit for simple generation flows: document, page, canvas text, shapes, and save.
- [x] `PdfDocument` collides with PXA.Pdf, so migration removes Spire usings and introduces `using PXA.Pdf`.
- [x] Direct `page.Canvas.DrawString(...)`, `DrawLine(...)`, and `DrawRectangle(...)` are deterministic enough for v1.
- [x] Images, tables, forms, security, attachments, extraction/conversion, and existing-PDF editing remain manual in v1.

## Package / API Identification

- [x] NuGet packages:
  - [x] `Spire.PDF`
  - [x] `FreeSpire.PDF`
- [x] Common namespaces to detect:
  - [x] `Spire.Pdf`
  - [x] `Spire.Pdf.Graphics`
  - [x] `Spire.Pdf.Tables`
  - [x] `Spire.Pdf.Widget`
- [x] Common classes to detect:
  - [x] `PdfDocument`
  - [x] `PdfPageBase`
  - [x] `PdfFont`
  - [x] `PdfTrueTypeFont`
  - [x] `PdfBrush`
  - [x] `PdfTable`
  - [x] `PdfFormWidget`
  - [x] `PdfSecurity`

## Roslyn Prototype Status

- [x] Add `src/PXA.Migration.Pdf.Code.Spire`
- [x] Add `tests/PXA.Migration.Pdf.Code.Spire.Tests`
- [x] Add WebApi converter integration
- [x] Add API service smoke test
- [x] Rewrite deterministic document/page/text/line/rectangle/save patterns
- [x] Warn on unsupported/manual Spire feature areas

## Mapping Table

| Spire.PDF API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PdfDocument()` | `new PXA.Pdf.PdfDocument()` | Code fix in v1 | Removes Spire usings to resolve type conflict. |
| `document.Pages.Add(...)` | `document.AddPage()` | Code fix in v1 | Page size overloads need future mapping. |
| `page.Canvas.DrawString(text, font, brush, x, y)` | `page.DrawTextFromTop(text, x, y, fontSize)` | Code fix in v1 | Extracts simple literal text and simple font size. |
| `page.Canvas.DrawString(text, font, brush, new PointF(x, y))` | `page.DrawTextFromTop(text, x, y, fontSize)` | Code fix in v1 | Direct `PointF` only. |
| `page.Canvas.DrawLine(pen, x1, y1, x2, y2)` | `page.DrawLineFromTop(x1, y1, x2, y2)` | Code fix in v1 | Pen style needs future mapping. |
| `page.Canvas.DrawRectangle(pen, x, y, w, h)` | `page.DrawRectangleFromTop(x, y, w, h)` | Code fix in v1 | Stroke/fill style needs future mapping. |
| `page.Canvas.DrawImage(...)` | `page.DrawImage(...)` | Warning in v1 | Image resource and scaling need manual review. |
| `document.SaveToFile(path)` | `document.Save(path)` | Code fix in v1 | Output format overloads need review. |

## Unsupported / Manual Follow-Up

- [x] PDF conversion features
- [x] Existing PDF manipulation
- [x] Forms
- [x] Security/encryption
- [x] Digital signatures/certificates
- [x] Attachments
- [x] Complex tables
- [x] Text extraction
- [x] Annotations

## Analyzer Diagnostics

| Diagnostic | Severity | Meaning |
| --- | --- | --- |
| `CANMIGSPIRE001` | Info | `PdfDocument` construction converted. |
| `CANMIGSPIRE002` | Info | `document.Pages.Add()` converted. |
| `CANMIGSPIRE003` | Info/Warning | Simple `DrawString` converted, complex text warned. |
| `CANMIGSPIRE005` | Warning | Image drawing needs manual migration. |
| `CANMIGSPIRE006` | Info | Line/rectangle drawing converted. |
| `CANMIGSPIRE007` | Info | `SaveToFile` converted. |
| `CANMIGSPIRE020` | Warning | Tables, forms, security, attachments, annotations, or extraction need manual migration. |
| `CANMIGSPIRE021` | Warning | Existing-PDF editing/loading/merge/split/conversion needs manual migration. |

## Sample Input Snippets

```csharp
using Spire.Pdf;
using Spire.Pdf.Graphics;

var document = new PdfDocument();
var page = document.Pages.Add();
page.Canvas.DrawString("Hello", new PdfFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
page.Canvas.DrawLine(pen, 40, 700, 555, 700);
page.Canvas.DrawRectangle(pen, 40, 620, 200, 80);
document.SaveToFile(path);
```

## Expected PXA.Pdf Output Snippets

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawTextFromTop("Hello", 40, 40, 12);
page.DrawLineFromTop(40, 700, 555, 700);
page.DrawRectangleFromTop(40, 620, 200, 80);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [x] Detect Spire document/page creation
- [x] Detect canvas draw calls
- [x] Detect simple font size usage
- [x] Warn on image drawing APIs
- [x] Warn on conversion/editing APIs
- [x] Warn on forms/security/table APIs

## Code Fix Checklist

- [x] Replace document creation
- [x] Replace page creation
- [x] Replace simple draw string calls
- [x] Replace line/rectangle calls
- [x] Replace save calls
- [x] Add `using PXA.Pdf`
- [x] Remove Spire usings
- [ ] Convert image drawing after resource mapping is defined
- [ ] Convert table/form/security APIs only after PXA equivalents exist

## Tests Checklist

- [x] Basic document/page/text/save sample
- [x] DrawString with `PointF` sample
- [x] Line and rectangle sample
- [x] Image warning sample
- [x] Unsupported table/forms/security/extraction diagnostic sample
- [x] Existing-PDF editing diagnostic sample
- [x] WebApi smoke test
- [ ] Snapshot before/after migration sample
- [ ] Real package identification sample from a customer repository
