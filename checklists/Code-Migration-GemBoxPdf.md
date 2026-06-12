# Canvas Migration: GemBox.Pdf

## V1 Pilot Analysis

- [x] GemBox.Pdf is a close fit for simple Canvas.Pdf generation: document, page, text content, and save/export.
- [x] `PdfDocument` has a name collision with Canvas.Pdf, so migration must remove GemBox usings and introduce `using Canvas.Pdf`.
- [x] GemBox content APIs vary between formatted text, low-level content objects, and direct draw calls; only simple direct text is deterministic in v1.
- [x] Forms, signatures, security, annotations, tagged PDF, attachments, and existing-PDF editing remain manual in v1.

## Package / API Identification

- [x] NuGet packages:
  - [x] `GemBox.Pdf`
- [x] Common namespaces to detect:
  - [x] `GemBox.Pdf`
  - [x] `GemBox.Pdf.Content`
  - [x] `GemBox.Pdf.Forms`
- [x] Common classes to detect:
  - [x] `PdfDocument`
  - [x] `PdfPage`
  - [x] `PdfFormattedText`
  - [x] `PdfTextContent`
  - [x] `PdfImage`
  - [x] `PdfInteractiveForm`
  - [x] `PdfSignature`
  - [x] `PdfEncryption`
  - [x] `PdfAttachment`
  - [x] `PdfTaggedContent`

## Roslyn Prototype Status

- [x] Add `src/Canvas.Migration.GemBoxPdf`
- [x] Add `tests/Canvas.Migration.GemBoxPdf.Tests`
- [x] Add WebApi converter integration
- [x] Add API service smoke test
- [x] Rewrite deterministic document/page/simple text/save patterns
- [x] Warn on unsupported/manual GemBox feature areas

## Mapping Table

| GemBox.Pdf API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `ComponentInfo.SetLicense(...)` | No Canvas equivalent | Code removal in v1 | Emits info diagnostic. |
| `new PdfDocument()` | `new Canvas.Pdf.PdfDocument()` | Code fix in v1 | Requires removing GemBox usings to resolve type conflict. |
| `document.Pages.Add()` | `document.AddPage()` | Code fix in v1 | Page size/media box still needs review for overloads. |
| `page.Content.DrawText("text", new PdfPoint(x, y))` | `page.DrawTextFromTop("text", x, y, 12)` | Code fix in v1 | Direct literal text only. Review coordinate origin. |
| `page.Content.DrawText("text", x, y)` | `page.DrawTextFromTop("text", x, y, 12)` | Code fix in v1 | Direct literal text only. |
| `page.Content.DrawText(formattedText, ...)` | Manual Canvas text calls | Warning in v1 | Requires formatted-text extraction. |
| `page.Content.DrawImage(...)` | `page.DrawImage(...)` | Warning in v1 | Image resource and sizing need manual review. |
| `page.Content.DrawLine(...)`, `DrawRectangle(...)`, `DrawPath(...)` | Canvas shape/path drawing | Warning in v1 | Shape styles and geometry need manual review. |
| `document.Save(...)` | `document.Save(...)` | Code fix in v1 | Stream/path overload preserved. |

## Unsupported / Manual Follow-Up

- [x] Interactive forms
- [x] Incremental updates
- [x] Complex content editing
- [x] Encryption/security permissions
- [x] Digital signatures
- [x] Tagged PDF
- [x] Attachments/portfolio APIs
- [x] Annotations/link annotations
- [x] Existing PDF loading/import/editing

## Analyzer Diagnostics

| Diagnostic | Severity | Meaning |
| --- | --- | --- |
| `CANMIGGEMBOX000` | Info | `ComponentInfo.SetLicense(...)` removed. |
| `CANMIGGEMBOX001` | Info | `PdfDocument` construction converted. |
| `CANMIGGEMBOX002` | Info | `document.Pages.Add()` converted. |
| `CANMIGGEMBOX003` | Info/Warning | Simple `DrawText` converted, complex text warned. |
| `CANMIGGEMBOX005` | Warning | Image content drawing needs manual migration. |
| `CANMIGGEMBOX006` | Warning | Shape/path content drawing needs manual migration. |
| `CANMIGGEMBOX007` | Info | Save/export converted. |
| `CANMIGGEMBOX020` | Warning | Forms, annotations, tagged PDF, attachments, encryption, or signatures need manual migration. |
| `CANMIGGEMBOX021` | Warning | Existing-PDF loading/import/content editing needs manual migration. |

## Sample Input Snippets

```csharp
using GemBox.Pdf;
using GemBox.Pdf.Content;

ComponentInfo.SetLicense("FREE-LIMITED-KEY");
var doc = new PdfDocument();
var page = doc.Pages.Add();
page.Content.DrawText("Hello", new PdfPoint(40, 40));
doc.Save(path);
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

- [x] Detect GemBox document creation
- [x] Detect page creation
- [x] Detect license initialization
- [x] Detect simple content/text operations
- [x] Warn on complex formatted text
- [x] Warn on image/shape content operations
- [x] Warn on forms/security/signature APIs
- [x] Warn on existing-PDF editing APIs

## Code Fix Checklist

- [x] Replace basic document creation
- [x] Replace basic page creation
- [x] Replace simple save calls
- [x] Add `using Canvas.Pdf`
- [x] Remove GemBox usings
- [x] Remove license initialization
- [x] Convert direct literal text draw calls
- [ ] Convert `PdfFormattedText` flows after real samples
- [ ] Convert image/shape content calls after geometry/style mapping

## Tests Checklist

- [x] Basic document/page/text/save sample
- [x] Direct coordinate text sample
- [x] Complex formatted text warning sample
- [x] Image/shape warning sample
- [x] Forms/security/signature/attachment/tagged-PDF diagnostic sample
- [x] Existing-PDF editing diagnostic sample
- [x] WebApi smoke test
- [ ] Snapshot before/after migration sample
- [ ] Real package identification sample from a customer repository
