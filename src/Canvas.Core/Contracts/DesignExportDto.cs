namespace Canvas.Core.Contracts;

public sealed class DesignExportDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Untitled";
    public string? Category { get; set; }
    public string? Description { get; set; }
    public List<PageDto> Pages { get; set; } = [];
    public List<ElementDto> SharedElements { get; set; } = [];
    public PageSettingsDto? PageSettings { get; set; }
}

public sealed class PageDto
{
    public string Id { get; set; } = "";
    public List<ElementDto> Elements { get; set; } = [];
}

public sealed class PageSettingsDto
{
    public double Width { get; set; } = 595;
    public double Height { get; set; } = 842;
    public string Orientation { get; set; } = "portrait";
    public string? BackgroundColor { get; set; }
    public string? BackgroundImage { get; set; }
    public string? BackgroundImageFit { get; set; }
    public MarginsDto? Margins { get; set; }
    public PageNumberingDto? PageNumbering { get; set; }
    public GlobalWatermarkDto? GlobalWatermark { get; set; }
    public PdfMetadataDto? Metadata { get; set; }
    public List<NamedStyleDto>? NamedStyles { get; set; }
    public DocumentProtectionDto? Protection { get; set; }
    public List<CustomDocumentPropertyDto>? CustomProperties { get; set; }
    public bool TrackChanges { get; set; }
}

public sealed class MarginsDto
{
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Left { get; set; }
}

public sealed class PageNumberingDto
{
    public string Format { get; set; } = "current";
    public int StartNumber { get; set; } = 1;
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public bool ShowOnFirstPage { get; set; } = true;
    public string Placement { get; set; } = "bottom-center";
}

public sealed class GlobalWatermarkDto
{
    public string Mode { get; set; } = "text";
    public string? Content { get; set; }
    public double Rotation { get; set; } = 45;
    public double Scale { get; set; } = 1.0;
    public string? PageScope { get; set; }
    public string? PageRange { get; set; }
    public string? Color { get; set; }
    public double FontSize { get; set; } = 48;
}

public sealed class PdfMetadataDto
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
}

public sealed class NamedStyleDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "paragraph"; // paragraph | character | list | table
    public string? BasedOn { get; set; }
    public string? NextStyle { get; set; }
    public Dictionary<string, object>? Style { get; set; }
}

public sealed class DocumentProtectionDto
{
    public bool Enabled { get; set; }
    public string Mode { get; set; } = "readOnly"; // readOnly | comments | trackedChanges | formFields
    public string? PasswordHash { get; set; }
}

public sealed class CustomDocumentPropertyDto
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Type { get; set; } = "text"; // text | number | boolean | date
}

/// <summary>Maps directly to the frontend SimpleElement type.</summary>
public sealed class ElementDto
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "text";
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool? Hidden { get; set; }
    public bool? Locked { get; set; }

    // Text / content
    public string? Content { get; set; }
    public Dictionary<string, object>? Style { get; set; }

    // Rich text
    public string? HtmlContent { get; set; }

    // Form fields
    public string? FieldLabel { get; set; }
    public string? FieldName { get; set; }
    public bool? Required { get; set; }

    // Signature
    public string? SignatureLabel { get; set; }

    // QR code
    public string? QrValue { get; set; }
    public int? QrSize { get; set; }

    // Barcode
    public string? BarcodeValue { get; set; }
    public string? BarcodeType { get; set; }

    // Table
    public string[][]? CellData { get; set; }
    public double[]? ColumnWidths { get; set; }
    public string[]? ColumnAlignments { get; set; }
    public bool? HeaderRow { get; set; }
    public bool? FooterRow { get; set; }
    public string? HeaderBgColor { get; set; }
    public bool? ZebraEnabled { get; set; }
    public string? ZebraColor { get; set; }

    // Note
    public string? NoteTitle { get; set; }
    public string? NoteBody { get; set; }
    public string? NoteAuthor { get; set; }
    public bool? NoteCollapsed { get; set; }

    // Image
    public string? FitMode { get; set; }
    public double? CropX { get; set; }
    public double? CropY { get; set; }
    public double? CropWidth { get; set; }
    public double? CropHeight { get; set; }
    public double? FocalX { get; set; }
    public double? FocalY { get; set; }

    // Watermark
    public string? WatermarkMode { get; set; }
    public string? PageScope { get; set; }
    public string? PageRange { get; set; }

    // Arrow
    public string? ArrowMode { get; set; }
    public string? ArrowDirection { get; set; }
    public double? ArrowRotation { get; set; }
    public string? StartMarker { get; set; }
    public string? EndMarker { get; set; }

    // Draw / freehand
    public string? DrawTool { get; set; }
    public string? PathData { get; set; }

    // Date
    public string? DateMode { get; set; }
    public string? DateFormat { get; set; }
    public string? Locale { get; set; }
    public string? Timezone { get; set; }
    public string? FallbackText { get; set; }

    // Highlight / checkmark
    public string? MarkMode { get; set; }
    public string? CheckState { get; set; }

    // Page boundary
    public string? PageBoundaryMode { get; set; }

    // Page number
    public string? NumberingFormat { get; set; }
    public int? StartNumber { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }

    // Select / list / radio
    public string[]? Options { get; set; }
    public string? SelectedValue { get; set; }
    public bool? MultiSelect { get; set; }
    public bool? Ordered { get; set; }
    public string? ListStyle { get; set; }

    // Chart
    public string? ChartType { get; set; }
    public Dictionary<string, object>? ChartData { get; set; }

    // Link
    public string? Href { get; set; }
    public string? LinkTarget { get; set; }

    // Button
    public string? ButtonAction { get; set; }

    // Number
    public double? NumberValue { get; set; }
    public string? NumberStyle { get; set; }
    public int? NumberDecimals { get; set; }
    public string? NumberCurrency { get; set; }
    public string? NumberLocale { get; set; }

    // Named style reference
    public string? StyleName { get; set; }
    public string? CharacterStyle { get; set; }

    // Footnote / Endnote
    public string? FootnoteText { get; set; }
    public string? FootnoteRef { get; set; }

    // Bookmark
    public string? BookmarkName { get; set; }
    public string? BookmarkTarget { get; set; }

    // Word-native comment
    public string? CommentAuthor { get; set; }
    public string? CommentDate { get; set; }
    public string? CommentText { get; set; }
    public string? CommentId { get; set; }

    // Content control
    public string? ContentControlType { get; set; }
    public string? ContentControlTag { get; set; }
    public string? ContentControlTitle { get; set; }
    public string? ContentControlPlaceholder { get; set; }

    // Track changes revision
    public string? RevisionType { get; set; }
    public string? RevisionAuthor { get; set; }
    public string? RevisionDate { get; set; }
    public string? RevisionId { get; set; }

    // Auto-hyphenation
    public bool? AutoHyphenation { get; set; }
}
