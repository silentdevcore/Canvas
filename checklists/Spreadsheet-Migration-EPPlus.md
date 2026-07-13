# Spreadsheet Migration — EPPlus

Migrates EPPlus (`ExcelPackage`) authoring code → PXA spreadsheet API (`PxaWorkbook`). Clones the
ClosedXML reference; EPPlus differs in the cell **indexer** (`Cells[..]`), the `Merge` property, the
`pkg.Workbook.Worksheets` path, and `SaveAs(FileInfo)`.

- **Project:** `src/Migrations/Spreadsheet/PXA.Migration.Spreadsheet.Code.Epplus/`. **Converter:** `EpplusSpreadsheetConverter` (`EpplusSpreadsheet`, `full`), in `MigrationService`.
- **Diagnostics:** `CANMIGEPPL`. **Tests:** `tests/PXA.Migration.Spreadsheet.Code.Epplus.Tests/` (3, green).

## API mapping
| EPPlus | PXA |
|---|---|
| `new ExcelPackage()` | `new PxaWorkbook()` |
| `pkg.Workbook.Worksheets.Add("S")` | `pkg.AddSheet("S")` |
| `ws.Cells["A1"]` | `ws.Cell("A1")` |
| `ws.Cells[1,2]` (1-based) | `ws.Cell(0,1)` |
| `ws.Cells["A1"].Value = v` | `ws.Cell("A1").Value(v)` |
| `ws.Cells["A1"].Formula = "f"` | `ws.Cell("A1").Formula("f")` |
| `ws.Cells["A1"].Style.Font.Bold/Italic/Size = v` | `.Style(s => s.Bold(v)/Italic(v)/FontSize(v))` |
| `ws.Cells["A1:B1"].Merge = true` | `ws.Range("A1:B1").Merge()` |
| `ws.Column(i).Width = w` | `ws.Column(i-1).Width(w)` |
| `pkg.SaveAs(...)` | `pkg.Save(...)` (string path — FileInfo/Stream flagged) |

## Diagnostics
`CANMIGEPPL010` index shift (Info) · `020` other styles · `021` Row(i) · `023` ExcelPackage(file) load ·
`024` SaveAs(FileInfo/Stream) · `025` Save() no-path · `030` pivots/charts/drawings · `031` CF/auto-filter/validation.
