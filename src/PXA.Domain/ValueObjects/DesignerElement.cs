namespace PXA.Domain.ValueObjects;

public class DesignerElement
{
    public required string Id { get; set; }
    public required ElementType Type { get; set; }
    public required Dictionary<string, object> Props { get; set; } = new();
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public List<string>? Children { get; set; }
    public bool IsGroup { get; set; }
    public string? GroupId { get; set; }
    public BindingConfig? Binding { get; set; }
    public ExpressionConfig? Expression { get; set; }
    public RepeatConfig? Repeat { get; set; }
    public OverflowConfig? Overflow { get; set; }
    public ImageConfig? Image { get; set; }
    public TableConfig? Table { get; set; }
    public ValidationConfig? Validation { get; set; }
    public TextConfig? Text { get; set; }
    public RichTextConfig? RichText { get; set; }
    public TextFieldConfig? TextField { get; set; }
    public DropdownConfig? Dropdown { get; set; }
    public CheckboxConfig? Checkbox { get; set; }
    public ButtonConfig? Button { get; set; }
    public ListConfig? List { get; set; }
    public WatermarkConfig? Watermark { get; set; }
    public NoteConfig? Note { get; set; }
    public ArrowConfig? Arrow { get; set; }
    public DrawConfig? Draw { get; set; }
    public DateConfig? Date { get; set; }
    public HighlightConfig? Highlight { get; set; }
    public CheckMarkConfig? CheckMark { get; set; }
    public PageBoundaryConfig? PageBoundary { get; set; }
    public PageNumberConfig? PageNumber { get; set; }
    public bool Locked { get; set; }
}

public class BindingConfig
{
    public string? DataPath { get; set; }
    public object? FallbackValue { get; set; }
    public string? Formatter { get; set; }
    public bool Required { get; set; }
    public string? RequiredMessage { get; set; }
    public string? ValueType { get; set; }
    public string? BindingScope { get; set; }
}

public class ExpressionConfig
{
    public string? VisibleWhen { get; set; }
    public string? EnabledWhen { get; set; }
    public string? ValueExpression { get; set; }
    public Dictionary<string, string>? StyleExpression { get; set; }
    public bool SafeExpressionMode { get; set; }
}

public class RepeatConfig
{
    public string? RepeatSource { get; set; }
    public string? ItemAlias { get; set; }
    public string? IndexAlias { get; set; }
    public int? MaxItems { get; set; }
    public string? EmptyBehavior { get; set; }
    public string? EmptyRowText { get; set; }
    public bool? PageBreakBetweenItems { get; set; }
    public string? RowTemplateMode { get; set; }
}

public class OverflowConfig
{
    public string? TextOverflow { get; set; }
    public int? MaxLines { get; set; }
    public bool? KeepTogether { get; set; }
    public bool? AvoidPageBreakInside { get; set; }
    public string? Anchor { get; set; }
    public string? Alignment { get; set; }
}

public class ImageConfig
{
    public string? FitMode { get; set; }
    public string? CropX { get; set; }
    public string? CropY { get; set; }
    public string? CropWidth { get; set; }
    public string? CropHeight { get; set; }
    public string? FocalX { get; set; }
    public string? FocalY { get; set; }
    public string? RemoteFetchPolicy { get; set; }
    public string? PlaceholderImage { get; set; }
    public bool? PreserveAspectRatio { get; set; }
}

public class TableConfig
{
    public string? DataPath { get; set; }
    public bool? HeaderRepeatOnPageBreak { get; set; }
    public List<ColumnConfig>? Columns { get; set; }
    public bool? RowStriping { get; set; }
    public Dictionary<string, string>? ConditionalRowStyles { get; set; }
    public string? EmptyRowsPolicy { get; set; }
}

public class ColumnConfig
{
    public string? Header { get; set; }
    public string? DataPath { get; set; }
    public string? Formatter { get; set; }
    public double? Width { get; set; }
    public string? Alignment { get; set; }
}

public class ValidationConfig
{
    public string? ElementValidationMode { get; set; }
    public string? CustomErrorMessage { get; set; }
    public string? DebugLabel { get; set; }
    public string? DiagnosticId { get; set; }
}

public class TextConfig
{
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
    public string? Alignment { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public double? LineHeight { get; set; }
    public int? MaxLines { get; set; }
    public string? Language { get; set; }
    public string? TextDirection { get; set; }
}

public class RichTextConfig
{
    public string? Content { get; set; }
    public string? FontFamily { get; set; }
    public double? BaseFontSize { get; set; }
    public string? Color { get; set; }
    public string? Alignment { get; set; }
    public List<string>? AllowedTags { get; set; }
    public string? Language { get; set; }
    public string? TextDirection { get; set; }
}

public class TextFieldConfig
{
    public string? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public int? MaxLength { get; set; }
    public bool? Multiline { get; set; }
    public bool? ReadOnly { get; set; }
    public bool? Required { get; set; }
    public string? ValidationPattern { get; set; }
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
    public string? Language { get; set; }
    public string? TextDirection { get; set; }
}

public class DropdownConfig
{
    public List<string>? Options { get; set; }
    public string? SelectedValue { get; set; }
    public bool? MultiSelect { get; set; }
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
}

public class CheckboxConfig
{
    public string? Label { get; set; }
    public bool? Checked { get; set; }
    public double? Size { get; set; }
    public string? Color { get; set; }
    public bool? Disabled { get; set; }
}

public class ButtonConfig
{
    public string? Label { get; set; }
    public string? Action { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public double? FontSize { get; set; }
    public double? BorderRadius { get; set; }
}

public class ListConfig
{
    public List<string>? Items { get; set; }
    public bool? Ordered { get; set; }
    public string? MarkerStyle { get; set; }
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
}

public class WatermarkConfig
{
    public string? Mode { get; set; }
    public string? Content { get; set; }
    public string? PageScope { get; set; }
    public string? PageRange { get; set; }
    public string? Color { get; set; }
    public double? Opacity { get; set; }
    public double? Rotation { get; set; }
    public double? Scale { get; set; }
    public double? FontSize { get; set; }
}

public class NoteConfig
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Author { get; set; }
    public bool? Collapsed { get; set; }
    public string? BackgroundColor { get; set; }
    public string? Color { get; set; }
}

public class ArrowConfig
{
    public string? Mode { get; set; }
    public string? StartMarker { get; set; }
    public string? EndMarker { get; set; }
    public string? Color { get; set; }
    public double? StrokeWidth { get; set; }
    public string? DashStyle { get; set; }
}

public class DrawConfig
{
    public string? Tool { get; set; }
    public string? PathData { get; set; }
    public string? Color { get; set; }
    public double? StrokeWidth { get; set; }
    public double? Opacity { get; set; }
}

public class DateConfig
{
    public string? Mode { get; set; }
    public string? Value { get; set; }
    public string? Binding { get; set; }
    public string? Format { get; set; }
    public string? Locale { get; set; }
    public string? Timezone { get; set; }
    public string? FallbackText { get; set; }
    public string? Color { get; set; }
    public double? FontSize { get; set; }
}

public class HighlightConfig
{
    public string? Mode { get; set; }
    public string? Color { get; set; }
    public double? Opacity { get; set; }
    public double? BorderRadius { get; set; }
    public string? BlendMode { get; set; }
}

public class CheckMarkConfig
{
    public string? Label { get; set; }
    public string? Name { get; set; }
    public string? State { get; set; }
    public string? Color { get; set; }
    public double? StrokeWidth { get; set; }
    public string? Binding { get; set; }
}

public class PageBoundaryConfig
{
    public string? Mode { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }
}

public class PageNumberConfig
{
    public string? Format { get; set; }
    public string? PageScope { get; set; }
    public string? PageRange { get; set; }
    public int? StartNumber { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public string? Color { get; set; }
    public double? FontSize { get; set; }
}
