# Canvas Migration: Spire.PDF

## Package / API Identification

- [ ] NuGet packages:
  - [ ] `Spire.PDF`
  - [ ] `FreeSpire.PDF`
- [ ] Common namespaces to detect:
  - [ ] `Spire.Pdf`
  - [ ] `Spire.Pdf.Graphics`
  - [ ] `Spire.Pdf.Tables`
- [ ] Common classes to detect:
  - [ ] `PdfDocument`
  - [ ] `PdfPageBase`
  - [ ] `PdfTrueTypeFont`
  - [ ] `PdfBrush`
  - [ ] `PdfTable`

## Mapping Table Placeholders

| Spire.PDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new PdfDocument()` | `new Canvas.Pdf.PdfDocument()` | Code fix candidate | Resolve type name conflict |
| `document.Pages.Add(...)` | `document.AddPage(...)` | Code fix candidate | Map page size |
| `page.Canvas.DrawString(...)` | `page.DrawText(...)` | Code fix candidate | Map coordinates/font/brush |
| `document.SaveToFile(...)` | `document.Save(...)` | Code fix candidate | Confirm format overloads |

## Unsupported / Manual Follow-Up

- [ ] PDF conversion features
- [ ] Forms
- [ ] Security/encryption
- [ ] Attachments
- [ ] Complex tables
- [ ] Existing PDF manipulation

## Sample Input Snippets

```csharp
using Spire.Pdf;
using Spire.Pdf.Graphics;

var document = new PdfDocument();
var page = document.Pages.Add();
page.Canvas.DrawString("Hello", new PdfFont(PdfFontFamily.Helvetica, 12), PdfBrushes.Black, 40, 40);
document.SaveToFile(path);
```

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawText("Hello", 40, 800, 12);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [ ] Detect Spire document/page creation
- [ ] Detect canvas draw calls
- [ ] Detect font and brush usage
- [ ] Warn on conversion/editing APIs
- [ ] Warn on forms/security APIs

## Code Fix Checklist

- [ ] Replace document creation
- [ ] Replace page creation
- [ ] Replace simple draw string calls
- [ ] Replace save calls
- [ ] Add `using Canvas.Pdf`

## Tests Checklist

- [ ] Basic document sample
- [ ] DrawString sample
- [ ] SaveToFile sample
- [ ] Unsupported conversion diagnostic sample
- [ ] Snapshot before/after migration sample
