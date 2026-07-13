# PXA Migration: IronPDF

## V1 Implementation Status

- [x] V1 scope: Roslyn-backed pilot that generates a compilable PXA.Pdf scaffold and provides diagnostics for manual HTML draw call migration.
- [x] Roslyn-backed migration connected through `PXA.WebApi` via framework id `IronPdf`.
- [x] Status: **Pilot** — HTML rendering is not automatically convertible to PXA draw calls; scaffold + diagnostics are provided instead.
- [x] `ChromePdfRenderer` / `HtmlToPdf` creation → `var document = new PdfDocument(); var page = document.AddPage();`
- [x] Chained form `new ChromePdfRenderer().RenderHtmlAsPdf(...)` → scaffold generated on the render call statement.
- [x] `renderer.RenderHtmlAsPdf(html)` → removed; HTML content preserved in CANMIGIRONPDF002 diagnostic message.
- [x] `renderer.RenderHtmlFileAsPdf(path)` → removed with CANMIGIRONPDF003 warning.
- [x] `renderer.RenderUrlAsPdf(url)` → removed with CANMIGIRONPDF004 warning.
- [x] `renderer.RenderRazorToPdf(...)` / `RenderRazorViewToPdf(...)` → removed with CANMIGIRONPDF005 warning.
- [x] `pdf.SaveAs(path)` → `document.Save(path)`.
- [x] `await pdf.SaveAsAsync(path)` → `document.Save(path)` (synchronous; CANMIGIRONPDF007 info diagnostic).
- [x] `renderer.RenderingOptions.X = ...` property assignments removed (renderer is gone after migration).
- [x] IronPDF editing/merge/signing calls (`Merge`, `AppendPdf`, `SignPdfWithDigitalSignature`) → kept with CANMIGIRONPDF020 warning.
- [x] All `IronPdf` / `IronPdf.Rendering` usings removed; `using PXA.Pdf;` added.
- [x] Scan for unsupported identifiers (`PdfMerger`, `SecuritySettings`, `PdfSignature`, etc.) → CANMIGIRONPDF020 warning.
- [ ] V1 does not parse or render HTML/CSS/JavaScript.
- [ ] V1 does not generate PXA draw calls from HTML element analysis.
- [ ] Future: add optional HTML literal extraction for simple headings/paragraphs.

## Package / API Identification

- [x] NuGet packages:
  - [x] `IronPdf`
- [x] Common namespaces to detect:
  - [x] `IronPdf`
  - [x] `IronPdf.Rendering`
- [x] Common classes to detect:
  - [x] `ChromePdfRenderer`
  - [x] `HtmlToPdf` (legacy)
  - [x] `PdfDocument` (IronPdf)
  - [x] `PdfSignature`
  - [x] `SecuritySettings`
  - [x] `PdfMerger`

## Roslyn Implementation Status

- [x] Add `src/PXA.Migration.Pdf.Code.IronPdf`
- [x] Add `tests/PXA.Migration.Pdf.Code.IronPdf.Tests`
- [x] Add projects to `PXA.sln`
- [x] Implement `IronPdfMigration` as a real `CSharpSyntaxRewriter`
- [x] Pre-scan phase: find renderer var, pdf var, save target
- [x] `VisitCompilationUnit` override for one-to-many statement replacement
- [x] Convert renderer creation → `var document = new PdfDocument(); var page = document.AddPage();`
- [x] Handle chained form: `new ChromePdfRenderer().RenderXxx(...)`
- [x] Remove render calls (`RenderHtmlAsPdf`, `RenderHtmlFileAsPdf`, `RenderUrlAsPdf`, `RenderRazorToPdf`)
- [x] Preserve HTML literal content in diagnostic message
- [x] Remove `renderer.RenderingOptions.*` property assignments
- [x] Convert `pdf.SaveAs(path)` → `document.Save(path)`
- [x] Convert `await pdf.SaveAsAsync(path)` → `document.Save(path)`
- [x] Warn and keep editing/security/signing calls
- [x] Remove IronPdf usings, add `using PXA.Pdf;`
- [x] Connect WebApi IronPDF converter to the Roslyn migration engine
- [x] Verified with `dotnet test tests/PXA.Migration.Pdf.Code.IronPdf.Tests`: `11/11` passed
- [x] Verified with `dotnet test tests/PXA.Api.Tests`: `22/22` passed

## Mapping Table

| IronPDF API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new ChromePdfRenderer()` | `new PdfDocument()` + `AddPage()` | Automatic | Scaffold only; HTML content needs manual translation |
| `new HtmlToPdf()` | `new PdfDocument()` + `AddPage()` | Automatic | Legacy renderer |
| `new ChromePdfRenderer().RenderHtmlAsPdf(...)` | `new PdfDocument()` + `AddPage()` | Automatic | Chained form |
| `renderer.RenderHtmlAsPdf(html)` | Removed + CANMIGIRONPDF002 warning | Manual | HTML content in diagnostic |
| `renderer.RenderHtmlFileAsPdf(path)` | Removed + CANMIGIRONPDF003 warning | Manual | |
| `renderer.RenderUrlAsPdf(url)` | Removed + CANMIGIRONPDF004 warning | Manual | URL rendering out of scope |
| `renderer.RenderRazorToPdf(...)` | Removed + CANMIGIRONPDF005 warning | Manual | Razor template review required |
| `renderer.RenderingOptions.X = ...` | Removed | Automatic | Renderer is gone after migration |
| `pdf.SaveAs(path)` | `document.Save(path)` | Automatic | |
| `await pdf.SaveAsAsync(path)` | `document.Save(path)` | Automatic | Drops async; CANMIGIRONPDF007 info |
| PDF editing/merge/signing APIs | Kept + warning | Manual | |

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGIRONPDF001` | Info | ChromePdfRenderer/HtmlToPdf → PdfDocument + AddPage scaffold | Yes |
| `CANMIGIRONPDF002` | Warning | RenderHtmlAsPdf — HTML rendering requires manual PXA draw call migration | No |
| `CANMIGIRONPDF003` | Warning | RenderHtmlFileAsPdf — HTML template requires manual review | No |
| `CANMIGIRONPDF004` | Warning | RenderUrlAsPdf — URL rendering is outside PXA.Pdf scope | No |
| `CANMIGIRONPDF005` | Warning | RenderRazorToPdf — Razor template requires manual migration | No |
| `CANMIGIRONPDF006` | Info | `SaveAs(path)` → `document.Save(path)` | Yes |
| `CANMIGIRONPDF007` | Info | `SaveAsAsync(path)` → `document.Save(path)` (sync) | Yes |
| `CANMIGIRONPDF020` | Warning | PDF editing, merge, security, or signing APIs outside v1 | No |

## Unsupported / Manual Follow-Up

- [ ] HTML/CSS/JavaScript rendering
- [ ] Header/footer HTML templates
- [ ] PDF editing/merging APIs
- [ ] Security and signing APIs
- [ ] URL-to-PDF rendering
- [ ] Razor template rendering
- [ ] HTML literal extraction to PXA draw calls

## Sample Input

```csharp
using IronPdf;

var renderer = new ChromePdfRenderer();
renderer.RenderingOptions.MarginTop = 20;
var pdf = renderer.RenderHtmlAsPdf("<h1>Invoice #2024</h1><p>Total: $150</p>");
pdf.SaveAs(outputPath);
```

## Expected PXA.Pdf Output

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
document.Save(outputPath);
```

**Diagnostics emitted:**
- CANMIGIRONPDF001 Info: ChromePdfRenderer → PdfDocument + AddPage
- CANMIGIRONPDF002 Warning: RenderHtmlAsPdf(`<h1>Invoice #2024</h1><p>Total: $150</p>`) — manually add PXA draw calls
- CANMIGIRONPDF006 Info: SaveAs(outputPath) → document.Save(outputPath)

## Tests Checklist

- [x] Basic HTML render workflow → PXA scaffold
- [x] Literal HTML content preserved in diagnostic message
- [x] Dynamic HTML rendering warning
- [x] Chained `new ChromePdfRenderer().RenderHtmlAsPdf(...)` form
- [x] HTML file rendering warning
- [x] URL rendering warning
- [x] Razor rendering warning
- [x] Async SaveAsAsync → sync document.Save
- [x] RenderingOptions property assignments removed
- [x] Editing/signing API warning
- [x] Legacy HtmlToPdf renderer
- [x] WebApi migration-service smoke test
