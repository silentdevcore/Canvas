# Canvas Migration: ActivePDF

## V1 Pilot Analysis

- [x] Added cautious Roslyn-backed provider project: `src/Canvas.Migration.ActivePdf`
- [x] Added provider tests: `tests/Canvas.Migration.ActivePdf.Tests`
- [x] Connected WebApi converter: `PXA.WebApi/Services/Converters/ActivePdfConverter.cs`
- [x] Added UI fallback status/example as `pilot`
- [ ] Confirm exact ActivePDF product, NuGet package, or COM interop reference with a real source sample

ActivePDF has several product lines and legacy COM/server workflows. V1 only attempts likely Toolkit-style direct PDF-generation patterns. DocConverter, WebGrabber, printer, merge/stamp, COM/server automation, and existing-PDF editing flows are intentionally reported as manual work.

## Package / API Identification

- [ ] NuGet packages / COM references:
  - [ ] ActivePDF package used by the project
  - [ ] ActivePDF COM interop references, if any
- [x] Likely namespaces to detect/remove:
  - [x] `activePDF.*`
  - [x] `ActivePDF.*`
- [x] Likely document/product classes to detect:
  - [x] `Toolkit`
  - [x] `DocConverter`
  - [x] `Document`
  - [x] `APDoc`
  - [x] `Server`
- [x] Manual-only product/classes to flag:
  - [x] `WebGrabber`
  - [x] `DocConverter`
  - [x] `Merger`
  - [x] `Printer`
  - [x] `Server`
  - [x] `ComObject`

## Mapping Table

| ActivePDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new Toolkit()` / `new APDoc()` / `new Document()` | `var document = new PdfDocument();` | Pilot code fix | Treat as likely direct generation |
| `document.AddPage()` / `BeginPage()` / `NewPage()` | `var page = document.AddPage();` | Pilot code fix | Preserves assigned page variable |
| `PrintText("text", x, y)` / `DrawText` / `AddText` / `TextOut` | `page.DrawTextFromTop("text", x, y, 12);` | Pilot code fix | Uses default font size 12 |
| `DrawLine(x1, y1, x2, y2)` / `AddLine` | `page.DrawLineFromTop(x1, y1, x2, y2);` | Pilot code fix | Assumes top-left coordinate semantics |
| `DrawRectangle(x, y, w, h)` / `AddRectangle` | `page.DrawRectangleFromTop(x, y, w, h);` | Pilot code fix | Assumes top-left coordinate semantics |
| `Save(path)` / `SaveAs(path)` / `SaveToFile(path)` / `CloseDocument(path)` | `document.Save(path);` | Pilot code fix | Keeps first output argument |

## Unsupported / Manual Follow-Up

- [x] DocConverter and WebGrabber HTML/web conversion
- [x] COM automation workflows
- [x] Printer/driver based output
- [x] Existing PDF merge/stamp/edit workflows
- [x] Image/stamp drawing
- [x] Forms
- [x] Security/signatures
- [x] Annotations
- [ ] Real product-specific API details after sample collection

## Sample Input Snippets

```csharp
using activePDF.Toolkit;

var toolkit = new Toolkit();
var page = toolkit.AddPage();
toolkit.PrintText("Hello", 40, 40);
toolkit.DrawLine(40, 80, 200, 80);
toolkit.DrawRectangle(40, 100, 200, 80);
toolkit.Save(outputPath);
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
| `CANMIGACTIVE000` | Warning | [x] | ActivePDF product/COM workflow warning |
| `CANMIGACTIVE001` | Info | [x] | Document/product object creation converted |
| `CANMIGACTIVE002` | Info | [x] | Page creation converted |
| `CANMIGACTIVE003` | Info/Warning | [x] | Text drawing converted or flagged |
| `CANMIGACTIVE005` | Warning | [x] | Image/stamp drawing requires manual migration |
| `CANMIGACTIVE006` | Info | [x] | Shape drawing converted |
| `CANMIGACTIVE007` | Info | [x] | Save/close converted |
| `CANMIGACTIVE020` | Warning | [x] | Product families / COM / printer / security features require manual migration |
| `CANMIGACTIVE021` | Warning | [x] | HTML conversion, merge, print, or existing-PDF editing requires manual migration |

## Code Fix Checklist

- [x] Replace likely Toolkit-style document creation
- [x] Replace likely page creation
- [x] Replace simple text drawing
- [x] Replace simple line/rectangle drawing
- [x] Replace simple save/close
- [x] Add `using Canvas.Pdf`
- [x] Remove ActivePDF usings
- [x] Leave COM/conversion/merge/print flows as manual diagnostics
- [ ] Validate mappings against real ActivePDF product API before promoting beyond pilot

## Tests Checklist

- [ ] Real package or COM reference sample
- [x] Basic Toolkit-style generation sample
- [x] `BeginPage()` + `DrawText(...)` sample
- [x] Line/rectangle drawing sample
- [x] Image/stamp unsupported diagnostic sample
- [x] Product family / COM / printer / security diagnostic sample
- [x] HTML conversion / merge / print / existing-PDF diagnostic sample
- [x] WebApi smoke test
