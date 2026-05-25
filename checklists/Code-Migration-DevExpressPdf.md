# Canvas Migration: DevExpress PDF

## Package / API Identification

- [ ] NuGet packages:
  - [ ] `DevExpress.Pdf`
  - [ ] Related DevExpress Drawing packages
- [ ] Common namespaces to detect:
  - [ ] `DevExpress.Pdf`
  - [ ] `DevExpress.Drawing`
- [ ] Common classes to detect:
  - [ ] `PdfDocumentProcessor`
  - [ ] `PdfGraphics`
  - [ ] `PdfRectangle`
  - [ ] `PdfFont`
  - [ ] `PdfAcroForm`

## Mapping Table Placeholders

| DevExpress PDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| PDF processor creation | `new Canvas.Pdf.PdfDocument()` | Manual | Processor often targets editing/processing |
| Graphics page creation | `document.AddPage(...)` | Manual | Confirm generation path |
| Graphics text drawing | `page.DrawText(...)` | Manual | Map drawing units and fonts |
| Save/export | `document.Save(...)` | Manual | Confirm API path |

## Unsupported / Manual Follow-Up

- [ ] Existing PDF processing/editing
- [ ] AcroForms
- [ ] Document merge/split APIs
- [ ] Digital signatures
- [ ] Encryption
- [ ] Advanced printing/rendering workflows

## Sample Input Snippets

```csharp
// TODO: Add real DevExpress PDF-generation sample after API identification.
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

- [ ] Detect DevExpress PDF package references
- [ ] Distinguish generation from processing/editing APIs
- [ ] Detect graphics drawing APIs
- [ ] Warn on forms/security/signature APIs
- [ ] Report manual migration items

## Code Fix Checklist

- [ ] Implement only for deterministic generation APIs
- [ ] Replace simple document/page patterns after API confirmation
- [ ] Replace simple text drawing after API confirmation
- [ ] Add `using Canvas.Pdf`
- [ ] Preserve processing APIs with diagnostics

## Tests Checklist

- [ ] Real generation sample
- [ ] PDF processor unsupported diagnostic sample
- [ ] Text drawing sample
- [ ] Forms/signature unsupported sample
- [ ] Snapshot before/after migration sample
