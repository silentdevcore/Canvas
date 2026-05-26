# Canvas Migration: Aspose.PDF

## V1 Pilot Analysis

- [x] V1 scope is limited to deterministic C# source-to-source migration for simple generated PDFs.
- [x] Roslyn-backed migration is connected through `Canvas.WebApi` via framework id `Aspose`.
- [x] Basic document lifecycle is covered: `new Document()` becomes `new Canvas.Pdf.PdfDocument()`.
- [x] Basic page creation is covered: `document.Pages.Add()` becomes `document.AddPage()`.
- [x] Simple `TextFragment` paragraph flows are migrated to `DrawTextFromTop`.
- [x] Positioned `TextFragment` flows using `TextFragment.Position = new Position(x, y)` are migrated to `DrawText`.
- [x] Simple `TextBuilder(page).AppendText(...)` flows are migrated.
- [x] `document.Save(...)` is preserved as Canvas save after document migration.
- [x] Unsupported table/forms/security/stamp/annotation/redaction/optimization usage produces diagnostics for manual follow-up.
- [x] WebApi conversion response includes migrated code, diagnostics, and summary counts.
- [ ] V1 does not preserve Aspose font, color, margins, line spacing, floating boxes, HTML fragments, or complex layout state.
- [ ] V1 does not compile-check output when unsupported Aspose statements intentionally remain for manual migration.
- [ ] Future hardening: preserve vendor usings when unsupported vendor statements remain, or wrap unsupported remnants in a report-only block.
- [ ] Future hardening: replace syntax-only matching with semantic matching before broad rollout.

## Package / API Identification

- [x] NuGet packages:
  - [x] `Aspose.PDF`
- [x] Common namespaces to detect:
  - [x] `Aspose.Pdf`
  - [x] `Aspose.Pdf.Text`
  - [x] `Aspose.Pdf.Drawing`
  - [x] `Aspose.Pdf.Facades`
- [x] Common classes to detect:
  - [x] `Document`
  - [x] `Page`
  - [x] `PageCollection`
  - [x] `TextFragment`
  - [x] `TextBuilder`
  - [x] `Position`
  - [x] `Table`
  - [x] `Image`

## Roslyn Prototype Status

- [x] Add `src/Canvas.Migration.AsposePdf`
- [x] Add `tests/Canvas.Migration.AsposePdf.Tests`
- [x] Add projects to `Canvas.sln`
- [x] Implement first source migration entry point: `AsposePdfMigration`
- [x] Convert Hello World sample end to end
- [x] Convert `new Document()` to `new PdfDocument()`
- [x] Convert `document.Pages.Add()` to `document.AddPage()`
- [x] Convert inline `page.Paragraphs.Add(new TextFragment("..."))`
- [x] Fold removable `TextFragment` variables into Canvas draw calls
- [x] Convert `TextFragment.Position = new Position(x, y)` into `DrawText` coordinates
- [x] Convert simple `TextBuilder(page).AppendText(...)`
- [x] Warn for `TextFragment.TextState` styling that needs manual font/color review
- [x] Warn for table/forms/security/stamp/annotation/redaction/optimization APIs
- [x] Connect WebApi Aspose converter to the Roslyn migration engine
- [x] Add WebApi migration-service smoke test for Aspose summary/diagnostics
- [x] Verified with `dotnet test tests/Canvas.Migration.AsposePdf.Tests/Canvas.Migration.AsposePdf.Tests.csproj --no-restore --no-build`: `7/7` passed
- [x] Verified with `dotnet test tests/Canvas.Api.Tests/Canvas.Api.Tests.csproj --no-restore --no-build`: `17/17` passed
- [ ] Replace syntax-only matching with semantic matching before broad rollout

## Mapping Table Placeholders

| Aspose.PDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new Document()` | `new Canvas.Pdf.PdfDocument()` | Code fix candidate | Implemented for simple generated documents |
| `document.Pages.Add()` | `document.AddPage()` | Code fix candidate | Uses Canvas default A4 page |
| `page.Paragraphs.Add(new TextFragment(text))` | `page.DrawTextFromTop(text, 40, 40, 12)` | Code fix candidate | Starter fixed position until flow layout exists |
| `var text = new TextFragment(text); page.Paragraphs.Add(text)` | `page.DrawTextFromTop(text, 40, 40, 12)` | Code fix candidate | Removes text variable when all usages are supported |
| `text.Position = new Position(x, y); page.Paragraphs.Add(text)` | `page.DrawText(text, x, y, 12)` | Code fix candidate | Aspose explicit position is treated as PDF coordinate text |
| `new TextBuilder(page).AppendText(new TextFragment(text))` | `page.DrawTextFromTop(text, 40, 40, 12)` | Code fix candidate | Removes builder variable when all usages are supported |
| `text.TextState.FontSize/ForegroundColor/...` | Remove and warn | Manual | Styling state needs Canvas font/color review |
| `document.Save(...)` | `document.Save(...)` | Code fix candidate | Save target is preserved after document type migration |

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGASPOSE001` | Info | `Document` construction was migrated to Canvas `PdfDocument` | Yes |
| `CANMIGASPOSE002` | Info | `document.Pages.Add()` was migrated to `document.AddPage()` | Yes |
| `CANMIGASPOSE003` | Info | Simple `TextFragment` paragraph was migrated to `DrawTextFromTop` | Yes |
| `CANMIGASPOSE004` | Info | Simple `TextBuilder.AppendText` was migrated to `DrawTextFromTop` | Yes |
| `CANMIGASPOSE005` | Info | Positioned `TextFragment` paragraph was migrated to `DrawText` | Yes |
| `CANMIGASPOSE006` | Info | Positioned `TextBuilder.AppendText` was migrated to `DrawText` | Yes |
| `CANMIGASPOSE007` | Info | `document.Save(...)` now targets Canvas `PdfDocument.Save(...)` | Yes |
| `CANMIGASPOSE008` | Info | Supported `TextFragment` variable was folded into draw calls | Yes |
| `CANMIGASPOSE009` | Info | Supported `TextBuilder` variable was removed | Yes |
| `CANMIGASPOSE010` | Info | `TextFragment.Position` assignment was folded into coordinates | Yes |
| `CANMIGASPOSE011` | Warning | `TextFragment.TextState` styling needs manual review | No |
| `CANMIGASPOSE020` | Warning | `Table` usage requires manual table migration | No |
| `CANMIGASPOSE021` | Warning | Forms, stamps, annotations, redaction, optimization, or security APIs are outside v1 scope | No |

## Unsupported / Manual Follow-Up

- [ ] PDF forms
- [ ] Stamps and advanced annotations
- [ ] Redaction
- [ ] Optimization/compression settings
- [ ] Security/encryption
- [ ] Complex table layout
- [ ] Floating boxes and complex paragraph layout
- [ ] HTML fragments
- [ ] Font/color/style state preservation

## Sample Input Snippets

```csharp
using Aspose.Pdf;
using Aspose.Pdf.Text;

var document = new Document();
var page = document.Pages.Add();
var text = new TextFragment("Hello");
page.Paragraphs.Add(text);
document.Save(path);
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

- [x] Detect Aspose document/page construction
- [x] Detect paragraph text additions
- [x] Detect text style usage
- [x] Detect `TextBuilder.AppendText`
- [x] Warn on forms/security APIs
- [x] Warn when flow layout cannot be mapped deterministically

## Code Fix Checklist

- [x] Replace basic document creation
- [x] Replace basic page creation
- [x] Replace simple paragraph text
- [x] Replace simple positioned text
- [x] Replace simple text builder appends
- [x] Replace simple save calls
- [x] Add `using Canvas.Pdf`
- [x] Report manual text position/style work
- [ ] Preserve comments and surrounding code

## Tests Checklist

- [x] Basic document sample
- [x] Inline text fragment sample
- [x] Positioned text fragment sample
- [x] Text builder sample
- [x] Text state warning sample
- [x] Unsupported text fragment usage sample
- [x] Unsupported table/security diagnostic sample
- [x] Save sample
- [x] WebApi migration-service smoke test
- [ ] Snapshot before/after migration sample
