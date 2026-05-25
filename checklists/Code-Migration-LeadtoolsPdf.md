# Canvas Migration: LEADTOOLS PDF

## Package / API Identification

- [ ] NuGet packages:
  - [ ] LEADTOOLS PDF packages used by the project
- [ ] Common namespaces to detect:
  - [ ] `Leadtools`
  - [ ] `Leadtools.Pdf`
  - [ ] `Leadtools.Document`
- [ ] Common classes to detect:
  - [ ] `PDFDocument`
  - [ ] `PDFFile`
  - [ ] `DocumentFactory`
  - [ ] PDF rendering/conversion classes

## Mapping Table Placeholders

| LEADTOOLS API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| PDF document creation | `new Canvas.Pdf.PdfDocument()` | Manual | Confirm generation API |
| Page creation | `document.AddPage(...)` | Manual | Confirm API and units |
| Rendering/conversion calls | Manual Canvas layout | Manual | Often not a direct generation match |
| Save/export | `document.Save(...)` | Manual | Confirm output API |

## Unsupported / Manual Follow-Up

- [ ] Raster/document conversion pipelines
- [ ] OCR
- [ ] Existing PDF editing
- [ ] Forms
- [ ] Annotations
- [ ] Security/signatures

## Sample Input Snippets

```csharp
// TODO: Add real LEADTOOLS PDF-generation sample after API identification.
```

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawText("Hello", 40, 800);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [ ] Detect LEADTOOLS PDF package references
- [ ] Detect PDF generation APIs separately from conversion/OCR APIs
- [ ] Warn on OCR/conversion-only APIs
- [ ] Warn on editing/security APIs
- [ ] Report manual migration items

## Code Fix Checklist

- [ ] Implement only after generation API confirmation
- [ ] Replace deterministic document creation
- [ ] Replace deterministic page creation
- [ ] Add `using Canvas.Pdf`
- [ ] Leave conversion/OCR flows as manual diagnostics

## Tests Checklist

- [ ] Real package identification sample
- [ ] Basic generation sample
- [ ] Conversion unsupported diagnostic sample
- [ ] OCR unsupported diagnostic sample
- [ ] Snapshot before/after migration sample
