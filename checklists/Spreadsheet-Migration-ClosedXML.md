# Spreadsheet Migration — ClosedXML

Migrates ClosedXML (`XLWorkbook`) authoring code → the PXA spreadsheet API (`PxaWorkbook`,
`PXA.Infrastructure.Spreadsheet.PxaWorkbookBuilder`). The **reference** spreadsheet migration —
the other libraries (EPPlus / GemBox / Aspose) clone this structure.

- **Project:** `src/PXA.Migration.Spreadsheet.Code.ClosedXml/` (`ClosedXmlSpreadsheetMigration : CSharpSourceMigration`, Roslyn).
- **Converter:** `PXA.WebApi/Services/Converters/ClosedXmlSpreadsheetConverter.cs` (`FrameworkId = "ClosedXmlSpreadsheet"`, Status `full`), registered in `MigrationService`.
- **Diagnostics prefix:** `CANMIGCLXL`.
- **Tests:** `tests/PXA.Migration.Spreadsheet.Code.ClosedXml.Tests/` (3, green). Live: `/api/migration/convert`.

## API mapping
| ClosedXML | PXA |
|---|---|
| `new XLWorkbook()` | `new PxaWorkbook()` |
| `wb.Worksheets.Add("S")` / `wb.AddWorksheet("S")` | `wb.AddSheet("S")` |
| `ws.Cell("A1").Value = v` | `ws.Cell("A1").Value(v)` |
| `ws.Cell(1,2)` (1-based) | `ws.Cell(0,1)` (0-based; literals computed, expressions wrapped `(e-1)`) |
| `ws.Cell("A1").FormulaA1 = "f"` | `ws.Cell("A1").Formula("f")` |
| `ws.Cell("A1").Style.Font.Bold/Italic/FontSize = v` | `ws.Cell("A1").Style(s => s.Bold(v)/Italic(v)/FontSize(v))` |
| `ws.Column(i).Width = w` | `ws.Column(i-1).Width(w)` |
| `ws.Range("A1:B1").Merge()` | (unchanged — same API) |
| `wb.SaveAs(path)` | `wb.Save(path)` |
| `using ClosedXML.Excel;` | `using PXA.Infrastructure.Spreadsheet;` |

## Diagnostics
- `CANMIGCLXL010` (Info) — applied 1-based→0-based index shift; verify computed indexes.
- `CANMIGCLXL020` (Warn) — other `.Style.*` (Fill/Border/Alignment) needs manual `.Style(s => …)`.
- `CANMIGCLXL021` (Warn) — `ws.Row(i)` has no direct builder method.
- `CANMIGCLXL022` (Warn) — R1C1 formula converted as-is (PXA expects A1).
- `CANMIGCLXL030` (Warn) — pivot tables unsupported.
- `CANMIGCLXL031` (Warn) — conditional formatting / auto-filter / data validation are model-level, not fluent.

## V2 next
Read-side `.Value` access; Fill/Border/Alignment style mapping; `ws.Row(i).Height`; named ranges; validate on real samples.
