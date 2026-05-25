# Canvas Migration: Apryse SDK

## Package / API Identification

- [ ] NuGet packages:
  - [ ] Apryse/PDFTron package used by the project
- [ ] Common namespaces to detect:
  - [ ] `pdftron`
  - [ ] `pdftron.PDF`
  - [ ] `pdftron.SDF`
- [ ] Common classes to detect:
  - [ ] `PDFDoc`
  - [ ] `Page`
  - [ ] `ElementBuilder`
  - [ ] `ElementWriter`
  - [ ] `PDFDraw`
  - [ ] `Font`

## Mapping Table Placeholders

| Apryse API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PDFDoc()` | `new Canvas.Pdf.PdfDocument()` | Code fix candidate | Confirm lifecycle/dispose pattern |
| `Page.Create(...)` | `document.AddPage(...)` | Manual | Map rectangle units |
| `ElementBuilder.CreateText...` | `page.DrawText(...)` | Manual | Map text matrix and style |
| `doc.Save(...)` | `document.Save(...)` | Code fix candidate | Confirm overload semantics |

## Unsupported / Manual Follow-Up

- [ ] Low-level SDF object manipulation
- [ ] PDF editing and incremental save
- [ ] ElementReader/ElementWriter advanced content streams
- [ ] Forms
- [ ] Redaction
- [ ] Digital signatures

## Sample Input Snippets

```csharp
using pdftron.PDF;

using var doc = new PDFDoc();
var page = doc.PageCreate();
doc.PagePushBack(page);
doc.Save(path, SDFDoc.SaveOptions.e_linearized);
```

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [ ] Detect Apryse/PDFTron document construction
- [ ] Detect page creation/push patterns
- [ ] Detect ElementBuilder text/image operations
- [ ] Warn on low-level SDF APIs
- [ ] Warn on editing-only APIs

## Code Fix Checklist

- [ ] Replace basic document creation
- [ ] Replace basic page append patterns
- [ ] Replace simple save calls
- [ ] Add `using Canvas.Pdf`
- [ ] Report low-level content stream work as manual

## Tests Checklist

- [ ] Basic PDFDoc sample
- [ ] Page append sample
- [ ] Save sample
- [ ] SDF unsupported diagnostic sample
- [ ] Snapshot before/after migration sample
