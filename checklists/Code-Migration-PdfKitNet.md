# Canvas Migration: PDFKit.NET

## V1 Pilot Analysis

- [x] Added cautious Roslyn-backed provider project: `src/Canvas.Migration.PdfKitNet`
- [x] Added provider tests: `tests/Canvas.Migration.PdfKitNet.Tests`
- [x] Connected WebApi converter: `Canvas.WebApi/Services/Converters/PdfKitNetConverter.cs`
- [x] Added UI fallback status/example as `pilot`
- [ ] Confirm the exact NuGet package and public API names with a real customer/source sample

PDFKit.NET remains less certain than the other providers because the package/API identity is not confirmed in this repository. V1 therefore treats the converter as a guarded pilot: it migrates likely, simple document/page/text/shape/save patterns and always emits `CANMIGPDFKIT000` so users validate the mappings before applying them broadly.

## Package / API Identification

- [ ] NuGet packages:
  - [ ] Exact PDFKit.NET package used by the source project
- [x] Likely namespaces to detect/remove:
  - [x] `PdfKitNet`
  - [x] `PdfKit`
  - [x] `PDFKit`
- [x] Likely classes to detect:
  - [x] `Document`
  - [x] `PdfDocument`
  - [x] `PDFDocument`
- [ ] Real package classes still to confirm:
  - [ ] Page class
  - [ ] Graphics/canvas class
  - [ ] Font class
  - [ ] Image class

## Mapping Table

| PDFKit.NET API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new Document()` / `new PdfDocument()` / `new PDFDocument()` | `var document = new PdfDocument();` | Pilot code fix | API identity must be validated |
| `document.NewPage()` / `document.AddPage()` | `var page = document.AddPage();` | Pilot code fix | Preserves assigned page variable |
| `document.Pages.Add()` | `var page = document.AddPage();` | Pilot code fix | Preserves assigned page variable |
| `page.DrawText("text", x, y)` | `page.DrawTextFromTop("text", x, y, 12);` | Pilot code fix | Uses default font size 12 |
| `page.DrawString("text", x, y)` | `page.DrawTextFromTop("text", x, y, 12);` | Pilot code fix | Uses default font size 12 |
| `page.DrawLine(x1, y1, x2, y2)` | `page.DrawLineFromTop(x1, y1, x2, y2);` | Pilot code fix | Assumes top-left coordinate semantics |
| `page.DrawRectangle(x, y, w, h)` | `page.DrawRectangleFromTop(x, y, w, h);` | Pilot code fix | Assumes top-left coordinate semantics |
| `document.Save(path)` / `Render(path)` / `Write(path)` / `SaveAs(path)` | `document.Save(path);` | Pilot code fix | Keeps first output argument |

## Unsupported / Manual Follow-Up

- [x] Always warn that package/API identity is unconfirmed
- [x] Existing PDF loading/editing/merging/splitting/page deletion
- [x] Image drawing
- [x] Forms / AcroForm
- [x] Security / encryption
- [x] Digital signatures
- [x] Annotations / bookmarks / outlines
- [x] HTML / table / template helpers
- [ ] Real package-specific layout helpers after sample collection

## Sample Input Snippets

```csharp
using PdfKitNet;

var doc = new Document();
var page = doc.NewPage();
page.DrawText("Hello", 40, 40);
page.DrawLine(40, 80, 200, 80);
page.DrawRectangle(40, 100, 200, 80);
doc.Render(outputPath);
```

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawTextFromTop("Hello", 40, 40, 12);
page.DrawLineFromTop(40, 80, 200, 80);
page.DrawRectangleFromTop(40, 100, 200, 80);
document.Save(outputPath);
```

## Analyzer Diagnostics Checklist

| Diagnostic | Severity | Status | Purpose |
| --- | --- | --- | --- |
| `CANMIGPDFKIT000` | Warning | [x] | Package/API identity unconfirmed |
| `CANMIGPDFKIT001` | Info | [x] | Document creation converted |
| `CANMIGPDFKIT002` | Info | [x] | Page creation converted |
| `CANMIGPDFKIT003` | Info/Warning | [x] | Text drawing converted or flagged |
| `CANMIGPDFKIT005` | Warning | [x] | Image drawing requires manual migration |
| `CANMIGPDFKIT006` | Info | [x] | Shape drawing converted |
| `CANMIGPDFKIT007` | Info | [x] | Save/export converted |
| `CANMIGPDFKIT020` | Warning | [x] | Complex features require manual migration |
| `CANMIGPDFKIT021` | Warning | [x] | Existing-PDF editing requires manual migration |

## Code Fix Checklist

- [x] Replace likely document creation
- [x] Replace likely page creation
- [x] Replace simple text drawing
- [x] Replace simple line/rectangle drawing
- [x] Replace simple save/export
- [x] Add `using Canvas.Pdf`
- [x] Remove likely PDFKit.NET usings
- [x] Emit manual migration report entries
- [ ] Validate mappings against real package API before promoting beyond pilot

## Tests Checklist

- [ ] Real package identification sample
- [x] Basic document/page/text/save sample
- [x] `Pages.Add()` + `DrawString(...)` sample
- [x] Line/rectangle drawing sample
- [x] Image unsupported diagnostic sample
- [x] Forms/security/signature/annotation/table/template diagnostic sample
- [x] Existing-PDF editing diagnostic sample
- [x] WebApi smoke test
