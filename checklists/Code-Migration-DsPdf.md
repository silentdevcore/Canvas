# Canvas Migration: DsPdf / Document Solutions

## Package / API Identification

- [ ] NuGet packages:
  - [ ] `DS.Documents.Pdf`
  - [ ] Legacy `GrapeCity.Documents.Pdf`
- [ ] Common namespaces to detect:
  - [ ] `DsPdf`
  - [ ] `GrapeCity.Documents.Pdf`
  - [ ] `GrapeCity.Documents.Drawing`
- [ ] Common classes to detect:
  - [ ] `GcPdfDocument`
  - [ ] `Page`
  - [ ] `Graphics`
  - [ ] `TextFormat`
  - [ ] `Image`
  - [ ] `TableRenderer`

## Mapping Table Placeholders

| DsPdf API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new GcPdfDocument()` | `new Canvas.Pdf.PdfDocument()` | Code fix candidate | Confirm current namespace |
| `doc.NewPage()` | `document.AddPage(...)` | Code fix candidate | Map page size |
| `page.Graphics.DrawString(...)` | `page.DrawText(...)` | Code fix candidate | Map rectangle/layout options |
| `doc.Save(...)` | `document.Save(...)` | Code fix candidate | Confirm stream/path overload |

## Unsupported / Manual Follow-Up

- [ ] Advanced text layout
- [ ] AcroForms
- [ ] PDF/A and compliance options
- [ ] Redaction
- [ ] Signature APIs
- [ ] Complex table rendering

## Sample Input Snippets

```csharp
using GrapeCity.Documents.Pdf;
using GrapeCity.Documents.Drawing;

var document = new GcPdfDocument();
var page = document.NewPage();
page.Graphics.DrawString("Hello", new TextFormat(), new PointF(40, 40));
document.Save(path);
```

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawText("Hello", 40, 800);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [ ] Detect DsPdf/GcPdf package generation
- [ ] Detect page creation
- [ ] Detect graphics text calls
- [ ] Warn on compliance/security APIs
- [ ] Warn on complex layout APIs

## Code Fix Checklist

- [ ] Replace basic document creation
- [ ] Replace page creation
- [ ] Replace simple text drawing
- [ ] Add `using Canvas.Pdf`
- [ ] Report coordinate-system assumptions

## Tests Checklist

- [ ] Basic document sample
- [ ] DrawString sample
- [ ] Save sample
- [ ] Unsupported forms diagnostic sample
- [ ] Snapshot before/after migration sample
