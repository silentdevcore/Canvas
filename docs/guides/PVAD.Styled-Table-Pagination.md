# Styled Tables With Automatic Pagination

Use `PdfFlowContext.AddSimpleTable` in native C# when the number or height of
rows is not known in advance. Use `PdfPage.DrawSimpleTable` when you control
each page explicitly. Both paths use the same cell measurement and painting
rules. Designer tables remain explicitly positioned elements; this API does
not enable automatic page creation in the Designer.

## Create A Multi-Page Table

```csharp
using System.Collections.Generic;
using PVAD.Pdf;

var document = PVAD.Generator.Pdf.CreateDocument();
var flow = document.CreateFlow(new PdfFlowOptions
{
    MarginLeft = 40,
    MarginRight = 40,
    MarginTop = 40,
    MarginBottom = 40
});
var rows = new List<IReadOnlyList<string>> { new[] { "Item", "Quantity" } };
for (var index = 1; index <= 40; index++)
    rows.Add(new[] { $"Product {index}", index.ToString() });
rows.Add(new[] { "Total", "820" });

var layout = flow.AddSimpleTable(rows, new PdfTableOptions
{
    HasHeaderRow = true,
    HasFooterRow = true,
    RepeatFooterOnEachChunk = false,
    AutoRowHeight = true,
    RowHeight = 24,
    ColumnWidths = new[] { 3d, 1d },
    CellStyles = new PdfTableCellStyle[]
    {
        new()
        {
            Row = 0, Column = 0, FontSize = 13, Bold = false,
            BackgroundColor = PdfColor.FromRgb(220, 235, 255)
        },
        new()
        {
            Row = 5, Column = 0, FontSize = 18, Italic = true, Padding = 8,
            BorderBottom = new PdfTableCellBorder
            {
                Width = 2, Color = PdfColor.FromRgb(0, 100, 80)
            }
        },
        new() { Row = 41, Column = 1, Bold = true }
    }
});

byte[] pdf = document.ToBytes();
```

All positions and dimensions are PDF points. Supply matching font files through
`document.FontLoader` when embedded fonts are required. The example uses the
default standard font; embedding and archival conformance are separate choices.

## Indexing And Page Breaks

1. `CellStyles.Row`, `RowHeights` and `RowLineHeights` address the original
   complete row list, including the header at index zero and the final footer.
   Do not renumber these arrays for later pages.
2. Enable `AutoRowHeight` to measure wrapped text using each cell's font, size
   and padding. Explicit `RowHeights` then act as minimums. Without automatic
   height, the supplied heights remain fixed.
3. The header repeats on each table page. A header is not left on a page without
   its first data row when data exists.
4. `RepeatFooterOnEachChunk = true` repeats the footer on every table page;
   `false` reserves it for the final page. Footer-only tables also work.
5. Row options and cell styles are remapped for each chunk. Alternating row
   fills continue their original data-row sequence across page breaks.
6. Shared borders follow the same explicit-rule, width and right/bottom tie
   precedence as explicitly positioned tables. Each page paints its own grid.
7. Optional `ShowContinuationMarkers` reserves space for the continuation header
   and footer rather than painting them over the table or outside the margins.

`layout.Height` is the total table height actually painted, including repeated
headers and footers. `layout.BottomY` is the bottom of the last chunk on its
page; `flow.CurrentY` also includes the configured paragraph spacing. These
values are not a single bounding rectangle spanning multiple PDF pages.

## Errors And Limits

Rows remain whole; a row is never silently clipped or split across pages. If a
row cannot fit on a fresh page together with the required header, footer and
continuation markers, `AddSimpleTable` throws `InvalidOperationException`
before painting any table chunk. Reduce content, font size or padding, or use
a larger page. Existing document content remains intact for these layout
validation failures.

Invalid or non-finite row metrics are rejected before pagination. Row arrays
must match the original number of rows; header and footer must be separate
rows. Formatters should be deterministic because layout measurement and
painting may evaluate them separately.

This native flow API is not a promise that arbitrary low-level C# imports can
be reconstructed as semantic Designer tables. Font availability, code-worker
restrictions and the existing Code Workspace fidelity rules remain unchanged.
