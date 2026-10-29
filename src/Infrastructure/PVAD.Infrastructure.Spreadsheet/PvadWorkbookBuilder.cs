using System.Globalization;
using PVAD.Core.Contracts;
using PVAD.Core.Primitives;

namespace PVAD.Infrastructure.Spreadsheet;

/// <summary>
/// A fluent authoring API over <see cref="SpreadsheetDto"/> — the idiomatic PVAD way to build a workbook
/// in C#, and the rewrite target for spreadsheet-library migrations (ClosedXML / EPPlus / GemBox / Aspose).
/// Wraps the existing <see cref="ExcelWorkbookExporter"/>, <see cref="XlsWorkbookIo"/>, and
/// <see cref="CsvSheetIo"/> for output.
/// </summary>
/// <example>
/// var wb = new PvadWorkbook();
/// var ws = wb.AddSheet("Sales");
/// ws.Cell("A1").Value("Item").Style(s => s.Bold());
/// ws.Cell(0, 1).Value(10);
/// ws.Cell("B2").Formula("=B1*2");
/// ws.Range("A1:B1").Merge();
/// wb.Save("out.xlsx");
/// </example>
public sealed class PvadWorkbook
{
    private readonly SpreadsheetDto _wb;
    private readonly List<PvadWorksheet> _sheets = [];

    public PvadWorkbook(string name = "Workbook")
    {
        _wb = new SpreadsheetDto { Id = Guid.NewGuid().ToString("n"), Name = name };
    }

    public string Name { get => _wb.Name; set => _wb.Name = value; }

    public PvadWorksheet AddSheet(string name = "Sheet1")
    {
        var dto = new SheetDto { Id = Guid.NewGuid().ToString("n"), Name = name };
        _wb.Sheets.Add(dto);
        var ws = new PvadWorksheet(dto);
        _sheets.Add(ws);
        return ws;
    }

    public PvadWorksheet Sheet(int index) => _sheets[index];
    public PvadWorksheet Sheet(string name) => _sheets.First(s => s.Name == name);
    public int SheetCount => _sheets.Count;

    /// <summary>Adds a named range, e.g. <c>DefineName("Sales", "Sheet1!A1:A10")</c>.</summary>
    public PvadWorkbook DefineName(string name, string refersTo)
    {
        _wb.DefinedNames.Add(new DefinedNameDto { Name = name, RefersTo = refersTo });
        return this;
    }

    /// <summary>The underlying PVADS Workbook JSON model.</summary>
    public SpreadsheetDto ToWorkbook() => _wb;

    public byte[] ToXlsx(bool recalculate = false) => new ExcelWorkbookExporter().Export(_wb, recalculate);

    /// <summary>Writes the workbook to disk; format is chosen by extension (.xlsx, .xls, .csv, .tsv).</summary>
    public void Save(string path)
    {
        var bytes = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".xls" => new XlsWorkbookIo().Export(_wb),
            ".csv" => System.Text.Encoding.UTF8.GetBytes(_wb.Sheets.Count > 0 ? CsvSheetIo.ToCsv(_wb.Sheets[0]) : ""),
            ".tsv" => System.Text.Encoding.UTF8.GetBytes(_wb.Sheets.Count > 0 ? CsvSheetIo.ToCsv(_wb.Sheets[0], '\t') : ""),
            _ => ToXlsx(),
        };
        File.WriteAllBytes(path, bytes);
    }
}

public sealed class PvadWorksheet
{
    private readonly SheetDto _sheet;
    private readonly Dictionary<(int, int), CellDto> _cells = [];

    internal PvadWorksheet(SheetDto sheet)
    {
        _sheet = sheet;
        foreach (var c in sheet.Cells) _cells[(c.Row, c.Col)] = c;
    }

    public string Name { get => _sheet.Name; set => _sheet.Name = value; }

    public PvadCell Cell(int row, int col)
    {
        if (!_cells.TryGetValue((row, col), out var dto))
        {
            dto = new CellDto { Row = row, Col = col };
            _cells[(row, col)] = dto;
            _sheet.Cells.Add(dto);
        }
        return new PvadCell(this, dto);
    }

    public PvadCell Cell(string a1)
    {
        var r = A1Reference.Parse(a1);
        return Cell(r.Row, r.Col);
    }

    public PvadWorksheet Merge(string a1Range) { _sheet.Merges.Add(a1Range); return this; }

    public PvadRange Range(string a1Range) => new(this, a1Range);

    public PvadColumn Column(int index) => new(GetColumn(index));

    public PvadWorksheet Freeze(int rows, int cols) { _sheet.FrozenRows = rows; _sheet.FrozenCols = cols; return this; }

    private SheetColumnDto GetColumn(int index)
    {
        var col = _sheet.Columns.FirstOrDefault(c => c.Index == index);
        if (col is null) { col = new SheetColumnDto { Index = index }; _sheet.Columns.Add(col); }
        return col;
    }
}

public sealed class PvadRange
{
    private readonly PvadWorksheet _ws;
    private readonly string _a1;
    internal PvadRange(PvadWorksheet ws, string a1) { _ws = ws; _a1 = a1; }
    public PvadWorksheet Merge() => _ws.Merge(_a1);
}

public sealed class PvadColumn
{
    private readonly SheetColumnDto _col;
    internal PvadColumn(SheetColumnDto col) { _col = col; }
    public PvadColumn Width(double width) { _col.Width = width; return this; }
    public PvadColumn Hidden(bool hidden = true) { _col.Hidden = hidden; return this; }
    public PvadColumn OutlineLevel(int level) { _col.OutlineLevel = level; return this; }
}

public sealed class PvadCell
{
    private readonly PvadWorksheet _ws;
    private readonly CellDto _dto;
    internal PvadCell(PvadWorksheet ws, CellDto dto) { _ws = ws; _dto = dto; }

    /// <summary>The owning worksheet — lets generated code chain back, e.g. <c>ws.Cell("A1").Value(1).Sheet</c>.</summary>
    public PvadWorksheet Sheet => _ws;

    /// <summary>Sets the value, inferring the cell type from the runtime type.</summary>
    public PvadCell Value(object? value)
    {
        switch (value)
        {
            case null: _dto.Type = "empty"; _dto.Value = null; break;
            case bool b: _dto.Type = "boolean"; _dto.Value = b; break;
            case DateTime dt: _dto.Type = "date"; _dto.Value = dt.ToString("o", CultureInfo.InvariantCulture); break;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                _dto.Type = "number"; _dto.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture); break;
            default: _dto.Type = "text"; _dto.Value = value.ToString(); break;
        }
        return this;
    }

    public PvadCell Formula(string formula)
    {
        _dto.Type = "formula";
        _dto.Formula = formula.StartsWith('=') ? formula : "=" + formula;
        return this;
    }

    public PvadCell NumberFormat(string format) { _dto.NumberFormat = format; return this; }
    public PvadCell Comment(string text) { _dto.Comment = text; return this; }
    public PvadCell Hyperlink(string url) { _dto.Hyperlink = url; return this; }

    public PvadCell Style(Action<PvadCellStyle> build)
    {
        _dto.Style ??= new CellStyleDto();
        build(new PvadCellStyle(_dto.Style));
        return this;
    }
}

public sealed class PvadCellStyle
{
    private readonly CellStyleDto _s;
    internal PvadCellStyle(CellStyleDto s) { _s = s; }
    public PvadCellStyle Bold(bool on = true) { _s.Bold = on; return this; }
    public PvadCellStyle Italic(bool on = true) { _s.Italic = on; return this; }
    public PvadCellStyle Background(string color) { _s.BackgroundColor = color; return this; }
    public PvadCellStyle Color(string color) { _s.Color = color; return this; }
    public PvadCellStyle Font(string family) { _s.FontFamily = family; return this; }
    public PvadCellStyle FontSize(double size) { _s.FontSize = size; return this; }
    public PvadCellStyle Align(string textAlign) { _s.TextAlign = textAlign; return this; }
}
