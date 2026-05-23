namespace Canvas.Pdf;

public sealed class PdfViewerPreferencesOptions
{
    public static PdfViewerPreferencesOptions Default { get; } = new();

    public PdfPageMode? PageMode { get; init; }

    public PdfPageLayoutMode? PageLayout { get; init; }

    public bool HideToolbar { get; init; }

    public bool HideMenubar { get; init; }

    public bool HideWindowUI { get; init; }

    public bool FitWindow { get; init; }

    public bool CenterWindow { get; init; }

    public bool DisplayDocTitle { get; init; }

    public PdfReadingDirection? ReadingDirection { get; init; }

    public bool DisablePrintScaling { get; init; }

    public bool DuplexFlipLongEdge { get; init; }

    public bool DuplexFlipShortEdge { get; init; }

    public int? OpenPageNumber { get; init; }

    public double? OpenZoomPercent { get; init; }
}
