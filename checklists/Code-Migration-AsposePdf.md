# Canvas Migration: Aspose.PDF

## Package / API Identification

- [ ] NuGet packages:
  - [ ] `Aspose.PDF`
- [ ] Common namespaces to detect:
  - [ ] `Aspose.Pdf`
  - [ ] `Aspose.Pdf.Text`
  - [ ] `Aspose.Pdf.Drawing`
- [ ] Common classes to detect:
  - [ ] `Document`
  - [ ] `Page`
  - [ ] `PageCollection`
  - [ ] `TextFragment`
  - [ ] `TextBuilder`
  - [ ] `Table`
  - [ ] `Image`

## Mapping Table Placeholders

| Aspose.PDF API / pattern | Canvas.Pdf replacement | Migration mode | Notes |
| --- | --- | --- | --- |
| `new Document()` | `new Canvas.Pdf.PdfDocument()` | Code fix candidate | Confirm aliasing for `Document` |
| `document.Pages.Add()` | `document.AddPage(...)` | Code fix candidate | Determine default page size |
| `TextFragment` | `page.DrawText(...)` | Manual | Map position and style |
| `document.Save(...)` | `document.Save(...)` | Code fix candidate | Confirm target variable type |

## Unsupported / Manual Follow-Up

- [ ] PDF forms
- [ ] Stamps and advanced annotations
- [ ] Redaction
- [ ] Optimization/compression settings
- [ ] Security/encryption
- [ ] Complex table layout

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
page.DrawText("Hello", 40, 800);
document.Save(path);
```

## Analyzer Diagnostics Checklist

- [ ] Detect Aspose document/page construction
- [ ] Detect paragraph text additions
- [ ] Detect text style usage
- [ ] Warn on forms/security APIs
- [ ] Warn when flow layout cannot be mapped deterministically

## Code Fix Checklist

- [ ] Replace basic document creation
- [ ] Replace basic page creation
- [ ] Replace simple save calls
- [ ] Add `using Canvas.Pdf`
- [ ] Report manual text position work

## Tests Checklist

- [ ] Basic document sample
- [ ] Text fragment sample
- [ ] Save sample
- [ ] Unsupported forms diagnostic sample
- [ ] Snapshot before/after migration sample
