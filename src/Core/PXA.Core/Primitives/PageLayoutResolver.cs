using PXA.Core.Contracts;

namespace PXA.Core.Primitives;

public static class PageLayoutResolver
{
    public const double DefaultWidth = 595;
    public const double DefaultHeight = 842;

    public static PageLayoutDto Resolve(PageSettingsDto? documentSettings, PageLayoutDto? pageLayout)
    {
        var width = Positive(pageLayout?.Width) ?? Positive(documentSettings?.Width) ?? DefaultWidth;
        var height = Positive(pageLayout?.Height) ?? Positive(documentSettings?.Height) ?? DefaultHeight;
        var orientation = NormalizeOrientation(pageLayout?.Orientation ?? documentSettings?.Orientation, width, height);
        if (orientation == "landscape" && width < height || orientation == "portrait" && width > height)
            (width, height) = (height, width);

        var margins = pageLayout?.Margins ?? documentSettings?.Margins;
        return new PageLayoutDto
        {
            Width = width,
            Height = height,
            Orientation = orientation,
            BackgroundColor = pageLayout?.BackgroundColor ?? documentSettings?.BackgroundColor ?? "#ffffff",
            BackgroundImage = pageLayout?.BackgroundImage ?? documentSettings?.BackgroundImage,
            BackgroundImageFit = pageLayout?.BackgroundImageFit ?? documentSettings?.BackgroundImageFit ?? "cover",
            Margins = new MarginsDto
            {
                Top = Math.Max(0, margins?.Top ?? 0),
                Right = Math.Max(0, margins?.Right ?? 0),
                Bottom = Math.Max(0, margins?.Bottom ?? 0),
                Left = Math.Max(0, margins?.Left ?? 0),
            },
            BleedSize = Math.Min(
                Math.Max(0, pageLayout?.BleedSize ?? documentSettings?.BleedSize ?? 0),
                Math.Min(width, height) / 2 - 0.01),
        };
    }

    public static IReadOnlyList<ImportDiagnosticDto> Validate(PageSettingsDto? documentSettings, PageLayoutDto? pageLayout, string source)
    {
        var diagnostics = new List<ImportDiagnosticDto>();
        AddInvalidDimension(pageLayout?.Width, "width", "PXALAYOUT001");
        AddInvalidDimension(pageLayout?.Height, "height", "PXALAYOUT002");
        var requestedOrientation = pageLayout?.Orientation ?? documentSettings?.Orientation;
        if (requestedOrientation is not null
            && !requestedOrientation.Equals("portrait", StringComparison.OrdinalIgnoreCase)
            && !requestedOrientation.Equals("landscape", StringComparison.OrdinalIgnoreCase))
            Add("PXALAYOUT003", $"Unknown orientation '{requestedOrientation}' was normalized.");

        var width = Positive(pageLayout?.Width) ?? Positive(documentSettings?.Width) ?? DefaultWidth;
        var height = Positive(pageLayout?.Height) ?? Positive(documentSettings?.Height) ?? DefaultHeight;
        if (requestedOrientation?.Equals("landscape", StringComparison.OrdinalIgnoreCase) == true && width < height
            || requestedOrientation?.Equals("portrait", StringComparison.OrdinalIgnoreCase) == true && width > height)
            Add("PXALAYOUT004", "Page dimensions were swapped to match the requested orientation.");

        var margins = pageLayout?.Margins ?? documentSettings?.Margins;
        if (margins is not null && new[] { margins.Top, margins.Right, margins.Bottom, margins.Left }.Any(value => value < 0))
            Add("PXALAYOUT005", "Negative page margins were clamped to zero.");
        var bleed = pageLayout?.BleedSize ?? documentSettings?.BleedSize ?? 0;
        if (bleed < 0 || bleed >= Math.Min(width, height) / 2)
            Add("PXALAYOUT006", "The page bleed was clamped to the supported page bounds.");
        return diagnostics;

        void AddInvalidDimension(double? value, string name, string code)
        {
            if (value.HasValue && Positive(value) is null)
                Add(code, $"Invalid page {name} was replaced with the inherited or A4 default value.");
        }

        void Add(string code, string message) => diagnostics.Add(new ImportDiagnosticDto
        {
            Code = code,
            Severity = "warning",
            Source = source,
            Message = message,
        });
    }

    public static PageSettingsDto ResolveSettings(PageSettingsDto? documentSettings, PageLayoutDto? pageLayout)
    {
        var source = documentSettings ?? new PageSettingsDto();
        var layout = Resolve(source, pageLayout);
        return new PageSettingsDto
        {
            Width = layout.Width ?? DefaultWidth,
            Height = layout.Height ?? DefaultHeight,
            Orientation = layout.Orientation ?? "portrait",
            BackgroundColor = layout.BackgroundColor,
            BackgroundImage = layout.BackgroundImage,
            BackgroundImageFit = layout.BackgroundImageFit,
            Margins = layout.Margins,
            BleedSize = layout.BleedSize ?? 0,
            Unit = source.Unit,
            Metadata = source.Metadata,
            Protection = source.Protection,
            TrackChanges = source.TrackChanges,
            SystemLanguage = source.SystemLanguage,
            ActiveLanguages = source.ActiveLanguages,
            LocalizedProperties = source.LocalizedProperties,
            TargetLanguage = source.TargetLanguage,
            CustomProperties = source.CustomProperties,
            NamedStyles = source.NamedStyles,
            PageNumbering = source.PageNumbering,
            GlobalWatermark = source.GlobalWatermark,
            Encryption = source.Encryption,
            Extensions = source.Extensions,
        };
    }

    private static double? Positive(double? value) => value is > 0 and <= 14400 ? value : null;

    private static string NormalizeOrientation(string? value, double width, double height)
    {
        if (string.Equals(value, "landscape", StringComparison.OrdinalIgnoreCase)) return "landscape";
        if (string.Equals(value, "portrait", StringComparison.OrdinalIgnoreCase)) return "portrait";
        return width > height ? "landscape" : "portrait";
    }
}
