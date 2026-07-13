# PXA Migration: Foxit PDF SDK

## V1 Pilot Analysis

- [x] Foxit PDF SDK is a broad SDK with document generation, editing, annotations, forms, security, rendering, OCR/conversion, viewer, and redaction surfaces.
- [x] PXA.Pdf currently covers the simple generation subset best: new document, new page, text, images, shapes, and save/export.
- [x] Existing-PDF editing and processing features should remain report-only/manual in v1.
- [x] First implementation is a Roslyn-backed reporting pilot, not an automatic code rewrite.

## Package / API Identification

- [x] NuGet packages:
  - [x] Foxit PDF SDK package used by the consuming project, exact package id to be confirmed per customer repository
- [x] Common namespaces to detect:
  - [x] `foxit`
  - [x] `foxit.pdf`
  - [x] `foxit.common`
  - [x] `foxit.addon`
- [x] Common classes to detect:
  - [x] `Library`
  - [x] `PDFDoc`
  - [x] `PDFPage`
  - [x] `Graphics` / `PDFGraphics`
  - [x] `PDFViewCtrl`
  - [x] `Annot`
  - [x] `Field` / `PDFForm`
  - [x] `SecurityHandler`

## Roslyn Prototype Status

- [x] Add `src/Migrations/PDF/PXA.Migration.Pdf.Code.Foxit`
- [x] Add `tests/PXA.Migration.Pdf.Code.Foxit.Tests`
- [x] Add WebApi converter integration
- [x] Add API service smoke test
- [x] Report deterministic candidates while preserving original source
- [x] Warn on unsupported/manual Foxit feature areas

## Mapping Table

| Foxit PDF SDK API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `Library.Initialize(...)` | No PXA equivalent | Report-only | PXA.Pdf does not require global SDK initialization. |
| `new PDFDoc()` | `new PXA.Pdf.PdfDocument()` | Report-only in v1 | Input-file constructors imply existing-PDF editing and need manual review. |
| `doc.InsertPage(...)`, `CreatePage(...)`, `AddPage(...)` | `document.AddPage(...)` | Report-only in v1 | Review page size, orientation, and coordinate origin. |
| `GetGraphics(...)`, `StartGenerateContents(...)`, `GenerateContent(...)` | Draw directly on `PdfPage` | Report-only in v1 | PXA draw calls target the page returned by `AddPage`. |
| `DrawText(...)`, `ShowText(...)`, `DrawString(...)`, `TextOut(...)` | `page.DrawText(...)` / `page.DrawTextFromTop(...)` | Report-only in v1 | Font and coordinate mapping must be reviewed. |
| `DrawImage(...)`, `AddImage(...)` | `page.DrawImage(...)` | Report-only in v1 | Image resource and scaling semantics need manual review. |
| `DrawLine(...)`, `DrawRect(...)`, `DrawRectangle(...)`, `FillRect(...)`, `DrawPath(...)` | `page.DrawLine(...)`, `page.DrawRectangle(...)`, path drawing | Report-only in v1 | Geometry and stroke/fill styles need review. |
| `doc.Save(...)`, `doc.SaveAs(...)` | `document.Save(...)` | Report-only in v1 | Foxit save flags/options need manual review. |

## Unsupported / Manual Follow-Up

- [x] Existing PDF editing
- [x] Forms
- [x] Redaction
- [x] Security/encryption
- [x] Digital signatures
- [x] Annotation workflows
- [x] OCR/conversion
- [x] Rendering/viewer APIs
- [x] Attachments and package-level PDF processing

## Analyzer Diagnostics

| Diagnostic | Severity | Meaning |
| --- | --- | --- |
| `CANMIGFOXIT000` | Info | `Library.Initialize(...)` detected. |
| `CANMIGFOXIT001` | Info | `PDFDoc` construction detected. |
| `CANMIGFOXIT002` | Info | Page creation/insertion candidate detected. |
| `CANMIGFOXIT003` | Info | Graphics/content workflow detected. |
| `CANMIGFOXIT004` | Info | Text drawing candidate detected. |
| `CANMIGFOXIT005` | Info | Image drawing candidate detected. |
| `CANMIGFOXIT006` | Info | Shape/path drawing candidate detected. |
| `CANMIGFOXIT007` | Info | Save/export target detected. |
| `CANMIGFOXIT020` | Warning | Existing-PDF editing, forms, annotations, signing, or security APIs need manual migration. |
| `CANMIGFOXIT021` | Warning | OCR, conversion, rendering, viewer, redaction, attachments, or related processing APIs need manual migration. |

## Sample Input Snippets

```csharp
using foxit;
using foxit.pdf;

Library.Initialize(licenseKey);
using var doc = new PDFDoc();
var page = doc.InsertPage(0, PageSize.e_SizeA4);
graphics.DrawText("Hello", font, 40, 40);
graphics.DrawImage(image, 40, 120, 200, 80);
graphics.DrawRect(pen, 40, 620, 200, 80);
doc.SaveAs(path);
```

## Expected PXA.Pdf Output Snippets

```csharp
// PXA.Pdf migration report: Foxit PDF SDK
// - Library.Initialize(...) detected. PXA.Pdf does not require a global Foxit SDK initialization call.
// - new PDFDoc(...) detected. Candidate PXA rewrite starts with `var document = new PdfDocument();`; input-file constructors need manual review.
// - InsertPage(...) detected. Candidate PXA rewrite is `var page = document.AddPage(...)` after page size/orientation review.
// - DrawText(...) detected. Candidate PXA rewrite is `page.DrawText(...)` or `page.DrawTextFromTop(...)` after coordinate review.
// - DrawImage(...) detected. Candidate PXA rewrite is `page.DrawImage(...)` after image sizing/resource review.
// - DrawRect(...) detected. Candidate PXA rewrite is `page.DrawLine(...)`, `page.DrawRectangle(...)`, or path drawing after geometry review.
// - SaveAs(...) detected. Candidate PXA rewrite ends with `document.Save(...)`; review Foxit save flags.
```

## Analyzer Diagnostics Checklist

- [x] Confirm Foxit namespace identifiers
- [x] Detect SDK initialization
- [x] Detect document/page creation
- [x] Detect graphics/content workflows
- [x] Detect text/image/shape drawing
- [x] Detect save/export targets
- [x] Warn on editing/forms/security APIs
- [x] Warn on rendering/viewer/OCR/conversion APIs
- [x] Report manual migration items

## Code Fix Checklist

- [ ] Implement only after exact API confirmation from real Foxit customer samples
- [ ] Replace deterministic document creation
- [ ] Replace deterministic page creation
- [ ] Add `using PXA.Pdf`
- [ ] Preserve unsupported calls with diagnostics

## Tests Checklist

- [x] Basic document/page/save workflow
- [x] Text/image/shape drawing workflow
- [x] Unsupported existing-PDF editing diagnostic sample
- [x] Unsupported forms/annotations/security diagnostic sample
- [x] Unsupported rendering/OCR/viewer/conversion diagnostic sample
- [x] WebApi smoke test
- [ ] Snapshot before/after migration sample
- [ ] Real package identification sample from a customer repository
