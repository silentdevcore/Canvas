# Canvas Migration: IronPDF

## V1 Pilot Analysis

- [x] V1 scope is a reporting migration, not an automatic HTML-to-Canvas rewriter.
- [x] Roslyn-backed migration is connected through `Canvas.WebApi` via framework id `IronPdf`.
- [x] `ChromePdfRenderer` and legacy `HtmlToPdf` renderer construction is detected.
- [x] `RenderHtmlAsPdf(...)` is detected and reported as manual HTML/CSS-to-Canvas work.
- [x] Literal HTML snippets are preserved in the report for manual extraction.
- [x] Dynamic HTML inputs are flagged for template/data-flow review.
- [x] `RenderHtmlFileAsPdf(...)`, `RenderUrlAsPdf(...)`, and Razor rendering calls are detected.
- [x] `SaveAs(...)` and `SaveAsAsync(...)` targets are detected for later Canvas `document.Save(...)` mapping.
- [x] PDF editing, merging, extraction, security, and signing APIs produce manual follow-up diagnostics.
- [x] WebApi conversion response includes report code, diagnostics, and summary counts.
- [ ] V1 intentionally keeps the original IronPDF source code after the migration report.
- [ ] V1 does not attempt to parse or render HTML/CSS/JavaScript.
- [ ] Future hardening: add optional HTML literal extraction helpers for simple headings/paragraphs/tables.
- [ ] Future hardening: preserve report entries as structured JSON in addition to comment output.

## Package / API Identification

- [x] NuGet packages:
  - [x] `IronPdf`
- [x] Common namespaces to detect:
  - [x] `IronPdf`
  - [x] `IronPdf.Rendering`
- [x] Common classes to detect:
  - [x] `ChromePdfRenderer`
  - [x] `PdfDocument`
  - [x] `Installation`
  - [x] `RenderingOptions`
  - [x] `HtmlToPdf`
  - [x] `PdfSignature`
  - [x] `SecuritySettings`

## Roslyn Prototype Status

- [x] Add `src/Canvas.Migration.IronPdf`
- [x] Add `tests/Canvas.Migration.IronPdf.Tests`
- [x] Add projects to `Canvas.sln`
- [x] Implement first source migration entry point: `IronPdfMigration`
- [x] Generate a Canvas.Pdf migration report comment while preserving original IronPDF source
- [x] Detect `ChromePdfRenderer` / `HtmlToPdf`
- [x] Detect literal and dynamic `RenderHtmlAsPdf(...)`
- [x] Detect `RenderHtmlFileAsPdf(...)`
- [x] Detect `RenderUrlAsPdf(...)`
- [x] Detect Razor rendering calls
- [x] Detect `SaveAs(...)` and `SaveAsAsync(...)`
- [x] Warn for editing/merge/security/signing-style APIs
- [x] Connect WebApi IronPDF converter to the Roslyn reporting migration engine
- [x] Add WebApi migration-service smoke test for IronPDF summary/diagnostics
- [x] Verified with `dotnet test tests/Canvas.Migration.IronPdf.Tests/Canvas.Migration.IronPdf.Tests.csproj --no-restore --no-build`: `7/7` passed
- [x] Verified with `dotnet test tests/Canvas.Api.Tests/Canvas.Api.Tests.csproj --no-restore --no-build`: `18/18` passed
- [ ] Replace syntax-only matching with semantic matching before broad rollout

## Mapping Table Placeholders

| IronPDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new ChromePdfRenderer()` | Migration report entry | Report-only | HTML rendering is not a direct API match |
| `new HtmlToPdf()` | Migration report entry | Report-only | Legacy renderer pattern |
| `renderer.RenderHtmlAsPdf(html)` | Manual Canvas layout | Report-only | Requires HTML-to-Canvas conversion strategy |
| `renderer.RenderHtmlAsPdf("<h1>Hello</h1>")` | Manual Canvas layout | Report-only | Literal HTML is surfaced for extraction |
| `renderer.RenderHtmlFileAsPdf(path)` | Manual Canvas layout | Report-only | Requires template file review |
| `renderer.RenderUrlAsPdf(url)` | Unsupported/manual | Report-only | Out of scope for direct code fix |
| `renderer.RenderRazorToPdf(...)` | Unsupported/manual | Report-only | Requires Razor view/model review |
| `pdf.SaveAs(...)` | `document.Save(...)` | Report-only | Save target is detected for later manual rewrite |
| `pdf.SaveAsAsync(...)` | Async save strategy review | Report-only | Canvas async save strategy not defined in v1 |

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGIRONPDF001` | Info | IronPDF renderer construction detected | No |
| `CANMIGIRONPDF002` | Warning | HTML rendering requires manual Canvas layout migration | No |
| `CANMIGIRONPDF003` | Warning | HTML file rendering requires manual template review | No |
| `CANMIGIRONPDF004` | Warning | URL-to-PDF rendering is outside direct Canvas source migration | No |
| `CANMIGIRONPDF005` | Warning | Razor-to-PDF rendering requires manual template/view migration | No |
| `CANMIGIRONPDF006` | Info | `SaveAs(...)` target detected | No |
| `CANMIGIRONPDF007` | Info | `SaveAsAsync(...)` target detected | No |
| `CANMIGIRONPDF020` | Warning | Editing, merge, extraction, security, or signing APIs are outside v1 scope | No |

## Unsupported / Manual Follow-Up

- [x] URL-to-PDF rendering
- [x] Browser-based HTML/CSS rendering
- [x] JavaScript rendering
- [x] Header/footer HTML templates
- [x] PDF editing/merging APIs
- [x] Security and signing APIs

## Sample Input Snippets

```csharp
using IronPdf;

var renderer = new ChromePdfRenderer();
var pdf = renderer.RenderHtmlAsPdf("<h1>Hello</h1>");
pdf.SaveAs(path);
```

## Expected Canvas.Pdf Output Snippets

```csharp
// Canvas.Pdf migration report: IronPDF
// IronPDF commonly renders HTML/CSS through Chromium. Canvas.Pdf is a drawing API,
// so v1 keeps the original code and reports the manual rewrite work instead of
// producing misleading Canvas draw calls.
// - Detected ChromePdfRenderer/HtmlToPdf renderer construction.
// - RenderHtmlAsPdf: Literal HTML detected for manual extraction: <h1>Hello</h1>
// - SaveAs(...) detected. Keep this as the final Canvas document.Save(...) target after manual rewrite.

using IronPdf;

var renderer = new ChromePdfRenderer();
var pdf = renderer.RenderHtmlAsPdf("<h1>Hello</h1>");
pdf.SaveAs(path);
```

## Analyzer Diagnostics Checklist

- [x] Detect IronPDF renderer construction
- [x] Detect HTML rendering calls
- [x] Warn that HTML/CSS layout requires manual migration
- [x] Detect simple literal HTML that may be manually simplified
- [x] Warn on URL rendering calls
- [x] Warn on PDF editing/security APIs

## Code Fix Checklist

- [x] Add diagnostics before any automatic conversion
- [x] Offer no automatic code fix for browser-rendered HTML in v1
- [x] Add `using Canvas.Pdf` only for confirmed replacements
- [x] Emit manual migration report entries
- [x] Preserve original HTML snippets for review
- [x] Preserve original source code after report
- [ ] Add optional generated Canvas skeleton only when user explicitly asks

## Tests Checklist

- [x] Basic HTML render diagnostic sample
- [x] Dynamic HTML render diagnostic sample
- [x] URL render unsupported sample
- [x] HTML file render unsupported sample
- [x] Razor render unsupported sample
- [x] Save call sample
- [x] Async save call sample
- [x] Literal HTML report sample
- [x] PDF editing/security diagnostic sample
- [x] WebApi migration-service smoke test
- [ ] Snapshot before/after diagnostic sample
