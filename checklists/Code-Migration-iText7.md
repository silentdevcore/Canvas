# Canvas Migration: iText7

## Package / API Identification

- [ ] NuGet packages:
  - [ ] `itext7`
  - [ ] `itext7.bouncy-castle-adapter`
  - [ ] Other project-specific iText packages
- [ ] Common namespaces to detect:
  - [ ] `iText.Kernel.Pdf`
  - [ ] `iText.Layout`
  - [ ] `iText.Layout.Element`
  - [ ] `iText.Layout.Properties`
- [ ] Common classes to detect:
  - [ ] `PdfWriter`
  - [ ] `PdfDocument`
  - [ ] `Document`
  - [ ] `PageSize`
  - [ ] `Paragraph`
  - [ ] `Table`
  - [ ] `Image`

## Mapping Table Placeholders

| iText7 API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PdfWriter(...)` | `PdfDocument.Save(...)` | Manual | Define final save pattern later |
| `new PdfDocument(...)` | `new Canvas.Pdf.PdfDocument()` | Code fix candidate | Confirm namespace conflicts |
| `document.Add(new Paragraph(...))` | `page.DrawText(...)` or flow API | Manual | Requires layout intent analysis |
| `document.Add(new Table(...))` | Canvas table API | Manual | Map after table API review |

## Unsupported / Manual Follow-Up

- [ ] Tagged PDF
- [ ] Advanced layout renderers
- [ ] Form fields
- [ ] Digital signatures
- [ ] Encryption
- [ ] Custom fonts requiring embedding policy

## Sample Input Snippets

```csharp
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

using var writer = new PdfWriter(path);
using var pdf = new PdfDocument(writer);
using var document = new Document(pdf);
document.Add(new Paragraph("Hello"));
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

- [ ] Detect iText document construction
- [ ] Detect text additions
- [ ] Detect page size usage
- [ ] Warn on advanced layout features
- [ ] Warn on unsupported PDF features

## Code Fix Checklist

- [ ] Replace basic document creation
- [ ] Replace basic page creation
- [ ] Replace simple text drawing where coordinates are explicit
- [ ] Add `using Canvas.Pdf`
- [ ] Preserve comments and surrounding code

## Tests Checklist

- [ ] Basic document sample
- [ ] Explicit page size sample
- [ ] Simple text sample
- [ ] Unsupported table/layout diagnostic sample
- [ ] Snapshot before/after migration sample
