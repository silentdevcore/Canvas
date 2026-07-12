# PXA Migration: Apryse SDK

## V1 Implementation Status

- [x] V1 scope: deterministic C# source-to-source migration for the core Apryse document lifecycle.
- [x] Roslyn-backed migration is connected through `PXA.WebApi` via framework id `Apryse`.
- [x] Status upgraded from reporting pilot to **full** converter.
- [x] `PDFNet.Initialize(...)` is removed — PXA.Pdf requires no SDK initialisation call.
- [x] `new PDFDoc(...)` is rewritten to `var document = new PdfDocument()` (both `var` and `using var` forms).
- [x] `doc.PageCreate(...)` is removed — PXA AddPage creates and attaches the page in one step.
- [x] `doc.PagePushBack(page)` is rewritten to `var <pageVarName> = document.AddPage()` — variable name is read from the PagePushBack argument, so multiple pages get distinct names.
- [x] `doc.Save(path, SDFDoc.SaveOptions.*)` is rewritten to `document.Save(path)` — extra Apryse save flags are dropped.
- [x] All `pdftron.*` usings are removed; `using PXA.Pdf;` is inserted.
- [x] `ElementBuilder`, `ElementWriter`, `CreateTextBegin/Run/End`, `CreateImage*`, `CreateRect`, `CreatePath`, `WriteElement`, `Begin` are kept as-is (manual migration required — no diagnostic yet).
- [x] SDF, `ElementReader`, annotations, forms, redaction, rendering, viewer, OCR, conversion, and signature APIs are kept as-is (out of v1 scope — no diagnostic yet).
- [x] WebApi conversion response includes migrated code, diagnostics, and summary counts.
- [ ] V1 does not yet emit diagnostics for ElementBuilder / ElementWriter / unsupported APIs (report items removed when converting from reporting pilot).
- [ ] V1 does not resolve Apryse overloads or page-size arguments semantically.
- [ ] Future hardening: map `ElementBuilder` text/image/path elements to PXA draw calls.
- [ ] Future hardening: map `PageCreate(Rect)` page-size argument to `PdfPagePreset`.
- [ ] Future hardening: replace syntax-only matching with semantic matching before broad rollout.

## Package / API Identification

- [x] NuGet packages:
  - [x] Apryse/PDFTron package used by the project
- [x] Common namespaces to detect and remove:
  - [x] `pdftron`
  - [x] `pdftron.PDF`
  - [x] `pdftron.SDF`
- [x] Common classes handled:
  - [x] `PDFNet` (Initialize removed)
  - [x] `PDFDoc` (→ PdfDocument)
  - [x] `Page` via PageCreate + PagePushBack (→ AddPage)
  - [x] `SDFDoc.SaveOptions` (save flags dropped)
- [ ] Classes kept as-is (manual migration):
  - [ ] `ElementBuilder`
  - [ ] `ElementWriter`
  - [ ] `ElementReader`
  - [ ] `PDFDraw`
  - [ ] `Font`
  - [ ] `SDFDoc`
  - [ ] `Field`
  - [ ] `Annot`
  - [ ] `DigitalSignatureField`

## Roslyn Implementation

- [x] `ApryseMigration` uses a `CSharpSyntaxRewriter` (`ApryseRewriter`) — full code transformation, not a report.
- [x] Pre-scan phase: `FindDocVariable`, `FindPageVariable`, `FindSaveTarget` resolve variable names before rewriting.
- [x] `VisitGlobalStatement` handles all five rewrite rules in priority order.
- [x] `IsDeclarationWithCall` added to handle `var page = doc.PageCreate()` (local declaration, not expression statement).
- [x] `GetFirstArgName` reads the `PagePushBack` argument to preserve per-page variable names (e.g. `page1`, `page2`).
- [x] `TryGetPdfDocDeclaration` handles both `var doc = new PDFDoc()` and `using var doc = new PDFDoc()`.
- [x] pdftron usings removed via `RemoveAprysedUsings`; `using PXA.Pdf;` inserted via `EnsurePxaUsing`.

## Mapping Table

| Apryse API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `using pdftron;` / `using pdftron.PDF;` / `using pdftron.SDF;` | *(removed)* + `using PXA.Pdf;` | Automatic | All pdftron.* namespaces stripped |
| `PDFNet.Initialize(key)` | *(removed)* | Automatic | No SDK init needed |
| `using var doc = new PDFDoc()` | `var document = new PdfDocument()` | Automatic | `using` keyword dropped |
| `var doc = new PDFDoc()` | `var document = new PdfDocument()` | Automatic | |
| `var page = doc.PageCreate(...)` | *(removed)* | Automatic | AddPage subsumes both steps |
| `doc.PagePushBack(page)` | `var page = document.AddPage()` | Automatic | Argument name preserved |
| `doc.PagePushBack(page1)` + `doc.PagePushBack(page2)` | `var page1 = document.AddPage()` + `var page2 = document.AddPage()` | Automatic | Each pushed page gets its own variable |
| `doc.Save(path, SDFDoc.SaveOptions.e_linearized)` | `document.Save(path)` | Automatic | Extra args dropped |
| `new ElementBuilder()` | kept as-is | Manual | Map to PXA page draw calls |
| `new ElementWriter()` / `writer.Begin(page)` | kept as-is | Manual | Content stream → page drawing |
| `writer.WriteElement(...)` | kept as-is | Manual | Review ElementBuilder source |
| `builder.CreateTextBegin/Run/End(...)` | kept as-is | Manual | → `page.DrawText(...)` after review |
| `builder.CreateImageFromFile(...)` | kept as-is | Manual | → `page.DrawImage(...)` after review |
| `builder.CreateRect/CreatePath(...)` | kept as-is | Manual | → `page.DrawRectangle(...)` after review |

## Diagnostic IDs

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGAPRYSE000` | Info | `PDFNet.Initialize(...)` removed |
| `CANMIGAPRYSE001` | Info | `new PDFDoc()` → `new PdfDocument()` |
| `CANMIGAPRYSE002` | Info | `PageCreate(...)` removed (AddPage creates and attaches) |
| `CANMIGAPRYSE003` | Info | `PagePushBack(...)` → `document.AddPage()` |
| `CANMIGAPRYSE004` | Info | `doc.Save(...)` → `document.Save(path)` — save flags removed |

## Unsupported / Manual Follow-Up

- [ ] ElementBuilder / ElementWriter content streams
- [ ] Low-level SDF object manipulation
- [ ] PDF editing and incremental save
- [ ] Forms (AcroForm / Field)
- [ ] Redaction
- [ ] Digital signatures
- [ ] OCR / conversion / viewer / rendering APIs
- [ ] Page-size arguments from `PageCreate(Rect)`
- [ ] Font and color state

## Sample Input

```csharp
using pdftron;
using pdftron.PDF;
using pdftron.SDF;

PDFNet.Initialize(licenseKey);

using var doc = new PDFDoc();

var page1 = doc.PageCreate(new Rect(0, 0, 612, 792));
doc.PagePushBack(page1);

var page2 = doc.PageCreate(new Rect(0, 0, 612, 792));
doc.PagePushBack(page2);

var builder = new ElementBuilder();
var writer  = new ElementWriter();

writer.Begin(page1);
var element = builder.CreateTextRun("Hello from Apryse SDK");
writer.WriteElement(element);
writer.End();

doc.Save(outputPath, SDFDoc.SaveOptions.e_linearized);
```

## Expected PXA.Pdf Output

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page1 = document.AddPage();
var page2 = document.AddPage();
var builder = new ElementBuilder();
var writer = new ElementWriter();
writer.Begin(page1);
var element = builder.CreateTextRun("Hello from Apryse SDK");
writer.WriteElement(element);
writer.End();
document.Save(outputPath);
```

## Code Fix Checklist

- [x] Remove `pdftron.*` usings, add `using PXA.Pdf;`
- [x] Remove `PDFNet.Initialize(...)`
- [x] Replace `new PDFDoc()` with `new PdfDocument()` (both `var` and `using var`)
- [x] Remove `doc.PageCreate(...)` statements (both expression and local-declaration forms)
- [x] Replace `doc.PagePushBack(pageVar)` with `var pageVar = document.AddPage()` — preserving variable name from argument
- [x] Replace `doc.Save(path, flags)` with `document.Save(path)` — drop extra arguments
- [ ] Emit diagnostics for ElementBuilder / ElementWriter kept statements
- [ ] Map `PageCreate(Rect)` page size to `PdfPagePreset`
- [ ] Map ElementBuilder text elements to `page.DrawText(...)`
- [ ] Map ElementBuilder image elements to `page.DrawImage(...)`
- [ ] Map ElementBuilder shape elements to `page.DrawRectangle(...)`

## Tests Checklist

- [x] Basic PDFDoc + PageCreate + PagePushBack + Save sample
- [x] Two-page sample (page1, page2 get distinct variable names)
- [x] `PDFNet.Initialize` removed
- [x] `using var doc` form handled
- [x] Save flags stripped, path preserved
- [x] ElementBuilder / ElementWriter kept as-is
- [x] WebApi migration-service smoke test
- [ ] Snapshot before/after migration sample
- [ ] SDF / annotation / signature diagnostic sample
