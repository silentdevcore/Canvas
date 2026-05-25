# Canvas Migration: IronPDF

## Package / API Identification

- [ ] NuGet packages:
  - [ ] `IronPdf`
- [ ] Common namespaces to detect:
  - [ ] `IronPdf`
  - [ ] `IronPdf.Rendering`
- [ ] Common classes to detect:
  - [ ] `ChromePdfRenderer`
  - [ ] `PdfDocument`
  - [ ] `Installation`
  - [ ] `RenderingOptions`
  - [ ] `HtmlToPdf`

## Mapping Table Placeholders

| IronPDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `ChromePdfRenderer` | Canvas document generation | Manual | HTML rendering is not a direct API match |
| `RenderHtmlAsPdf(...)` | Manual Canvas layout | Manual | Requires HTML-to-Canvas conversion strategy |
| `RenderUrlAsPdf(...)` | Unsupported/manual | Manual | Out of scope for direct code fix |
| `pdf.SaveAs(...)` | `document.Save(...)` | Manual | Only after document mapping exists |

## Unsupported / Manual Follow-Up

- [ ] URL-to-PDF rendering
- [ ] Browser-based HTML/CSS rendering
- [ ] JavaScript rendering
- [ ] Header/footer HTML templates
- [ ] PDF editing/merging APIs
- [ ] Security and signing APIs

## Sample Input Snippets

```csharp
using IronPdf;

var renderer = new ChromePdfRenderer();
var pdf = renderer.RenderHtmlAsPdf("<h1>Hello</h1>");
pdf.SaveAs(path);
```

## Expected Canvas.Pdf Output Snippets

```csharp
using Canvas.Pdf;

var document = new PdfDocument();
var page = document.AddPage();
page.DrawText("Hello", 40, 800, fontSize: 24);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [ ] Detect IronPDF renderer construction
- [ ] Detect HTML rendering calls
- [ ] Warn that HTML/CSS layout requires manual migration
- [ ] Detect simple literal HTML that may be manually simplified
- [ ] Warn on URL rendering calls

## Code Fix Checklist

- [ ] Add diagnostics before any automatic conversion
- [ ] Offer no automatic code fix for browser-rendered HTML in v1
- [ ] Add `using Canvas.Pdf` only for confirmed replacements
- [ ] Emit manual migration report entries
- [ ] Preserve original HTML snippets for review

## Tests Checklist

- [ ] Basic HTML render diagnostic sample
- [ ] URL render unsupported sample
- [ ] Save call sample
- [ ] Literal HTML report sample
- [ ] Snapshot before/after diagnostic sample
