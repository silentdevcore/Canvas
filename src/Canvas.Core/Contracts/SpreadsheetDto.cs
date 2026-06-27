namespace Canvas.Core.Contracts;

/// <summary>
/// A workbook for the Spreadsheet Editor SDK — an Excel-like model distinct from the document
/// <see cref="DesignExportDto"/>. Round-trips to/from <c>.xlsx</c> (ClosedXML) preserving typed values,
/// formulas, number formats, styles, merges, column widths, frozen panes, and defined names.
/// </summary>
public sealed class SpreadsheetDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Workbook";
    public List<SheetDto> Sheets { get; set; } = [];
    /// <summary>Named ranges (e.g. "Sales" → "Sheet1!$A$1:$A$10").</summary>
    public List<DefinedNameDto> DefinedNames { get; set; } = [];
}

public sealed class SheetDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Sheet1";
    /// <summary>Logical grid extent (the editor may show more; cells are sparse).</summary>
    public int RowCount { get; set; } = 100;
    public int ColCount { get; set; } = 26;
    public List<SheetColumnDto> Columns { get; set; } = [];
    public List<SheetRowDto> Rows { get; set; } = [];
    /// <summary>Sparse cells — only non-empty/styled cells are present.</summary>
    public List<CellDto> Cells { get; set; } = [];
    /// <summary>Merged ranges in A1 notation, e.g. "A1:B2".</summary>
    public List<string> Merges { get; set; } = [];
    public int FrozenRows { get; set; }
    public int FrozenCols { get; set; }
}

public sealed class SheetColumnDto
{
    /// <summary>0-based column index.</summary>
    public int Index { get; set; }
    /// <summary>Width in Excel character units (ClosedXML's column width unit).</summary>
    public double? Width { get; set; }
    public bool Hidden { get; set; }
}

public sealed class SheetRowDto
{
    /// <summary>0-based row index.</summary>
    public int Index { get; set; }
    /// <summary>Height in points.</summary>
    public double? Height { get; set; }
    public bool Hidden { get; set; }
}

/// <summary>A single sparse cell. <see cref="Row"/>/<see cref="Col"/> are 0-based.</summary>
public sealed class CellDto
{
    public int Row { get; set; }
    public int Col { get; set; }
    /// <summary>"number" | "text" | "boolean" | "date" | "formula" | "empty".</summary>
    public string Type { get; set; } = "empty";
    /// <summary>The literal value (text/number/bool/date) or, for a formula cell, its last computed value.</summary>
    public object? Value { get; set; }
    /// <summary>An A1 formula starting with '=', e.g. "=SUM(A1:A10)". Set when <see cref="Type"/> = "formula".</summary>
    public string? Formula { get; set; }
    /// <summary>Excel number-format code, e.g. "#,##0.00", "0%", "dd.MM.yyyy".</summary>
    public string? NumberFormat { get; set; }
    /// <summary>Cell styling. Reuses <see cref="CellStyleDto"/>; its Row/Col are unused here (this cell's own
    /// <see cref="Row"/>/<see cref="Col"/> are authoritative).</summary>
    public CellStyleDto? Style { get; set; }
}

public sealed class DefinedNameDto
{
    public string Name { get; set; } = "";
    public string RefersTo { get; set; } = "";
}
