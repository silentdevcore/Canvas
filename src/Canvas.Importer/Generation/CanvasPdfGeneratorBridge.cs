using System.Globalization;
using System.Reflection;
using Canvas.Importer.Document;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;
using Canvas.Infrastructure.Pdf;
using Canvas.Pdf;
using ImporterPdfColor = Canvas.Importer.Graphics.PdfColor;
using ImporterPdfMatrix = Canvas.Importer.Graphics.PdfMatrix;
using ImporterPdfPoint = Canvas.Importer.Graphics.PdfPoint;
using ImporterPdfRectangle = Canvas.Importer.Graphics.PdfRectangle;
using CanvasPdfColor = Canvas.Pdf.PdfColor;
using CanvasPdfDocument = Canvas.Pdf.PdfDocument;
using CanvasPdfPage = Canvas.Pdf.PdfPage;
using CanvasPdfPoint = Canvas.Pdf.PdfPoint;

namespace Canvas.Importer.Generation;

public sealed class CanvasPdfGeneratorBridge : IPdfGeneratorBridge
{
    private static readonly FieldInfo PageElementsField = typeof(CanvasPdfPage).GetField("_elements", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Canvas.Pdf.PdfPage._elements field was not found.");
    private static readonly Type CanvasPdfImageDataType = typeof(CanvasPdfPage).Assembly.GetType("Canvas.Pdf.PdfImageData", throwOnError: true)
        ?? throw new InvalidOperationException("Canvas.Pdf.PdfImageData type was not found.");

    private readonly PdfDocumentRenderer _renderer;

    public CanvasPdfGeneratorBridge()
        : this(new PdfDocumentRenderer())
    {
    }

    public CanvasPdfGeneratorBridge(PdfDocumentRenderer renderer)
    {
        _renderer = renderer;
    }

    public async Task RegenerateAsync(PdfDocumentModel document, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);

        var bytes = _renderer.Render(CreateDocument(document));
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public object MapObject(PdfObject parsedObject)
    {
        ArgumentNullException.ThrowIfNull(parsedObject);

        return parsedObject switch
        {
            PdfNull => DBNull.Value,
            PdfBoolean boolean => boolean.Value,
            PdfInteger integer => integer.Value,
            PdfNumber number => number.Value,
            PdfName name => name.Value,
            PdfString text => text.ToLatin1String(),
            PdfReference reference => $"{reference.Id.Number} {reference.Id.Generation} R",
            PdfArray array => array.Items.Select(MapObject).ToArray(),
            PdfDictionary dictionary => dictionary.Values.ToDictionary(static entry => entry.Key, entry => MapObject(entry.Value), StringComparer.Ordinal),
            PdfStreamObject stream => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Dictionary"] = MapObject(stream.Dictionary),
                ["EncodedBytes"] = stream.EncodedBytes.ToArray(),
                ["DecodedBytes"] = stream.IsDecoded ? stream.DecodedBytes.ToArray() : null
            },
            _ => throw new NotSupportedException($"PDF object type '{parsedObject.GetType().Name}' is not supported by the Canvas.Pdf sample bridge.")
        };
    }

    public object MapGraphicsElement(PdfGraphicsElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return element switch
        {
            PdfTextElement text => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Kind"] = nameof(PdfTextElement),
                ["Text"] = text.Text,
                ["X"] = text.Transform.E,
                ["Y"] = text.Transform.F,
                ["FontSize"] = text.FontSize,
                ["FillColor"] = DescribeColor(text.FillColor)
            },
            PdfPathElement path => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Kind"] = nameof(PdfPathElement),
                ["Operator"] = path.SourceCommand.Operator.Name,
                ["LineWidth"] = path.LineWidth,
                ["StrokeColor"] = DescribeColor(path.StrokeColor),
                ["FillColor"] = DescribeColor(path.FillColor),
                ["Segments"] = path.Segments.Select(DescribePathSegment).ToArray()
            },
            PdfImageElement image => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Kind"] = nameof(PdfImageElement),
                ["ResourceName"] = image.ResourceName,
                ["Width"] = ResolveImageWidth(image),
                ["Height"] = ResolveImageHeight(image),
                ["ByteLength"] = image.ImageBytes.Length
            },
            PdfShadingElement shading => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Kind"] = nameof(PdfShadingElement),
                ["ResourceName"] = shading.ResourceName
            },
            PdfGroupElement group => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Kind"] = nameof(PdfGroupElement),
                ["Compatibility"] = group.IsCompatibilitySection,
                ["MarkedContentTag"] = group.MarkedContentTag,
                ["Children"] = group.Children.Select(MapGraphicsElement).ToArray()
            },
            _ => throw new NotSupportedException($"Graphics element type '{element.GetType().Name}' is not supported by the Canvas.Pdf sample bridge.")
        };
    }

    public object MapResources(PdfDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return MapObject(resources);
    }

    private CanvasPdfDocument CreateDocument(PdfDocumentModel source)
    {
        var document = new CanvasPdfDocument();
        ApplyMetadata(document, source.Metadata);

        foreach (var pageModel in source.Pages)
        {
            var mediaBox = pageModel.MediaBox ?? new ImporterPdfRectangle(0, 0, PdfPageSizes.A4Width, PdfPageSizes.A4Height);
            var page = document.AddPage(mediaBox.Width, mediaBox.Height);
            page.RotationDegrees = pageModel.Rotate;
            ApplyPageBoundary(page, PdfPageBoundary.CropBox, pageModel.CropBox);
            ApplyPageBoundary(page, PdfPageBoundary.BleedBox, pageModel.BleedBox);
            ApplyPageBoundary(page, PdfPageBoundary.TrimBox, pageModel.TrimBox);
            ApplyPageBoundary(page, PdfPageBoundary.ArtBox, pageModel.ArtBox);

            foreach (var element in pageModel.GraphicsObjects.Where(static element => !element.IsDeleted).OrderBy(static element => element.ZOrder))
            {
                RenderElement(page, pageModel, source.ObjectGraph, element);
            }
        }

        return document;
    }

    private static void RenderElement(CanvasPdfPage page, PdfPageModel sourcePage, PdfObjectGraph graph, PdfGraphicsElement element)
    {
        switch (element)
        {
            case PdfTextElement text:
                RenderText(page, text);
                return;
            case PdfPathElement path:
                RenderPath(page, path);
                return;
            case PdfImageElement image:
                RenderImage(page, sourcePage, graph, image);
                return;
            case PdfGroupElement group:
                foreach (var child in group.Children.Where(static child => !child.IsDeleted).OrderBy(static child => child.ZOrder))
                {
                    RenderElement(page, sourcePage, graph, child);
                }

                return;
            case PdfShadingElement shading:
                throw new NotSupportedException($"Shading resource '{shading.ResourceName}' cannot be regenerated by the Canvas.Pdf sample bridge.");
            default:
                throw new NotSupportedException($"Graphics element type '{element.GetType().Name}' is not supported by the Canvas.Pdf sample bridge.");
        }
    }

    private static void RenderText(CanvasPdfPage page, PdfTextElement text)
    {
        page.DrawText(text.Text, text.Transform.E, text.Transform.F, new PdfDrawTextOptions
        {
            FontSize = text.FontSize > 0 ? text.FontSize : 12,
            FillColor = MapColor(text.FillColor),
            RotationDegrees = ResolveRotationDegrees(text.Transform)
        });
    }

    private static void RenderPath(CanvasPdfPage page, PdfPathElement path)
    {
        var operatorName = path.SourceCommand.Operator.Name;
        var fill = IsFillOperator(operatorName);
        var stroke = IsStrokeOperator(operatorName);

        var strokeStyle = new PdfStrokeStyle { LineWidth = path.LineWidth > 0 ? path.LineWidth : 1 };
        var translatedSegments = TranslateSegments(path.Segments, path.Transform);
        var strokeColor = MapColor(path.StrokeColor);
        var fillColor = MapColor(path.FillColor);

        if (translatedSegments.Count == 1 && translatedSegments[0] is RectangleSegment rectangle)
        {
            if (fill && !stroke)
            {
                AddInternalPageElement(
                    page,
                    "Canvas.Pdf.Layout.RectangleElement",
                    rectangle.Rectangle.X,
                    rectangle.Rectangle.Y,
                    rectangle.Rectangle.Width,
                    rectangle.Rectangle.Height,
                    strokeStyle,
                    false,
                    true,
                    strokeColor,
                    fillColor);
            }
            else
            {
                page.DrawRectangle(rectangle.Rectangle.X, rectangle.Rectangle.Y, rectangle.Rectangle.Width, rectangle.Rectangle.Height, strokeStyle.LineWidth, fill, strokeColor, fillColor, strokeStyle);
            }

            return;
        }

        if (TryRenderLine(page, translatedSegments, strokeStyle, strokeColor))
        {
            return;
        }

        if (TryRenderBezier(page, translatedSegments, strokeStyle, strokeColor))
        {
            return;
        }

        if (TryRenderPolygon(page, translatedSegments, fill, stroke, strokeStyle, strokeColor, fillColor))
        {
            return;
        }

        throw new NotSupportedException($"Path element with operator '{operatorName}' is not supported by the Canvas.Pdf sample bridge.");
    }

    private static void RenderImage(CanvasPdfPage page, PdfPageModel sourcePage, PdfObjectGraph graph, PdfImageElement image)
    {
        if (!string.IsNullOrEmpty(image.ResourceName) &&
            TryResolveImageXObject(sourcePage.Resources, graph, image.ResourceName, out var imageStream) &&
            TryCreateCanvasImageData(imageStream, out var imageData))
        {
            AddInternalPageElement(
                page,
                "Canvas.Pdf.Layout.ImageElement",
                imageData,
                image.ResourceName,
                image.Transform.E,
                image.Transform.F,
                ResolveImageWidth(image),
                ResolveImageHeight(image),
                1d,
                false,
                null,
                null,
                null,
                null);
            return;
        }

        if (image.ImageBytes.IsEmpty)
        {
            throw new NotSupportedException($"Image resource '{image.ResourceName}' has no decoded bytes for regeneration.");
        }

        var imagePath = Path.Combine(Path.GetTempPath(), $"canvas-importer-{Guid.NewGuid():N}.img");
        try
        {
            File.WriteAllBytes(imagePath, image.ImageBytes.ToArray());
            page.DrawImage(imagePath, image.Transform.E, image.Transform.F, ResolveImageWidth(image), ResolveImageHeight(image));
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                File.Delete(imagePath);
            }
        }
    }

    private static bool TryRenderLine(CanvasPdfPage page, IReadOnlyList<PdfPathSegment> segments, PdfStrokeStyle strokeStyle, IPdfColor strokeColor)
    {
        if (segments.Count == 2 &&
            segments[0] is MoveToSegment move &&
            segments[1] is LineToSegment line)
        {
            page.DrawLine(move.Point.X, move.Point.Y, line.Point.X, line.Point.Y, strokeStyle.LineWidth, strokeColor, strokeStyle);
            return true;
        }

        return false;
    }

    private static bool TryRenderBezier(CanvasPdfPage page, IReadOnlyList<PdfPathSegment> segments, PdfStrokeStyle strokeStyle, IPdfColor strokeColor)
    {
        if (segments.Count == 2 &&
            segments[0] is MoveToSegment move &&
            segments[1] is CurveToSegment curve)
        {
            page.DrawBezierCurve(ToCanvasPoint(move.Point), ToCanvasPoint(curve.Control1), ToCanvasPoint(curve.Control2), ToCanvasPoint(curve.End), strokeStyle.LineWidth, strokeColor, strokeStyle);
            return true;
        }

        return false;
    }

    private static bool TryRenderPolygon(CanvasPdfPage page, IReadOnlyList<PdfPathSegment> segments, bool fill, bool stroke, PdfStrokeStyle strokeStyle, IPdfColor strokeColor, IPdfColor fillColor)
    {
        if (segments.Count < 2 || segments[0] is not MoveToSegment start)
        {
            return false;
        }

        var points = new List<CanvasPdfPoint> { ToCanvasPoint(start.Point) };
        var lastSegmentIndex = segments.Count - 1;
        var hasExplicitClose = segments[^1] is ClosePathSegment;
        var requiresExplicitClose = stroke || !fill;
        if (requiresExplicitClose && !hasExplicitClose)
        {
            return false;
        }

        var terminalIndex = hasExplicitClose ? lastSegmentIndex : segments.Count;
        for (var index = 1; index < terminalIndex; index++)
        {
            if (segments[index] is not LineToSegment line)
            {
                return false;
            }

            points.Add(ToCanvasPoint(line.Point));
        }

        if (points.Count < 3)
        {
            return false;
        }

        if (fill && !stroke)
        {
            AddInternalPageElement(
                page,
                "Canvas.Pdf.Layout.PolygonElement",
                points,
                strokeStyle,
                false,
                true,
                strokeColor,
                fillColor);
        }
        else
        {
            page.DrawPolygon(points, strokeStyle.LineWidth, fill, strokeColor, fillColor, strokeStyle);
        }

        return true;
    }

    private static void AddInternalPageElement(CanvasPdfPage page, string typeName, params object?[] arguments)
    {
        var elementType = typeof(CanvasPdfPage).Assembly.GetType(typeName, throwOnError: true)
            ?? throw new InvalidOperationException($"Canvas.Pdf internal type '{typeName}' was not found.");
        var element = Activator.CreateInstance(
            elementType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: arguments,
            culture: CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Canvas.Pdf internal type '{typeName}' could not be constructed.");

        if (PageElementsField.GetValue(page) is not System.Collections.IList elements)
        {
            throw new InvalidOperationException("Canvas.Pdf.PdfPage._elements is not accessible as a mutable list.");
        }

        elements.Add(element);
    }

    private static bool TryResolveImageXObject(PdfDictionary resources, PdfObjectGraph graph, string resourceName, out PdfStreamObject stream)
    {
        stream = null!;
        if (ResolveObject(resources["XObject"], graph) is not PdfDictionary xObjects)
        {
            return false;
        }

        if (!xObjects.Values.TryGetValue(resourceName, out var resource) || ResolveObject(resource, graph) is not PdfStreamObject candidate)
        {
            return false;
        }

        if (ResolveObject(candidate.Dictionary["Subtype"], graph) is not PdfName { Value: "Image" })
        {
            return false;
        }

        stream = candidate;
        return true;
    }

    private static bool TryCreateCanvasImageData(PdfStreamObject stream, out object imageData)
    {
        imageData = null!;

        if (ResolveNumber(stream.Dictionary["Width"]) is not { } width ||
            ResolveNumber(stream.Dictionary["Height"]) is not { } height ||
            ResolveNumber(stream.Dictionary["BitsPerComponent"]) is not { } bitsPerComponent ||
            ResolveColorSpaceName(stream.Dictionary["ColorSpace"]) is not { } colorSpaceName ||
            ResolveSingleSupportedFilterName(stream.Dictionary["Filter"]) is not { } filterName)
        {
            return false;
        }

        var data = stream.EncodedBytes;
        var decodeParameters = ResolveDecodeParameters(stream.Dictionary["DecodeParms"]);

        var instance = Activator.CreateInstance(CanvasPdfImageDataType)
            ?? throw new InvalidOperationException("Canvas.Pdf.PdfImageData could not be constructed.");

        CanvasPdfImageDataType.GetProperty("Width")!.SetValue(instance, (int)width);
        CanvasPdfImageDataType.GetProperty("Height")!.SetValue(instance, (int)height);
        CanvasPdfImageDataType.GetProperty("BitsPerComponent")!.SetValue(instance, (int)bitsPerComponent);
        CanvasPdfImageDataType.GetProperty("ColorSpaceName")!.SetValue(instance, colorSpaceName);
        CanvasPdfImageDataType.GetProperty("FilterName")!.SetValue(instance, filterName);
        CanvasPdfImageDataType.GetProperty("DecodeParameters")!.SetValue(instance, decodeParameters);
        CanvasPdfImageDataType.GetProperty("Data")!.SetValue(instance, data.ToArray());
        CanvasPdfImageDataType.GetProperty("SoftMask")!.SetValue(instance, null);
        imageData = instance;
        return true;
    }

    private static string? ResolveColorSpaceName(PdfObject? colorSpace)
    {
        return colorSpace switch
        {
            PdfName name when name.Value is "DeviceGray" or "DeviceRGB" or "DeviceCMYK" => name.Value,
            PdfArray { Items.Count: > 0 } array when array.Items[0] is PdfName { Value: "ICCBased" } => "DeviceRGB",
            _ => null
        };
    }

    private static string? ResolveSingleSupportedFilterName(PdfObject? filter)
    {
        return filter switch
        {
            PdfName name when name.Value is "DCTDecode" or "FlateDecode" => name.Value,
            _ => null
        };
    }

    private static string? ResolveDecodeParameters(PdfObject? decodeParms)
    {
        if (decodeParms is not PdfDictionary dictionary)
        {
            return null;
        }

        return string.Join(' ', dictionary.Values.Select(static entry => $"/{entry.Key} {FormatPdfObject(entry.Value)}"));
    }

    private static string FormatPdfObject(PdfObject value)
    {
        return value switch
        {
            PdfInteger integer => integer.Value.ToString(CultureInfo.InvariantCulture),
            PdfNumber number => number.Value.ToString("0.###", CultureInfo.InvariantCulture),
            PdfName name => $"/{name.Value}",
            PdfBoolean boolean => boolean.Value ? "true" : "false",
            PdfArray array => $"[{string.Join(' ', array.Items.Select(FormatPdfObject))}]",
            _ => "null"
        };
    }

    private static double? ResolveNumber(PdfObject? value)
    {
        return value switch
        {
            PdfInteger integer => integer.Value,
            PdfNumber number => number.Value,
            _ => null
        };
    }

    private static PdfObject? ResolveObject(PdfObject? value, PdfObjectGraph graph)
    {
        return value switch
        {
            PdfReference reference => graph.Resolve(reference.Id)?.Value,
            _ => value
        };
    }

    private static List<PdfPathSegment> TranslateSegments(IReadOnlyList<PdfPathSegment> segments, ImporterPdfMatrix transform)
    {
        return segments.Select(segment => segment switch
        {
            MoveToSegment move => (PdfPathSegment)new MoveToSegment(ApplyTransform(move.Point, transform)),
            LineToSegment line => new LineToSegment(ApplyTransform(line.Point, transform)),
            CurveToSegment curve => new CurveToSegment(ApplyTransform(curve.Control1, transform), ApplyTransform(curve.Control2, transform), ApplyTransform(curve.End, transform)),
            RectangleSegment rectangle => new RectangleSegment(new ImporterPdfRectangle(rectangle.Rectangle.X + transform.E, rectangle.Rectangle.Y + transform.F, rectangle.Rectangle.Width, rectangle.Rectangle.Height)),
            ClosePathSegment close => close,
            _ => throw new NotSupportedException($"Path segment type '{segment.GetType().Name}' is not supported by the Canvas.Pdf sample bridge.")
        }).ToList();
    }

    private static ImporterPdfPoint ApplyTransform(ImporterPdfPoint point, ImporterPdfMatrix transform)
    {
        return new ImporterPdfPoint(
            point.X * transform.A + point.Y * transform.C + transform.E,
            point.X * transform.B + point.Y * transform.D + transform.F);
    }

    private static CanvasPdfPoint ToCanvasPoint(ImporterPdfPoint point) => new(point.X, point.Y);

    private static Dictionary<string, object?> DescribeColor(ImporterPdfColor color)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ColorSpace"] = color.ColorSpace.ToString(),
            ["C1"] = color.C1,
            ["C2"] = color.C2,
            ["C3"] = color.C3,
            ["C4"] = color.C4
        };
    }

    private static Dictionary<string, object?> DescribePathSegment(PdfPathSegment segment)
    {
        return segment switch
        {
            MoveToSegment move => new Dictionary<string, object?> { ["Kind"] = nameof(MoveToSegment), ["X"] = move.Point.X, ["Y"] = move.Point.Y },
            LineToSegment line => new Dictionary<string, object?> { ["Kind"] = nameof(LineToSegment), ["X"] = line.Point.X, ["Y"] = line.Point.Y },
            CurveToSegment curve => new Dictionary<string, object?>
            {
                ["Kind"] = nameof(CurveToSegment),
                ["Control1X"] = curve.Control1.X,
                ["Control1Y"] = curve.Control1.Y,
                ["Control2X"] = curve.Control2.X,
                ["Control2Y"] = curve.Control2.Y,
                ["EndX"] = curve.End.X,
                ["EndY"] = curve.End.Y
            },
            RectangleSegment rectangle => new Dictionary<string, object?>
            {
                ["Kind"] = nameof(RectangleSegment),
                ["X"] = rectangle.Rectangle.X,
                ["Y"] = rectangle.Rectangle.Y,
                ["Width"] = rectangle.Rectangle.Width,
                ["Height"] = rectangle.Rectangle.Height
            },
            ClosePathSegment => new Dictionary<string, object?> { ["Kind"] = nameof(ClosePathSegment) },
            _ => throw new NotSupportedException($"Path segment type '{segment.GetType().Name}' is not supported by the Canvas.Pdf sample bridge.")
        };
    }

    private static IPdfColor MapColor(ImporterPdfColor color)
    {
        return color.ColorSpace switch
        {
            PdfColorSpace.DeviceGray => new PdfGrayColor(color.C1),
            PdfColorSpace.DeviceRgb => new CanvasPdfColor(color.C1, color.C2, color.C3),
            PdfColorSpace.DeviceCmyk => new PdfCmykColor(color.C1, color.C2, color.C3, color.C4),
            _ => throw new NotSupportedException($"Color space '{color.ColorSpace}' is not supported by the Canvas.Pdf sample bridge.")
        };
    }

    private static double ResolveRotationDegrees(ImporterPdfMatrix transform)
    {
        if (Math.Abs(transform.A - 1) < 0.0001 && Math.Abs(transform.B) < 0.0001 && Math.Abs(transform.C) < 0.0001 && Math.Abs(transform.D - 1) < 0.0001)
        {
            return 0;
        }

        return Math.Atan2(transform.B, transform.A) * 180d / Math.PI;
    }

    private static bool IsStrokeOperator(string operatorName)
        => operatorName is "S" or "s" or "B" or "B*" or "b" or "b*";

    private static bool IsFillOperator(string operatorName)
        => operatorName is "f" or "F" or "f*" or "B" or "B*" or "b" or "b*";

    private static double ResolveImageWidth(PdfImageElement image)
    {
        if (image.Bounds is { Width: > 0 } bounds)
        {
            return bounds.Width;
        }

        return Math.Abs(image.Transform.A) > 0.0001 ? Math.Abs(image.Transform.A) : 1;
    }

    private static double ResolveImageHeight(PdfImageElement image)
    {
        if (image.Bounds is { Height: > 0 } bounds)
        {
            return bounds.Height;
        }

        return Math.Abs(image.Transform.D) > 0.0001 ? Math.Abs(image.Transform.D) : 1;
    }

    private static void ApplyPageBoundary(CanvasPdfPage page, PdfPageBoundary boundary, ImporterPdfRectangle? rectangle)
    {
        if (rectangle is null || rectangle.Value.Width <= 0 || rectangle.Value.Height <= 0)
        {
            return;
        }

        page.SetPageBoundary(
            boundary,
            new CanvasPdfPoint(rectangle.Value.X, rectangle.Value.Y),
            new CanvasPdfPoint(rectangle.Value.X + rectangle.Value.Width, rectangle.Value.Y + rectangle.Value.Height));
    }

    private static void ApplyMetadata(CanvasPdfDocument document, PdfDictionary metadata)
    {
        foreach (var entry in metadata.Values)
        {
            if (entry.Value is not PdfString value)
            {
                continue;
            }

            var text = value.ToLatin1String();
            switch (entry.Key)
            {
                case "Title":
                    document.Info.Title = text;
                    break;
                case "Author":
                    document.Info.Author = text;
                    break;
                case "Subject":
                    document.Info.Subject = text;
                    break;
                case "Keywords":
                    document.Info.Keywords = text;
                    break;
                case "Creator":
                    document.Info.Creator = text;
                    break;
                case "Producer":
                    document.Info.Producer = text;
                    break;
                case "CreationDate" when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var creationDate):
                    document.Info.CreationDate = creationDate;
                    break;
                case "ModDate" when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var modificationDate):
                    document.Info.ModificationDate = modificationDate;
                    break;
            }
        }
    }
}