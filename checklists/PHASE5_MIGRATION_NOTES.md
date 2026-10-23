# Phase 5 — Public API Stabilization Notes

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

## Stable façade entry point
Use `PXA.Infrastructure.Pdf.PdfFacade` as the primary entry point for orchestration-based document generation.

### Facade capabilities
- `GenerateToFile(documentModel, outputPath)`
- `ApplyPageNumbering(documentModel, options?)`
- `ApplyHeaderFooter(documentModel, options?)`
- `ApplyWatermark(documentModel, text, options?)`
- `ApplyTableOfContents(documentModel, options?)`
- `ReadDiagnostics(documentModel)`
- `GetPagesWithText/GetPagesWithImages/GetPagesWithLinks/GetPagesWithShapes(documentModel)`

## Compatibility shim guidance
Legacy direct model APIs remain available on `PXA.Pdf.PdfDocument` (`Save`, `ToBytes`, etc.) for compatibility.

Recommended migration path is to keep model composition code and route orchestration/output through `PdfFacade`.

## Old API -> new API mapping
- `document.AddPageNumbers(...)` -> `PdfFacade.ApplyPageNumbering(document, ... )`
- `document.AddHeadersAndFooters(...)` -> `PdfFacade.ApplyHeaderFooter(document, ... )`
- `document.AddTextWatermark(...)` -> `PdfFacade.ApplyWatermark(document, text, ... )`
- `document.AddTableOfContents(...)` -> `PdfFacade.ApplyTableOfContents(document, ... )`
- `document.Save(path)` -> `PdfFacade.GenerateToFile(document, path)`

## Sample status
`samples/PXA.Demo` now uses `PdfFacade` and generates `demo-facade-output.pdf`.
