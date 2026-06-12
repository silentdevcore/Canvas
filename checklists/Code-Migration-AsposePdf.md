# Canvas Migration: Aspose.PDF

## V1 Implementation Status

- [x] V1 scope: deterministic C# source-to-source migration for simple generated PDFs using the Aspose.PDF document/page/text API.
- [x] Roslyn-backed migration connected through `Canvas.WebApi` via framework id `Aspose`.
- [x] Status upgraded from pilot to **full** converter.
- [x] `new Document()` → `new PdfDocument()`.
- [x] `document.Pages.Add()` → `document.AddPage()`.
- [x] Inline `page.Paragraphs.Add(new TextFragment("..."))` → `page.DrawTextFromTop("...", 40, 40, 12)`.
- [x] `TextFragment` variables are folded into draw calls and removed when all usages are supported.
- [x] `TextFragment.Position = new Position(x, y)` is folded into `DrawText(text, x, y, 12)` coordinates.
- [x] `TextFragment.TextState.*` styling assignments are removed with a warning (font/color needs manual review).
- [x] `TextBuilder(page).AppendText(fragment)` → `page.DrawTextFromTop(...)` or `page.DrawText(...)`.
- [x] `TextBuilder` variables are removed when all `AppendText` usages are supported.
- [x] `document.Save(...)` preserved — target type is now Canvas `PdfDocument`.
- [x] All `Aspose.Pdf.*` usings removed; `using Canvas.Pdf;` inserted.
- [x] `Table` usage emits `CANMIGASPOSE020` warning.
- [x] Forms, stamps, annotations, redaction, optimization, security APIs emit `CANMIGASPOSE021` warning.
- [x] WebApi conversion response includes migrated code, diagnostics, and summary counts.
- [ ] V1 does not preserve font size from `TextState.FontSize` — all text defaults to `fontSize: 12`.
- [ ] V1 does not preserve foreground color from `TextState.ForegroundColor`.
- [ ] V1 does not preserve margins, line spacing, floating boxes, or HTML fragments.
- [ ] V1 does not compile-check output when unsupported statements remain for manual migration.
- [ ] Future hardening: map `TextState.FontSize` to the `fontSize` argument.
- [ ] Future hardening: replace syntax-only matching with semantic matching before broad rollout.

## Package / API Identification

- [x] NuGet package: `Aspose.PDF`
- [x] Namespaces removed: `Aspose.Pdf`, `Aspose.Pdf.Text`, `Aspose.Pdf.Drawing`, `Aspose.Pdf.Facades`
- [x] Classes fully converted:
  - [x] `Document` → `PdfDocument`
  - [x] `document.Pages.Add()` → `document.AddPage()`
  - [x] `TextFragment` (simple and positioned)
  - [x] `TextBuilder` + `AppendText`
  - [x] `Position`
- [ ] Classes kept as-is (manual migration):
  - [ ] `Table`
  - [ ] `Image`
  - [ ] `Form` / `Field` / `TextBoxField` / `SignatureField`
  - [ ] `Stamp` / `TextStamp`
  - [ ] `RedactionAnnotation`
  - [ ] `OptimizationOptions`
  - [ ] `Annotation`
  - [ ] `Facades` / `PdfFileSecurity` / `DocumentPrivilege`

## Roslyn Implementation

- [x] `AsposePdfMigration` uses `AsposePdfSyntaxRewriter : CSharpSyntaxRewriter`.
- [x] Pre-scan phase: `FindTextFragments`, `FindTextFragmentPositions`, `FindTextBuilderPages`, `FindRemovableTextFragments`, `FindRemovableTextBuilders`, `FindDocumentVariables`, `FindSavedDocumentVariables`.
- [x] `VisitObjectCreationExpression`: rewrites `new Document()` → `new PdfDocument()`.
- [x] `VisitMemberAccessExpression`: rewrites `.Pages.Add` → `.AddPage`.
- [x] `VisitInvocationExpression`: migrates `Paragraphs.Add(fragment)` and `builder.AppendText(fragment)` to Canvas draw calls.
- [x] `VisitLocalDeclarationStatement` / `VisitGlobalStatement`: removes folded `TextFragment` and `TextBuilder` variable declarations.
- [x] `VisitExpressionStatement`: removes folded `TextFragment.Position` and `TextFragment.TextState.*` assignments.
- [x] `TryRemoveSupportedTextFragmentPositionAssignment`: folds `Position` into `DrawText` coordinates.
- [x] `TryRemoveSupportedTextFragmentStateAssignment`: removes `TextState.*` with warning.
- [x] `IsSupportedTextFragmentUse` / `IsSupportedTextBuilderUse`: guard removable-variable logic.

## Mapping Table

| Aspose.PDF API / pattern | Canvas.Pdf replacement | Notes |
| --- | --- | --- |
| `using Aspose.Pdf[.*];` | *(removed)* + `using Canvas.Pdf;` | All Aspose.Pdf.* namespaces stripped |
| `new Document()` | `new PdfDocument()` | Zero-arg constructor only |
| `document.Pages.Add()` | `document.AddPage()` | Canvas default A4 page |
| `page.Paragraphs.Add(new TextFragment("text"))` | `page.DrawTextFromTop("text", 40, 40, 12)` | Starter fixed position |
| `var tf = new TextFragment("text"); page.Paragraphs.Add(tf)` | `page.DrawTextFromTop("text", 40, 40, 12)` — `tf` removed | Variable folded when all usages supported |
| `tf.Position = new Position(x, y); page.Paragraphs.Add(tf)` | `page.DrawText("text", x, y, 12)` — position folded | `Position` assignment removed |
| `tf.TextState.FontSize = 18` | *(removed)* + warning | Font size not yet mapped; defaults to 12 |
| `var builder = new TextBuilder(page); builder.AppendText(tf)` | `page.DrawTextFromTop("text", 40, 40, 12)` — builder and tf removed | Both variables folded |
| `builder.AppendText(tf)` with `tf.Position` | `page.DrawText("text", x, y, 12)` | Position honoured |
| `document.Save(path)` | `document.Save(path)` | Save target preserved |

## Diagnostic IDs

| ID | Severity | Meaning |
| --- | --- | --- |
| `CANMIGASPOSE001` | Info | `new Document()` → `new PdfDocument()` |
| `CANMIGASPOSE002` | Info | `document.Pages.Add()` → `document.AddPage()` |
| `CANMIGASPOSE003` | Info | Simple `TextFragment` paragraph → `DrawTextFromTop` |
| `CANMIGASPOSE004` | Info | Simple `TextBuilder.AppendText` → `DrawTextFromTop` |
| `CANMIGASPOSE005` | Info | Positioned `TextFragment` paragraph → `DrawText` |
| `CANMIGASPOSE006` | Info | Positioned `TextBuilder.AppendText` → `DrawText` |
| `CANMIGASPOSE007` | Info | `document.Save(...)` now targets Canvas `PdfDocument.Save(...)` |
| `CANMIGASPOSE008` | Info | `TextFragment` variable folded into draw calls and removed |
| `CANMIGASPOSE009` | Info | `TextBuilder` variable removed after `AppendText` migration |
| `CANMIGASPOSE010` | Info | `TextFragment.Position` assignment folded into `DrawText` coordinates |
| `CANMIGASPOSE011` | Warning | `TextFragment.TextState` styling requires manual Canvas font/color review |
| `CANMIGASPOSE020` | Warning | `Table` usage requires manual table migration |
| `CANMIGASPOSE021` | Warning | Forms, stamps, annotations, redaction, optimization, or security APIs are outside v1 scope |

## Unsupported / Manual Follow-Up

- [ ] Font size / family from `TextState`
- [ ] Foreground / background color from `TextState`
- [ ] Margins, line spacing, leading
- [ ] Floating boxes and complex paragraph layout
- [ ] HTML fragments (`HtmlFragment`)
- [ ] Table layout (`Table`, `Row`, `Cell`)
- [ ] PDF forms (`TextBoxField`, `SignatureField`, etc.)
- [ ] Stamps and advanced annotations
- [ ] Redaction
- [ ] Optimization / compression settings
- [ ] Security / encryption

## Sample Input

```csharp
using Aspose.Pdf;
using Aspose.Pdf.Text;

var document = new Document();
var page = document.Pages.Add();

// Positioned heading via TextFragment + Position
var heading = new TextFragment("Invoice #1042");
heading.Position = new Position(40, 750);
heading.TextState.FontSize = 18;
page.Paragraphs.Add(heading);

// Simple paragraph text (no position — uses starter coordinates)
page.Paragraphs.Add(new TextFragment("Thank you for your order."));

// TextBuilder flow
var builder = new TextBuilder(page);
var note = new TextFragment("Payment due within 30 days.");
note.Position = new Position(40, 650);
builder.AppendText(note);

document.Save(outputPath);
```

## Expected Canvas.Pdf Output

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawText("Invoice #1042", 40, 750, 12);
page.DrawTextFromTop("Thank you for your order.", 40, 40, 12);
page.DrawText("Payment due within 30 days.", 40, 650, 12);
document.Save(outputPath);
```

> **Note:** `heading.TextState.FontSize = 18` is removed with a `CANMIGASPOSE011` warning — font size mapping is a v2 item.

## Code Fix Checklist

- [x] Remove `Aspose.Pdf.*` usings, add `using Canvas.Pdf;`
- [x] Replace `new Document()` with `new PdfDocument()`
- [x] Replace `document.Pages.Add()` with `document.AddPage()`
- [x] Replace inline `page.Paragraphs.Add(new TextFragment("text"))` with `DrawTextFromTop`
- [x] Fold `TextFragment` variables into draw calls when all usages are supported
- [x] Fold `TextFragment.Position` into `DrawText` x/y coordinates
- [x] Remove `TextFragment.TextState.*` assignments with warning
- [x] Replace `TextBuilder.AppendText(fragment)` with `DrawTextFromTop` / `DrawText`
- [x] Remove `TextBuilder` variables when all `AppendText` usages are migrated
- [x] Preserve `document.Save(...)` call
- [ ] Map `TextState.FontSize` to the `fontSize` argument
- [ ] Map `TextState.ForegroundColor` to `PdfColor`
- [ ] Preserve comments and surrounding code

## Tests Checklist

- [x] Basic document sample
- [x] Inline text fragment sample
- [x] Positioned text fragment sample
- [x] Text builder sample
- [x] Text state warning sample
- [x] Unsupported text fragment usage sample
- [x] Unsupported table/security diagnostic sample
- [x] Final combined v1 fixture
- [x] Save sample
- [x] WebApi migration-service smoke test
- [ ] Snapshot before/after migration sample
