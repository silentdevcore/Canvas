namespace PXA.Pdf;

/// <summary>Accessibility conformance profile requested for PDF output.</summary>
public enum PdfAccessibilityProfile
{
    None = 0,
    PdfUa1 = 1
}

/// <summary>Standard structure types supported by the PXA tagged-PDF writer.</summary>
public enum PdfStructureRole
{
    Document,
    Part,
    Sect,
    Div,
    P,
    H1,
    H2,
    H3,
    H4,
    H5,
    H6,
    L,
    LI,
    Lbl,
    LBody,
    Table,
    TR,
    TH,
    TD,
    Span,
    Link,
    Figure,
    Form
}

public enum PdfTableHeaderScope
{
    None = 0,
    Row = 1,
    Column = 2,
    Both = 3
}

/// <summary>Semantic information attached to one visible page operation.</summary>
public sealed class PdfAccessibilityProperties
{
    public required PdfStructureRole Role { get; init; }

    /// <summary>
    /// Optional provider-specific PDF structure type. PDF/UA-1 serializes this name through
    /// the structure-tree role map to <see cref="Role"/>.
    /// </summary>
    public string? CustomRole { get; init; }

    /// <summary>
    /// Optional source namespace retained by PXA for imported semantics. PDF/UA-1 uses the
    /// role-map fallback; native namespace dictionaries require PDF 2.0/PDF/UA-2.
    /// </summary>
    public string? NamespaceUri { get; init; }

    internal string StructureTypeName => string.IsNullOrWhiteSpace(CustomRole)
        ? Role.ToString()
        : CustomRole;

    /// <summary>Stable logical identifier used by diagnostics and table associations.</summary>
    public string? ElementId { get; init; }

    /// <summary>Stable ID of the logical parent used for lists, tables, and grouped content.</summary>
    public string? ParentElementId { get; init; }

    /// <summary>Logical order independent from visual drawing order.</summary>
    public int? ReadingOrder { get; init; }

    public string? Language { get; init; }

    public string? AlternateText { get; init; }

    public string? ActualText { get; init; }

    public PdfTableHeaderScope TableHeaderScope { get; init; }

    /// <summary>Stable IDs of associated TH elements for a table data cell.</summary>
    public IReadOnlyList<string>? TableHeaderIds { get; init; }
}

public enum PdfAccessibilityDiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public sealed record PdfAccessibilityDiagnostic(
    string Code,
    PdfAccessibilityDiagnosticSeverity Severity,
    string Message,
    int? PageNumber = null,
    string? ElementId = null,
    string? LocalizationKey = null,
    IReadOnlyDictionary<string, string>? LocalizationParameters = null);

public sealed record PdfAccessibilityValidationReport(
    PdfAccessibilityProfile Profile,
    bool IsValid,
    string Validator,
    IReadOnlyList<PdfAccessibilityDiagnostic> Diagnostics);

public sealed class PdfAccessibilityException : ArgumentException
{
    public PdfAccessibilityException(PdfAccessibilityValidationReport report)
        : base(BuildMessage(report)) => Report = report;

    public PdfAccessibilityValidationReport Report { get; }

    private static string BuildMessage(PdfAccessibilityValidationReport report) =>
        $"The document cannot be written as {report.Profile}: {string.Join("; ", report.Diagnostics.Where(d => d.Severity == PdfAccessibilityDiagnosticSeverity.Error).Select(d => $"{d.Code}: {d.Message}"))}";
}
