using System.Text.Json.Serialization;

namespace PXA.Core.Contracts;

/// <summary>
/// A workbook for the Spreadsheet Editor SDK — an Excel-like model distinct from the document
/// <see cref="DesignExportDto"/>. This is the canonical <b>PXA Workbook JSON</b> format (camelCase);
/// round-trips to/from <c>.xlsx</c> (ClosedXML) preserving typed values, formulas, number formats, styles,
/// merges, column widths, frozen panes, and defined names.
/// </summary>
public sealed class SpreadsheetDto
{
    /// <summary>Current PXA Workbook JSON schema version. Bump the major when a change is breaking.</summary>
    public const string CurrentSchemaVersion = "1.0";

    /// <summary>Optional JSON Schema URL for editor/tooling validation (the <c>$schema</c> property).</summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    /// <summary>PXA Workbook JSON format version, e.g. "1.0". Defaults so older payloads still load.</summary>
    public string SchemaVersion { get; set; } = CurrentSchemaVersion;

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
    /// <summary>Range (A1) the auto-filter dropdowns cover, e.g. "A1:D20".</summary>
    public string? AutoFilterRange { get; set; }
    public PageSetupDto? PageSetup { get; set; }
    public ProtectionDto? Protection { get; set; }
    public List<ConditionalFormatDto> ConditionalFormats { get; set; } = [];
    public List<DataValidationDto> DataValidations { get; set; } = [];
    /// <summary>Images anchored to worksheet cells. Persisted documents use <see cref="SpreadsheetImageDto.AssetId"/>;</summary>
    public List<SpreadsheetImageDto> Images { get; set; } = [];
}

public sealed class SpreadsheetImageDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public Guid? AssetId { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    /// <summary>Transient import/export payload. Designer persistence replaces this with <see cref="AssetId"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Data { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int Width { get; set; } = 160;
    public int Height { get; set; } = 90;
    public string? AltText { get; set; }
}

public sealed class SheetColumnDto
{
    /// <summary>0-based column index.</summary>
    public int Index { get; set; }
    /// <summary>Width in Excel character units (ClosedXML's column width unit).</summary>
    public double? Width { get; set; }
    public bool Hidden { get; set; }
    /// <summary>Outline/grouping level (0 = none).</summary>
    public int OutlineLevel { get; set; }
}

public sealed class SheetRowDto
{
    /// <summary>0-based row index.</summary>
    public int Index { get; set; }
    /// <summary>Height in points.</summary>
    public double? Height { get; set; }
    public bool Hidden { get; set; }
    /// <summary>Outline/grouping level (0 = none).</summary>
    public int OutlineLevel { get; set; }
}

/// <summary>Print / page setup for a sheet.</summary>
public sealed class PageSetupDto
{
    public string? Orientation { get; set; }   // "portrait" | "landscape"
    public string? PaperSize { get; set; }     // "A4" | "Letter" | "A3" | …
    public string? PrintArea { get; set; }     // A1 range
    public string? Header { get; set; }        // center header text
    public string? Footer { get; set; }        // center footer text
    public int? FitToWidth { get; set; }       // fit-to N pages wide
    public int? FitToHeight { get; set; }      // fit-to N pages tall
    public double? Scale { get; set; }         // % scale (when not fit-to-page)
    public MarginsDto? Margins { get; set; }
    public List<int> RowPageBreaks { get; set; } = [];  // 0-based row indices
    public List<int> ColPageBreaks { get; set; } = [];  // 0-based column indices
}

/// <summary>Sheet protection (read-only cells except where unlocked).</summary>
public sealed class ProtectionDto
{
    public bool Protected { get; set; }
    public string? Password { get; set; }
}

/// <summary>A conditional-formatting rule over a range. (Export to .xlsx; import is best-effort.)</summary>
public sealed class ConditionalFormatDto
{
    public string Range { get; set; } = "";
    /// <summary>"cellIs" | "colorScale" | "dataBar".</summary>
    public string Type { get; set; } = "cellIs";
    /// <summary>For cellIs: "greaterThan" | "lessThan" | "equalTo" | "between" | "contains".</summary>
    public string? Operator { get; set; }
    public string? Value { get; set; }
    public string? Value2 { get; set; }
    /// <summary>Fill color applied when the rule matches (cellIs), or the high color (colorScale/dataBar).</summary>
    public string? Color { get; set; }
}

/// <summary>A data-validation rule over a range. (Export to .xlsx; import is best-effort.)</summary>
public sealed class DataValidationDto
{
    public string Range { get; set; } = "";
    /// <summary>"list" | "wholeNumber" | "decimal" | "date" | "textLength".</summary>
    public string Type { get; set; } = "list";
    /// <summary>For non-list types: "between" | "greaterThan" | "lessThan" | "equalTo".</summary>
    public string? Operator { get; set; }
    public string? Value1 { get; set; }
    public string? Value2 { get; set; }
    /// <summary>For list: comma-separated allowed values, or an A1 range reference.</summary>
    public string? ListSource { get; set; }
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
    /// <summary>Cell comment/note text.</summary>
    public string? Comment { get; set; }
    /// <summary>Hyperlink target (URL or internal "Sheet!A1").</summary>
    public string? Hyperlink { get; set; }
}

public sealed class DefinedNameDto
{
    public string Name { get; set; } = "";
    public string RefersTo { get; set; } = "";
}
