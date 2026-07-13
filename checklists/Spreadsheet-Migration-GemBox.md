# Spreadsheet Migration — GemBox.Spreadsheet

Migrates GemBox.Spreadsheet (`ExcelFile`) authoring code → PXA spreadsheet API (`PxaWorkbook`).
GemBox cell indexes are **already 0-based** (no shift), bold is `Font.Weight = BoldWeight`, and a
`SpreadsheetInfo.SetLicense(...)` call is dropped.

- **Project:** `src/Migrations/Spreadsheet/PXA.Migration.Spreadsheet.Code.GemBox/`. **Converter:** `GemBoxSpreadsheetConverter` (`GemBoxSpreadsheet`, `full`).
- **Diagnostics:** `CANMIGGBSS`. **Tests:** `tests/PXA.Migration.Spreadsheet.Code.GemBox.Tests/` (2, green).

## API mapping
| GemBox.Spreadsheet | PXA |
|---|---|
| `SpreadsheetInfo.SetLicense(...)` | (removed) |
| `new ExcelFile()` | `new PxaWorkbook()` |
| `wb.Worksheets.Add("S")` | `wb.AddSheet("S")` |
| `ws.Cells["A1"]` / `ws.Cells[0,1]` (0-based) | `ws.Cell("A1")` / `ws.Cell(0,1)` |
| `ws.Cells["A1"].Value = v` | `ws.Cell("A1").Value(v)` |
| `ws.Cells["B1"].Formula = "=f"` | `ws.Cell("B1").Formula("=f")` |
| `.Style.Font.Weight = ExcelFont.BoldWeight` | `.Style(s => s.Bold())` |
| `.Style.Font.Italic = true` / `.Size = n` | `.Style(s => s.Italic(true) / s.FontSize(n))` |
| `wb.Save("x.xlsx")` | `wb.Save("x.xlsx")` (unchanged; stream/options flagged) |

## Diagnostics
`CANMIGGBSS020` GetSubrange/Merged (range merge) · `021` other styles · `024` Save(stream/options) · `030` charts/pivots.
