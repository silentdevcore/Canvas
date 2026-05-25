namespace Canvas.Domain.ValueObjects;

public class DesignerElement
{
    public required string Id { get; set; }
    public required ElementType Type { get; set; }
    public required Dictionary<string, object> Props { get; set; } = new();

    // Layout properties
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    // Hierarchy
    public List<string>? Children { get; set; }
    public bool IsGroup { get; set; }
    public string? GroupId { get; set; }

    // Dynamic features
    public BindingConfig? Binding { get; set; }
    public ExpressionConfig? Expression { get; set; }
    public RepeatConfig? Repeat { get; set; }

    // Layout and overflow
    public OverflowConfig? Overflow { get; set; }

    // Element-specific configs
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

    // UI state
    public bool Locked { get; set; }

    public void MigratePropsToConfig()
    {
        switch (Type)
        {
            case ElementType.Text:
                Text = new TextConfig
                {
                    FontFamily = Props.GetValueOrDefault("FontFamily") as string,
                    FontSize = Props.GetValueOrDefault("FontSize") as double?,
                    Color = Props.GetValueOrDefault("Color") as string,
                    Alignment = Props.GetValueOrDefault("Alignment") as string,
                    Bold = Props.GetValueOrDefault("Bold") as bool?,
                    Italic = Props.GetValueOrDefault("Italic") as bool?,
                    Underline = Props.GetValueOrDefault("Underline") as bool?,
                    LineHeight = Props.GetValueOrDefault("LineHeight") as double?,
                    MaxLines = Props.GetValueOrDefault("MaxLines") as int?,
                    Language = Props.GetValueOrDefault("Language") as string,
                    TextDirection = Props.GetValueOrDefault("TextDirection") as string
                };
                break;
            case ElementType.RichText:
                RichText = new RichTextConfig
                {
                    Content = Props.GetValueOrDefault("Content") as string,
                    FontFamily = Props.GetValueOrDefault("FontFamily") as string,
                    BaseFontSize = Props.GetValueOrDefault("BaseFontSize") as double?,
                    Color = Props.GetValueOrDefault("Color") as string,
                    Alignment = Props.GetValueOrDefault("Alignment") as string,
                    AllowedTags = Props.GetValueOrDefault("AllowedTags") as List<string>,
                    Language = Props.GetValueOrDefault("Language") as string,
                    TextDirection = Props.GetValueOrDefault("TextDirection") as string
                };
                break;
            case ElementType.TextField:
                TextField = new TextFieldConfig
                {
                    DefaultValue = Props.GetValueOrDefault("DefaultValue") as string,
                    Placeholder = Props.GetValueOrDefault("Placeholder") as string,
                    MaxLength = Props.GetValueOrDefault("MaxLength") as int?,
                    Multiline = Props.GetValueOrDefault("Multiline") as bool?,
                    ReadOnly = Props.GetValueOrDefault("ReadOnly") as bool?,
                    Required = Props.GetValueOrDefault("Required") as bool?,
                    ValidationPattern = Props.GetValueOrDefault("ValidationPattern") as string,
                    FontFamily = Props.GetValueOrDefault("FontFamily") as string,
                    FontSize = Props.GetValueOrDefault("FontSize") as double?,
                    Color = Props.GetValueOrDefault("Color") as string,
                    Language = Props.GetValueOrDefault("Language") as string,
                    TextDirection = Props.GetValueOrDefault("TextDirection") as string
                };
                break;
            case ElementType.Dropdown:
                Dropdown = new DropdownConfig
                {
                    Options = Props.GetValueOrDefault("Options") as List<string>,
                    SelectedValue = Props.GetValueOrDefault("SelectedValue") as string,
                    MultiSelect = Props.GetValueOrDefault("MultiSelect") as bool?,
                    FontFamily = Props.GetValueOrDefault("FontFamily") as string,
                    FontSize = Props.GetValueOrDefault("FontSize") as double?,
                    Color = Props.GetValueOrDefault("Color") as string
                };
                break;
            case ElementType.Checkbox:
                Checkbox = new CheckboxConfig
                {
                    Label = Props.GetValueOrDefault("Label") as string,
                    Checked = Props.GetValueOrDefault("Checked") as bool?,
                    Size = Props.GetValueOrDefault("Size") as double?,
                    Color = Props.GetValueOrDefault("Color") as string,
                    Disabled = Props.GetValueOrDefault("Disabled") as bool?
                };
                break;
            case ElementType.Button:
                Button = new ButtonConfig
                {
                    Label = Props.GetValueOrDefault("Label") as string,
                    Action = Props.GetValueOrDefault("Action") as string,
                    BackgroundColor = Props.GetValueOrDefault("BackgroundColor") as string,
                    TextColor = Props.GetValueOrDefault("TextColor") as string,
                    FontSize = Props.GetValueOrDefault("FontSize") as double?,
                    BorderRadius = Props.GetValueOrDefault("BorderRadius") as double?
                };
                break;
            case ElementType.List:
                List = new ListConfig
                {
                    Items = Props.GetValueOrDefault("Items") as List<string>,
                    Ordered = Props.GetValueOrDefault("Ordered") as bool?,
                    MarkerStyle = Props.GetValueOrDefault("MarkerStyle") as string,
                    FontFamily = Props.GetValueOrDefault("FontFamily") as string,
                    FontSize = Props.GetValueOrDefault("FontSize") as double?
                };
                break;
            case ElementType.Watermark:
                Watermark = new WatermarkConfig
                {
                    Mode = GetStringProp("mode") ?? GetStringProp("Mode"),
                    Content = GetStringProp("content") ?? GetStringProp("Content"),
                    PageScope = GetStringProp("pageScope") ?? GetStringProp("PageScope"),
                    PageRange = GetStringProp("pageRange") ?? GetStringProp("PageRange"),
                    Color = GetStringProp("color") ?? GetStringProp("Color"),
                    Opacity = GetDoubleProp("opacity") ?? GetDoubleProp("Opacity"),
                    Rotation = GetDoubleProp("rotation") ?? GetDoubleProp("Rotation"),
                    Scale = GetDoubleProp("scale") ?? GetDoubleProp("Scale"),
                    FontSize = GetDoubleProp("fontSize") ?? GetDoubleProp("FontSize")
                };
                break;
            case ElementType.Note:
                Note = new NoteConfig
                {
                    Title = GetStringProp("title") ?? GetStringProp("Title"),
                    Body = GetStringProp("body") ?? GetStringProp("Body"),
                    Author = GetStringProp("author") ?? GetStringProp("Author"),
                    Collapsed = GetBoolProp("collapsed") ?? GetBoolProp("Collapsed"),
                    BackgroundColor = GetStringProp("backgroundColor") ?? GetStringProp("BackgroundColor"),
                    Color = GetStringProp("color") ?? GetStringProp("Color")
                };
                break;
            case ElementType.Arrow:
                Arrow = new ArrowConfig
                {
                    Mode = GetStringProp("mode") ?? GetStringProp("Mode"),
                    StartMarker = GetStringProp("startMarker") ?? GetStringProp("StartMarker"),
                    EndMarker = GetStringProp("endMarker") ?? GetStringProp("EndMarker"),
                    Color = GetStringProp("color") ?? GetStringProp("Color"),
                    StrokeWidth = GetDoubleProp("strokeWidth") ?? GetDoubleProp("StrokeWidth"),
                    DashStyle = GetStringProp("dashStyle") ?? GetStringProp("DashStyle")
                };
                break;
            case ElementType.Draw:
                Draw = new DrawConfig
                {
                    Tool = GetStringProp("tool") ?? GetStringProp("Tool"),
                    PathData = GetStringProp("pathData") ?? GetStringProp("PathData"),
                    Color = GetStringProp("color") ?? GetStringProp("Color"),
                    StrokeWidth = GetDoubleProp("strokeWidth") ?? GetDoubleProp("StrokeWidth"),
                    Opacity = GetDoubleProp("opacity") ?? GetDoubleProp("Opacity")
                };
                break;
            case ElementType.Date:
                Date = new DateConfig
                {
                    Mode = GetStringProp("mode") ?? GetStringProp("Mode"),
                    Value = GetStringProp("value") ?? GetStringProp("Value"),
                    Binding = GetStringProp("binding") ?? GetStringProp("Binding"),
                    Format = GetStringProp("format") ?? GetStringProp("Format"),
                    Locale = GetStringProp("locale") ?? GetStringProp("Locale"),
                    Timezone = GetStringProp("timezone") ?? GetStringProp("Timezone"),
                    FallbackText = GetStringProp("fallbackText") ?? GetStringProp("FallbackText"),
                    Color = GetStringProp("color") ?? GetStringProp("Color"),
                    FontSize = GetDoubleProp("fontSize") ?? GetDoubleProp("FontSize")
                };
                break;
            case ElementType.Highlight:
                Highlight = new HighlightConfig
                {
                    Mode = GetStringProp("mode") ?? GetStringProp("Mode"),
                    Color = GetStringProp("color") ?? GetStringProp("Color"),
                    Opacity = GetDoubleProp("opacity") ?? GetDoubleProp("Opacity"),
                    BorderRadius = GetDoubleProp("borderRadius") ?? GetDoubleProp("BorderRadius"),
                    BlendMode = GetStringProp("blendMode") ?? GetStringProp("BlendMode")
                };
                break;
            case ElementType.CheckMark:
                CheckMark = new CheckMarkConfig
                {
                    Label = GetStringProp("label") ?? GetStringProp("Label"),
                    Name = GetStringProp("name") ?? GetStringProp("Name"),
                    State = GetStringProp("state") ?? GetStringProp("State"),
                    Color = GetStringProp("color") ?? GetStringProp("Color"),
                    StrokeWidth = GetDoubleProp("strokeWidth") ?? GetDoubleProp("StrokeWidth"),
                    Binding = GetStringProp("binding") ?? GetStringProp("Binding")
                };
                break;
            case ElementType.PageBoundary:
                PageBoundary = new PageBoundaryConfig
                {
                    Mode = GetStringProp("mode") ?? GetStringProp("Mode"),
                    Label = GetStringProp("label") ?? GetStringProp("Label"),
                    Color = GetStringProp("color") ?? GetStringProp("Color")
                };
                break;
            case ElementType.PageNumber:
                PageNumber = new PageNumberConfig
                {
                    Format = GetStringProp("format") ?? GetStringProp("Format"),
                    PageScope = GetStringProp("pageScope") ?? GetStringProp("PageScope"),
                    PageRange = GetStringProp("pageRange") ?? GetStringProp("PageRange"),
                    StartNumber = GetIntProp("startNumber") ?? GetIntProp("StartNumber"),
                    Prefix = GetStringProp("prefix") ?? GetStringProp("Prefix"),
                    Suffix = GetStringProp("suffix") ?? GetStringProp("Suffix"),
                    Color = GetStringProp("color") ?? GetStringProp("Color"),
                    FontSize = GetDoubleProp("fontSize") ?? GetDoubleProp("FontSize")
                };
                break;
        }

        // Clear Props after migration
        Props.Clear();
    }

    private string? GetStringProp(string key) =>
        Props.TryGetValue(key, out var value) ? value?.ToString() : null;

    private double? GetDoubleProp(string key)
    {
        if (!Props.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            double number => number,
            float number => number,
            int number => number,
            long number => number,
            decimal number => (double)number,
            _ => double.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private int? GetIntProp(string key)
    {
        if (!Props.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int number => number,
            long number => (int)number,
            double number => (int)number,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private bool? GetBoolProp(string key)
    {
        if (!Props.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool boolean => boolean,
            _ => bool.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }
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
    public string? TextOverflow { get; set; } // wrap, clip, ellipsis, shrink
    public int? MaxLines { get; set; }
    public bool? KeepTogether { get; set; }
    public bool? AvoidPageBreakInside { get; set; }
    public string? Anchor { get; set; }
    public string? Alignment { get; set; }
}

public class ImageConfig
{
    public string? FitMode { get; set; } // contain, cover, fill, none
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
    public string? ElementValidationMode { get; set; } // strict, warn, ignore
    public string? CustomErrorMessage { get; set; }
    public string? DebugLabel { get; set; }
    public string? DiagnosticId { get; set; }
}

public class TextConfig
{
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
    public string? Alignment { get; set; } // left, center, right, justify
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public double? LineHeight { get; set; }
    public int? MaxLines { get; set; }
    public string? Language { get; set; }       // BCP-47 tag: "ar", "zh", "en", etc.
    public string? TextDirection { get; set; }  // "ltr" | "rtl"
}

public class RichTextConfig
{
    public string? Content { get; set; }
    public string? FontFamily { get; set; }
    public double? BaseFontSize { get; set; }
    public string? Color { get; set; }
    public string? Alignment { get; set; } // left, center, right, justify
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
