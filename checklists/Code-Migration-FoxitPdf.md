# Canvas Migration: Foxit PDF SDK

## Package / API Identification

- [ ] NuGet packages:
  - [ ] Foxit PDF SDK package used by the project
- [ ] Common namespaces to detect:
  - [ ] Foxit PDF SDK namespaces used by the project
- [ ] Common classes to detect:
  - [ ] PDF document class
  - [ ] Page class
  - [ ] Graphics/content class
  - [ ] Font class
  - [ ] Image class

## Mapping Table Placeholders

| Foxit PDF SDK API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| Document construction | `new Canvas.Pdf.PdfDocument()` | Manual | Confirm exact .NET API |
| Page creation | `document.AddPage(...)` | Manual | Confirm units/page size |
| Text drawing | `page.DrawText(...)` | Manual | Confirm content API |
| Save/export | `document.Save(...)` | Manual | Confirm save semantics |

## Unsupported / Manual Follow-Up

- [ ] Existing PDF editing
- [ ] Forms
- [ ] Redaction
- [ ] Security/encryption
- [ ] Digital signatures
- [ ] Annotation workflows

## Sample Input Snippets

```csharp
// TODO: Add real Foxit PDF SDK sample after package/API identification.
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

- [ ] Confirm Foxit package and namespace identifiers
- [ ] Detect document/page creation
- [ ] Detect text/image drawing
- [ ] Warn on editing/forms/security APIs
- [ ] Report manual migration items

## Code Fix Checklist

- [ ] Implement only after exact API confirmation
- [ ] Replace deterministic document creation
- [ ] Replace deterministic page creation
- [ ] Add `using Canvas.Pdf`
- [ ] Preserve unsupported calls with diagnostics

## Tests Checklist

- [ ] Real package identification sample
- [ ] Basic document sample
- [ ] Text drawing sample
- [ ] Unsupported editing diagnostic sample
- [ ] Snapshot before/after migration sample
