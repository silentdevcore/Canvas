using Canvas.Importer.Graphics;

namespace Canvas.Importer.Analysis;

public sealed record VisualGroup(
    string Kind,
    IReadOnlyList<PrimitiveObject> Objects,
    PdfRectangle Bounds,
    double Confidence);

public sealed class GroupingEngine
{
    public IReadOnlyList<VisualGroup> BuildGroups(IReadOnlyList<PrimitiveObject> primitives)
    {
        var flat = ReadingOrderEngine.Flatten(primitives).Where(static primitive => primitive.Kind != PrimitiveKind.Group).ToArray();
        var groups = new List<VisualGroup>();
        groups.AddRange(BuildContainmentGroups(flat));
        groups.AddRange(BuildLabelValueGroups(flat));
        groups.AddRange(BuildIconTextGroups(flat));
        groups.AddRange(BuildProximityGroups(flat));
        return groups;
    }

    private static IEnumerable<VisualGroup> BuildContainmentGroups(IReadOnlyList<PrimitiveObject> primitives)
    {
        foreach (var container in primitives.OfType<PrimitiveShape>())
        {
            var children = primitives
                .Where(candidate => !ReferenceEquals(candidate, container) && Contains(container.Bounds, candidate.Bounds))
                .OrderBy(static candidate => candidate.ZOrder)
                .ToArray();

            if (children.Length >= 1)
            {
                yield return new VisualGroup("Contained", [container, .. children], container.Bounds, 0.75d);
            }
        }
    }

    private static IEnumerable<VisualGroup> BuildLabelValueGroups(IReadOnlyList<PrimitiveObject> primitives)
    {
        var texts = primitives.OfType<PrimitiveText>().OrderByDescending(static text => text.Bounds.CenterY).ThenBy(static text => text.Bounds.Left).ToArray();
        foreach (var label in texts.Where(static text => text.Text.EndsWith(':')))
        {
            var value = texts.FirstOrDefault(candidate =>
                !ReferenceEquals(candidate, label) &&
                Math.Abs(candidate.Bounds.CenterY - label.Bounds.CenterY) <= Math.Max(label.FontSize, candidate.FontSize) * 0.7d &&
                candidate.Bounds.Left >= label.Bounds.Right);

            if (value is not null)
            {
                yield return new VisualGroup("LabelValue", [label, value], label.Bounds.Union(value.Bounds), 0.8d);
            }
        }
    }

    private static IEnumerable<VisualGroup> BuildIconTextGroups(IReadOnlyList<PrimitiveObject> primitives)
    {
        var icons = primitives.Where(static primitive => primitive.Classification is PrimitiveClassification.VectorIcon or PrimitiveClassification.SymbolFontIcon).ToArray();
        var texts = primitives.OfType<PrimitiveText>().ToArray();
        foreach (var icon in icons)
        {
            var text = texts.FirstOrDefault(candidate =>
                candidate.Bounds.Left >= icon.Bounds.Right &&
                Math.Abs(candidate.Bounds.CenterY - icon.Bounds.CenterY) <= Math.Max(icon.Bounds.Height, candidate.Bounds.Height));

            if (text is not null)
            {
                yield return new VisualGroup("IconText", [icon, text], icon.Bounds.Union(text.Bounds), 0.7d);
            }
        }
    }

    private static IEnumerable<VisualGroup> BuildProximityGroups(IReadOnlyList<PrimitiveObject> primitives)
    {
        var visited = new HashSet<PrimitiveObject>();
        foreach (var seed in primitives.OrderBy(static primitive => primitive.ZOrder))
        {
            if (!visited.Add(seed))
            {
                continue;
            }

            var group = primitives
                .Where(candidate => !ReferenceEquals(candidate, seed) && Distance(seed.Bounds, candidate.Bounds) <= Math.Max(seed.Bounds.Height, candidate.Bounds.Height) * 1.2d)
                .Take(8)
                .ToList();

            if (group.Count < 2)
            {
                continue;
            }

            group.Insert(0, seed);
            foreach (var item in group)
            {
                visited.Add(item);
            }

            yield return new VisualGroup("Proximity", group, ReadingOrderEngine.Union(group.Select(static item => item.Bounds)), 0.55d);
        }
    }

    private static bool Contains(PdfRectangle outer, PdfRectangle inner)
    {
        return outer.Left <= inner.Left && outer.Right >= inner.Right && outer.Bottom <= inner.Bottom && outer.Top >= inner.Top;
    }

    private static double Distance(PdfRectangle left, PdfRectangle right)
    {
        var dx = Math.Max(0, Math.Max(left.Left - right.Right, right.Left - left.Right));
        var dy = Math.Max(0, Math.Max(left.Bottom - right.Top, right.Bottom - left.Top));
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
