using PVAD.Pdf.Layout;
using System.Security.Cryptography;

namespace PVAD.Pdf;

/// <summary>
/// A single page you draw onto. Obtain one from <see cref="PdfDocument.AddPage(double, double)"/>. Coordinates are in
/// points (1/72 inch) with the origin at the bottom-left of the page; <c>x</c> increases to the right and
/// <c>y</c> increases upward (standard PDF coordinates). Many drawing methods also have a <c>*FromTop</c>
/// variant that measures <c>y</c> from the top of the page instead.
/// </summary>
public sealed class PdfPage
{
    private readonly List<PdfPageElement> _elements = new();
    private readonly List<PdfLinkAnnotation> _linkAnnotations = new();
    private readonly List<PdfComboBoxAnnotation> _comboBoxAnnotations = new();
    private readonly List<PdfMultilineTextFieldAnnotation> _multilineTextFields = new();
    private readonly List<PdfTextFieldAnnotation> _textFields = new();
    private readonly List<PdfCheckBoxAnnotation> _checkBoxAnnotations = new();
    private readonly List<PdfListBoxAnnotation> _listBoxAnnotations = new();
    private readonly List<PdfRadioButtonAnnotation> _radioButtonAnnotations = new();
    private readonly List<PdfSignatureFieldAnnotation> _signatureFieldAnnotations = new();
    private readonly List<PdfPushButtonAnnotation> _pushButtonAnnotations = new();
    private readonly List<PdfReviewAnnotation> _reviewAnnotations = new();
    private readonly List<PdfAccessibilityProperties> _semanticElements = new();
    private PdfAccessibilityProperties? _activeAccessibility;
    private bool _activeArtifact;

    private readonly PdfFontLoader? _fontLoader;

    internal PdfPage(double width, double height, PdfStandardFont defaultFont, PdfFontLoader? fontLoader = null)
    {
        Width = width;
        Height = height;
        DefaultFont = defaultFont;
        _fontLoader = fontLoader;
    }

    public double Width { get; }

    public double Height { get; }

    public PdfStandardFont DefaultFont { get; set; }

    public int RotationDegrees { get; set; }

    public PdfPoint? CropBoxLowerLeft { get; set; }

    public PdfPoint? CropBoxUpperRight { get; set; }

    public PdfPoint? BleedBoxLowerLeft { get; set; }

    public PdfPoint? BleedBoxUpperRight { get; set; }

    public PdfPoint? TrimBoxLowerLeft { get; set; }

    public PdfPoint? TrimBoxUpperRight { get; set; }

    public PdfPoint? ArtBoxLowerLeft { get; set; }

    public PdfPoint? ArtBoxUpperRight { get; set; }

    internal IReadOnlyList<PdfPageElement> Elements => _elements;

    internal IReadOnlyList<PdfLinkAnnotation> LinkAnnotations => _linkAnnotations;

    internal IReadOnlyList<PdfComboBoxAnnotation> ComboBoxAnnotations => _comboBoxAnnotations;

    internal IReadOnlyList<PdfMultilineTextFieldAnnotation> MultilineTextFields => _multilineTextFields;

    internal IReadOnlyList<PdfTextFieldAnnotation> TextFields => _textFields;

    internal IReadOnlyList<PdfCheckBoxAnnotation> CheckBoxAnnotations => _checkBoxAnnotations;

    internal IReadOnlyList<PdfListBoxAnnotation> ListBoxAnnotations => _listBoxAnnotations;

    internal IReadOnlyList<PdfRadioButtonAnnotation> RadioButtonAnnotations => _radioButtonAnnotations;

    internal IReadOnlyList<PdfSignatureFieldAnnotation> SignatureFieldAnnotations => _signatureFieldAnnotations;

    internal IReadOnlyList<PdfPushButtonAnnotation> PushButtonAnnotations => _pushButtonAnnotations;

    internal IReadOnlyList<PdfReviewAnnotation> ReviewAnnotations => _reviewAnnotations;

    internal IReadOnlyList<PdfAccessibilityProperties> SemanticElements => _semanticElements;

    /// <summary>Attaches semantic structure information to the most recently drawn page element.</summary>
    public void TagLastElement(PdfAccessibilityProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (_elements.Count == 0)
            throw new InvalidOperationException("No page element is available to tag.");
        _elements[^1].Accessibility = properties;
        _elements[^1].IsArtifact = false;
        RegisterSemanticElement(properties);
    }

    /// <summary>Marks the most recently drawn page element as pagination, layout, or decorative content.</summary>
    public void MarkLastElementAsArtifact()
    {
        if (_elements.Count == 0)
            throw new InvalidOperationException("No page element is available to mark as an artifact.");
        _elements[^1].Accessibility = null;
        _elements[^1].IsArtifact = true;
    }

    /// <summary>
    /// Tags all drawing operations created inside the scope as one logical structure element.
    /// The operations retain visual order while <see cref="PdfAccessibilityProperties.ReadingOrder"/>
    /// controls logical order.
    /// </summary>
    public IDisposable BeginAccessibilityElement(PdfAccessibilityProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        RegisterSemanticElement(properties);
        return BeginAccessibilityScope(properties, artifact: false);
    }

    /// <summary>Marks all drawing operations created inside the scope as artifacts.</summary>
    public IDisposable BeginArtifact() => BeginAccessibilityScope(null, artifact: true);

    private IDisposable BeginAccessibilityScope(PdfAccessibilityProperties? properties, bool artifact)
    {
        var previousProperties = _activeAccessibility;
        var previousArtifact = _activeArtifact;
        _activeAccessibility = properties;
        _activeArtifact = artifact;
        return new AccessibilityScope(this, previousProperties, previousArtifact);
    }

    private void AddElement(PdfPageElement element)
    {
        element.Accessibility = _activeAccessibility;
        element.IsArtifact = _activeArtifact;
        _elements.Add(element);
    }

    private void RegisterSemanticElement(PdfAccessibilityProperties properties)
    {
        if (!_semanticElements.Contains(properties, ReferenceEqualityComparer.Instance))
            _semanticElements.Add(properties);
    }

    private sealed class AccessibilityScope(
        PdfPage page,
        PdfAccessibilityProperties? previousProperties,
        bool previousArtifact) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            page._activeAccessibility = previousProperties;
            page._activeArtifact = previousArtifact;
            _disposed = true;
        }
    }

    public void SetPageBoundary(PdfPageBoundary boundary, PdfPoint lowerLeft, PdfPoint upperRight)
    {
        if (upperRight.X <= lowerLeft.X || upperRight.Y <= lowerLeft.Y)
        {
            throw new ArgumentException("Upper-right boundary point must be greater than lower-left point.");
        }

        switch (boundary)
        {
            case PdfPageBoundary.CropBox:
                CropBoxLowerLeft = lowerLeft;
                CropBoxUpperRight = upperRight;
                break;
            case PdfPageBoundary.BleedBox:
                BleedBoxLowerLeft = lowerLeft;
                BleedBoxUpperRight = upperRight;
                break;
            case PdfPageBoundary.TrimBox:
                TrimBoxLowerLeft = lowerLeft;
                TrimBoxUpperRight = upperRight;
                break;
            case PdfPageBoundary.ArtBox:
                ArtBoxLowerLeft = lowerLeft;
                ArtBoxUpperRight = upperRight;
                break;
            case PdfPageBoundary.MediaBox:
                throw new ArgumentException("MediaBox is defined by page size. Use AddPage dimensions to change it.");
            default:
                throw new ArgumentOutOfRangeException(nameof(boundary), boundary, "Unsupported page boundary.");
        }
    }

    /// <summary>Draws a single line of text at the given baseline position.</summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">Left x in points.</param>
    /// <param name="y">Text baseline y in points (from the bottom of the page).</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <param name="font">Standard font; falls back to the page/document default when null.</param>
    public void DrawText(string text, double x, double y, double fontSize = 12, PdfStandardFont? font = null)
    {
        DrawText(text, x, y, new PdfDrawTextOptions
        {
            FontSize = fontSize,
            Font = font
        });
    }

    public void DrawText(string text, double x, double y, double fontSize, PdfFontFamily fontFamily, bool bold = false, bool italic = false)
    {
        DrawText(text, x, y, new PdfDrawTextOptions
        {
            FontSize = fontSize,
            FontFamily = fontFamily,
            Bold = bold,
            Italic = italic
        });
    }

    /// <summary>Draws a single line of text with full styling control (font, weight, color, rotation,
    /// underline/strikethrough, character spacing, language/direction).</summary>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">Left x in points.</param>
    /// <param name="y">Text baseline y in points (from the bottom of the page).</param>
    /// <param name="options">Text styling options.</param>
    public void DrawText(string text, double x, double y, PdfDrawTextOptions options)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        ArgumentNullException.ThrowIfNull(options);
        if (!double.IsFinite(options.Opacity) || options.Opacity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Opacity must be between zero and one.");

        if (options.FontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Font size must be greater than zero.");
        }

        if (options.HorizontalScalingPercent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Horizontal scaling must be greater than zero.");
        }

        var resolvedFont = ResolveFont(options.Font, options.FontFamily, options.Bold, options.Italic);
        var fillColor = options.FillColor ?? PdfColor.Black;

        PdfEmbeddedFont? embeddedFont = null;
        _fontLoader?.TryLoad(options.Language, IsBoldFont(resolvedFont), IsItalicFont(resolvedFont), TableFontFamily(resolvedFont), out embeddedFont);

        AddElement(new TextElement(
            text,
            x,
            y,
            options.FontSize,
            resolvedFont,
            EmbeddedFont: embeddedFont,
            Language: options.Language,
            TextDirection: options.TextDirection,
            FillColor: fillColor,
            RotationDegrees: options.RotationDegrees,
            Underline: options.Underline,
            Strikethrough: options.Strikethrough,
            CharacterSpacing: options.CharacterSpacing,
            HorizontalScalingPercent: options.HorizontalScalingPercent,
            Opacity: options.Opacity));
    }

    /// <summary>Measures a single line using the same font resolution as DrawText.</summary>
    public double MeasureTextWidth(string text, PdfDrawTextOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);
        if (!double.IsFinite(options.FontSize) || options.FontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options));
        var resolved = ResolveFont(options.Font, options.FontFamily, options.Bold, options.Italic);
        PdfEmbeddedFont? embedded = null;
        _fontLoader?.TryLoad(options.Language, IsBoldFont(resolved), IsItalicFont(resolved), TableFontFamily(resolved), out embedded);
        return embedded?.MeasureWidth(text, options.FontSize) ?? EstimateTextWidth(text, options.FontSize, resolved);
    }

    public void DrawTextFromTop(string text, double x, double topY, double fontSize = 12, PdfStandardFont? font = null)
    {
        DrawText(text, x, ToBaselineYFromTop(topY, fontSize), fontSize, font);
    }

    public void DrawTextFromTop(string text, double x, double topY, double fontSize, PdfFontFamily fontFamily, bool bold = false, bool italic = false)
    {
        DrawText(text, x, ToBaselineYFromTop(topY, fontSize), fontSize, fontFamily, bold, italic);
    }

    public void DrawTextFromTop(string text, double x, double topY, PdfDrawTextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        DrawText(text, x, ToBaselineYFromTop(topY, options.FontSize), options);
    }

    public void DrawLine(
        double x1,
        double y1,
        double x2,
        double y2,
        double lineWidth = 1,
        IPdfColor? strokeColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        var resolvedStrokeStyle = ResolveStrokeStyle(lineWidth, strokeStyle);

        AddElement(new LineElement(x1, y1, x2, y2, resolvedStrokeStyle, strokeColor ?? PdfColor.Black));
    }

    public void DrawLineFromTop(
        double x1,
        double topY1,
        double x2,
        double topY2,
        double lineWidth = 1,
        IPdfColor? strokeColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        DrawLine(x1, ToYFromTop(topY1), x2, ToYFromTop(topY2), lineWidth, strokeColor, strokeStyle);
    }

    public void DrawRectangle(
        double x,
        double y,
        double width,
        double height,
        double lineWidth = 1,
        bool fill = false,
        IPdfColor? strokeColor = null,
        IPdfColor? fillColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        var resolvedStrokeStyle = ResolveStrokeStyle(lineWidth, strokeStyle);

        AddElement(new RectangleElement(
            x,
            y,
            width,
            height,
            resolvedStrokeStyle,
            Stroke: true,
            Fill: fill,
            StrokeColor: strokeColor ?? PdfColor.Black,
            FillColor: fillColor ?? PdfColor.Black));
    }

    public void DrawRectangleFromTop(
        double x,
        double topY,
        double width,
        double height,
        double lineWidth = 1,
        bool fill = false,
        IPdfColor? strokeColor = null,
        IPdfColor? fillColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        DrawRectangle(x, ToBottomYFromTop(topY, height), width, height, lineWidth, fill, strokeColor, fillColor, strokeStyle);
    }

    public void DrawBezierCurve(
        PdfPoint start,
        PdfPoint control1,
        PdfPoint control2,
        PdfPoint end,
        double lineWidth = 1,
        IPdfColor? strokeColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        var resolvedStrokeStyle = ResolveStrokeStyle(lineWidth, strokeStyle);

        AddElement(new BezierCurveElement(
            start,
            control1,
            control2,
            end,
            resolvedStrokeStyle,
            strokeColor ?? PdfColor.Black));
    }

    public void DrawPolygon(
        IReadOnlyList<PdfPoint> points,
        double lineWidth = 1,
        bool fill = false,
        IPdfColor? strokeColor = null,
        IPdfColor? fillColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 3)
        {
            throw new ArgumentException("Polygon requires at least 3 points.", nameof(points));
        }

        var resolvedStrokeStyle = ResolveStrokeStyle(lineWidth, strokeStyle);

        AddElement(new PolygonElement(
            points.ToArray(),
            resolvedStrokeStyle,
            Stroke: true,
            Fill: fill,
            StrokeColor: strokeColor ?? PdfColor.Black,
            FillColor: fillColor ?? PdfColor.Black));
    }

    public void DrawRoundedRectangle(
        double x,
        double y,
        double width,
        double height,
        double cornerRadius,
        double lineWidth = 1,
        bool fill = false,
        IPdfColor? strokeColor = null,
        IPdfColor? fillColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        if (cornerRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cornerRadius), "Corner radius cannot be negative.");
        }

        var maxRadius = Math.Min(width, height) / 2;
        if (cornerRadius > maxRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(cornerRadius), "Corner radius cannot exceed half of the smallest dimension.");
        }

        var resolvedStrokeStyle = ResolveStrokeStyle(lineWidth, strokeStyle);

        AddElement(new RoundedRectangleElement(
            x,
            y,
            width,
            height,
            cornerRadius,
            resolvedStrokeStyle,
            Stroke: true,
            Fill: fill,
            StrokeColor: strokeColor ?? PdfColor.Black,
            FillColor: fillColor ?? PdfColor.Black));
    }

    public void DrawRoundedRectangleFromTop(
        double x,
        double topY,
        double width,
        double height,
        double cornerRadius,
        double lineWidth = 1,
        bool fill = false,
        IPdfColor? strokeColor = null,
        IPdfColor? fillColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        DrawRoundedRectangle(x, ToBottomYFromTop(topY, height), width, height, cornerRadius, lineWidth, fill, strokeColor, fillColor, strokeStyle);
    }

    public void DrawCircle(
        double centerX,
        double centerY,
        double radius,
        double lineWidth = 1,
        bool fill = false,
        IPdfColor? strokeColor = null,
        IPdfColor? fillColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be greater than zero.");
        }

        var resolvedStrokeStyle = ResolveStrokeStyle(lineWidth, strokeStyle);

        AddElement(new CircleElement(
            centerX,
            centerY,
            radius,
            resolvedStrokeStyle,
            Stroke: true,
            Fill: fill,
            StrokeColor: strokeColor ?? PdfColor.Black,
            FillColor: fillColor ?? PdfColor.Black));
    }

    public void DrawCircleFromTop(
        double centerX,
        double topCenterY,
        double radius,
        double lineWidth = 1,
        bool fill = false,
        IPdfColor? strokeColor = null,
        IPdfColor? fillColor = null,
        PdfStrokeStyle? strokeStyle = null)
    {
        DrawCircle(centerX, ToYFromTop(topCenterY), radius, lineWidth, fill, strokeColor, fillColor, strokeStyle);
    }

    /// <summary>Draws an image loaded from a file path. Omit width/height to use the image's natural size.</summary>
    /// <param name="imagePath">Path to a PNG or JPEG file.</param>
    /// <param name="x">Left x in points.</param>
    /// <param name="y">Bottom y in points.</param>
    /// <param name="width">Target width in points, or null for natural width.</param>
    /// <param name="height">Target height in points, or null for natural height.</param>
    public void DrawImage(string imagePath, double x, double y, double? width = null, double? height = null)
    {
        var image = PdfImageReader.Read(imagePath);
        var resolvedWidth = width ?? image.Width;
        var resolvedHeight = height ?? image.Height;

        if (resolvedWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be greater than zero.");
        }

        if (resolvedHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be greater than zero.");
        }

        AddImageElement(imagePath, image, x, y, resolvedWidth, resolvedHeight, 1);
    }

    public void DrawImage(byte[] imageBytes, double x, double y, double? width = null, double? height = null)
    {
        var image = PdfImageReader.Read(imageBytes);
        var resolvedWidth = width ?? image.Width;
        var resolvedHeight = height ?? image.Height;

        if (resolvedWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be greater than zero.");
        }

        if (resolvedHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be greater than zero.");
        }

        AddImageElementWithCacheKey(GetImageBytesCacheKey(imageBytes), image, x, y, resolvedWidth, resolvedHeight, 1);
    }

    public void DrawImage(Stream imageStream, double x, double y, double? width = null, double? height = null)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using var memory = new MemoryStream();
        imageStream.CopyTo(memory);
        DrawImage(memory.ToArray(), x, y, width, height);
    }

    public void DrawImageFromTop(string imagePath, double x, double topY)
    {
        var image = PdfImageReader.Read(imagePath);
        AddImageElement(imagePath, image, x, ToBottomYFromTop(topY, image.Height), image.Width, image.Height, 1);
    }

    public void DrawImageFromTop(byte[] imageBytes, double x, double topY)
    {
        var image = PdfImageReader.Read(imageBytes);
        AddImageElementWithCacheKey(GetImageBytesCacheKey(imageBytes), image, x, ToBottomYFromTop(topY, image.Height), image.Width, image.Height, 1);
    }

    public void DrawImageFromTop(Stream imageStream, double x, double topY)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using var memory = new MemoryStream();
        imageStream.CopyTo(memory);
        DrawImageFromTop(memory.ToArray(), x, topY);
    }

    public void DrawImageFromTop(string imagePath, double x, double topY, double width, double height)
    {
        DrawImage(imagePath, x, ToBottomYFromTop(topY, height), width, height);
    }

    public void DrawImageFromTop(byte[] imageBytes, double x, double topY, double width, double height)
    {
        DrawImage(imageBytes, x, ToBottomYFromTop(topY, height), width, height);
    }

    public void DrawImageFromTop(Stream imageStream, double x, double topY, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using var memory = new MemoryStream();
        imageStream.CopyTo(memory);
        DrawImageFromTop(memory.ToArray(), x, topY, width, height);
    }

    public void DrawImage(string imagePath, double x, double y, double width, double height, double opacity)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be greater than zero.");
        }

        if (opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        var image = PdfImageReader.Read(imagePath);
        AddImageElement(imagePath, image, x, y, width, height, opacity);
    }

    public void DrawImage(byte[] imageBytes, double x, double y, double width, double height, double opacity)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be greater than zero.");
        }

        if (opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        var image = PdfImageReader.Read(imageBytes);
        AddImageElementWithCacheKey(GetImageBytesCacheKey(imageBytes), image, x, y, width, height, opacity);
    }

    public void DrawImage(Stream imageStream, double x, double y, double width, double height, double opacity)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using var memory = new MemoryStream();
        imageStream.CopyTo(memory);
        DrawImage(memory.ToArray(), x, y, width, height, opacity);
    }

    public void DrawImageFromTop(string imagePath, double x, double topY, double opacity)
    {
        if (opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        var image = PdfImageReader.Read(imagePath);
        AddImageElement(imagePath, image, x, ToBottomYFromTop(topY, image.Height), image.Width, image.Height, opacity);
    }

    public void DrawImageFromTop(byte[] imageBytes, double x, double topY, double opacity)
    {
        if (opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        var image = PdfImageReader.Read(imageBytes);
        AddImageElementWithCacheKey(GetImageBytesCacheKey(imageBytes), image, x, ToBottomYFromTop(topY, image.Height), image.Width, image.Height, opacity);
    }

    public void DrawImageFromTop(Stream imageStream, double x, double topY, double opacity)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using var memory = new MemoryStream();
        imageStream.CopyTo(memory);
        DrawImageFromTop(memory.ToArray(), x, topY, opacity);
    }

    public void DrawImageFromTop(string imagePath, double x, double topY, double width, double height, double opacity)
    {
        DrawImage(imagePath, x, ToBottomYFromTop(topY, height), width, height, opacity);
    }

    public void DrawImageFromTop(byte[] imageBytes, double x, double topY, double width, double height, double opacity)
    {
        DrawImage(imageBytes, x, ToBottomYFromTop(topY, height), width, height, opacity);
    }

    public void DrawImageFromTop(Stream imageStream, double x, double topY, double width, double height, double opacity)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using var memory = new MemoryStream();
        imageStream.CopyTo(memory);
        DrawImageFromTop(memory.ToArray(), x, topY, width, height, opacity);
    }

    public void DrawImageClipped(string imagePath, double x, double y, double width, double height, double clipX, double clipY, double clipWidth, double clipHeight, double opacity = 1)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width and height must be greater than zero.");
        }

        if (clipWidth <= 0 || clipHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clipWidth), "Clip width and height must be greater than zero.");
        }

        if (opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        var image = PdfImageReader.Read(imagePath);
        var cacheKey = Path.GetFullPath(imagePath);
        AddElement(new ImageElement(
            image,
            cacheKey,
            x,
            y,
            width,
            height,
            opacity,
            ClipToBounds: true,
            ClipX: clipX,
            ClipY: clipY,
            ClipWidth: clipWidth,
            ClipHeight: clipHeight));
    }

    /// <summary>
    /// Fits an in-memory image inside a clipped box. Focus is a percentage of the remaining
    /// space, measured from the left/top, matching browser object-position semantics.
    /// Natural image pixels map to points when <see cref="PdfImageFitMode.None"/> is used.
    /// </summary>
    public PdfImageLayoutResult DrawImageFitted(byte[] imageBytes, double x, double y,
        double width, double height, PdfImageFitMode fitMode = PdfImageFitMode.Contain,
        double focalX = 50, double focalY = 50, double opacity = 1)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x), "Image position must be finite.");
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Image box dimensions must be finite and positive.");
        if (!double.IsFinite(opacity) || opacity < 0 || opacity > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        if (!Enum.IsDefined(fitMode))
            throw new ArgumentOutOfRangeException(nameof(fitMode));

        var image = PdfImageReader.Read(imageBytes);
        var scale = fitMode switch
        {
            PdfImageFitMode.Contain => Math.Min(width / image.Width, height / image.Height),
            PdfImageFitMode.Cover => Math.Max(width / image.Width, height / image.Height),
            _ => 1
        };
        var drawWidth = fitMode == PdfImageFitMode.Fill ? width : image.Width * scale;
        var drawHeight = fitMode == PdfImageFitMode.Fill ? height : image.Height * scale;
        var horizontal = double.IsFinite(focalX) ? Math.Clamp(focalX, 0, 100) / 100 : 0.5;
        var vertical = double.IsFinite(focalY) ? Math.Clamp(focalY, 0, 100) / 100 : 0.5;
        var drawX = x + (width - drawWidth) * horizontal;
        // PDF coordinates start at the bottom; the focus percentage starts at the top.
        var drawY = y + (height - drawHeight) * (1 - vertical);
        if (!double.IsFinite(drawX) || !double.IsFinite(drawY) ||
            !double.IsFinite(drawWidth) || !double.IsFinite(drawHeight))
            throw new ArgumentOutOfRangeException(nameof(width), "Fitted image bounds exceed the supported numeric range.");
        AddElement(new ImageElement(image, GetImageBytesCacheKey(imageBytes),
            drawX, drawY, drawWidth, drawHeight, opacity, ClipToBounds: true,
            ClipX: x, ClipY: y, ClipWidth: width, ClipHeight: height));
        return new PdfImageLayoutResult { X = drawX, Y = drawY, Width = drawWidth, Height = drawHeight };
    }

    public PdfImageLayoutResult DrawImageScaledToWidth(string imagePath, double x, double y, double width)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be greater than zero.");
        }

        var image = PdfImageReader.Read(imagePath);
        var height = width * image.Height / image.Width;
        AddImageElement(imagePath, image, x, y, width, height, 1);

        return new PdfImageLayoutResult
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }

    public PdfImageLayoutResult DrawImageScaledToHeight(string imagePath, double x, double y, double height)
    {
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be greater than zero.");
        }

        var image = PdfImageReader.Read(imagePath);
        var width = height * image.Width / image.Height;
        AddImageElement(imagePath, image, x, y, width, height, 1);

        return new PdfImageLayoutResult
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }

    public PdfImageLayoutResult DrawImageFit(string imagePath, double x, double y, double maxWidth, double maxHeight)
    {
        if (maxWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "Max width must be greater than zero.");
        }

        if (maxHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "Max height must be greater than zero.");
        }

        var image = PdfImageReader.Read(imagePath);
        var (width, height) = CalculateFitSize(image, maxWidth, maxHeight);

        AddImageElement(imagePath, image, x, y, width, height, 1);

        return new PdfImageLayoutResult
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }

    public PdfImageLayoutResult DrawImageFitCenter(string imagePath, double x, double y, double maxWidth, double maxHeight)
    {
        if (maxWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "Max width must be greater than zero.");
        }

        if (maxHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "Max height must be greater than zero.");
        }

        var image = PdfImageReader.Read(imagePath);
        var (width, height) = CalculateFitSize(image, maxWidth, maxHeight);
        var centeredX = x + ((maxWidth - width) / 2);
        var centeredY = y + ((maxHeight - height) / 2);

        AddImageElement(imagePath, image, centeredX, centeredY, width, height, 1);

        return new PdfImageLayoutResult
        {
            X = centeredX,
            Y = centeredY,
            Width = width,
            Height = height
        };
    }

    /// <summary>Lays out and draws a wrapped paragraph within <paramref name="maxWidth"/>, returning the
    /// measured layout (so you can position following content).</summary>
    /// <param name="text">The paragraph text.</param>
    /// <param name="x">Left x in points.</param>
    /// <param name="y">Top y of the paragraph block in points.</param>
    /// <param name="maxWidth">Wrapping width in points.</param>
    /// <param name="options">Paragraph options (font, alignment, color, line spacing).</param>
    /// <returns>The resulting layout, including the consumed height.</returns>
    public PdfParagraphLayoutResult DrawParagraph(string text, double x, double y, double maxWidth, PdfParagraphOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        if (maxWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "Max width must be greater than zero.");
        }

        options ??= PdfParagraphOptions.Default;

        if (options.FontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Font size must be greater than zero.");
        }

        if (options.HorizontalScalingPercent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Horizontal scaling must be greater than zero.");
        }

        var lineHeight = options.LineHeight ?? options.FontSize * 1.2;

        if (lineHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Line height must be greater than zero.");
        }

        var resolvedFont = ResolveFont(options.Font, options.FontFamily, options.Bold, options.Italic);
        PdfEmbeddedFont? embeddedFont = null;
        _fontLoader?.TryLoad(options.Language, IsBoldFont(resolvedFont), IsItalicFont(resolvedFont), TableFontFamily(resolvedFont), out embeddedFont);
        var lines = WrapText(text, maxWidth, options.FontSize, resolvedFont);
        var widestLine = 0d;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineY = y - (i * lineHeight);

            if (line.Length == 0)
            {
                continue;
            }

            var lineWidth = embeddedFont is not null
                ? embeddedFont.MeasureWidth(line, options.FontSize)
                : EstimateTextWidth(line, options.FontSize, resolvedFont);
            var isLastLine = i == lines.Count - 1;
            var (lineX, wordSpacing) = ResolveLineLayout(x, maxWidth, lineWidth, line, options.Alignment, isLastLine);

            widestLine = Math.Max(widestLine, lineWidth + GetJustificationWidthContribution(line, wordSpacing));

            AddElement(new TextElement(
                line,
                lineX,
                lineY,
                options.FontSize,
                resolvedFont,
                EmbeddedFont: embeddedFont,
                Language: options.Language,
                TextDirection: options.TextDirection,
                WordSpacing: wordSpacing,
                FillColor: options.FillColor ?? PdfColor.Black,
                RotationDegrees: options.RotationDegrees,
                Underline: options.Underline,
                Strikethrough: options.Strikethrough,
                CharacterSpacing: options.CharacterSpacing,
                HorizontalScalingPercent: options.HorizontalScalingPercent));
        }

        var lineCount = lines.Count;
        var height = lineCount > 0 ? ((lineCount - 1) * lineHeight) + options.FontSize : 0;
        var bottomY = y - height;

        return new PdfParagraphLayoutResult
        {
            X = x,
            TopY = y,
            Width = Math.Min(maxWidth, widestLine),
            Height = height,
            BottomY = bottomY,
            LineCount = lineCount
        };
    }

    public PdfParagraphLayoutResult DrawParagraphFromTop(string text, double x, double topY, double maxWidth, PdfParagraphOptions? options = null)
    {
        options ??= PdfParagraphOptions.Default;

        return DrawParagraph(text, x, ToBaselineYFromTop(topY, options.FontSize), maxWidth, options);
    }

    public PdfParagraphLayoutResult DrawTextBoxFromTop(
        string text,
        double x,
        double topY,
        double width,
        double height,
        PdfTextBoxOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        options ??= PdfTextBoxOptions.Default;

        if (options.FontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Font size must be greater than zero.");
        }

        if (options.HorizontalScalingPercent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Horizontal scaling must be greater than zero.");
        }

        var lineHeight = options.LineHeight ?? options.FontSize * 1.2;

        if (lineHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Line height must be greater than zero.");
        }

        var resolvedFont = ResolveFont(options.Font, options.FontFamily, options.Bold, options.Italic);
        PdfEmbeddedFont? embeddedFont = null;
        _fontLoader?.TryLoad(options.Language, IsBoldFont(resolvedFont), IsItalicFont(resolvedFont), TableFontFamily(resolvedFont), out embeddedFont);
        var lines = WrapText(text, width, options.FontSize, resolvedFont);
        var contentHeight = lines.Count > 0 ? ((lines.Count - 1) * lineHeight) + options.FontSize : 0;
        var topBaselineY = ResolveTextBoxTopBaselineY(topY, height, contentHeight, options.FontSize, options.VerticalAlignment);
        var widestLine = 0d;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineY = topBaselineY - (i * lineHeight);

            if (line.Length == 0)
            {
                continue;
            }

            var lineWidth = embeddedFont is not null
                ? embeddedFont.MeasureWidth(line, options.FontSize)
                : EstimateTextWidth(line, options.FontSize, resolvedFont);
            var isLastLine = i == lines.Count - 1;
            var (lineX, wordSpacing) = ResolveLineLayout(x, width, lineWidth, line, options.Alignment, isLastLine);

            widestLine = Math.Max(widestLine, lineWidth + GetJustificationWidthContribution(line, wordSpacing));

            AddElement(new TextElement(
                line,
                lineX,
                lineY,
                options.FontSize,
                resolvedFont,
                EmbeddedFont: embeddedFont,
                Language: options.Language,
                TextDirection: options.TextDirection,
                WordSpacing: wordSpacing,
                FillColor: options.FillColor ?? PdfColor.Black,
                RotationDegrees: options.RotationDegrees,
                Underline: options.Underline,
                Strikethrough: options.Strikethrough,
                CharacterSpacing: options.CharacterSpacing,
                HorizontalScalingPercent: options.HorizontalScalingPercent));
        }

        return new PdfParagraphLayoutResult
        {
            X = x,
            TopY = topBaselineY,
            Width = Math.Min(width, widestLine),
            Height = contentHeight,
            BottomY = topBaselineY - contentHeight,
            LineCount = lines.Count
        };
    }

    private double ResolveTextBoxTopBaselineY(
        double topY,
        double height,
        double contentHeight,
        double fontSize,
        PdfVerticalAlignment verticalAlignment)
    {
        if (topY < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topY), "Top Y cannot be negative.");
        }

        var boxTopY = Height - topY;
        var boxBottomY = boxTopY - height;

        if (contentHeight >= height)
        {
            return boxTopY - fontSize;
        }

        return verticalAlignment switch
        {
            PdfVerticalAlignment.Top => boxTopY - fontSize,
            PdfVerticalAlignment.Middle => boxBottomY + ((height - contentHeight) / 2) + contentHeight,
            PdfVerticalAlignment.Bottom => boxBottomY + contentHeight,
            _ => throw new ArgumentOutOfRangeException(nameof(verticalAlignment), verticalAlignment, "Unsupported vertical alignment.")
        };
    }

    private double ToBaselineYFromTop(double topY, double fontSize)
    {
        if (topY < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topY), "Top Y cannot be negative.");
        }

        if (fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be greater than zero.");
        }

        return Height - topY - fontSize;
    }

    private double ToBottomYFromTop(double topY, double height)
    {
        if (topY < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topY), "Top Y cannot be negative.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        return Height - topY - height;
    }

    private double ToYFromTop(double topY)
    {
        if (topY < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topY), "Top Y cannot be negative.");
        }

        return Height - topY;
    }

    /// <summary>Draws a simple grid table from rows of strings and returns its measured layout.</summary>
    /// <param name="x">Left x in points.</param>
    /// <param name="y">Top y of the table in points.</param>
    /// <param name="width">Total table width in points.</param>
    /// <param name="rows">Rows of cell text (the first row may be styled as a header via options).</param>
    /// <param name="options">Table styling (borders, header, column widths, padding).</param>
    /// <returns>The resulting layout, including the consumed height.</returns>
    public PdfTableLayoutResult DrawSimpleTable(double x, double y, double width, IReadOnlyList<IReadOnlyList<string>> rows, PdfTableOptions? options = null)
    {
        options ??= PdfTableOptions.Default;
        return DrawPreparedSimpleTable(x, y, width, rows, options, PrepareSimpleTable(width, rows, options));
    }

    internal double[] MeasureSimpleTableRows(double width, IReadOnlyList<IReadOnlyList<string>> rows, PdfTableOptions options)
        => PrepareSimpleTable(width, rows, options).RowHeights;

    private sealed record PreparedTable(double[] ColumnWidths,
        Dictionary<(int Row, int Column), PdfTableCellStyle> CellStyles,
        PdfTextAlignment[] ColumnAlignments, PdfVerticalAlignment[] ColumnVerticalAlignments,
        Func<string, string>[] ColumnFormatters, double[] RowHeights);

    private PreparedTable PrepareSimpleTable(double width, IReadOnlyList<IReadOnlyList<string>> rows, PdfTableOptions options)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            throw new ArgumentException("Table requires at least one row.", nameof(rows));
        }

        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Table width must be greater than zero.");
        }

        if (!double.IsFinite(options.RowHeight) || options.RowHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "RowHeight must be greater than zero.");
        }

        if (options.RowHeights is { } customRowHeights)
        {
            if (customRowHeights.Count != rows.Count)
            {
                throw new ArgumentException("RowHeights must have exactly one entry per row.", nameof(options));
            }

            foreach (var rowHeight in customRowHeights)
            {
                if (!double.IsFinite(rowHeight) || rowHeight <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(options), "RowHeights values must be greater than zero.");
                }
            }
        }

        if (!double.IsFinite(options.FontSize) || options.FontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "FontSize must be greater than zero.");
        }

        if (options.CellLineHeight is { } cellLineHeight && (!double.IsFinite(cellLineHeight) || cellLineHeight <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "CellLineHeight must be greater than zero when provided.");
        }

        if (options.RowLineHeights is { } rowLineHeights)
        {
            if (rowLineHeights.Count != rows.Count)
            {
                throw new ArgumentException("RowLineHeights must have exactly one entry per row.", nameof(options));
            }

            foreach (var rowLineHeight in rowLineHeights)
            {
                if (!double.IsFinite(rowLineHeight) || rowLineHeight <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(options), "RowLineHeights values must be greater than zero.");
                }
            }
        }

        if (!double.IsFinite(options.CellPadding) || options.CellPadding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "CellPadding cannot be negative.");
        }

        if (new[] { options.CellPaddingLeft, options.CellPaddingRight, options.CellPaddingTop, options.CellPaddingBottom }
            .Any(value => value is { } padding && (!double.IsFinite(padding) || padding < 0)))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Cell padding side values cannot be negative.");
        }

        if (options.CellCornerRadius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "CellCornerRadius cannot be negative.");
        }

        if (options.BorderLineWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "BorderLineWidth must be greater than zero.");
        }

        if (options.MinColumnWidth is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MinColumnWidth must be greater than zero when provided.");
        }

        if (options.MaxColumnWidth is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxColumnWidth must be greater than zero when provided.");
        }

        if (options.MinColumnWidth is { } minWidth && options.MaxColumnWidth is { } maxWidthConstraint && minWidth > maxWidthConstraint)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MinColumnWidth cannot exceed MaxColumnWidth.");
        }

        if (options.OuterBorderLineWidth is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "OuterBorderLineWidth must be greater than zero when provided.");
        }

        if (options.InnerBorderLineWidth is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "InnerBorderLineWidth must be greater than zero when provided.");
        }

        var columnCount = rows[0].Count;

        if (columnCount == 0)
        {
            throw new ArgumentException("Table rows must contain at least one column.", nameof(rows));
        }

        for (var i = 1; i < rows.Count; i++)
        {
            if (rows[i].Count != columnCount)
            {
                throw new ArgumentException("All table rows must have the same number of columns.", nameof(rows));
            }
        }

        var columnWidths = ResolveTableColumnWidths(width, columnCount, options);
        var cellStyles = new Dictionary<(int Row, int Column), PdfTableCellStyle>();
        foreach (var cell in options.CellStyles ?? [])
        {
            if (cell.Row < 0 || cell.Row >= rows.Count || cell.Column < 0 || cell.Column >= columnCount)
                continue;
            if (cellStyles.ContainsKey((cell.Row, cell.Column))) continue;
            if (cell.FontSize is { } size && (!double.IsFinite(size) || size <= 0)
                || cell.Padding is { } padding && (!double.IsFinite(padding) || padding < 0)
                || cell.BorderWidth is { } border && (!double.IsFinite(border) || border < 0)
                || new[] { cell.BorderTop, cell.BorderRight, cell.BorderBottom, cell.BorderLeft }
                    .Any(side => side is not null && (!double.IsFinite(side.Width) || side.Width < 0)))
                throw new ArgumentOutOfRangeException(nameof(options), "Cell styles require valid font sizes, padding and border widths.");
            cellStyles.TryAdd((cell.Row, cell.Column), cell);
        }
        var columnAlignments = ResolveTableColumnAlignments(columnCount, options);
        var columnVerticalAlignments = ResolveTableColumnVerticalAlignments(columnCount, options);
        var columnFormatters = ResolveTableColumnFormatters(columnCount, options);
        var rowHeights = ResolveTableRowHeights(rows, columnWidths, columnFormatters, options);
        if (options.AutoRowHeight && cellStyles.Count > 0)
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                rowHeights[rowIndex] = Math.Max(options.RowHeight, options.RowHeights?[rowIndex] ?? 0);
                for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    cellStyles.TryGetValue((rowIndex, columnIndex), out var cell);
                    var size = cell?.FontSize ?? options.FontSize;
                    var left = cell?.Padding ?? options.CellPaddingLeft ?? options.CellPadding;
                    var right = cell?.Padding ?? options.CellPaddingRight ?? options.CellPadding;
                    var baseFont = ResolveTableFont(options);
                    if (options.HasHeaderRow && rowIndex == 0 || options.HasFooterRow && rowIndex == rows.Count - 1)
                        baseFont = WithBoldStyle(baseFont);
                    var lines = WrapTableCellText(columnFormatters[columnIndex](rows[rowIndex][columnIndex] ?? ""),
                        columnWidths[columnIndex] - left - right, size, ResolveStyledTableFont(baseFont, cell), options.WrapCellText);
                    var lineHeight = cell?.FontSize is null ? ResolveRowLineHeight(rowIndex, options) : size * 1.2;
                    rowHeights[rowIndex] = Math.Max(rowHeights[rowIndex], lines.Count * lineHeight
                        + (cell?.Padding ?? options.CellPaddingTop ?? options.CellPadding)
                        + (cell?.Padding ?? options.CellPaddingBottom ?? options.CellPadding));
                }
            }
        return new PreparedTable(columnWidths, cellStyles, columnAlignments, columnVerticalAlignments, columnFormatters, rowHeights);
    }

    private PdfTableLayoutResult DrawPreparedSimpleTable(double x, double y, double width,
        IReadOnlyList<IReadOnlyList<string>> rows, PdfTableOptions options, PreparedTable prepared)
    {
        var (columnWidths, cellStyles, columnAlignments, columnVerticalAlignments, columnFormatters, rowHeights) = prepared;
        var columnCount = columnWidths.Length;
        var font = ResolveTableFont(options);
        var textColor = options.TextColor ?? PdfColor.Black;
        var outerBorderColor = options.OuterBorderColor ?? options.BorderColor ?? PdfColor.Black;
        var innerBorderColor = options.InnerBorderColor ?? options.BorderColor ?? PdfColor.Black;
        var outerBorderLineWidth = options.OuterBorderLineWidth ?? options.BorderLineWidth;
        var innerBorderLineWidth = options.InnerBorderLineWidth ?? options.BorderLineWidth;
        var outerStrokeStyle = ResolveBorderStrokeStyle(options.OuterBorderStrokeStyle, outerBorderLineWidth);
        var innerStrokeStyle = ResolveBorderStrokeStyle(options.InnerBorderStrokeStyle, innerBorderLineWidth);
        var paddingLeft = options.CellPaddingLeft ?? options.CellPadding;
        var paddingRight = options.CellPaddingRight ?? options.CellPadding;
        var paddingTop = options.CellPaddingTop ?? options.CellPadding;
        var paddingBottom = options.CellPaddingBottom ?? options.CellPadding;
        var currentRowTop = y;
        var tableBottom = y - rowHeights.Sum();
        var tableSemantic = !_activeArtifact && _activeAccessibility?.Role == PdfStructureRole.Table
            ? _activeAccessibility
            : null;
        var tableId = tableSemantic?.ElementId;
        var headerIds = tableSemantic is not null && options.HasHeaderRow && tableId is { Length: > 0 }
            ? Enumerable.Range(0, columnCount).Select(columnIndex => $"{tableId}-r1-c{columnIndex + 1}").ToArray()
            : [];

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var rowHeight = rowHeights[rowIndex];
            var rowBottom = currentRowTop - rowHeight;
            var isHeader = options.HasHeaderRow && rowIndex == 0;
            var isFooter = options.HasFooterRow && rowIndex == rows.Count - 1;
            var dataRowIndex = (options.HasHeaderRow ? rowIndex - 1 : rowIndex) + options.DataRowOffset;
            var cellX = x;
            var rowSemantic = tableSemantic is not null && tableId is { Length: > 0 }
                ? new PdfAccessibilityProperties
                {
                    Role = PdfStructureRole.TR,
                    ElementId = $"{tableId}-r{rowIndex + 1}",
                    ParentElementId = tableId,
                    ReadingOrder = rowIndex,
                    Language = tableSemantic!.Language
                }
                : null;
            if (rowSemantic is not null)
                RegisterSemanticElement(rowSemantic);

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cellWidth = columnWidths[columnIndex];
                cellStyles.TryGetValue((rowIndex, columnIndex), out var cellStyle);
                var cellSemantic = rowSemantic is not null
                    ? new PdfAccessibilityProperties
                    {
                        Role = isHeader ? PdfStructureRole.TH : PdfStructureRole.TD,
                        ElementId = $"{tableId}-r{rowIndex + 1}-c{columnIndex + 1}",
                        ParentElementId = rowSemantic.ElementId,
                        ReadingOrder = columnIndex,
                        Language = tableSemantic!.Language,
                        TableHeaderScope = isHeader ? PdfTableHeaderScope.Column : PdfTableHeaderScope.None,
                        TableHeaderIds = !isHeader && headerIds.Length > columnIndex ? [headerIds[columnIndex]] : null
                    }
                    : null;
                if (cellSemantic is not null)
                    RegisterSemanticElement(cellSemantic);
                var fillColor = isHeader
                    ? options.HeaderFillColor
                    : isFooter
                        ? options.FooterFillColor
                    : dataRowIndex >= 0 && (dataRowIndex % 2 == 1)
                        ? options.AlternateRowFillColor
                        : null;
                fillColor = cellStyle?.BackgroundColor ?? fillColor;

                if (fillColor is not null)
                {
                    var artifactScope = tableSemantic is null ? null : BeginArtifact();
                    try
                    {
                        if (options.CellCornerRadius > 0)
                        {
                            var maxCellRadius = Math.Min(cellWidth, rowHeight) / 2;
                            var radius = Math.Min(options.CellCornerRadius, maxCellRadius);

                            AddElement(new RoundedRectangleElement(
                                cellX,
                                rowBottom,
                                cellWidth,
                                rowHeight,
                                radius,
                                new PdfStrokeStyle { LineWidth = 1 },
                                Stroke: false,
                                Fill: true,
                                StrokeColor: innerBorderColor,
                                FillColor: fillColor));
                        }
                        else
                        {
                            AddElement(new RectangleElement(
                                cellX,
                                rowBottom,
                                cellWidth,
                                rowHeight,
                                new PdfStrokeStyle { LineWidth = 1 },
                                Stroke: false,
                                Fill: true,
                                StrokeColor: innerBorderColor,
                                FillColor: fillColor));
                        }
                    }
                    finally
                    {
                        artifactScope?.Dispose();
                    }
                }

                var rawCellText = row[columnIndex] ?? string.Empty;
                var cellText = columnFormatters[columnIndex](rawCellText);
                var cellFont = isHeader || isFooter ? WithBoldStyle(font) : font;
                cellFont = ResolveStyledTableFont(cellFont, cellStyle);
                var cellFontSize = cellStyle?.FontSize ?? options.FontSize;
                var cellPaddingLeft = cellStyle?.Padding ?? paddingLeft;
                var cellPaddingRight = cellStyle?.Padding ?? paddingRight;
                var cellPaddingTop = cellStyle?.Padding ?? paddingTop;
                var cellPaddingBottom = cellStyle?.Padding ?? paddingBottom;

                var availableTextWidth = cellWidth - cellPaddingLeft - cellPaddingRight;
                var lines = WrapTableCellText(cellText, availableTextWidth, cellFontSize, cellFont, options.WrapCellText);
                var lineHeight = cellStyle?.FontSize is null ? ResolveRowLineHeight(rowIndex, options) : cellFontSize * 1.2;
                var contentHeight = lines.Count * lineHeight;
                var verticalAlignment = columnVerticalAlignments[columnIndex];
                var firstBaselineY = verticalAlignment switch
                {
                    PdfVerticalAlignment.Top => (rowBottom + rowHeight) - cellPaddingTop - lineHeight,
                    PdfVerticalAlignment.Middle => rowBottom + ((rowHeight + contentHeight) / 2) - lineHeight,
                    PdfVerticalAlignment.Bottom => rowBottom + cellPaddingBottom + ((lines.Count - 1) * lineHeight),
                    _ => rowBottom + ((rowHeight + contentHeight) / 2) - lineHeight
                };

                var cellScope = cellSemantic is null ? null : BeginAccessibilityElement(cellSemantic);
                try
                {
                    for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                    {
                        var line = lines[lineIndex];
                        if (string.IsNullOrEmpty(line))
                            continue;

                        var textWidth = EstimateTextWidth(line, cellFontSize, cellFont);
                        var alignment = cellStyle?.Alignment ?? columnAlignments[columnIndex];
                        var textX = alignment switch
                        {
                            PdfTextAlignment.Left or PdfTextAlignment.Justify => cellX + cellPaddingLeft,
                            PdfTextAlignment.Center => cellX + ((cellWidth - textWidth) / 2),
                            PdfTextAlignment.Right => cellX + cellWidth - cellPaddingRight - textWidth,
                            _ => cellX + cellPaddingLeft
                        };

                        if (textX < cellX + cellPaddingLeft)
                            textX = cellX + cellPaddingLeft;

                        var textY = firstBaselineY - (lineIndex * lineHeight);
                        DrawText(line, textX, textY, new PdfDrawTextOptions
                        {
                            FontSize = cellFontSize,
                            Font = cellFont,
                            FillColor = cellStyle?.TextColor ?? textColor
                        });
                    }
                }
                finally
                {
                    cellScope?.Dispose();
                }

                cellX += cellWidth;
            }

            currentRowTop = rowBottom;
        }

        // Table borders carry layout rather than semantic content.
        var bordersArtifactScope = tableSemantic is null ? null : BeginArtifact();
        try
        {
            if (cellStyles.Count > 0)
            {
                DrawStyledTableBorders(x, y, columnWidths, rowHeights, cellStyles, options);
            }
            // Borders must be emitted after cell fills. PDF elements are painted in
            // insertion order, so drawing fills later would otherwise hide the grid.
            if (options.DrawOuterBorder && cellStyles.Count == 0)
            {
                DrawRectangle(
                    x: x,
                    y: tableBottom,
                    width: width,
                    height: y - tableBottom,
                    lineWidth: outerBorderLineWidth,
                    fill: false,
                    strokeColor: outerBorderColor,
                    strokeStyle: outerStrokeStyle);
            }

            if (options.DrawInnerHorizontalBorders && cellStyles.Count == 0)
            {
                var horizontalY = y;

                for (var rowIndex = 0; rowIndex < rowHeights.Length - 1; rowIndex++)
                {
                    horizontalY -= rowHeights[rowIndex];
                    DrawLine(
                        x1: x,
                        y1: horizontalY,
                        x2: x + width,
                        y2: horizontalY,
                        lineWidth: innerBorderLineWidth,
                        strokeColor: innerBorderColor,
                        strokeStyle: innerStrokeStyle);
                }
            }

            if (options.DrawInnerVerticalBorders && cellStyles.Count == 0)
            {
                var verticalX = x;

                for (var columnIndex = 0; columnIndex < columnWidths.Length - 1; columnIndex++)
                {
                    verticalX += columnWidths[columnIndex];
                    DrawLine(
                        x1: verticalX,
                        y1: tableBottom,
                        x2: verticalX,
                        y2: y,
                        lineWidth: innerBorderLineWidth,
                        strokeColor: innerBorderColor,
                        strokeStyle: innerStrokeStyle);
                }
            }
        }
        finally
        {
            bordersArtifactScope?.Dispose();
        }

        var totalHeight = rowHeights.Sum();

        return new PdfTableLayoutResult
        {
            X = x,
            TopY = y,
            Width = width,
            Height = totalHeight,
            BottomY = y - totalHeight,
            RowCount = rows.Count,
            ColumnCount = columnCount
        };
    }

    /// <summary>Adds a clickable rectangle that opens a URL.</summary>
    /// <param name="x">Left x in points.</param>
    /// <param name="y">Bottom y in points.</param>
    /// <param name="width">Clickable width in points.</param>
    /// <param name="height">Clickable height in points.</param>
    /// <param name="url">The destination URL.</param>
    public void AddWebLink(double x, double y, double width, double height, string url)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Link width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Link height must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));
        }

        _linkAnnotations.Add(PdfLinkAnnotation.ForUrl(x, y, width, height, url) with
        {
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    public void AddPageLink(double x, double y, double width, double height, int targetPageNumber)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Link width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Link height must be greater than zero.");
        }

        if (targetPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetPageNumber), "Target page number must be greater than zero.");
        }

        _linkAnnotations.Add(PdfLinkAnnotation.ForPage(x, y, width, height, targetPageNumber) with
        {
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    public void AddNamedDestinationLink(double x, double y, double width, double height, string destinationName)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Link width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Link height must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(destinationName))
        {
            throw new ArgumentException("Destination name cannot be null or empty.", nameof(destinationName));
        }

        _linkAnnotations.Add(PdfLinkAnnotation.ForNamedDestination(x, y, width, height, destinationName) with
        {
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    public void AddComboBox(
        string fieldName,
        double x,
        double y,
        double width,
        double height,
        IReadOnlyList<string> options,
        string? selectedValue = null,
        double fontSize = 10)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        if (options is null || options.Count == 0)
        {
            throw new ArgumentException("Options cannot be null or empty.", nameof(options));
        }

        if (fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be greater than zero.");
        }

        _comboBoxAnnotations.Add(new PdfComboBoxAnnotation
        {
            FieldName = fieldName,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Options = options,
            SelectedValue = selectedValue,
            FontSize = fontSize,
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    public void AddMultilineTextField(
        string fieldName,
        double x,
        double y,
        double width,
        double height,
        string defaultValue = "",
        double fontSize = 10)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        if (fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be greater than zero.");
        }

        _multilineTextFields.Add(new PdfMultilineTextFieldAnnotation
        {
            FieldName = fieldName,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            DefaultValue = defaultValue,
            FontSize = fontSize,
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    public void AddTextField(
        string fieldName,
        double x,
        double y,
        double width,
        double height,
        string defaultValue = "",
        double fontSize = 10)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        if (fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be greater than zero.");
        }

        _textFields.Add(new PdfTextFieldAnnotation
        {
            FieldName = fieldName,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            DefaultValue = defaultValue,
            FontSize = fontSize,
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    /// <summary>Adds an interactive checkbox form field.</summary>
    /// <param name="fieldName">The form field name (unique within the document).</param>
    /// <param name="x">Left x in points.</param>
    /// <param name="y">Bottom y in points.</param>
    /// <param name="size">Box size (width and height) in points.</param>
    /// <param name="isChecked">Initial checked state.</param>
    public void AddCheckBox(string fieldName, double x, double y, double size, bool isChecked = false)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero.");

        _checkBoxAnnotations.Add(new PdfCheckBoxAnnotation
        {
            FieldName = fieldName,
            X = x,
            Y = y,
            Width = size,
            Height = size,
            IsChecked = isChecked,
            Accessibility = _activeArtifact ? null : _activeAccessibility,
        });
    }

    /// <summary>Adds a checkbox with an explicit normal appearance shared by viewers and printing.</summary>
    public void AddCheckBox(PdfCheckBoxAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ValidateFormField(annotation.FieldName, annotation.Width, annotation.Height);
        if (annotation.CheckStyle is not ("checked" or "cross" or "dot" or "empty")
            || !double.IsFinite(annotation.BorderWidth) || annotation.BorderWidth <= 0)
            throw new ArgumentException("Invalid checkbox appearance.", nameof(annotation));
        _checkBoxAnnotations.Add(annotation with { Accessibility = _activeArtifact ? null : _activeAccessibility });
    }

    /// <summary>Adds an interactive list-box field.</summary>
    public void AddListBox(
        string fieldName,
        double x,
        double y,
        double width,
        double height,
        IReadOnlyList<string> options,
        IReadOnlyList<string>? selectedValues = null,
        bool multiSelect = false,
        double fontSize = 10)
    {
        ValidateFormField(fieldName, width, height);
        if (options is null || options.Count == 0)
            throw new ArgumentException("Options cannot be null or empty.", nameof(options));
        if (fontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be greater than zero.");

        _listBoxAnnotations.Add(new PdfListBoxAnnotation
        {
            FieldName = fieldName,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Options = options,
            SelectedValues = selectedValues ?? [],
            MultiSelect = multiSelect,
            FontSize = fontSize,
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    /// <summary>Adds one widget belonging to an interactive radio-button group.</summary>
    public void AddRadioButton(
        string fieldName,
        string exportValue,
        int optionIndex,
        double x,
        double y,
        double width,
        double height,
        bool isSelected = false)
    {
        ValidateFormField(fieldName, width, height);
        if (string.IsNullOrWhiteSpace(exportValue))
            throw new ArgumentException("Export value cannot be null or empty.", nameof(exportValue));
        if (optionIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(optionIndex));

        _radioButtonAnnotations.Add(new PdfRadioButtonAnnotation
        {
            FieldName = fieldName,
            ExportValue = exportValue,
            OptionIndex = optionIndex,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsSelected = isSelected,
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    /// <summary>Adds an unsigned interactive signature field.</summary>
    public void AddSignatureField(string fieldName, double x, double y, double width, double height)
    {
        ValidateFormField(fieldName, width, height);
        _signatureFieldAnnotations.Add(new PdfSignatureFieldAnnotation
        {
            FieldName = fieldName,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    /// <summary>Adds an interactive push-button field.</summary>
    public void AddPushButton(
        string fieldName,
        string caption,
        double x,
        double y,
        double width,
        double height,
        string? action = null)
    {
        ValidateFormField(fieldName, width, height);
        var parsedAction = action?.Trim().ToLowerInvariant() switch
        {
            null or "" => PdfPushButtonAction.None,
            "submit" => PdfPushButtonAction.Submit,
            "reset" => PdfPushButtonAction.Reset,
            _ => throw new ArgumentException("Push-button action must be 'submit', 'reset', or empty.", nameof(action))
        };
        _pushButtonAnnotations.Add(new PdfPushButtonAnnotation
        {
            FieldName = fieldName,
            Caption = caption ?? string.Empty,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Action = parsedAction,
            Accessibility = _activeArtifact ? null : _activeAccessibility
        });
    }

    private static void ValidateFormField(string fieldName, double width, double height)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name cannot be null or empty.", nameof(fieldName));
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
    }

    public void AddStickyNoteAnnotation(double x, double y, double width, double height, string contents, PdfColor? color = null, double opacity = 1)
    {
        AddReviewAnnotation(PdfReviewAnnotationType.StickyNote, x, y, width, height, contents, color, opacity);
    }

    public void AddFreeTextAnnotation(double x, double y, double width, double height, string contents, PdfColor? color = null, double opacity = 1)
    {
        AddReviewAnnotation(PdfReviewAnnotationType.FreeText, x, y, width, height, contents, color, opacity);
    }

    public void AddHighlightAnnotation(double x, double y, double width, double height, string contents = "", PdfColor? color = null, double opacity = 0.45, IReadOnlyList<PdfMarkupQuadPoint>? quadPoints = null)
    {
        AddReviewAnnotation(PdfReviewAnnotationType.Highlight, x, y, width, height, contents, color ?? PdfColor.FromRgb(254, 240, 138), opacity, quadPoints);
    }

    public void AddUnderlineAnnotation(double x, double y, double width, double height, string contents = "", PdfColor? color = null, double opacity = 1, IReadOnlyList<PdfMarkupQuadPoint>? quadPoints = null)
    {
        AddReviewAnnotation(PdfReviewAnnotationType.Underline, x, y, width, height, contents, color ?? PdfColor.FromRgb(37, 99, 235), opacity, quadPoints);
    }

    public void AddStrikeOutAnnotation(double x, double y, double width, double height, string contents = "", PdfColor? color = null, double opacity = 1, IReadOnlyList<PdfMarkupQuadPoint>? quadPoints = null)
    {
        AddReviewAnnotation(PdfReviewAnnotationType.StrikeOut, x, y, width, height, contents, color ?? PdfColor.FromRgb(220, 38, 38), opacity, quadPoints);
    }

    public void AddSquareAnnotation(double x, double y, double width, double height, string contents = "", PdfColor? color = null, double opacity = 1)
    {
        AddReviewAnnotation(PdfReviewAnnotationType.Square, x, y, width, height, contents, color, opacity);
    }

    public void AddCircleAnnotation(double x, double y, double width, double height, string contents = "", PdfColor? color = null, double opacity = 1)
    {
        AddReviewAnnotation(PdfReviewAnnotationType.Circle, x, y, width, height, contents, color, opacity);
    }

    public void AddRedactionAnnotation(double x, double y, double width, double height, string contents = "Redaction mark", PdfColor? color = null, double opacity = 1)
    {
        AddReviewAnnotation(PdfReviewAnnotationType.Redaction, x, y, width, height, contents, color ?? PdfColor.Black, opacity);
    }

    private static (double X, double WordSpacing) ResolveLineLayout(
        double startX,
        double maxWidth,
        double lineWidth,
        string line,
        PdfTextAlignment alignment,
        bool isLastLine)
    {
        if (alignment == PdfTextAlignment.Justify && !isLastLine)
        {
            var spaceCount = CountSpaces(line);

            if (spaceCount > 0 && lineWidth < maxWidth)
            {
                var extraSpace = maxWidth - lineWidth;
                var wordSpacing = extraSpace / spaceCount;
                return (startX, wordSpacing);
            }
        }

        var x = ResolveAlignedX(startX, maxWidth, lineWidth, alignment);
        return (x, 0);
    }

    private static int CountSpaces(string text)
    {
        var count = 0;

        foreach (var ch in text)
        {
            if (ch == ' ')
            {
                count++;
            }
        }

        return count;
    }

    private static double GetJustificationWidthContribution(string text, double wordSpacing)
    {
        if (wordSpacing == 0)
        {
            return 0;
        }

        return CountSpaces(text) * wordSpacing;
    }

    private static double ResolveAlignedX(double startX, double maxWidth, double lineWidth, PdfTextAlignment alignment)
    {
        return alignment switch
        {
            PdfTextAlignment.Left or PdfTextAlignment.Justify => startX,
            PdfTextAlignment.Center => startX + ((maxWidth - lineWidth) / 2),
            PdfTextAlignment.Right => startX + (maxWidth - lineWidth),
            _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unsupported text alignment.")
        };
    }

    private static List<string> WrapText(string text, double maxWidth, double fontSize, PdfStandardFont font)
    {
        var lines = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var currentLine = string.Empty;

            foreach (var word in words)
            {
                if (currentLine.Length == 0)
                {
                    currentLine = word;
                    continue;
                }

                var candidate = $"{currentLine} {word}";

                if (EstimateTextWidth(candidate, fontSize, font) <= maxWidth)
                {
                    currentLine = candidate;
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine);
            }
        }

        return lines;
    }

    private static double EstimateTextWidth(string text, double fontSize, PdfStandardFont font)
    {
        var avgCharacterWidthFactor = font switch
        {
            PdfStandardFont.Courier or PdfStandardFont.CourierBold or PdfStandardFont.CourierOblique or PdfStandardFont.CourierBoldOblique => 0.6,
            PdfStandardFont.TimesRoman or PdfStandardFont.TimesBold or PdfStandardFont.TimesItalic or PdfStandardFont.TimesBoldItalic => 0.5,
            _ => 0.52
        };

        return text.Length * fontSize * avgCharacterWidthFactor;
    }

    private PdfStandardFont ResolveTableFont(PdfTableOptions options)
    {
        return options.Font
            ?? (options.FontFamily is { } family
                ? PdfStandardFonts.FromStyle(family, options.Bold, options.Italic)
                : DefaultFont);
    }

    private static PdfStandardFont WithBoldStyle(PdfStandardFont font) => font switch
    {
        PdfStandardFont.Helvetica or PdfStandardFont.HelveticaBold => PdfStandardFont.HelveticaBold,
        PdfStandardFont.HelveticaOblique or PdfStandardFont.HelveticaBoldOblique => PdfStandardFont.HelveticaBoldOblique,
        PdfStandardFont.TimesRoman or PdfStandardFont.TimesBold => PdfStandardFont.TimesBold,
        PdfStandardFont.TimesItalic or PdfStandardFont.TimesBoldItalic => PdfStandardFont.TimesBoldItalic,
        PdfStandardFont.Courier or PdfStandardFont.CourierBold => PdfStandardFont.CourierBold,
        PdfStandardFont.CourierOblique or PdfStandardFont.CourierBoldOblique => PdfStandardFont.CourierBoldOblique,
        _ => font
    };

    private static bool IsBoldFont(PdfStandardFont font) => font is
        PdfStandardFont.HelveticaBold or PdfStandardFont.HelveticaBoldOblique or
        PdfStandardFont.TimesBold or PdfStandardFont.TimesBoldItalic or
        PdfStandardFont.CourierBold or PdfStandardFont.CourierBoldOblique;

    private static bool IsItalicFont(PdfStandardFont font) => font is
        PdfStandardFont.HelveticaOblique or PdfStandardFont.HelveticaBoldOblique or
        PdfStandardFont.TimesItalic or PdfStandardFont.TimesBoldItalic or
        PdfStandardFont.CourierOblique or PdfStandardFont.CourierBoldOblique;

    private static double[] ResolveTableColumnWidths(double tableWidth, int columnCount, PdfTableOptions options)
    {
        double[] resolved;

        if (options.ColumnWidths is null)
        {
            var equalWidth = tableWidth / columnCount;
            resolved = Enumerable.Repeat(equalWidth, columnCount).ToArray();
        }
        else
        {
            if (options.ColumnWidths.Count != columnCount)
            {
                throw new ArgumentException("ColumnWidths must have exactly one entry per column.", nameof(options));
            }

            var totalWeight = 0d;

            for (var i = 0; i < options.ColumnWidths.Count; i++)
            {
                var weight = options.ColumnWidths[i];
                if (weight <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(options), "Column width weights must be greater than zero.");
                }

                totalWeight += weight;
            }

            resolved = new double[columnCount];

            for (var i = 0; i < columnCount; i++)
            {
                resolved[i] = tableWidth * (options.ColumnWidths[i] / totalWeight);
            }
        }

        if (options.MinColumnWidth is { } minWidth)
        {
            for (var i = 0; i < resolved.Length; i++)
            {
                if (resolved[i] < minWidth)
                {
                    throw new ArgumentException("Resolved column width is below MinColumnWidth.", nameof(options));
                }
            }
        }

        if (options.MaxColumnWidth is { } maxWidth)
        {
            for (var i = 0; i < resolved.Length; i++)
            {
                if (resolved[i] > maxWidth)
                {
                    throw new ArgumentException("Resolved column width exceeds MaxColumnWidth.", nameof(options));
                }
            }
        }

        return resolved;
    }

    private static PdfTextAlignment[] ResolveTableColumnAlignments(int columnCount, PdfTableOptions options)
    {
        if (options.ColumnAlignments is null)
        {
            return Enumerable.Repeat(options.CellAlignment, columnCount).ToArray();
        }

        if (options.ColumnAlignments.Count != columnCount)
        {
            throw new ArgumentException("ColumnAlignments must have exactly one entry per column.", nameof(options));
        }

        return options.ColumnAlignments.ToArray();
    }

    private static PdfVerticalAlignment[] ResolveTableColumnVerticalAlignments(int columnCount, PdfTableOptions options)
    {
        if (options.ColumnVerticalAlignments is null)
        {
            return Enumerable.Repeat(options.CellVerticalAlignment, columnCount).ToArray();
        }

        if (options.ColumnVerticalAlignments.Count != columnCount)
        {
            throw new ArgumentException("ColumnVerticalAlignments must have exactly one entry per column.", nameof(options));
        }

        return options.ColumnVerticalAlignments.ToArray();
    }

    private static Func<string, string>[] ResolveTableColumnFormatters(int columnCount, PdfTableOptions options)
    {
        if (options.ColumnValueFormatters is null)
        {
            return Enumerable.Repeat<Func<string, string>>(static value => value, columnCount).ToArray();
        }

        if (options.ColumnValueFormatters.Count != columnCount)
        {
            throw new ArgumentException("ColumnValueFormatters must have exactly one entry per column.", nameof(options));
        }

        var formatters = new Func<string, string>[columnCount];

        for (var i = 0; i < columnCount; i++)
        {
            formatters[i] = options.ColumnValueFormatters[i] ?? (static value => value);
        }

        return formatters;
    }

    private static PdfStrokeStyle ResolveBorderStrokeStyle(PdfStrokeStyle? style, double lineWidth)
    {
        if (style is null)
        {
            return new PdfStrokeStyle { LineWidth = lineWidth };
        }

        return new PdfStrokeStyle
        {
            LineWidth = lineWidth,
            LineCap = style.LineCap,
            LineJoin = style.LineJoin,
            DashArray = style.DashArray,
            DashPhase = style.DashPhase
        };
    }

    private static PdfFontFamily TableFontFamily(PdfStandardFont font) => font switch
    {
        PdfStandardFont.TimesRoman or PdfStandardFont.TimesBold or PdfStandardFont.TimesItalic or PdfStandardFont.TimesBoldItalic => PdfFontFamily.Times,
        PdfStandardFont.Courier or PdfStandardFont.CourierBold or PdfStandardFont.CourierOblique or PdfStandardFont.CourierBoldOblique => PdfFontFamily.Courier,
        _ => PdfFontFamily.Helvetica
    };

    private static PdfStandardFont ResolveStyledTableFont(PdfStandardFont font, PdfTableCellStyle? cell)
    {
        if (cell is null || cell.FontFamily is null && cell.Bold is null && cell.Italic is null) return font;
        var family = TableFontFamily(font);
        var bold = font is PdfStandardFont.HelveticaBold or PdfStandardFont.HelveticaBoldOblique
            or PdfStandardFont.TimesBold or PdfStandardFont.TimesBoldItalic or PdfStandardFont.CourierBold or PdfStandardFont.CourierBoldOblique;
        var italic = font is PdfStandardFont.HelveticaOblique or PdfStandardFont.HelveticaBoldOblique
            or PdfStandardFont.TimesItalic or PdfStandardFont.TimesBoldItalic or PdfStandardFont.CourierOblique or PdfStandardFont.CourierBoldOblique;
        return PdfStandardFonts.FromStyle(cell.FontFamily ?? family, cell.Bold ?? bold, cell.Italic ?? italic);
    }

    private void DrawStyledTableBorders(double x, double top, double[] widths, double[] heights,
        Dictionary<(int Row, int Column), PdfTableCellStyle> cells, PdfTableOptions options)
    {
        var xs = new double[widths.Length + 1];
        var ys = new double[heights.Length + 1];
        xs[0] = x; ys[0] = top;
        for (var c = 0; c < widths.Length; c++) xs[c + 1] = xs[c] + widths[c];
        for (var r = 0; r < heights.Length; r++) ys[r + 1] = ys[r] - heights[r];
        (double Width, IPdfColor Color, bool Explicit) Side(int r, int c, string side, bool outer, bool enabled)
        {
            cells.TryGetValue((r, c), out var cell);
            var specific = side switch { "top" => cell?.BorderTop, "right" => cell?.BorderRight,
                "bottom" => cell?.BorderBottom, _ => cell?.BorderLeft };
            var custom = cell is not null && (cell.BorderWidth is not null || cell.BorderColor is not null
                || cell.BorderTop is not null || cell.BorderRight is not null || cell.BorderBottom is not null || cell.BorderLeft is not null);
            if (custom)
                return (specific?.Width ?? (cell!.BorderWidth is not null || cell.BorderColor is not null ? cell.BorderWidth ?? 1 : 0),
                    specific?.Color ?? cell!.BorderColor ?? PdfColor.Black, true);
            return (enabled ? (outer ? options.OuterBorderLineWidth : options.InnerBorderLineWidth) ?? options.BorderLineWidth : 0,
                (outer ? options.OuterBorderColor : options.InnerBorderColor) ?? options.BorderColor ?? PdfColor.Black, false);
        }
        void Edge((double Width, IPdfColor Color, bool Explicit) first, (double Width, IPdfColor Color, bool Explicit) second,
            double x1, double y1, double x2, double y2, bool outer)
        {
            var selected = first.Explicit != second.Explicit ? (first.Explicit ? first : second)
                : first.Width > second.Width ? first : second;
            if (selected.Width > 0)
                DrawLine(x1, y1, x2, y2, selected.Width, selected.Color,
                    selected.Explicit ? null : outer ? options.OuterBorderStrokeStyle : options.InnerBorderStrokeStyle);
        }
        for (var r = 0; r < heights.Length; r++)
            for (var c = 0; c <= widths.Length; c++)
            {
                var outer = c == 0 || c == widths.Length;
                var enabled = outer ? options.DrawOuterBorder : options.DrawInnerVerticalBorders;
                var left = Side(r, c - 1, "right", outer, enabled);
                var right = Side(r, c, "left", outer, enabled);
                if (c == 0) left = right;
                if (c == widths.Length) right = left;
                Edge(left, right, xs[c], ys[r], xs[c], ys[r + 1], outer);
            }
        for (var r = 0; r <= heights.Length; r++)
            for (var c = 0; c < widths.Length; c++)
            {
                var outer = r == 0 || r == heights.Length;
                var enabled = outer ? options.DrawOuterBorder : options.DrawInnerHorizontalBorders;
                var above = Side(r - 1, c, "bottom", outer, enabled);
                var below = Side(r, c, "top", outer, enabled);
                if (r == 0) above = below;
                if (r == heights.Length) below = above;
                Edge(above, below, xs[c], ys[r], xs[c + 1], ys[r], outer);
            }
    }

    private static double[] ResolveTableRowHeights(
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<Func<string, string>> columnFormatters,
        PdfTableOptions options)
    {
        if (options.RowHeights is { } explicitHeights)
        {
            if (!options.AutoRowHeight)
            {
                return explicitHeights.ToArray();
            }

            var autoHeights = ResolveAutoRowHeights(rows, columnWidths, columnFormatters, options);
            var merged = new double[rows.Count];

            for (var i = 0; i < rows.Count; i++)
            {
                merged[i] = Math.Max(explicitHeights[i], autoHeights[i]);
            }

            return merged;
        }

        return options.AutoRowHeight
            ? ResolveAutoRowHeights(rows, columnWidths, columnFormatters, options)
            : Enumerable.Repeat(options.RowHeight, rows.Count).ToArray();
    }

    private static double[] ResolveAutoRowHeights(
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<Func<string, string>> columnFormatters,
        PdfTableOptions options)
    {
        if (!options.AutoRowHeight)
        {
            return Enumerable.Repeat(options.RowHeight, rows.Count).ToArray();
        }

        var heights = new double[rows.Count];

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var maxContentLines = 1;

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                var formatter = columnFormatters[columnIndex];
                var text = formatter(row[columnIndex] ?? string.Empty);
                var paddingLeft = options.CellPaddingLeft ?? options.CellPadding;
                var paddingRight = options.CellPaddingRight ?? options.CellPadding;
                var availableWidth = columnWidths[columnIndex] - paddingLeft - paddingRight;
                var lineCount = CountWrappedLines(text, availableWidth, options.FontSize, options.FontFamily, options.Bold, options.Italic, options.Font, options.WrapCellText);

                if (lineCount > maxContentLines)
                {
                    maxContentLines = lineCount;
                }
            }

            var lineHeight = ResolveRowLineHeight(rowIndex, options);
            var paddingTop = options.CellPaddingTop ?? options.CellPadding;
            var paddingBottom = options.CellPaddingBottom ?? options.CellPadding;
            var calculatedHeight = (maxContentLines * lineHeight) + paddingTop + paddingBottom;
            heights[rowIndex] = Math.Max(options.RowHeight, calculatedHeight);
        }

        return heights;
    }

    private static double ResolveRowLineHeight(int rowIndex, PdfTableOptions options)
    {
        if (options.RowLineHeights is { } rowLineHeights)
        {
            return rowLineHeights[rowIndex];
        }

        return options.CellLineHeight ?? options.FontSize * 1.2;
    }

    private static int CountWrappedLines(string text, double availableWidth, double fontSize, PdfFontFamily? fontFamily, bool bold, bool italic, PdfStandardFont? font, bool wrap)
    {
        var resolvedFont = font ?? (fontFamily is { } family
            ? PdfStandardFonts.FromStyle(family, bold, italic)
            : PdfStandardFont.Helvetica);

        return WrapTableCellText(text, availableWidth, fontSize, resolvedFont, wrap).Count;
    }

    private static List<string> WrapTableCellText(string text, double maxWidth, double fontSize, PdfStandardFont font, bool wrap)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string> { string.Empty };
        }

        if (!wrap || maxWidth <= 0)
        {
            return new List<string> { text };
        }

        var lines = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var currentLine = string.Empty;

            foreach (var word in words)
            {
                if (currentLine.Length == 0)
                {
                    currentLine = word;
                    continue;
                }

                var candidate = $"{currentLine} {word}";

                if (EstimateTextWidth(candidate, fontSize, font) <= maxWidth)
                {
                    currentLine = candidate;
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine);
            }
        }

        return lines;
    }

    private PdfStandardFont ResolveFont(PdfStandardFont? font, PdfFontFamily? fontFamily, bool bold, bool italic)
    {
        if (font is { } explicitFont) return explicitFont;
        if (fontFamily is { } family) return PdfStandardFonts.FromStyle(family, bold, italic);
        if (!bold && !italic) return DefaultFont;

        var defaultFamily = DefaultFont switch
        {
            PdfStandardFont.TimesRoman or PdfStandardFont.TimesBold or PdfStandardFont.TimesItalic
                or PdfStandardFont.TimesBoldItalic => PdfFontFamily.Times,
            PdfStandardFont.Courier or PdfStandardFont.CourierBold or PdfStandardFont.CourierOblique
                or PdfStandardFont.CourierBoldOblique => PdfFontFamily.Courier,
            _ => PdfFontFamily.Helvetica
        };
        return PdfStandardFonts.FromStyle(defaultFamily,
            bold || IsBoldFont(DefaultFont), italic || IsItalicFont(DefaultFont));
    }

    private static (double Width, double Height) CalculateFitSize(PdfImageData image, double maxWidth, double maxHeight)
    {
        var scale = Math.Min(maxWidth / image.Width, maxHeight / image.Height);
        return (image.Width * scale, image.Height * scale);
    }

    private static PdfStrokeStyle ResolveStrokeStyle(double lineWidth, PdfStrokeStyle? strokeStyle)
    {
        if (strokeStyle is null)
        {
            if (lineWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lineWidth), "Line width must be greater than zero.");
            }

            return new PdfStrokeStyle { LineWidth = lineWidth };
        }

        ValidateStrokeStyle(strokeStyle);
        return strokeStyle;
    }

    private static void ValidateStrokeStyle(PdfStrokeStyle strokeStyle)
    {
        if (strokeStyle.LineWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strokeStyle), "Line width must be greater than zero.");
        }

        if (strokeStyle.DashPhase < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strokeStyle), "Dash phase cannot be negative.");
        }

        if (strokeStyle.DashArray is null)
        {
            return;
        }

        foreach (var segmentLength in strokeStyle.DashArray)
        {
            if (segmentLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(strokeStyle), "Dash array values must be greater than zero.");
            }
        }
    }

    private void AddImageElement(string imagePath, PdfImageData image, double x, double y, double width, double height, double opacity)
    {
        var cacheKey = Path.GetFullPath(imagePath);
        AddImageElementWithCacheKey(cacheKey, image, x, y, width, height, opacity);
    }

    private void AddImageElementWithCacheKey(string cacheKey, PdfImageData image, double x, double y, double width, double height, double opacity)
    {
        AddElement(new ImageElement(image, cacheKey, x, y, width, height, opacity));
    }

    private void AddReviewAnnotation(PdfReviewAnnotationType type, double x, double y, double width, double height, string contents, PdfColor? color, double opacity, IReadOnlyList<PdfMarkupQuadPoint>? quadPoints = null)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Annotation width and height must be greater than zero.");
        }

        _reviewAnnotations.Add(new PdfReviewAnnotation(
            type,
            x,
            y,
            width,
            height,
            contents,
            color ?? PdfColor.FromRgb(250, 204, 21),
            Math.Clamp(opacity, 0.1, 1),
            quadPoints?.Count > 0 ? quadPoints : []));
    }

    private static string GetImageBytesCacheKey(byte[] imageBytes)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        return $"bytes:{Convert.ToHexString(SHA256.HashData(imageBytes))}";
    }
}
