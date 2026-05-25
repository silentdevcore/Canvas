# Canvas Migration: PDFKit.NET

## Package / API Identification

- [ ] NuGet packages:
  - [ ] PDFKit.NET package used by the project
- [ ] Common namespaces to detect:
  - [ ] Provider-specific PDFKit.NET namespaces
- [ ] Common classes to detect:
  - [ ] Document class
  - [ ] Page class
  - [ ] Graphics/canvas class
  - [ ] Font class
  - [ ] Image class

## Mapping Table Placeholders

| PDFKit.NET API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| Document construction | `new Canvas.Pdf.PdfDocument()` | Manual | Confirm actual package/API |
| Page creation | `document.AddPage(...)` | Manual | Confirm actual API |
| Text drawing | `page.DrawText(...)` | Manual | Confirm coordinates and fonts |
| Save/export | `document.Save(...)` | Manual | Confirm output API |

## Unsupported / Manual Follow-Up

- [ ] Confirm exact PDFKit.NET package identity
- [ ] Existing PDF editing
- [ ] Forms
- [ ] Security/encryption
- [ ] Digital signatures
- [ ] Advanced layout helpers

## Sample Input Snippets

```csharp
// TODO: Add real PDFKit.NET sample after package/API identification.
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

- [ ] Confirm namespaces before diagnostics are implemented
- [ ] Detect document construction
- [ ] Detect page creation
- [ ] Detect text drawing
- [ ] Warn on unsupported features

## Code Fix Checklist

- [ ] Replace basic document creation after API confirmation
- [ ] Replace basic page creation after API confirmation
- [ ] Replace simple text drawing after API confirmation
- [ ] Add `using Canvas.Pdf`
- [ ] Emit manual migration report entries

## Tests Checklist

- [ ] Real package identification sample
- [ ] Basic document sample
- [ ] Text drawing sample
- [ ] Unsupported diagnostic sample
- [ ] Snapshot before/after migration sample
