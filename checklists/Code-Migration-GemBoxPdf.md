# Canvas Migration: GemBox.Pdf

## Package / API Identification

- [ ] NuGet packages:
  - [ ] `GemBox.Pdf`
- [ ] Common namespaces to detect:
  - [ ] `GemBox.Pdf`
  - [ ] `GemBox.Pdf.Content`
  - [ ] `GemBox.Pdf.Forms`
- [ ] Common classes to detect:
  - [ ] `PdfDocument`
  - [ ] `PdfPage`
  - [ ] `PdfFormattedText`
  - [ ] `PdfTextContent`
  - [ ] `PdfImage`

## Mapping Table Placeholders

| GemBox.Pdf API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PdfDocument()` | `new Canvas.Pdf.PdfDocument()` | Code fix candidate | Resolve type name conflict |
| `document.Pages.Add()` | `document.AddPage(...)` | Code fix candidate | Map media box |
| Text content additions | `page.DrawText(...)` | Manual | API patterns vary |
| `document.Save(...)` | `document.Save(...)` | Code fix candidate | Confirm overload semantics |

## Unsupported / Manual Follow-Up

- [ ] Interactive forms
- [ ] Incremental updates
- [ ] Complex content editing
- [ ] Encryption
- [ ] Digital signatures
- [ ] Tagged PDF

## Sample Input Snippets

```csharp
using GemBox.Pdf;

using var document = new PdfDocument();
var page = document.Pages.Add();
document.Save(path);
```

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [ ] Detect GemBox document creation
- [ ] Detect page creation
- [ ] Detect content/text operations
- [ ] Warn on forms/security APIs
- [ ] Warn on editing APIs

## Code Fix Checklist

- [ ] Replace basic document creation
- [ ] Replace basic page creation
- [ ] Replace simple save calls
- [ ] Add `using Canvas.Pdf`
- [ ] Report manual content conversion items

## Tests Checklist

- [ ] Basic document sample
- [ ] Page creation sample
- [ ] Save sample
- [ ] Unsupported forms diagnostic sample
- [ ] Snapshot before/after migration sample
