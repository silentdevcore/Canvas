# PXA Migration: LEADTOOLS PDF

## V1 Pilot Analysis

- [x] Added cautious Roslyn-backed provider project: `src/PXA.Migration.Pdf.Code.Leadtools`
- [x] Added provider tests: `tests/PXA.Migration.Pdf.Code.Leadtools.Tests`
- [x] Connected WebApi converter: `PXA.WebApi/Services/Converters/LeadtoolsPdfConverter.cs`
- [x] Added UI fallback status/example as `pilot`
- [ ] Confirm exact LEADTOOLS PDF-generation package/API with a real source sample

LEADTOOLS covers PDF, raster imaging, OCR, barcode, document conversion, and existing-document workflows. V1 only attempts likely direct PDF-generation patterns. Raster/OCR/conversion APIs are intentionally flagged for manual migration because PXA.Pdf output must be recreated as vector/layout calls.

## Package / API Identification

- [ ] NuGet packages:
  - [ ] Exact LEADTOOLS PDF-generation package used by the source project
- [x] Common namespaces to detect/remove:
  - [x] `Leadtools`
  - [x] `Leadtools.Pdf`
  - [x] `Leadtools.Document`
- [x] Likely classes to detect:
  - [x] `PDFDocument`
  - [x] `PdfDocument`
  - [x] `PDFFile`
  - [x] `PdfFile`
- [x] Manual-only classes to flag:
  - [x] `RasterCodecs`
  - [x] `RasterImage`
  - [x] `OcrEngineManager`
  - [x] `DocumentConverter`
  - [x] `DocumentFactory`
  - [x] `BarcodeEngine`

## Mapping Table

| LEADTOOLS API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PDFDocument()` / `new PdfDocument()` / `new PDFFile()` / `new PdfFile()` | `var document = new PdfDocument();` | Pilot code fix | API identity must be validated |
| `document.AddPage()` / `document.NewPage()` | `var page = document.AddPage();` | Pilot code fix | Preserves assigned page variable |
| `document.Pages.Add()` | `var page = document.AddPage();` | Pilot code fix | Preserves assigned page variable |
| `page.DrawText("text", x, y)` / `DrawString` / `TextOut` / `AddText` | `page.DrawTextFromTop("text", x, y, 12);` | Pilot code fix | Uses default font size 12 |
| `page.DrawLine(x1, y1, x2, y2)` / `AddLine` | `page.DrawLineFromTop(x1, y1, x2, y2);` | Pilot code fix | Assumes top-left coordinate semantics |
| `page.DrawRectangle(x, y, w, h)` / `AddRectangle` | `page.DrawRectangleFromTop(x, y, w, h);` | Pilot code fix | Assumes top-left coordinate semantics |
| `document.Save(path)` / `SaveToFile(path)` / `Write(path)` / `Export(path)` | `document.Save(path);` | Pilot code fix | Keeps first output argument |

## Unsupported / Manual Follow-Up

- [x] Raster/document conversion pipelines
- [x] OCR
- [x] Barcode extraction/generation workflows
- [x] Existing PDF editing/loading/merging/splitting/page deletion
- [x] Image/raster drawing
- [x] Forms
- [x] Annotations
- [x] Security/signatures
- [ ] Real package-specific generation APIs after sample collection

## Sample Input Snippets

```csharp
using Leadtools.Pdf;

var doc = new PDFDocument();
var page = doc.AddPage();
page.DrawText("Hello", 40, 40);
page.DrawLine(40, 80, 200, 80);
page.DrawRectangle(40, 100, 200, 80);
doc.Save(outputPath);
```

## Expected PXA.Pdf Output Snippets

```csharp
using PXA.Pdf;

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
| `CANMIGLEAD000` | Warning | [x] | LEADTOOLS API family warning |
| `CANMIGLEAD001` | Info | [x] | Document creation converted |
| `CANMIGLEAD002` | Info | [x] | Page creation converted |
| `CANMIGLEAD003` | Info/Warning | [x] | Text drawing converted or flagged |
| `CANMIGLEAD005` | Warning | [x] | Image/raster drawing requires manual migration |
| `CANMIGLEAD006` | Info | [x] | Shape drawing converted |
| `CANMIGLEAD007` | Info | [x] | Save/export converted |
| `CANMIGLEAD020` | Warning | [x] | Raster/OCR/barcode/conversion/security features require manual migration |
| `CANMIGLEAD021` | Warning | [x] | Existing-PDF editing/conversion requires manual migration |

## Code Fix Checklist

- [x] Replace likely document creation
- [x] Replace likely page creation
- [x] Replace simple text drawing
- [x] Replace simple line/rectangle drawing
- [x] Replace simple save/export
- [x] Add `using PXA.Pdf`
- [x] Remove LEADTOOLS usings
- [x] Leave conversion/OCR flows as manual diagnostics
- [ ] Validate mappings against real LEADTOOLS generation API before promoting beyond pilot

## Tests Checklist

- [ ] Real package identification sample
- [x] Basic document/page/text/save sample
- [x] `Pages.Add()` + `DrawString(...)` sample
- [x] Line/rectangle drawing sample
- [x] Image/raster unsupported diagnostic sample
- [x] OCR/raster/barcode/conversion/security diagnostic sample
- [x] Existing-PDF editing/conversion diagnostic sample
- [x] WebApi smoke test
