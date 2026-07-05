using PXA.Core.Contracts;
using CanvasSpreadsheet = Canvas.Infrastructure.Spreadsheet;

namespace PXA.Infrastructure.Spreadsheet;

/// <summary>
/// Power Dox Automation fluent authoring API for spreadsheet workbooks.
/// </summary>
public sealed class CanvasWorkbook
{
    private readonly CanvasSpreadsheet.CanvasWorkbook inner;

    public CanvasWorkbook(string name = "Workbook")
    {
        inner = new CanvasSpreadsheet.CanvasWorkbook(name);
    }

    public string Name { get => inner.Name; set => inner.Name = value; }

    public int SheetCount => inner.SheetCount;

    public CanvasWorksheet AddSheet(string name = "Sheet1") => new(inner.AddSheet(name));

    public CanvasWorksheet Sheet(int index) => new(inner.Sheet(index));

    public CanvasWorksheet Sheet(string name) => new(inner.Sheet(name));

    public CanvasWorkbook DefineName(string name, string refersTo)
    {
        inner.DefineName(name, refersTo);
        return this;
    }

    public SpreadsheetDto ToWorkbook() => inner.ToWorkbook().ToPxa();

    public byte[] ToXlsx(bool recalculate = false) => inner.ToXlsx(recalculate);

    public void Save(string path) => inner.Save(path);
}

public sealed class CanvasWorksheet
{
    private readonly CanvasSpreadsheet.CanvasWorksheet inner;

    internal CanvasWorksheet(CanvasSpreadsheet.CanvasWorksheet inner)
    {
        this.inner = inner;
    }

    public string Name { get => inner.Name; set => inner.Name = value; }

    public CanvasCell Cell(int row, int col) => new(inner.Cell(row, col));

    public CanvasCell Cell(string a1) => new(inner.Cell(a1));

    public CanvasWorksheet Merge(string a1Range)
    {
        inner.Merge(a1Range);
        return this;
    }

    public CanvasRange Range(string a1Range) => new(inner.Range(a1Range));

    public CanvasColumn Column(int index) => new(inner.Column(index));

    public CanvasWorksheet Freeze(int rows, int cols)
    {
        inner.Freeze(rows, cols);
        return this;
    }
}

public sealed class CanvasRange
{
    private readonly CanvasSpreadsheet.CanvasRange inner;

    internal CanvasRange(CanvasSpreadsheet.CanvasRange inner)
    {
        this.inner = inner;
    }

    public CanvasWorksheet Merge() => new(inner.Merge());
}

public sealed class CanvasColumn
{
    private readonly CanvasSpreadsheet.CanvasColumn inner;

    internal CanvasColumn(CanvasSpreadsheet.CanvasColumn inner)
    {
        this.inner = inner;
    }

    public CanvasColumn Width(double width)
    {
        inner.Width(width);
        return this;
    }

    public CanvasColumn Hidden(bool hidden = true)
    {
        inner.Hidden(hidden);
        return this;
    }

    public CanvasColumn OutlineLevel(int level)
    {
        inner.OutlineLevel(level);
        return this;
    }
}

public sealed class CanvasCell
{
    private readonly CanvasSpreadsheet.CanvasCell inner;

    internal CanvasCell(CanvasSpreadsheet.CanvasCell inner)
    {
        this.inner = inner;
    }

    public CanvasWorksheet Sheet => new(inner.Sheet);

    public CanvasCell Value(object? value)
    {
        inner.Value(value);
        return this;
    }

    public CanvasCell Formula(string formula)
    {
        inner.Formula(formula);
        return this;
    }

    public CanvasCell NumberFormat(string format)
    {
        inner.NumberFormat(format);
        return this;
    }

    public CanvasCell Comment(string text)
    {
        inner.Comment(text);
        return this;
    }

    public CanvasCell Hyperlink(string url)
    {
        inner.Hyperlink(url);
        return this;
    }

    public CanvasCell Style(Action<CanvasCellStyle> build)
    {
        inner.Style(style => build(new CanvasCellStyle(style)));
        return this;
    }
}

public sealed class CanvasCellStyle
{
    private readonly CanvasSpreadsheet.CanvasCellStyle inner;

    internal CanvasCellStyle(CanvasSpreadsheet.CanvasCellStyle inner)
    {
        this.inner = inner;
    }

    public CanvasCellStyle Bold(bool on = true) { inner.Bold(on); return this; }
    public CanvasCellStyle Italic(bool on = true) { inner.Italic(on); return this; }
    public CanvasCellStyle Background(string color) { inner.Background(color); return this; }
    public CanvasCellStyle Color(string color) { inner.Color(color); return this; }
    public CanvasCellStyle Font(string family) { inner.Font(family); return this; }
    public CanvasCellStyle FontSize(double size) { inner.FontSize(size); return this; }
    public CanvasCellStyle Align(string textAlign) { inner.Align(textAlign); return this; }
}
