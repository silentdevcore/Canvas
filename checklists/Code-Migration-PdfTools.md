# PXA Migration: PDFTools / Pdftools SDK

## V1 Pilot Analysis

- [x] Add cautious Roslyn-backed provider project: `src/Migrations/PDF/PXA.Migration.Pdf.Code.PdfTools`
- [x] Add provider tests: `tests/PXA.Migration.Pdf.Code.PdfTools.Tests`
- [x] Connect WebApi converter: `PXA.WebApi/Services/Converters/PdfToolsConverter.cs`
- [x] Add UI fallback status/example as `pilot`
- [x] Confirm current .NET package name from official docs: `PdfTools`
- [x] Confirm current SDK initialization pattern from official docs: `Sdk.Initialize(...)`
- [x] Validate from official docs/API references that direct PDF generation belongs to PDF Toolbox SDK/add-on, not this Pdftools SDK provider

The official Pdftools SDK .NET getting-started guide identifies the NuGet package as `PdfTools`, requires .NET/Core 2.0+ or .NET Framework 4.6.1+, and documents optional `Sdk.Initialize("YOUR_LICENSE_KEY")` startup initialization for non-watermarked production output. The public samples emphasize PDF operations such as PDF-to-image conversion and broader SDK workflows. Official API references identify `PdfTools.Pdf.Document` as a document opened from a stream or produced by operations, while PDF creation from scratch belongs to the separate PDF Toolbox SDK/add-on (`PdfTools.Toolbox.Pdf.Document.Create`, `Page.Create`). V1 therefore removes SDK initialization where safe and reports SDK workflows as manual migration work instead of inventing direct PXA.Pdf rewrites.

Reference: https://www.pdf-tools.com/docs/pdf-tools-sdk/getting-started/pdftools-sdk/pdftools-sdk-dotnet/
Reference: https://www.pdf-tools.com/docs/pdf-toolbox-sdk/use/generate/

## Package / API Identification

- [x] NuGet packages:
  - [x] `PdfTools`
- [x] Current documented SDK version line:
  - [x] Pdftools SDK documentation version `1.17`
- [x] Common namespaces to detect:
  - [x] `PdfTools`
  - [x] `PdfTools.*`
- [x] Initialization APIs to detect:
  - [x] `Sdk.Initialize(...)`
- [x] Confirmed SDK document model:
  - [x] `PdfTools.Pdf.Document` is opened from a stream or returned by SDK operations
  - [x] Direct document/page creation is in `PdfTools.Toolbox.Pdf`, a separate Toolbox add-on API
- [x] Manual-only workflow areas to flag:
  - [x] PDF-to-image conversion
  - [x] PDF conversion pipelines
  - [x] Existing PDF processing/editing
  - [x] Optimization/validation/repair workflows
  - [x] OCR/raster workflows, if present through samples or add-ons

## Mapping Table

| PDFTools / Pdftools SDK API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `Sdk.Initialize(...)` | No PXA equivalent | Code removal in v1 | Emit info diagnostic; PXA.Pdf has no SDK license initialization call. |
| `PdfTools.Pdf.Document.Open(...)` | Manual PXA.Pdf document recreation | Warning in v1 | Existing-PDF processing, not from-scratch PXA generation. |
| SDK conversion/optimization/validation/signing APIs | Manual PXA.Pdf document recreation or workflow redesign | Warning in v1 | Not equivalent to PXA.Pdf draw calls. |
| `PdfTools.Toolbox.Pdf.Document.Create(...)` / `Page.Create(...)` | Future Toolbox-specific migration | Warning in v1 | Separate product/add-on; collect Toolbox samples before automatic rewrites. |
| PDF-to-image / conversion samples such as `Pdf2ImgSimple` | Manual rewrite to PXA document generation | Warning in v1 | Not equivalent to PXA.Pdf generation; requires product-specific redesign. |
| Existing PDF load/process/optimize/validate/repair workflows | Manual follow-up | Warning in v1 | PXA.Pdf migration target is generated document composition. |

## Unsupported / Manual Follow-Up

- [x] PDF-to-image rendering/conversion
- [x] Office/image/HTML conversion workflows, if used through SDK samples
- [x] Existing PDF loading/editing/processing
- [x] Optimization, validation, repair, archival/conformance workflows
- [x] Digital signatures/certificates
- [x] Security/encryption/decryption
- [x] Forms
- [x] Annotations/bookmarks/outlines
- [x] Attachments/portfolio APIs
- [x] OCR/raster pipelines
- [ ] Direct document-generation APIs after real sample collection

## Sample Input Snippets

```csharp
using PdfTools;
using PdfTools.Pdf;

Sdk.Initialize(licenseKey);

using var input = File.OpenRead(inputPath);
using var document = Document.Open(input, null);
document.Save(outputPath);
```

## Expected Migrated Output Snippets

```csharp
using PdfTools;
using PdfTools.Pdf;

using var input = File.OpenRead(inputPath);
using var document = Document.Open(input, null);
document.Save(outputPath);
```

## Analyzer Diagnostics Checklist

| Diagnostic | Severity | Status | Purpose |
| --- | --- | --- | --- |
| `CANMIGPDFTOOLS000` | Warning | [x] | Pdftools SDK is not treated as a direct-generation API |
| `CANMIGPDFTOOLS001` | Info | [x] | SDK initialization removed |
| `CANMIGPDFTOOLS002` | Info | [ ] | Reserved; document creation conversion not implemented for SDK provider |
| `CANMIGPDFTOOLS003` | Info | [ ] | Reserved; page creation conversion not implemented for SDK provider |
| `CANMIGPDFTOOLS004` | Info/Warning | [ ] | Reserved; text drawing conversion not implemented for SDK provider |
| `CANMIGPDFTOOLS005` | Warning | [ ] | Reserved; image/raster direct drawing not implemented for SDK provider |
| `CANMIGPDFTOOLS006` | Info/Warning | [ ] | Reserved; shape drawing conversion not implemented for SDK provider |
| `CANMIGPDFTOOLS007` | Info | [ ] | Reserved; save/export conversion not implemented for SDK provider |
| `CANMIGPDFTOOLS020` | Warning | [x] | Conversion, optimization, validation, signing, security, forms, or annotations require manual migration |
| `CANMIGPDFTOOLS021` | Warning | [x] | Existing-PDF editing/processing requires manual migration |
| `CANMIGPDFTOOLS022` | Warning | [x] | PDF Toolbox SDK/add-on direct generation requires separate sample collection |

## Code Fix Checklist

- [ ] Replace direct document creation only in a future Toolbox-specific provider or mode
- [ ] Replace direct page creation only in a future Toolbox-specific provider or mode
- [ ] Replace simple text drawing only after Toolbox samples are collected
- [ ] Replace simple line/rectangle drawing only after Toolbox samples are collected
- [ ] Replace simple save/export only after Toolbox samples are collected
- [x] Remove `Sdk.Initialize(...)`
- [ ] Add `using PXA.Pdf` only when a real PXA code fix is introduced
- [ ] Remove PDFTools/Pdftools usings only when source is fully rewritten
- [x] Leave conversion/editing/processing workflows as manual diagnostics
- [x] Validate SDK-vs-Toolbox boundary against official docs/API references

## Tests Checklist

- [x] Real SDK processing sample shape
- [x] SDK initialization removal sample
- [ ] Basic direct document/page/text/save sample belongs to future Toolbox-specific validation
- [ ] Line/rectangle drawing sample belongs to future Toolbox-specific validation
- [x] Conversion workflow diagnostic sample
- [x] Existing-PDF processing diagnostic sample
- [x] Security/forms/signature/annotation diagnostic sample
- [x] Toolbox direct-generation boundary diagnostic sample
- [x] WebApi smoke test
- [ ] Snapshot before/after migration sample
