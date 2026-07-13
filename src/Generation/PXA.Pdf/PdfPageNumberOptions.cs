namespace PXA.Pdf;

public sealed class PdfPageNumberOptions
{
    public static PdfPageNumberOptions Default { get; } = new();

    public int StartNumber { get; init; } = 1;

    public bool ShowTotalPages { get; init; } = true;

    public bool UseFilteredPageSequence { get; init; }

    public bool UseSectionNumbering { get; init; }

    public IReadOnlyList<int>? SectionStartPages { get; init; }

    public string Prefix { get; init; } = "Page ";

    public string Separator { get; init; } = " of ";

    public string Suffix { get; init; } = string.Empty;

    public string? NumberFormat { get; init; }

    public double Y { get; init; } = 20;

    public double MarginX { get; init; } = 40;

    public double FontSize { get; init; } = 10;

    public PdfTextAlignment Alignment { get; init; } = PdfTextAlignment.Center;

    public PdfStandardFont? Font { get; init; }

    public PdfFontFamily? FontFamily { get; init; }

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public IPdfColor? FillColor { get; init; }

    public int? StartPageNumber { get; init; }

    public int? EndPageNumber { get; init; }

    public IReadOnlyList<int>? IncludePageNumbers { get; init; }

    public IReadOnlyList<int>? ExcludePageNumbers { get; init; }

    public bool ExcludeTableOfContentsPages { get; init; }

    public bool SkipFirstPage { get; init; }

    public bool SkipLastPage { get; init; }

    public int? MaximumNumber { get; init; }

    public int? MinimumNumber { get; init; }

    public PdfPageParity PageParity { get; init; } = PdfPageParity.Both;

    public bool UseRomanNumerals { get; init; }
}
