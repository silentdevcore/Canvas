# PXA Migration: PDF Toolbox SDK / Toolbox Add-On

## V1 Pilot Analysis

- [x] Add cautious Roslyn-backed provider project: `src/PXA.Migration.PdfToolsToolbox`
- [x] Add provider tests: `tests/PXA.Migration.PdfToolsToolbox.Tests`
- [x] Connect WebApi converter: `PXA.WebApi/Services/Converters/PdfToolsToolboxConverter.cs`
- [x] Add UI fallback status/example as `pilot`
- [x] Split from `PDFTools / Pdftools SDK`; Toolbox is the direct-generation/content-editing API family
- [x] Confirm official direct-generation entry points:
  - [x] `PdfTools.Toolbox.Pdf.Document.Create(...)`
  - [x] `PdfTools.Toolbox.Pdf.Page.Create(...)`
- [x] Confirm sample-level content APIs:
  - [x] `ContentGenerator`
  - [x] `Text.Create(...)`
  - [x] `TextGenerator`
  - [x] `Font.CreateFromSystem(...)`
- [x] Validate exact .NET package/reference names for current Toolbox add-on version
- [x] Validate simple from-scratch generation sample shape before implementing automatic rewrites

The PDF Toolbox SDK / Toolbox add-on is the PDF Tools family member that supports creating PDFs from scratch and adding page-level content. This must remain separate from `PdfTools` / Pdftools SDK, which is primarily conversion, optimization, validation, signing, rendering, and existing-document processing.

References:

- https://www.pdf-tools.com/docs/pdf-toolbox-sdk/overview/
- https://www.pdf-tools.com/docs/pdf-toolbox-sdk/use/generate/
- https://www.pdf-tools.com/docs/pdf-toolbox-sdk/code-samples/
- https://www.pdf-tools.com/docs/toolbox-add-on/getting-started/
- https://www.nuget.org/packages/PdfTools.Toolbox/1.11.0
- https://api-reference.pdf-tools.com/pdfsdkxt/1.11/dotnet/html/T_PdfTools_Toolbox_Pdf_Content_ContentGenerator.htm

## Package / API Identification

- [x] NuGet packages / references:
  - [x] `PdfTools.Toolbox`
  - [x] Assembly: `PdfTools.Toolbox.dll`
  - [x] Current NuGet package observed: `PdfTools.Toolbox` 1.11.0
  - [x] API uses `PdfTools.Toolbox.*` namespaces from a separate Toolbox package/add-on
- [x] Common namespaces to detect:
  - [x] `PdfTools.Toolbox`
  - [x] `PdfTools.Toolbox.Pdf`
  - [x] `PdfTools.Toolbox.Pdf.Content`
  - [x] `PdfTools.Toolbox.Pdf.Content.Text`
  - [x] `PdfTools.Toolbox.Pdf.Graphics`
- [x] Common classes to detect:
  - [x] `Document`
  - [x] `Page`
  - [x] `PageList`
  - [x] `ContentGenerator`
  - [x] `Text`
  - [x] `TextGenerator`
  - [x] `Font`
  - [x] `Point`
  - [x] `Paint`
  - [x] `Fill`
  - [x] `ColorSpace`
  - [x] `PageCopyOptions`

## Mapping Table

| PDF Toolbox SDK API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `Document.Create(outStream, ..., ...)` | `var document = new PdfDocument();` | Candidate pilot code fix | Stream ownership/save semantics still need conservative handling. |
| `Page.Create(document, PageSize.A4/Letter)` | `var page = document.AddPage(PdfPagePreset.A4/Letter, landscape)` | Pilot code fix | A4/Letter and rotated/landscape cases mapped. |
| `Page.Create(document, customSize)` | `var page = document.AddPage()` | Warning in v1 | Unknown page sizes require manual review. |
| `outDoc.Pages.Add(page)` | Already represented by `document.AddPage()` | Future removal/code fix | Only remove if page creation was converted. |
| `Font.CreateFromSystem(...)` | PXA text font settings or default font | Warning or future mapping | PXA font API and fallback behavior need review. |
| `Text.Create(document)` + `TextGenerator` + `ShowLine(...)` | `page.DrawTextFromTop(...)` | Future pilot code fix | Need coordinate extraction from `MoveTo(Point)` and text extraction from `ShowLine`. |
| `ContentGenerator(...).PaintText(text)` | Already represented by `page.DrawText...` | Future removal/code fix | Only after associated text flow is converted. |
| `Page.Copy(...)`, `PageList.Copy(...)`, `Document.Open(...)` | Manual migration | Warning in v1 | Existing-PDF copy/edit workflows are not PXA generation. |
| `Add annotations/forms/metadata/outlines` | Manual migration | Warning in v1 | Needs PXA feature parity review. |

## Unsupported / Manual Follow-Up

- [x] Existing PDF copying/editing/tagging workflows
- [x] `Document.Open(...)` + `Page.Copy(...)` processing flows
- [x] Annotations
- [x] Forms
- [x] Metadata and viewer settings
- [x] Outlines/bookmarks/destinations
- [x] Tagged PDF / PDF/UA workflows
- [x] Color spaces, transparency, paint/fill details
- [x] Barcodes/images/watermarks
- [x] Embedded files
- [ ] Simple from-scratch `Document.Create` + `Page.Create` + text layout after sample validation
- [x] Simple from-scratch sample shape from official docs

## Sample Input Snippets

```csharp
using PdfTools.Toolbox.Pdf;
using PdfTools.Toolbox.Pdf.Content;
using PdfTools.Toolbox.Pdf.Content.Text;

using var outStream = new FileStream(outPath, FileMode.CreateNew, FileAccess.ReadWrite);
using var outDoc = Document.Create(outStream, null, null);

var font = Font.CreateFromSystem(outDoc, "Arial", "Italic", true);
var outPage = Page.Create(outDoc, PageSize);

using var gen = new ContentGenerator(outPage.Content, false);
var text = Text.Create(outDoc);
using var textGenerator = new TextGenerator(text, font, 20, null);
textGenerator.MoveTo(new Point { X = 72, Y = outPage.Size.Height - 72 });
textGenerator.ShowLine("Hello from Toolbox");
gen.PaintText(text);

outDoc.Pages.Add(outPage);
```

## Expected PXA.Pdf Output Snippets

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawTextFromTop("Hello from Toolbox", 72, 72, 20);
document.Save(outPath);
```

## Analyzer Diagnostics Checklist

| Diagnostic | Severity | Status | Purpose |
| --- | --- | --- | --- |
| `CANMIGPDFTOOLBOX000` | Warning | [x] | Cautious pilot warning until Toolbox package/version and samples are validated |
| `CANMIGPDFTOOLBOX001` | Info | [x] | `Document.Create(...)` converted |
| `CANMIGPDFTOOLBOX002` | Info | [x] | `Page.Create(...)` / `Pages.Add(...)` converted |
| `CANMIGPDFTOOLBOX003` | Info/Warning | [x] | TextGenerator flow converted or flagged |
| `CANMIGPDFTOOLBOX004` | Warning | [x] | Font/color/paint style requires manual review |
| `CANMIGPDFTOOLBOX005` | Warning | [ ] | Image/barcode/watermark requires manual migration |
| `CANMIGPDFTOOLBOX006` | Warning | [x] | Forms/annotations/metadata/outlines/tagging require manual migration |
| `CANMIGPDFTOOLBOX007` | Info | [x] | Output stream target detected and `document.Save(...)` inserted |
| `CANMIGPDFTOOLBOX008` | Warning | [x] | Output path could not be safely inferred; save must be added manually |
| `CANMIGPDFTOOLBOX009` | Warning | [x] | Unknown page size requires manual review |
| `CANMIGPDFTOOLBOX010` | Warning | [x] | Toolbox code remains after partial migration; Toolbox usings preserved |
| `CANMIGPDFTOOLBOX020` | Warning | [x] | Existing-PDF copy/edit/tag workflows require manual migration |

## Code Fix Checklist

- [x] Replace confirmed from-scratch `Document.Create(...)`
- [x] Replace confirmed `Page.Create(...)` + `outDoc.Pages.Add(...)`
- [x] Map simple A4/Letter page sizes and landscape/rotate cases
- [x] Warn on custom page sizes
- [x] Convert simple `TextGenerator.MoveTo(...)` + `ShowLine(...)` + `PaintText(...)`
- [x] Remove associated `FileStream` output setup when `outPath` can be recovered safely
- [x] Insert `document.Save(outPath)` when a safe output path is recovered
- [x] Warn when output stream target cannot be safely mapped to a path
- [x] Add `using PXA.Pdf` only when PXA code is introduced
- [x] Remove Toolbox usings only when no Toolbox code remains
- [x] Preserve Toolbox usings for partially migrated snippets
- [ ] Warn on font/style/color details until PXA style mapping is defined
- [ ] Warn on existing-PDF copy/edit/tag workflows
- [ ] Warn on forms/annotations/metadata/outlines/tagging

## Tests Checklist

- [x] Real Toolbox package/reference identification sample
- [x] Basic from-scratch document/page/text sample shape
- [x] `TextGenerator.MoveTo(Point)` coordinate extraction test
- [x] Safe output path save insertion test
- [x] Unknown output stream warning test
- [x] Known page size mapping test
- [x] Unknown page size warning test
- [x] Partial migration preserves Toolbox usings test
- [ ] Font/style manual diagnostic sample
- [ ] Existing-PDF `Document.Open` + `Page.Copy` diagnostic sample
- [ ] Annotation/form/metadata/outlines/tagging diagnostic sample
- [x] WebApi smoke test
- [ ] Snapshot before/after migration sample
