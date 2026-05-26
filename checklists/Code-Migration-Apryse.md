# Canvas Migration: Apryse SDK

## V1 Pilot Analysis

- [x] V1 scope is a reporting migration, not a broad automatic PDFNet rewrite.
- [x] Roslyn-backed migration is connected through `Canvas.WebApi` via framework id `Apryse`.
- [x] `PDFNet.Initialize(...)` is detected and reported as unnecessary for Canvas.Pdf.
- [x] `new PDFDoc(...)` is detected as a Canvas document candidate.
- [x] `PageCreate(...)` and `PagePushBack(...)` are detected as Canvas page candidates.
- [x] `ElementBuilder` and `ElementWriter` workflows are detected.
- [x] Text, image, rectangle, and path element creation are reported as Canvas drawing candidates.
- [x] `doc.Save(...)` targets are detected for later Canvas `document.Save(...)` mapping.
- [x] SDF, ElementReader, annotations, fields, redaction, rendering, viewer, conversion, OCR, and signature APIs are reported as unsupported in v1.
- [x] WebApi conversion response includes report code, diagnostics, and summary counts.
- [ ] V1 intentionally keeps the original Apryse source code after the migration report.
- [ ] V1 does not resolve Apryse overloads semantically.
- [ ] Future hardening: add deterministic rewrite for simple `PDFDoc` + `PageCreate` + `Save` samples.
- [ ] Future hardening: map ElementBuilder text matrices, fonts, images, and paths into Canvas drawing calls.

## Package / API Identification

- [x] NuGet packages:
  - [x] Apryse/PDFTron package used by the project
- [x] Common namespaces to detect:
  - [x] `pdftron`
  - [x] `pdftron.PDF`
  - [x] `pdftron.SDF`
- [x] Common classes to detect:
  - [x] `PDFNet`
  - [x] `PDFDoc`
  - [x] `Page`
  - [x] `ElementBuilder`
  - [x] `ElementWriter`
  - [x] `ElementReader`
  - [x] `PDFDraw`
  - [x] `Font`
  - [x] `SDFDoc`
  - [x] `Field`
  - [x] `Annot`
  - [x] `DigitalSignatureField`

## Roslyn Prototype Status

- [x] Add `src/Canvas.Migration.Apryse`
- [x] Add `tests/Canvas.Migration.Apryse.Tests`
- [x] Add projects to `Canvas.sln`
- [x] Implement first source migration entry point: `ApryseMigration`
- [x] Generate a Canvas.Pdf migration report comment while preserving original Apryse source
- [x] Detect `PDFNet.Initialize(...)`
- [x] Detect `new PDFDoc(...)`
- [x] Detect `PageCreate(...)` and `PagePushBack(...)`
- [x] Detect `doc.Save(...)`
- [x] Detect `ElementBuilder`, `ElementWriter`, `Begin(...)`, and `WriteElement(...)`
- [x] Detect text/image/path/rectangle element creation
- [x] Warn for SDF, ElementReader, annotations, fields, redaction, rendering, viewer, conversion, OCR, and signatures
- [x] Connect WebApi Apryse converter to the Roslyn reporting migration engine
- [x] Add WebApi migration-service smoke test for Apryse summary/diagnostics
- [x] Verified with `dotnet test tests/Canvas.Migration.Apryse.Tests/Canvas.Migration.Apryse.Tests.csproj --no-restore --no-build`: `5/5` passed
- [x] Verified with `dotnet test tests/Canvas.Api.Tests/Canvas.Api.Tests.csproj --no-restore --no-build`: `20/20` passed
- [ ] Replace syntax-only matching with semantic matching before broad rollout

## Mapping Table Placeholders

| Apryse API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `PDFNet.Initialize(...)` | none | Report-only | Canvas.Pdf does not need SDK initialization |
| `new PDFDoc()` | `new Canvas.Pdf.PdfDocument()` | Report-only | Confirm lifecycle/dispose pattern before automatic rewrite |
| `doc.PageCreate(...)` | `document.AddPage(...)` | Report-only | Map media box/page units |
| `doc.PagePushBack(page)` | included in `document.AddPage(...)` | Report-only | Canvas creates and attaches the page in one step |
| `new ElementBuilder()` | Canvas page draw calls | Report-only | Requires element-by-element review |
| `new ElementWriter()` / `writer.Begin(page)` | Canvas page draw calls | Report-only | Content stream writing maps to page drawing |
| `writer.WriteElement(...)` | Canvas page draw calls | Report-only | Inspect ElementBuilder source |
| `ElementBuilder.CreateText...` | `page.DrawText(...)` | Report-only | Map text matrix and style |
| `ElementBuilder.CreateImage...` | `page.DrawImage(...)` | Report-only | Map image resources |
| `ElementBuilder.CreateRect/CreatePath` | `page.DrawRectangle(...)` or path drawing | Report-only | Map geometry/fill/stroke |
| `doc.Save(...)` | `document.Save(...)` | Report-only | Confirm overload/save flag semantics |

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGAPRYSE000` | Info | `PDFNet.Initialize(...)` detected | No |
| `CANMIGAPRYSE001` | Info | `PDFDoc` construction detected | No |
| `CANMIGAPRYSE002` | Info | `PageCreate(...)` page candidate detected | No |
| `CANMIGAPRYSE003` | Info | `PagePushBack(...)` page append candidate detected | No |
| `CANMIGAPRYSE004` | Info | `doc.Save(...)` save target detected | No |
| `CANMIGAPRYSE005` | Info | `ElementBuilder` construction detected | No |
| `CANMIGAPRYSE006` | Info | `ElementWriter` construction detected | No |
| `CANMIGAPRYSE007` | Info | `ElementWriter.Begin(...)` detected | No |
| `CANMIGAPRYSE008` | Info | `WriteElement(...)` content stream work detected | No |
| `CANMIGAPRYSE009` | Info | Text element creation detected | No |
| `CANMIGAPRYSE010` | Info | Image element creation detected | No |
| `CANMIGAPRYSE011` | Info | Path/shape element creation detected | No |
| `CANMIGAPRYSE020` | Warning | Processing, conversion, OCR, forms, or SDF APIs require manual migration | No |
| `CANMIGAPRYSE021` | Warning | SDF, reader, annotation, field, redaction, rendering, viewer, conversion, or OCR APIs are outside v1 | No |

## Unsupported / Manual Follow-Up

- [x] Low-level SDF object manipulation
- [x] PDF editing and incremental save
- [x] ElementReader/ElementWriter advanced content streams
- [x] Forms
- [x] Redaction
- [x] Digital signatures
- [x] OCR/conversion/viewer/rendering APIs

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

// Canvas.Pdf migration report: Apryse SDK
// - new PDFDoc(...) detected. Candidate Canvas rewrite starts with `var document = new PdfDocument();`.
// - PageCreate(...) detected. Candidate Canvas rewrite is `var page = document.AddPage(...)` after media box review.
// - PagePushBack(page) detected. Canvas `document.AddPage(...)` creates and attaches the page in one step.
// - doc.Save(...) detected. Candidate Canvas rewrite ends with `document.Save(...)`; review Apryse save flags.
```

## Analyzer Diagnostics Checklist

- [x] Detect Apryse/PDFTron document construction
- [x] Detect page creation/push patterns
- [x] Detect ElementBuilder text/image operations
- [x] Warn on low-level SDF APIs
- [x] Warn on editing-only APIs
- [x] Warn on OCR/conversion/viewer APIs

## Code Fix Checklist

- [x] Report basic document creation
- [x] Report basic page append patterns
- [x] Report simple save calls
- [x] Add `using Canvas.Pdf` only for confirmed replacements
- [x] Report low-level content stream work as manual
- [ ] Add automatic code fix for simple `PDFDoc` + page + save sample

## Tests Checklist

- [x] Basic PDFDoc sample
- [x] Page append sample
- [x] Save sample
- [x] ElementBuilder/ElementWriter text sample
- [x] Image/path/shape element sample
- [x] SDF unsupported diagnostic sample
- [x] Conversion/OCR/signature diagnostic sample
- [x] WebApi migration-service smoke test
- [ ] Snapshot before/after migration sample
