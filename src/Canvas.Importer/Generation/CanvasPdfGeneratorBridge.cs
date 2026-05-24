using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Canvas.Importer.Content;
using Canvas.Importer.Document;
using Canvas.Importer.Graphics;
using Canvas.Importer.Objects;
using Canvas.Importer.Parsing;
using Canvas.Importer.Streams;
using Canvas.Importer.Tokenizer;
using Canvas.Importer.Xref;
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

        var shadingPlans = document.Pages
            .Select(static page => page.GraphicsObjects
                .Where(static element => !element.IsDeleted)
                .Select(ExtractShadingPlan)
                .OfType<PdfGraphicsElement>()
                .ToList())
            .ToList();

        var bytes = _renderer.Render(CreateDocument(document));
        if (shadingPlans.Any(static plan => plan.Count > 0))
        {
            bytes = PreserveShadings(bytes, document, shadingPlans);
        }

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
                return;
            default:
                throw new NotSupportedException($"Graphics element type '{element.GetType().Name}' is not supported by the Canvas.Pdf sample bridge.");
        }
    }

    private byte[] PreserveShadings(byte[] basePdfBytes, PdfDocumentModel sourceDocument, IReadOnlyList<List<PdfGraphicsElement>> shadingPlans)
    {
        var context = new PdfParseContext(basePdfBytes, new PdfImporterOptions());
        var xref = new PdfCrossReferenceParser().Parse(context);
        var generatedGraph = new PdfObjectParser(context, xref).ParseDocumentGraph();
        var generatedDocument = new PdfDocumentBuilder(new PdfContentStreamParser(), new PdfGraphicsInterpreter()).Build(generatedGraph);
        var rewriter = new PdfContentStreamRewriter();

        var maxObjectNumber = generatedGraph.Objects.Keys.Max(static id => id.Number);
        var nextObjectNumber = maxObjectNumber + 1;
        var appendedObjects = new List<(PdfObjectId Id, PdfObject Value)>();

        for (var pageIndex = 0; pageIndex < shadingPlans.Count; pageIndex++)
        {
            var shadingElements = shadingPlans[pageIndex];
            if (shadingElements.Count == 0)
            {
                continue;
            }

            var sourcePage = sourceDocument.Pages[pageIndex];
            var generatedPage = generatedDocument.Pages[pageIndex];
            var pageObjectId = generatedPage.OriginalReference
                ?? throw new InvalidOperationException("Generated page does not expose an original reference for shading preservation.");
            var generatedPageObject = generatedGraph.Resolve(pageObjectId)
                ?? throw new InvalidOperationException($"Generated page object {pageObjectId.Number} {pageObjectId.Generation} was not found.");
            var generatedPageDictionary = (PdfDictionary)generatedPageObject.Value;

            var shadingResources = CloneShadingResourceBundle(sourcePage.Resources, sourceDocument.ObjectGraph, ref nextObjectNumber, appendedObjects);
            if (shadingResources.Count == 0)
            {
                throw new NotSupportedException("Shading elements require a source page /Shading resource dictionary for compatibility regeneration.");
            }

            var shadingContentBytes = rewriter.Rewrite(shadingElements).ToArray();
            var shadingStreamId = new PdfObjectId(nextObjectNumber++, 0);
            appendedObjects.Add((shadingStreamId, new PdfStreamObject(new PdfDictionary(new Dictionary<string, PdfObject>
            {
                ["Length"] = new PdfInteger(shadingContentBytes.Length)
            }), shadingContentBytes)));

            var updatedPageDictionary = CloneDictionary(generatedPageDictionary);
            updatedPageDictionary["Resources"] = MergePageResources(generatedPageDictionary["Resources"], shadingResources);
            updatedPageDictionary["Contents"] = AppendContentReference(generatedPageDictionary["Contents"], shadingStreamId);
            appendedObjects.Add((pageObjectId, updatedPageDictionary));
        }

        if (appendedObjects.Count == 0)
        {
            return basePdfBytes;
        }

        return AppendIncrementalUpdate(basePdfBytes, generatedGraph.Trailer, appendedObjects);
    }

    private static bool ContainsShading(PdfGraphicsElement element)
    {
        if (element.IsDeleted)
        {
            return false;
        }

        return element switch
        {
            PdfShadingElement => true,
            PdfGroupElement group => group.Children.Any(ContainsShading),
            _ => false
        };
    }

    private static PdfGraphicsElement? ExtractShadingPlan(PdfGraphicsElement element)
    {
        if (element.IsDeleted)
        {
            return null;
        }

        return element switch
        {
            PdfShadingElement shading => shading,
            PdfGroupElement group => ExtractShadingGroup(group),
            _ => null
        };
    }

    private static PdfGroupElement? ExtractShadingGroup(PdfGroupElement group)
    {
        if (!ContainsShading(group))
        {
            return null;
        }

        var shadingChildren = new List<PdfGraphicsElement>();
        foreach (var child in group.Children)
        {
            if (ExtractShadingPlan(child) is { } shadingChild)
            {
                shadingChildren.Add(shadingChild);
            }
        }

        if (shadingChildren.Count == 0)
        {
            return null;
        }

        var shadingGroup = new PdfGroupElement(group.ZOrder, group.Transform, group.SourceCommand)
        {
            Bounds = group.Bounds,
            ClippingPath = group.ClippingPath,
            IsCompatibilitySection = group.IsCompatibilitySection,
            IsDeleted = group.IsDeleted,
            MarkedContentTag = group.MarkedContentTag,
            Properties = group.Properties
        };

        shadingGroup.Children.AddRange(shadingChildren);
        return shadingGroup;
    }

    private static Dictionary<string, PdfObject> CloneShadingResourceBundle(PdfDictionary resources, PdfObjectGraph sourceGraph, ref int nextObjectNumber, List<(PdfObjectId Id, PdfObject Value)> appendedObjects)
    {
        var clonedResources = new Dictionary<string, PdfObject>(StringComparer.Ordinal);

        if (resources["Shading"] is not { } shadingResources)
        {
            return clonedResources;
        }

        var clonedReferences = new Dictionary<PdfObjectId, PdfObjectId>();
        clonedResources["Shading"] = CloneForIncrementalUpdate(shadingResources, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences);

        if (resources["ColorSpace"] is { } colorSpaceResources)
        {
            clonedResources["ColorSpace"] = CloneForIncrementalUpdate(colorSpaceResources, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences);
        }

        return clonedResources;
    }

    private static PdfObject CloneForIncrementalUpdate(PdfObject value, PdfObjectGraph sourceGraph, ref int nextObjectNumber, List<(PdfObjectId Id, PdfObject Value)> appendedObjects, Dictionary<PdfObjectId, PdfObjectId> clonedReferences)
    {
        return value switch
        {
            PdfReference reference => CloneReferencedObject(reference, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences),
            PdfDictionary dictionary => CloneDictionary(dictionary, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences),
            PdfArray array => CloneArray(array, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences),
            PdfStreamObject stream => CloneStream(stream, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences),
            PdfName name => new PdfName(name.Value),
            PdfString text => new PdfString(text.Bytes, text.IsHex),
            PdfInteger integer => new PdfInteger(integer.Value),
            PdfNumber number => new PdfNumber(number.Value),
            PdfBoolean boolean => new PdfBoolean(boolean.Value),
            PdfNull => PdfNull.Value,
            _ => value
        };
    }

    private static PdfReference CloneReferencedObject(PdfReference reference, PdfObjectGraph sourceGraph, ref int nextObjectNumber, List<(PdfObjectId Id, PdfObject Value)> appendedObjects, Dictionary<PdfObjectId, PdfObjectId> clonedReferences)
    {
        if (clonedReferences.TryGetValue(reference.Id, out var clonedId))
        {
            return new PdfReference(clonedId);
        }

        var sourceObject = sourceGraph.Resolve(reference.Id)
            ?? throw new InvalidOperationException($"Referenced shading object {reference.Id.Number} {reference.Id.Generation} was not found.");
        var newId = new PdfObjectId(nextObjectNumber++, 0);
        clonedReferences[reference.Id] = newId;
        var clonedValue = CloneForIncrementalUpdate(sourceObject.Value, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences);
        appendedObjects.Add((newId, clonedValue));
        return new PdfReference(newId);
    }

    private static PdfArray CloneArray(PdfArray array, PdfObjectGraph sourceGraph, ref int nextObjectNumber, List<(PdfObjectId Id, PdfObject Value)> appendedObjects, Dictionary<PdfObjectId, PdfObjectId> clonedReferences)
    {
        var items = new List<PdfObject>(array.Items.Count);
        foreach (var item in array.Items)
        {
            items.Add(CloneForIncrementalUpdate(item, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences));
        }

        return new PdfArray(items);
    }

    private static PdfDictionary CloneDictionary(PdfDictionary dictionary)
    {
        return new PdfDictionary(new Dictionary<string, PdfObject>(dictionary.Values, StringComparer.Ordinal));
    }

    private static PdfDictionary CloneDictionary(PdfDictionary dictionary, PdfObjectGraph sourceGraph, ref int nextObjectNumber, List<(PdfObjectId Id, PdfObject Value)> appendedObjects, Dictionary<PdfObjectId, PdfObjectId> clonedReferences)
    {
        var values = new Dictionary<string, PdfObject>(StringComparer.Ordinal);
        foreach (var entry in dictionary.Values)
        {
            values[entry.Key] = CloneForIncrementalUpdate(entry.Value, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences);
        }

        return new PdfDictionary(values);
    }

    private static PdfStreamObject CloneStream(PdfStreamObject stream, PdfObjectGraph sourceGraph, ref int nextObjectNumber, List<(PdfObjectId Id, PdfObject Value)> appendedObjects, Dictionary<PdfObjectId, PdfObjectId> clonedReferences)
    {
        var cloned = new PdfStreamObject(CloneDictionary(stream.Dictionary, sourceGraph, ref nextObjectNumber, appendedObjects, clonedReferences), stream.EncodedBytes);
        if (stream.IsDecoded)
        {
            cloned.SetDecodedBytes(stream.DecodedBytes);
        }

        return cloned;
    }

    private static PdfObject MergePageResources(PdfObject? existingResources, IReadOnlyDictionary<string, PdfObject> shadingResources)
    {
        var resourceDictionary = existingResources switch
        {
            PdfDictionary dictionary => CloneDictionary(dictionary),
            _ => new PdfDictionary()
        };

        foreach (var resourceEntry in shadingResources)
        {
            resourceDictionary[resourceEntry.Key] = resourceEntry.Value;
        }

        return resourceDictionary;
    }

    private static PdfObject AppendContentReference(PdfObject? existingContents, PdfObjectId shadingStreamId)
    {
        var shadingReference = new PdfReference(shadingStreamId);
        return existingContents switch
        {
            PdfArray array => new PdfArray(array.Items.Concat([shadingReference])),
            PdfObject contents => new PdfArray([contents, shadingReference]),
            null => shadingReference
        };
    }

    private static byte[] AppendIncrementalUpdate(byte[] basePdfBytes, PdfDictionary? trailer, IReadOnlyList<(PdfObjectId Id, PdfObject Value)> appendedObjects)
    {
        if (trailer is null || trailer["Root"] is null)
        {
            throw new InvalidOperationException("Generated PDF trailer is missing /Root for incremental update.");
        }

        var existingStartXref = FindStartXrefOffset(basePdfBytes);
        using var stream = new MemoryStream();
        stream.Write(basePdfBytes, 0, basePdfBytes.Length);
        if (basePdfBytes.Length > 0 && basePdfBytes[^1] != (byte)'\n')
        {
            WriteAscii(stream, "\n");
        }

        var offsets = new SortedDictionary<PdfObjectId, long>(Comparer<PdfObjectId>.Create(static (left, right) => left.Number != right.Number ? left.Number.CompareTo(right.Number) : left.Generation.CompareTo(right.Generation)));
        foreach (var appendedObject in appendedObjects.OrderBy(static entry => entry.Id.Number).ThenBy(static entry => entry.Id.Generation))
        {
            offsets[appendedObject.Id] = stream.Position;
            WriteIndirectObject(stream, appendedObject.Id, appendedObject.Value);
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, "xref\n");
        foreach (var subsection in GroupSubsections(offsets))
        {
            WriteAscii(stream, $"{subsection.Start} {subsection.Entries.Count}\n");
            foreach (var entry in subsection.Entries)
            {
                WriteAscii(stream, $"{entry.Offset:0000000000} {entry.Generation:00000} n \n");
            }
        }

        var size = Math.Max((trailer["Size"] as PdfInteger)?.Value ?? 0, offsets.Keys.Max(static id => (long)id.Number) + 1);
        var trailerDictionary = new PdfDictionary();
        trailerDictionary["Size"] = new PdfInteger(size);
        trailerDictionary["Root"] = trailer["Root"]!;
        if (trailer["Info"] is { } info)
        {
            trailerDictionary["Info"] = info;
        }

        if (trailer["ID"] is { } id)
        {
            trailerDictionary["ID"] = id;
        }

        trailerDictionary["Prev"] = new PdfInteger(existingStartXref);

        WriteAscii(stream, "trailer\n");
        WriteObject(stream, trailerDictionary);
        WriteAscii(stream, "\nstartxref\n");
        WriteAscii(stream, xrefOffset.ToString(CultureInfo.InvariantCulture));
        WriteAscii(stream, "\n%%EOF\n");
        return stream.ToArray();
    }

    private static long FindStartXrefOffset(ReadOnlySpan<byte> bytes)
    {
        var marker = Encoding.ASCII.GetBytes("startxref");
        for (var index = bytes.Length - marker.Length; index >= 0; index--)
        {
            if (!bytes.Slice(index, marker.Length).SequenceEqual(marker))
            {
                continue;
            }

            var tokenizer = new PdfTokenizer(bytes[(index + marker.Length)..]);
            var token = tokenizer.ReadToken();
            if (long.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        throw new InvalidOperationException("Unable to find startxref in generated PDF bytes.");
    }

    private static IReadOnlyList<(int Start, List<(long Offset, int Generation)> Entries)> GroupSubsections(SortedDictionary<PdfObjectId, long> offsets)
    {
        var subsections = new List<(int Start, List<(long Offset, int Generation)> Entries)>();
        List<(long Offset, int Generation)>? currentEntries = null;
        var currentStart = -1;
        var expectedNumber = -1;

        foreach (var entry in offsets)
        {
            if (currentEntries is null || entry.Key.Number != expectedNumber)
            {
                currentEntries = [];
                currentStart = entry.Key.Number;
                subsections.Add((currentStart, currentEntries));
            }

            currentEntries.Add((entry.Value, entry.Key.Generation));
            expectedNumber = entry.Key.Number + 1;
        }

        return subsections;
    }

    private static void WriteIndirectObject(Stream stream, PdfObjectId id, PdfObject value)
    {
        WriteAscii(stream, $"{id.Number} {id.Generation} obj\n");
        WriteObject(stream, value);
        WriteAscii(stream, "\nendobj\n");
    }

    private static void WriteObject(Stream stream, PdfObject value)
    {
        switch (value)
        {
            case PdfName name:
                WriteAscii(stream, "/");
                WriteAscii(stream, name.Value);
                break;
            case PdfInteger integer:
                WriteAscii(stream, integer.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case PdfNumber number:
                WriteAscii(stream, number.Value.ToString("0.###", CultureInfo.InvariantCulture));
                break;
            case PdfString text:
                WriteAscii(stream, "<");
                foreach (var current in text.GetDecodedBytes().Span)
                {
                    WriteAscii(stream, current.ToString("X2", CultureInfo.InvariantCulture));
                }

                WriteAscii(stream, ">");
                break;
            case PdfBoolean boolean:
                WriteAscii(stream, boolean.Value ? "true" : "false");
                break;
            case PdfNull:
                WriteAscii(stream, "null");
                break;
            case PdfReference reference:
                WriteAscii(stream, $"{reference.Id.Number} {reference.Id.Generation} R");
                break;
            case PdfArray array:
                WriteAscii(stream, "[");
                for (var index = 0; index < array.Items.Count; index++)
                {
                    if (index > 0)
                    {
                        WriteAscii(stream, " ");
                    }

                    WriteObject(stream, array.Items[index]);
                }

                WriteAscii(stream, "]");
                break;
            case PdfDictionary dictionary:
                WriteAscii(stream, "<<");
                foreach (var entry in dictionary.Values)
                {
                    WriteAscii(stream, " ");
                    WriteAscii(stream, "/");
                    WriteAscii(stream, entry.Key);
                    WriteAscii(stream, " ");
                    WriteObject(stream, entry.Value);
                }

                WriteAscii(stream, " >>");
                break;
            case PdfStreamObject streamObject:
                var streamDictionary = CloneDictionary(streamObject.Dictionary);
                streamDictionary["Length"] = new PdfInteger(streamObject.EncodedBytes.Length);
                WriteObject(stream, streamDictionary);
                WriteAscii(stream, "\nstream\n");
                stream.Write(streamObject.EncodedBytes.Span);
                WriteAscii(stream, "\nendstream");
                break;
            default:
                throw new NotSupportedException($"PDF object type '{value.GetType().Name}' cannot be serialized for shading preservation.");
        }
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void RenderText(CanvasPdfPage page, PdfTextElement text)
    {
        page.DrawText(text.Text, text.Transform.E, text.Transform.F, new PdfDrawTextOptions
        {
            FontSize = text.FontSize > 0 ? text.FontSize : 12,
            FillColor = MapColor(text.FillColor),
            RotationDegrees = ResolveRotationDegrees(text.Transform),
            FontFamily = ResolveStandardFontFamily(text.FontName),
            Bold = text.Bold,
            Italic = text.Italic
        });
    }

    private static PdfFontFamily? ResolveStandardFontFamily(string? fontName)
    {
        if (fontName is null) return null;
        var lower = fontName.ToLowerInvariant();
        if (lower.StartsWith("times", StringComparison.Ordinal) || lower.Contains("garamond") || lower.Contains("palatino"))
            return PdfFontFamily.Times;
        if (lower.StartsWith("courier", StringComparison.Ordinal) || lower.Contains("mono") || lower.Contains("typewriter"))
            return PdfFontFamily.Courier;
        if (lower.StartsWith("helvetica", StringComparison.Ordinal) || lower.StartsWith("arial", StringComparison.Ordinal) ||
            lower.StartsWith("symbol", StringComparison.Ordinal) || lower.StartsWith("zapf", StringComparison.Ordinal))
            return PdfFontFamily.Helvetica;
        // Embedded custom fonts: fall back to null (Canvas.Pdf uses its document default)
        return null;
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
            TryCreateCanvasImageData(imageStream, sourcePage.Resources, graph, out var imageData))
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

    private static bool TryCreateCanvasImageData(PdfStreamObject stream, PdfDictionary resources, PdfObjectGraph graph, out object imageData)
    {
        imageData = null!;

        if (ResolveNumber(stream.Dictionary["Width"]) is not { } width ||
            ResolveNumber(stream.Dictionary["Height"]) is not { } height ||
            ResolveNumber(stream.Dictionary["BitsPerComponent"]) is not { } bitsPerComponent ||
            ResolveColorSpaceName(stream.Dictionary["ColorSpace"], resources, graph) is not { } colorSpaceName ||
            ResolveSingleSupportedFilterName(stream.Dictionary["Filter"], graph) is not { } filterName)
        {
            return false;
        }

        var data = stream.EncodedBytes;
        var decodeParameters = ResolveDecodeParameters(stream.Dictionary["DecodeParms"], graph);
        ReadOnlyMemory<byte>? decodedPixels = null;

        if (filterName is "CCITTFaxDecode" or "CCF" or "LZWDecode" or "LZW")
        {
            decodedPixels = new PdfStreamDecoderRegistry().Decode(stream);
        }

        if (TryGetIndexedColorSpaceArray(stream.Dictionary["ColorSpace"], resources, graph, out var indexedArray))
        {
            var rawPixels = decodedPixels ?? new PdfStreamDecoderRegistry().Decode(stream);
            if (!TryExpandIndexedPixels(indexedArray, resources, graph, rawPixels, (int)bitsPerComponent, (int)width, (int)height, out var expanded, out var componentCount))
            {
                return false;
            }
            decodedPixels = expanded;
            bitsPerComponent = 8;
            colorSpaceName = componentCount switch
            {
                1 => "DeviceGray",
                3 => "DeviceRGB",
                4 => "DeviceCMYK",
                _ => colorSpaceName
            };
        }

        if (decodedPixels.HasValue)
        {
            data = RecompressFlate(decodedPixels.Value);
            filterName = "FlateDecode";
            decodeParameters = null;
        }

        object? softMask = null;
        if (ResolveObject(stream.Dictionary["SMask"], graph) is PdfStreamObject softMaskStream)
        {
            if (!TryCreateCanvasImageData(softMaskStream, resources, graph, out softMask))
            {
                return false;
            }
        }

        var instance = Activator.CreateInstance(CanvasPdfImageDataType)
            ?? throw new InvalidOperationException("Canvas.Pdf.PdfImageData could not be constructed.");

        CanvasPdfImageDataType.GetProperty("Width")!.SetValue(instance, (int)width);
        CanvasPdfImageDataType.GetProperty("Height")!.SetValue(instance, (int)height);
        CanvasPdfImageDataType.GetProperty("BitsPerComponent")!.SetValue(instance, (int)bitsPerComponent);
        CanvasPdfImageDataType.GetProperty("ColorSpaceName")!.SetValue(instance, colorSpaceName);
        CanvasPdfImageDataType.GetProperty("FilterName")!.SetValue(instance, filterName);
        CanvasPdfImageDataType.GetProperty("DecodeParameters")!.SetValue(instance, decodeParameters);
        CanvasPdfImageDataType.GetProperty("Data")!.SetValue(instance, data.ToArray());
        CanvasPdfImageDataType.GetProperty("SoftMask")!.SetValue(instance, softMask);
        imageData = instance;
        return true;
    }

    private static string? ResolveColorSpaceName(PdfObject? colorSpace, PdfDictionary resources, PdfObjectGraph graph)
    {
        var resolvedColorSpace = ResolveObject(colorSpace, graph) ?? colorSpace;
        return resolvedColorSpace switch
        {
            PdfName name when name.Value is "DeviceGray" or "DeviceRGB" or "DeviceCMYK" => name.Value,
            PdfName name when ResolveNamedColorSpace(name.Value, resources, graph) is { } namedColorSpace => ResolveColorSpaceName(namedColorSpace, resources, graph),
            PdfArray { Items.Count: > 1 } array when ResolveObject(array.Items[0], graph) is PdfName { Value: "ICCBased" } => ResolveIccBasedColorSpaceName(array.Items[1], graph),
            PdfArray { Items.Count: 4 } array when ResolveObject(array.Items[0], graph) is PdfName { Value: "Indexed" } => ResolveColorSpaceName(array.Items[1], resources, graph),
            _ => null
        };
    }

    private static string? ResolveIccBasedColorSpaceName(PdfObject profileReference, PdfObjectGraph graph)
    {
        return ResolveObject(profileReference, graph) switch
        {
            PdfStreamObject profileStream when ResolveNumber(profileStream.Dictionary["N"]) is 1 => "DeviceGray",
            PdfStreamObject profileStream when ResolveNumber(profileStream.Dictionary["N"]) is 3 => "DeviceRGB",
            PdfStreamObject profileStream when ResolveNumber(profileStream.Dictionary["N"]) is 4 => "DeviceCMYK",
            _ => null
        };
    }

    private static PdfObject? ResolveNamedColorSpace(string resourceName, PdfDictionary resources, PdfObjectGraph graph)
    {
        if (ResolveObject(resources["ColorSpace"], graph) is not PdfDictionary colorSpaces)
        {
            return null;
        }

        return colorSpaces.Values.TryGetValue(resourceName, out var colorSpace)
            ? ResolveObject(colorSpace, graph) ?? colorSpace
            : null;
    }

    private static string? ResolveSingleSupportedFilterName(PdfObject? filter, PdfObjectGraph graph)
    {
        var resolvedFilter = ResolveObject(filter, graph) ?? filter;
        return resolvedFilter switch
        {
            PdfName name when name.Value is "DCTDecode" or "FlateDecode" or "CCITTFaxDecode" or "CCF" or "LZWDecode" or "LZW" => name.Value,
            PdfArray { Items.Count: 1 } array => ResolveSingleSupportedFilterName(array.Items[0], graph),
            _ => null
        };
    }

    private static string? ResolveDecodeParameters(PdfObject? decodeParms, PdfObjectGraph graph)
    {
        var resolvedDecodeParms = ResolveObject(decodeParms, graph) ?? decodeParms;
        if (resolvedDecodeParms is PdfArray { Items.Count: 1 } array)
        {
            resolvedDecodeParms = ResolveObject(array.Items[0], graph) ?? array.Items[0];
        }

        if (resolvedDecodeParms is not PdfDictionary dictionary)
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

    private static bool TryGetIndexedColorSpaceArray(PdfObject? colorSpace, PdfDictionary resources, PdfObjectGraph graph, out PdfArray indexedArray)
    {
        indexedArray = null!;
        var resolved = ResolveObject(colorSpace, graph) ?? colorSpace;
        if (resolved is PdfName name)
        {
            resolved = ResolveNamedColorSpace(name.Value, resources, graph) ?? resolved;
        }
        if (resolved is PdfArray { Items.Count: 4 } arr && ResolveObject(arr.Items[0], graph) is PdfName { Value: "Indexed" })
        {
            indexedArray = arr;
            return true;
        }
        return false;
    }

    private static bool TryExpandIndexedPixels(
        PdfArray indexedArray,
        PdfDictionary resources,
        PdfObjectGraph graph,
        ReadOnlyMemory<byte> rawPixels,
        int bitsPerComponent,
        int width,
        int height,
        out ReadOnlyMemory<byte> expanded,
        out int componentCount)
    {
        expanded = default;
        componentCount = 0;

        var baseCsName = ResolveColorSpaceName(indexedArray.Items[1], resources, graph);
        componentCount = baseCsName switch
        {
            "DeviceGray" => 1,
            "DeviceRGB" => 3,
            "DeviceCMYK" => 4,
            _ => 0
        };
        if (componentCount == 0) return false;

        if (ResolveNumber(indexedArray.Items[2]) is not { } hivalDouble) return false;
        var hival = (int)hivalDouble;

        var tableObj = ResolveObject(indexedArray.Items[3], graph) ?? indexedArray.Items[3];
        byte[] table;
        if (tableObj is PdfString tableString)
        {
            table = tableString.IsHex ? ParseHexBytes(tableString.Bytes.Span) : tableString.Bytes.ToArray();
        }
        else if (tableObj is PdfStreamObject tableStream)
        {
            table = new PdfStreamDecoderRegistry().Decode(tableStream).ToArray();
        }
        else
        {
            return false;
        }

        if (table.Length < (hival + 1) * componentCount) return false;

        var rowBytes = (width * bitsPerComponent + 7) / 8;
        var result = new byte[width * height * componentCount];
        var src = rawPixels.Span;
        var dst = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                int index;
                if (bitsPerComponent == 8)
                {
                    var pos = y * rowBytes + x;
                    if (pos >= src.Length) break;
                    index = src[pos];
                }
                else
                {
                    var bitIndex = y * width * bitsPerComponent + x * bitsPerComponent;
                    var byteOffset = bitIndex / 8;
                    var bitShift = 8 - bitsPerComponent - (bitIndex % 8);
                    if (byteOffset >= src.Length) break;
                    index = (src[byteOffset] >> bitShift) & ((1 << bitsPerComponent) - 1);
                }

                index = Math.Min(index, hival);
                var tableOffset = index * componentCount;
                for (var c = 0; c < componentCount; c++)
                {
                    result[dst++] = table[tableOffset + c];
                }
            }
        }

        expanded = result;
        return true;
    }

    private static byte[] ParseHexBytes(ReadOnlySpan<byte> hexChars)
    {
        var result = new List<byte>(hexChars.Length / 2);
        var i = 0;
        while (i < hexChars.Length)
        {
            while (i < hexChars.Length && (hexChars[i] == ' ' || hexChars[i] == '\n' || hexChars[i] == '\r' || hexChars[i] == '\t')) i++;
            if (i >= hexChars.Length) break;
            var high = HexNibble(hexChars[i++]);
            var low = i < hexChars.Length ? HexNibble(hexChars[i++]) : 0;
            result.Add((byte)((high << 4) | low));
        }
        return [.. result];
    }

    private static int HexNibble(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - '0',
        >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
        >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
        _ => 0
    };

    private static ReadOnlyMemory<byte> RecompressFlate(ReadOnlyMemory<byte> data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(data.Span);
        }
        return output.ToArray();
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