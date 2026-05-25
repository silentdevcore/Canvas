# Canvas Migration: ActivePDF

## Package / API Identification

- [ ] NuGet packages / COM references:
  - [ ] ActivePDF package used by the project
  - [ ] ActivePDF COM interop references, if any
- [ ] Common namespaces to detect:
  - [ ] ActivePDF namespaces used by the project
- [ ] Common classes to detect:
  - [ ] Toolkit/server document class
  - [ ] Page/canvas class
  - [ ] HTML/conversion class
  - [ ] Merge/stamp classes

## Mapping Table Placeholders

| ActivePDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| Document construction | `new Canvas.Pdf.PdfDocument()` | Manual | Confirm exact API/product |
| Page creation | `document.AddPage(...)` | Manual | Confirm units/page size |
| Stamping/text APIs | `page.DrawText(...)` | Manual | Determine generation vs editing |
| Save/export | `document.Save(...)` | Manual | Confirm output API |

## Unsupported / Manual Follow-Up

- [ ] Server-side HTML conversion
- [ ] COM automation workflows
- [ ] Existing PDF merge/stamp workflows
- [ ] Forms
- [ ] Security/signatures
- [ ] Printer/driver based output

## Sample Input Snippets

```csharp
// TODO: Add real ActivePDF sample after package/API identification.
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

- [ ] Confirm ActivePDF product/API identifiers
- [ ] Detect COM interop usage
- [ ] Detect generation APIs separately from conversion/stamping APIs
- [ ] Warn on printer/driver workflows
- [ ] Report manual migration items

## Code Fix Checklist

- [ ] Implement only after exact API confirmation
- [ ] Replace deterministic document creation
- [ ] Replace deterministic page creation
- [ ] Add `using Canvas.Pdf`
- [ ] Leave COM/conversion flows as manual diagnostics

## Tests Checklist

- [ ] Real package or COM reference sample
- [ ] Basic generation sample
- [ ] Conversion unsupported diagnostic sample
- [ ] COM workflow diagnostic sample
- [ ] Snapshot before/after migration sample
