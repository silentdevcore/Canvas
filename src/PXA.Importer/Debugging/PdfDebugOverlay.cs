using PXA.Importer.Analysis;
using PXA.Importer.Graphics;

namespace PXA.Importer.Debugging;

public enum PdfDebugOverlayKind
{
    Bounds,
    Baseline,
    Matrix,
    ZOrder,
    ObjectId,
    Group,
    ReadingOrder,
    Classification
}

public sealed record PdfDebugOverlayItem(
    PdfDebugOverlayKind Kind,
    PdfRectangle Bounds,
    string Label,
    PdfColor Color,
    PdfMatrix Transform);

public sealed class PdfDebugOverlayBuilder
{
    public IReadOnlyList<PdfDebugOverlayItem> Build(PdfScenePage page)
    {
        var overlays = new List<PdfDebugOverlayItem>();
        foreach (var primitive in page.Layers.SelectMany(static layer => layer.Objects).SelectMany(Expand))
        {
            overlays.Add(new PdfDebugOverlayItem(PdfDebugOverlayKind.Bounds, primitive.Bounds, primitive.Kind.ToString(), ColorFor(primitive.Classification), primitive.Transform));
            overlays.Add(new PdfDebugOverlayItem(PdfDebugOverlayKind.ZOrder, primitive.Bounds, primitive.ZOrder.ToString(), PdfColor.Black, primitive.Transform));
            overlays.Add(new PdfDebugOverlayItem(PdfDebugOverlayKind.Classification, primitive.Bounds, primitive.Classification.ToString(), ColorFor(primitive.Classification), primitive.Transform));

            if (primitive is PrimitiveText text)
            {
                overlays.Add(new PdfDebugOverlayItem(PdfDebugOverlayKind.Baseline, text.Bounds, $"{text.Geometry.RotationDegrees:0.#}°", new PdfColor(0, 0.4, 1, 1, PdfColorSpace.DeviceRgb), text.Transform));
            }
        }

        if (page.ReadingOrder is not null)
        {
            overlays.AddRange(page.ReadingOrder.Lines.Select(line => new PdfDebugOverlayItem(
                PdfDebugOverlayKind.ReadingOrder,
                line.Bounds,
                line.Order.ToString(),
                new PdfColor(0, 0.7, 0, 1, PdfColorSpace.DeviceRgb),
                PdfMatrix.Identity)));
        }

        overlays.AddRange(page.VisualGroups.Select(group => new PdfDebugOverlayItem(
            PdfDebugOverlayKind.Group,
            group.Bounds,
            group.Kind,
            new PdfColor(1, 0.5, 0, 1, PdfColorSpace.DeviceRgb),
            PdfMatrix.Identity)));

        return overlays;
    }

    private static IEnumerable<PrimitiveObject> Expand(PrimitiveObject primitive)
    {
        yield return primitive;
        foreach (var child in primitive.Children.SelectMany(Expand))
        {
            yield return child;
        }
    }

    private static PdfColor ColorFor(PrimitiveClassification classification)
    {
        return classification switch
        {
            PrimitiveClassification.Text => new PdfColor(0, 0, 1, 1, PdfColorSpace.DeviceRgb),
            PrimitiveClassification.VectorIcon or PrimitiveClassification.SymbolFontIcon => new PdfColor(0.6, 0, 0.8, 1, PdfColorSpace.DeviceRgb),
            PrimitiveClassification.LinearBarcode or PrimitiveClassification.MatrixBarcode or PrimitiveClassification.Barcode => new PdfColor(1, 0, 0, 1, PdfColorSpace.DeviceRgb),
            PrimitiveClassification.Image => new PdfColor(0, 0.6, 0.8, 1, PdfColorSpace.DeviceRgb),
            _ => PdfColor.Black
        };
    }
}
