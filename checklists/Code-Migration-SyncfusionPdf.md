# PXA Migration: Syncfusion PDF

## V1 Pilot Analysis

- [x] V1 pilot status: ready for simple generation migrations.
- [x] Deterministic code fixes are implemented for document/page creation, simple text, text boxes, lines, rectangles, images, save, and `Close(true)` cleanup.
- [x] Manual follow-up features are reported through warnings instead of being silently changed.
- [x] WebApi conversion uses the same Roslyn migration engine as the tests.
- [x] WebApi conversion returns summary counts for converted, warning, error, and total diagnostics.
- [x] Realistic invoice-style fixture validates the end-to-end migration shape.
- [x] Verified with `dotnet test tests/PXA.Migration.SyncfusionPdf.Tests/PXA.Migration.SyncfusionPdf.Tests.csproj --no-restore --no-build`: `20/20` passed.
- [x] Verified with `dotnet build PXA.WebApi/PXA.WebApi.csproj --no-restore`.
- [ ] Semantic symbol matching remains a post-v1 hardening task.
- [ ] Real analyzer/codefix packaging remains a post-v1 IDE integration task.
- [ ] Visual PDF fixture comparison remains a post-v1 quality gate.
- [ ] Full table/forms/security/PDF-A migration remains out of v1 scope.

## Package / API Identification

- [x] NuGet packages:
  - [x] `Syncfusion.Pdf.Net.Core`
  - [ ] Other Syncfusion PDF packages used by the project
- [x] Common namespaces to detect:
  - [x] `Syncfusion.Pdf`
  - [x] `Syncfusion.Pdf.Graphics`
  - [x] `Syncfusion.Pdf.Grid`
  - [ ] `Syncfusion.Drawing`
- [x] Common classes to detect:
  - [x] `PdfDocument`
  - [x] `PdfPage`
  - [x] `PdfGraphics`
  - [x] `PdfFont`
  - [x] `PdfStandardFont`
  - [x] `PdfFontFamily`
  - [x] `PdfBrush`
  - [x] `PdfBrushes`
  - [x] `PdfSolidBrush`
  - [x] `PdfPen`
  - [x] `PdfPens`
  - [x] `PdfGrid`

## Pilot Scope

- [x] Convert new-document generation only
- [x] Convert simple page creation
- [x] Convert simple `PdfGraphics.DrawString(...)` overloads
- [x] Convert standard Helvetica/Times/Courier fonts where explicit
- [x] Convert common predefined brushes and simple solid RGB brushes
- [x] Convert simple `document.Save(pathOrStream)` calls
- [ ] Defer grid/table migration until PXA table mapping is reviewed
- [ ] Defer existing-PDF processing, forms, security, and PDF/A

## Roslyn Prototype Status

- [x] Add `src/PXA.Migration.Abstractions`
- [x] Add `src/PXA.Migration.Roslyn`
- [x] Add `src/PXA.Migration.SyncfusionPdf`
- [x] Add `tests/PXA.Migration.SyncfusionPdf.Tests`
- [x] Implement first source migration entry point: `SyncfusionPdfMigration`
- [x] Convert Hello World sample end to end
- [x] Emit `CANMIGSYNC001`, `CANMIGSYNC002`, and `CANMIGSYNC003` diagnostics
- [x] Emit `CANMIGSYNC009` when a supported `PdfGraphics` variable is removed
- [x] Add snapshot-style before/after test for the first migration slice
- [x] Add realistic end-to-end before/after fixture for v1 pilot readiness
- [x] Connect WebApi Syncfusion converter to the Roslyn migration engine
- [x] Return migration summary counts from WebApi convert endpoint
- [ ] Replace syntax-only matching with semantic matching before broad provider rollout
- [ ] Add real analyzer/codefix packaging if IDE integration is required

## Mapping Rules

| Syncfusion API / pattern | PXA.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new Syncfusion.Pdf.PdfDocument()` | `new PXA.Pdf.PdfDocument()` | Code fix candidate | Prefer fully qualified replacement when both APIs use `PdfDocument` |
| `using var document = new PdfDocument();` | `var document = new PXA.Pdf.PdfDocument();` | Code fix candidate | PXA `PdfDocument` is not disposable |
| `document.Pages.Add()` | `document.AddPage()` | Code fix candidate | PXA default page is A4, same migration default for v1 |
| `var graphics = page.Graphics;` | Remove or inline `page` | Code fix candidate | Only remove when all uses are migrated |
| `page.Graphics.DrawString(text, font, brush, x, y)` | `page.DrawTextFromTop(text, x, y, options)` | Code fix candidate | Uses PXA top-left migration adapter |
| `graphics.DrawString(text, font, brush, new PointF(x, y))` | `page.DrawTextFromTop(text, x, y, options)` | Code fix candidate | Supports `Syncfusion.Drawing.PointF` and `System.Drawing.PointF` |
| `graphics.DrawString(text, font, brush, new RectangleF(x, y, w, h))` | `page.DrawTextBoxFromTop(text, x, y, w, h, options)` | Code fix candidate | Maps wrapping plus basic horizontal/vertical alignment |
| `graphics.DrawLine(pen, x1, y1, x2, y2)` | `page.DrawLineFromTop(x1, y1, x2, y2, lineWidth, color)` | Code fix candidate | Maps top-left endpoints |
| `graphics.DrawRectangle(pen, x, y, w, h)` | `page.DrawRectangleFromTop(x, y, w, h, lineWidth, ...)` | Code fix candidate | Maps top-left box to PXA bottom-left rectangle |
| `graphics.DrawRectangle(brush, x, y, w, h)` | `page.DrawRectangleFromTop(x, y, w, h, fill: true, fillColor: color)` | Code fix candidate | Fill-only Syncfusion call maps to filled PXA rectangle |
| `graphics.DrawImage(image, x, y)` | `page.DrawImageFromTop(imagePath, x, y)` | Manual/code fix candidate | Needs source image path or byte mapping strategy |
| `graphics.DrawImage(image, x, y, w, h)` | `page.DrawImageFromTop(imagePath, x, y, w, h)` | Manual/code fix candidate | Needs source image path or byte mapping strategy |
| `graphics.DrawImage(PdfImage.FromFile(path), x, y, w, h)` | `page.DrawImageFromTop(path, x, y, w, h)` | Code fix candidate | Direct file path mapping |
| `graphics.DrawImage(new PdfBitmap(stream), x, y, w, h)` | `page.DrawImageFromTop(stream, x, y, w, h)` | Code fix candidate | Direct stream mapping when stream symbol is available |
| `graphics.DrawImage(imageBytes, x, y, w, h)`-style wrappers | `page.DrawImageFromTop(imageBytes, x, y, w, h)` | Code fix candidate | Direct byte-array mapping when wrapper exposes bytes |
| `new PdfStandardFont(PdfFontFamily.Helvetica, size)` | `new PdfDrawTextOptions { FontFamily = PdfFontFamily.Helvetica, FontSize = size }` | Code fix candidate | Map TimesRoman/Times/Courier variants explicitly |
| `PdfBrushes.Black` | `PdfColor.Black` | Code fix candidate | Direct predefined color |
| `PdfBrushes.Red` | `PdfColor.RedColor` | Code fix candidate | Direct predefined color |
| `PdfBrushes.Green` | `PdfColor.GreenColor` | Code fix candidate | Direct predefined color |
| `PdfBrushes.Blue` | `PdfColor.BlueColor` | Code fix candidate | Direct predefined color |
| `new PdfSolidBrush(Color.FromArgb(r, g, b))` | `PdfColor.FromRgb(r, g, b)` | Code fix candidate | Only for integer RGB values |
| `new PdfPen(Color.FromArgb(r, g, b), width)` | `strokeColor: PdfColor.FromRgb(r, g, b), lineWidth: width` | Code fix candidate | Only for simple pen usages |
| `document.Save(path)` | `document.Save(path)` | Code fix candidate | Safe after target document variable is migrated |
| `document.Save(stream)` | `document.Save(stream)` | Code fix candidate | Safe after target document variable is migrated |
| `document.Close(true)` | Remove | Code fix candidate | PXA save handles writing; no close call exists |

## Coordinate Policy

- [x] Assume Syncfusion drawing coordinates use a top-left page origin in common graphics samples
- [x] Convert text Y with `canvasY = page.Height - syncfusionY - fontSize`
- [x] Keep X unchanged
- [x] Add `PdfPage.DrawTextFromTop(...)` and `PdfPage.DrawParagraphFromTop(...)` as migration-friendly PXA adapters
- [x] Add `PdfPage.DrawTextBoxFromTop(...)` for `RectangleF` text migration
- [x] Add `PdfPage.DrawLineFromTop(...)`, `DrawRectangleFromTop(...)`, `DrawRoundedRectangleFromTop(...)`, `DrawCircleFromTop(...)`, and `DrawImageFromTop(...)`
- [x] Add `DrawImage(...)` and `DrawImageFromTop(...)` overloads for `byte[]` and `Stream`
- [x] Add `PdfColor.FromRgb(...)` for `Color.FromArgb(r, g, b)` migration
- [x] Add a migration diagnostic when font size cannot be resolved at migration time
- [x] Use the new PXA top-left coordinate adapter in generated migration output
- [ ] Verify output visually with a fixture PDF after the first codefix prototype

## Diagnostic IDs

| ID | Severity | Meaning | Code fix |
| --- | --- | --- | --- |
| `CANMIGSYNC001` | Info | Syncfusion PDF document creation can migrate to `PXA.Pdf.PdfDocument` | Yes |
| `CANMIGSYNC002` | Info | Syncfusion page creation can migrate to `document.AddPage()` | Yes |
| `CANMIGSYNC003` | Info | Simple `DrawString` can migrate to `page.DrawText(...)` | Yes |
| `CANMIGSYNC004` | Warning | `DrawString` uses rectangle layout or string format requiring manual layout review | Partial/manual |
| `CANMIGSYNC005` | Warning | `PdfGrid`/table usage requires manual table migration | No |
| `CANMIGSYNC006` | Warning | Forms, security, PDF/A, or existing-PDF processing is outside v1 scope | No |
| `CANMIGSYNC007` | Warning | Coordinate conversion needs unresolved page height or font size | No |
| `CANMIGSYNC008` | Warning | Image drawing cannot be migrated until the source image path, stream, or bytes can be resolved | No |
| `CANMIGSYNC009` | Info | Supported `PdfGraphics` variable was removed after all usages were migrated | Yes |
| `CANMIGSYNC010` | Info | Simple `DrawLine` was migrated to `DrawLineFromTop` | Yes |
| `CANMIGSYNC011` | Info | Simple `DrawRectangle` was migrated to `DrawRectangleFromTop` | Yes |
| `CANMIGSYNC012` | Info | `RectangleF` `DrawString` was migrated to `DrawTextBoxFromTop` | Yes |
| `CANMIGSYNC013` | Info | Supported `PdfStringFormat` variable was removed after all usages were migrated | Yes |
| `CANMIGSYNC014` | Info | Simple `DrawImage` was migrated to `DrawImageFromTop` | Yes |
| `CANMIGSYNC015` | Info | `document.Close(true)` was removed after a saved document migration | Yes |

## Future Abstractions Proven By This Pilot

- [ ] `ProviderMigrationProfile`: provider id, namespaces, package names, diagnostics
- [ ] `MigrationRule`: semantic matcher plus replacement strategy
- [x] `MigrationDiagnostic`: provider diagnostic id, severity, message, manual guidance
- [ ] `CodeFixStrategy`: deterministic syntax rewrite
- [x] `MigrationReport`: converted nodes, unsupported nodes, manual follow-up notes
- [ ] `CoordinateTransform`: provider coordinate system to PXA coordinate system
- [ ] `SymbolMap`: vendor type/member symbol to PXA target symbol

## Unsupported / Manual Follow-Up

- [x] Advanced grid layout through `PdfGrid`
- [x] Template graphics
- [x] Forms
- [x] Security
- [x] PDF/A conversion
- [x] Existing PDF load/edit/process flows
- [x] Rectangle text layout with wrapping and alignment
- [x] Complex image transformations
- [x] Drawing state save/restore and transformations
- [x] Pens/stroked text beyond simple color mapping

## Sample Input Snippets

### Basic Document And Text

```csharp
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

using var document = new PdfDocument();
var page = document.Pages.Add();
page.Graphics.DrawString("Hello", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
document.Save(path);
```

### Graphics Variable

```csharp
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;

var document = new PdfDocument();
var page = document.Pages.Add();
PdfGraphics graphics = page.Graphics;
var font = new PdfStandardFont(PdfFontFamily.Courier, 10);
graphics.DrawString("Invoice", font, PdfBrushes.Blue, new PointF(24, 32));
document.Save(stream);
document.Close(true);
```

### Manual Layout Case

```csharp
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;

var document = new PdfDocument();
var page = document.Pages.Add();
var format = new PdfStringFormat { Alignment = PdfTextAlignment.Center };
page.Graphics.DrawString("Wrapped", new PdfStandardFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, new RectangleF(40, 40, 200, 80), format);
document.Save(path);
```

### Shapes And Image

```csharp
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

var document = new PdfDocument();
var page = document.Pages.Add();
page.Graphics.DrawLine(PdfPens.Black, 40, 40, 160, 40);
page.Graphics.DrawRectangle(PdfPens.Blue, 40, 60, 120, 50);
page.Graphics.DrawRectangle(PdfBrushes.Green, 40, 130, 120, 50);
page.Graphics.DrawRectangle(new PdfSolidBrush(Color.FromArgb(230, 240, 255)), 40, 190, 120, 40);
page.Graphics.DrawImage(PdfImage.FromFile(imagePath), 40, 200, 80, 40);
page.Graphics.DrawImage(new PdfBitmap(imageStream), 140, 200, 80, 40);
document.Save(path);
```

## Expected PXA.Pdf Output Snippets

### Basic Document And Text

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawTextFromTop("Hello", 40, 40, 12, PdfFontFamily.Helvetica);
document.Save(path);
```

### Graphics Variable

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
var font = new PdfDrawTextOptions
{
    FontFamily = PdfFontFamily.Courier,
    FontSize = 10,
    FillColor = PdfColor.BlueColor
};
page.DrawTextFromTop("Invoice", 24, 32, font);
document.Save(stream);
```

### Manual Layout Case

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
// CANMIGSYNC004: review rectangle text layout and alignment manually.
page.DrawTextBoxFromTop("Wrapped", 40, 40, 200, 80, new PdfTextBoxOptions
{
    FontFamily = PdfFontFamily.Helvetica,
    FontSize = 12,
    FillColor = PdfColor.Black,
    Alignment = PdfTextAlignment.Center
});
document.Save(path);
```

### Shapes And Image

```csharp
using PXA.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawLineFromTop(40, 40, 160, 40);
page.DrawRectangleFromTop(40, 60, 120, 50, strokeColor: PdfColor.BlueColor);
page.DrawRectangleFromTop(40, 130, 120, 50, fill: true, fillColor: PdfColor.GreenColor);
page.DrawRectangleFromTop(40, 190, 120, 40, fill: true, fillColor: PdfColor.FromRgb(230, 240, 255));
page.DrawImageFromTop(imagePath, 40, 200, 80, 40);
page.DrawImageFromTop(imageStream, 140, 200, 80, 40);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [x] Detect Syncfusion document/page creation
- [x] Detect graphics draw string calls
- [x] Detect font and brush usage
- [x] Warn on grid layout
- [x] Warn on templates/forms/security
- [x] Warn on rectangle/string-format text layout
- [x] Warn when coordinate conversion lacks font size
- [ ] Define final analyzer category names in implementation

## Code Fix Checklist

- [x] Replace basic document creation
- [x] Replace page creation
- [x] Replace simple `DrawString` calls
- [x] Replace supported `PdfGraphics graphics = page.Graphics; graphics.DrawString(...)` calls
- [x] Replace simple `DrawLine` calls
- [x] Replace simple pen/brush `DrawRectangle` calls
- [x] Replace `RectangleF` `DrawString` calls with `DrawTextBoxFromTop`
- [x] Map basic `PdfStringFormat.Alignment`
- [x] Map basic `PdfStringFormat.LineAlignment`
- [x] Replace `PdfImage.FromFile(...)` image draw calls
- [x] Replace `new PdfBitmap(stream)` image draw calls
- [x] Replace `PdfSolidBrush(Color.FromArgb(...))` in simple fill calls
- [x] Replace `PdfPen(Color.FromArgb(...), width)` in simple stroke calls
- [x] Add `using PXA.Pdf`
- [x] Convert obvious colors/fonts
- [x] Remove `document.Close(true)` after save when the target is migrated
- [ ] Remove unused Syncfusion `using` directives only when semantic analysis confirms no remaining Syncfusion symbols
- [ ] Keep vendor code untouched when any argument cannot be mapped deterministically

## Tests Checklist

- [x] Basic document sample
- [x] DrawString sample
- [x] Font/brush sample
- [x] Unsupported grid diagnostic sample
- [x] Snapshot before/after migration sample
- [x] Realistic invoice-style end-to-end fixture
- [x] `using var PdfDocument` becomes non-disposable PXA document
- [x] `page.Graphics.DrawString(...)` migrates without explicit graphics variable
- [x] `PdfGraphics graphics = page.Graphics` migrates when all graphics calls are supported
- [x] `RectangleF` `DrawString` migrates to `DrawTextBoxFromTop`
- [x] Basic `PdfStringFormat.Alignment` maps to `PdfTextBoxOptions.Alignment`
- [x] Basic `PdfStringFormat.LineAlignment` maps to `PdfTextBoxOptions.VerticalAlignment`
- [x] Simple `DrawLine` maps to `DrawLineFromTop`
- [x] Simple `DrawRectangle` maps to `DrawRectangleFromTop`
- [x] Simple image drawing maps to `DrawImageFromTop` when image path or stream is known
- [x] Simple image drawing maps to `DrawImageFromTop` when byte source is known
- [ ] `PdfBrushes.Black/Red/Green/Blue` map to PXA colors
- [x] `PdfSolidBrush(Color.FromArgb(...))` maps to `PdfColor.FromRgb(...)` for integer RGB
- [x] Simple `PdfPen(Color.FromArgb(...), width)` maps to `PdfColor.FromRgb(...)` plus line width
- [x] Rectangle/string-format `DrawString` emits `CANMIGSYNC004`
- [x] `PdfGrid` emits `CANMIGSYNC005`
- [x] Forms/security/existing-PDF flows emit `CANMIGSYNC006`
- [x] `document.Close(true)` is removed only after save/document migration
