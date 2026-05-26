# Canvas Migration: DevExpress PDF

## V1 Pilot Analysis

- [x] V1 scope is a reporting migration, not a broad automatic DevExpress rewrite.
- [x] Roslyn-backed migration is connected through `Canvas.WebApi` via framework id `DevExpress`.
- [x] `PdfDocumentProcessor` construction is detected.
- [x] Generated-document candidates are detected through `CreateEmptyDocument(...)`.
- [x] Drawing candidates are detected through `CreateGraphics(...)`, `DrawString(...)`, `DrawLine(...)`, and `DrawRectangle(...)`.
- [x] Page creation candidates are detected through `RenderNewPage(...)`.
- [x] Save targets are detected through `SaveDocument(...)`.
- [x] Existing-PDF editing/processing APIs are reported as manual follow-up.
- [x] Forms, signatures, encryption, annotations, and bookmarks are reported as unsupported in v1.
- [x] DevExpress reporting/export workflows are reported as manual template migration work.
- [x] WebApi conversion response includes report code, diagnostics, and summary counts.
- [ ] V1 intentionally keeps the original DevExpress source code after the migration report.
- [ ] V1 does not resolve DevExpress overloads semantically.
- [ ] Future hardening: add deterministic rewrite for simple generated-document `PdfDocumentProcessor` samples.
- [ ] Future hardening: map DevExpress drawing units, page sizes, fonts, and brushes explicitly.

## Package / API Identification

- [x] NuGet packages:
  - [x] `DevExpress.Pdf`
  - [x] Related DevExpress Drawing packages
  - [x] DevExpress reporting/export packages
- [x] Common namespaces to detect:
  - [x] `DevExpress.Pdf`
  - [x] `DevExpress.Drawing`
  - [x] `DevExpress.XtraReports.UI`
- [x] Common classes to detect:
  - [x] `PdfDocumentProcessor`
  - [x] `PdfGraphics`
  - [x] `PdfRectangle`
  - [x] `PdfFont`
  - [x] `PdfAcroForm`
  - [x] `PdfDocumentSigner`
  - [x] `PdfEncryptionOptions`
  - [x] `XtraReport`
  - [x] `PdfExportOptions`

## Roslyn Prototype Status

- [x] Add `src/Canvas.Migration.DevExpressPdf`
- [x] Add `tests/Canvas.Migration.DevExpressPdf.Tests`
- [x] Add projects to `Canvas.sln`
- [x] Implement first source migration entry point: `DevExpressPdfMigration`
- [x] Generate a Canvas.Pdf migration report comment while preserving original DevExpress source
- [x] Detect `PdfDocumentProcessor`
- [x] Detect `CreateEmptyDocument(...)`
- [x] Detect `CreateGraphics(...)`
- [x] Detect `RenderNewPage(...)`
- [x] Detect `DrawString(...)`, `DrawLine(...)`, and `DrawRectangle(...)`
- [x] Detect `SaveDocument(...)`
- [x] Warn for existing-PDF load/page-edit operations
- [x] Warn for forms/signatures/encryption/annotations/bookmarks
- [x] Warn for report export workflows
- [x] Connect WebApi DevExpress converter to the Roslyn reporting migration engine
- [x] Add WebApi migration-service smoke test for DevExpress summary/diagnostics
- [x] Verified with `dotnet test tests/Canvas.Migration.DevExpressPdf.Tests/Canvas.Migration.DevExpressPdf.Tests.csproj --no-restore --no-build`: `5/5` passed
- [x] Verified with `dotnet test tests/Canvas.Api.Tests/Canvas.Api.Tests.csproj --no-restore --no-build`: `19/19` passed
- [ ] Replace syntax-only matching with semantic matching before broad rollout

## Mapping Table Placeholders

| DevExpress PDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PdfDocumentProcessor()` | `new Canvas.Pdf.PdfDocument()` | Report-only | Processor may generate or edit PDFs; v1 reports only |
| `processor.CreateEmptyDocument(...)` | `new PdfDocument()` | Report-only | Generated-document candidate |
| `processor.CreateGraphics()` | `PdfPage` drawing surface | Report-only | Canvas drawing should move to `document.AddPage()` result |
| `processor.RenderNewPage(...)` | `document.AddPage(...)` | Report-only | Page size needs review |
| `graphics.DrawString(...)` | `page.DrawTextFromTop(...)` | Report-only | Coordinates, font, and brush need review |
| `graphics.DrawLine(...)` | `page.DrawLine(...)` | Report-only | Coordinate system needs review |
| `graphics.DrawRectangle(...)` | `page.DrawRectangle(...)` | Report-only | Coordinate system and fill/stroke need review |
| `processor.SaveDocument(...)` | `document.Save(...)` | Report-only | Save target is detected |
| `report.ExportToPdf(...)` | Manual Canvas generation | Manual | Requires report template review |
| `processor.LoadDocument(...)` / page edits | Manual/import workflow | Manual | Existing-PDF processing is outside v1 |

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGDEVEXP001` | Info | `PdfDocumentProcessor` construction detected | No |
| `CANMIGDEVEXP002` | Info | `CreateEmptyDocument(...)` generated-document candidate detected | No |
| `CANMIGDEVEXP003` | Info | `CreateGraphics(...)` drawing candidate detected | No |
| `CANMIGDEVEXP004` | Info | `RenderNewPage(...)` page creation candidate detected | No |
| `CANMIGDEVEXP005` | Info | `DrawString(...)` text drawing candidate detected | No |
| `CANMIGDEVEXP006` | Info | `DrawLine(...)` line drawing candidate detected | No |
| `CANMIGDEVEXP007` | Info | `DrawRectangle(...)` rectangle drawing candidate detected | No |
| `CANMIGDEVEXP008` | Info | `SaveDocument(...)` save target detected | No |
| `CANMIGDEVEXP020` | Warning | Report export workflow requires manual migration | No |
| `CANMIGDEVEXP021` | Warning | Existing-PDF processing/page editing is outside v1 | No |
| `CANMIGDEVEXP022` | Warning | Forms, signatures, encryption, annotations, or bookmarks are outside v1 | No |

## Unsupported / Manual Follow-Up

- [x] Existing PDF processing/editing
- [x] AcroForms
- [x] Document merge/split APIs
- [x] Digital signatures
- [x] Encryption
- [x] Advanced printing/rendering workflows

## Sample Input Snippets

```csharp
using DevExpress.Pdf;
using DevExpress.Drawing;

using var processor = new PdfDocumentProcessor();
processor.CreateEmptyDocument();
using var graphics = processor.CreateGraphics();
graphics.DrawString("Hello", new DXFont("Arial", 12), DXBrushes.Black, 40, 40);
processor.RenderNewPage(PdfPaperSize.A4, graphics);
processor.SaveDocument(path);
```

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawTextFromTop("Hello", 40, 40, 12);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [x] Detect DevExpress PDF package references
- [x] Distinguish generation from processing/editing APIs
- [x] Detect graphics drawing APIs
- [x] Warn on forms/security/signature APIs
- [x] Warn on report export workflows
- [x] Report manual migration items

## Code Fix Checklist

- [x] Implement only for deterministic generation APIs
- [x] Report simple document/page patterns after API confirmation
- [x] Report simple text drawing after API confirmation
- [x] Add `using Canvas.Pdf` only for confirmed replacements
- [x] Preserve processing APIs with diagnostics
- [ ] Add automatic code fix for simple generated-document sample

## Tests Checklist

- [x] Real generation sample
- [x] PDF processor unsupported diagnostic sample
- [x] Text drawing sample
- [x] Line/rectangle drawing sample
- [x] Forms/signature/encryption unsupported sample
- [x] Report export unsupported sample
- [x] WebApi migration-service smoke test
- [ ] Snapshot before/after migration sample
