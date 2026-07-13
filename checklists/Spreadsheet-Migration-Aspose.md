# Spreadsheet Migration — Aspose.Cells

Migrates Aspose.Cells (`Workbook`) authoring code → PXA spreadsheet API (`PxaWorkbook`). The most
divergent source: `PutValue()` method, `Worksheets[0]` default sheet, 0-based indexes, `GetStyle/SetStyle`.

- **Project:** `src/PXA.Migration.Spreadsheet.Code.Aspose/`. **Converter:** `AsposeCellsConverter` (`AsposeCells`, `full`).
- **Diagnostics:** `CANMIGASPC`. **Tests:** `tests/PXA.Migration.Spreadsheet.Code.Aspose.Tests/` (2, green).

## API mapping
| Aspose.Cells | PXA |
|---|---|
| `new Workbook()` | `new PxaWorkbook()` (+ default-sheet note) |
| `wb.Worksheets[0]` | `wb.AddSheet("Sheet1")` |
| `wb.Worksheets[i]` (i>0) | `wb.Sheet(i)` |
| `wb.Worksheets.Add("S")` | `wb.AddSheet("S")` |
| `ws.Cells["A1"]` / `ws.Cells[0,1]` (0-based) | `ws.Cell("A1")` / `ws.Cell(0,1)` |
| `cell.PutValue(v)` | `cell.Value(v)` (extra overload args dropped) |
| `cell.Formula = "=f"` | `cell.Formula("=f")` |
| `ws.Cells.SetColumnWidth(c, w)` | `ws.Column(c).Width(w)` |
| `wb.Save("x.xlsx")` | `wb.Save("x.xlsx")` (stream/SaveFormat flagged) |

## Diagnostics
`CANMIGASPC011` default-sheet note (Info) · `020` GetStyle/SetStyle styling · `022` PutValue extra args ·
`023` Workbook(path) load · `024` Save(stream/format) · `030` charts/pivots.

> ClosedXML's formula engine covers fewer functions than Aspose's ~450; exotic functions migrate
> structurally but may compute differently / `#ERROR` on `/calculate`.
