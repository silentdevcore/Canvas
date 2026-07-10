using PXA.Importer.Objects;

namespace PXA.Importer.Content;

public enum PdfOperatorCategory
{
    GeneralGraphicsState,
    SpecialGraphicsState,
    PathConstruction,
    PathPainting,
    Clipping,
    TextObject,
    TextState,
    TextPositioning,
    TextShowing,
    Type3Fonts,
    Color,
    Shading,
    XObject,
    InlineImage,
    MarkedContent,
    Compatibility
}

public sealed record PdfOperatorDescriptor(string Name, PdfOperatorCategory Category, int? OperandCount);

public static class PdfOperatorRegistry
{
    private static readonly Dictionary<string, PdfOperatorDescriptor> Operators = new(StringComparer.Ordinal)
    {
        ["w"] = new("w", PdfOperatorCategory.GeneralGraphicsState, 1),
        ["J"] = new("J", PdfOperatorCategory.GeneralGraphicsState, 1),
        ["j"] = new("j", PdfOperatorCategory.GeneralGraphicsState, 1),
        ["M"] = new("M", PdfOperatorCategory.GeneralGraphicsState, 1),
        ["d"] = new("d", PdfOperatorCategory.GeneralGraphicsState, 2),
        ["ri"] = new("ri", PdfOperatorCategory.GeneralGraphicsState, 1),
        ["i"] = new("i", PdfOperatorCategory.GeneralGraphicsState, 1),
        ["gs"] = new("gs", PdfOperatorCategory.GeneralGraphicsState, 1),
        ["q"] = new("q", PdfOperatorCategory.SpecialGraphicsState, 0),
        ["Q"] = new("Q", PdfOperatorCategory.SpecialGraphicsState, 0),
        ["cm"] = new("cm", PdfOperatorCategory.SpecialGraphicsState, 6),
        ["m"] = new("m", PdfOperatorCategory.PathConstruction, 2),
        ["l"] = new("l", PdfOperatorCategory.PathConstruction, 2),
        ["c"] = new("c", PdfOperatorCategory.PathConstruction, 6),
        ["v"] = new("v", PdfOperatorCategory.PathConstruction, 4),
        ["y"] = new("y", PdfOperatorCategory.PathConstruction, 4),
        ["h"] = new("h", PdfOperatorCategory.PathConstruction, 0),
        ["re"] = new("re", PdfOperatorCategory.PathConstruction, 4),
        ["S"] = new("S", PdfOperatorCategory.PathPainting, 0),
        ["s"] = new("s", PdfOperatorCategory.PathPainting, 0),
        ["f"] = new("f", PdfOperatorCategory.PathPainting, 0),
        ["F"] = new("F", PdfOperatorCategory.PathPainting, 0),
        ["f*"] = new("f*", PdfOperatorCategory.PathPainting, 0),
        ["B"] = new("B", PdfOperatorCategory.PathPainting, 0),
        ["B*"] = new("B*", PdfOperatorCategory.PathPainting, 0),
        ["b"] = new("b", PdfOperatorCategory.PathPainting, 0),
        ["b*"] = new("b*", PdfOperatorCategory.PathPainting, 0),
        ["n"] = new("n", PdfOperatorCategory.PathPainting, 0),
        ["W"] = new("W", PdfOperatorCategory.Clipping, 0),
        ["W*"] = new("W*", PdfOperatorCategory.Clipping, 0),
        ["BT"] = new("BT", PdfOperatorCategory.TextObject, 0),
        ["ET"] = new("ET", PdfOperatorCategory.TextObject, 0),
        ["Tc"] = new("Tc", PdfOperatorCategory.TextState, 1),
        ["Tw"] = new("Tw", PdfOperatorCategory.TextState, 1),
        ["Tz"] = new("Tz", PdfOperatorCategory.TextState, 1),
        ["TL"] = new("TL", PdfOperatorCategory.TextState, 1),
        ["Tf"] = new("Tf", PdfOperatorCategory.TextState, 2),
        ["Tr"] = new("Tr", PdfOperatorCategory.TextState, 1),
        ["Ts"] = new("Ts", PdfOperatorCategory.TextState, 1),
        ["Td"] = new("Td", PdfOperatorCategory.TextPositioning, 2),
        ["TD"] = new("TD", PdfOperatorCategory.TextPositioning, 2),
        ["Tm"] = new("Tm", PdfOperatorCategory.TextPositioning, 6),
        ["T*"] = new("T*", PdfOperatorCategory.TextPositioning, 0),
        ["Tj"] = new("Tj", PdfOperatorCategory.TextShowing, 1),
        ["TJ"] = new("TJ", PdfOperatorCategory.TextShowing, 1),
        ["'"] = new("'", PdfOperatorCategory.TextShowing, 1),
        ["\""] = new("\"", PdfOperatorCategory.TextShowing, 3),
        ["d0"] = new("d0", PdfOperatorCategory.Type3Fonts, 2),
        ["d1"] = new("d1", PdfOperatorCategory.Type3Fonts, 6),
        ["CS"] = new("CS", PdfOperatorCategory.Color, 1),
        ["cs"] = new("cs", PdfOperatorCategory.Color, 1),
        ["SC"] = new("SC", PdfOperatorCategory.Color, null),
        ["SCN"] = new("SCN", PdfOperatorCategory.Color, null),
        ["sc"] = new("sc", PdfOperatorCategory.Color, null),
        ["scn"] = new("scn", PdfOperatorCategory.Color, null),
        ["G"] = new("G", PdfOperatorCategory.Color, 1),
        ["g"] = new("g", PdfOperatorCategory.Color, 1),
        ["RG"] = new("RG", PdfOperatorCategory.Color, 3),
        ["rg"] = new("rg", PdfOperatorCategory.Color, 3),
        ["K"] = new("K", PdfOperatorCategory.Color, 4),
        ["k"] = new("k", PdfOperatorCategory.Color, 4),
        ["sh"] = new("sh", PdfOperatorCategory.Shading, 1),
        ["Do"] = new("Do", PdfOperatorCategory.XObject, 1),
        ["BI"] = new("BI", PdfOperatorCategory.InlineImage, null),
        ["ID"] = new("ID", PdfOperatorCategory.InlineImage, null),
        ["EI"] = new("EI", PdfOperatorCategory.InlineImage, null),
        ["MP"] = new("MP", PdfOperatorCategory.MarkedContent, 1),
        ["DP"] = new("DP", PdfOperatorCategory.MarkedContent, 2),
        ["BMC"] = new("BMC", PdfOperatorCategory.MarkedContent, 1),
        ["BDC"] = new("BDC", PdfOperatorCategory.MarkedContent, 2),
        ["EMC"] = new("EMC", PdfOperatorCategory.MarkedContent, 0),
        ["BX"] = new("BX", PdfOperatorCategory.Compatibility, 0),
        ["EX"] = new("EX", PdfOperatorCategory.Compatibility, 0)
    };

    public static bool TryGet(string name, out PdfOperatorDescriptor descriptor) => Operators.TryGetValue(name, out descriptor!);
}

public sealed record PdfContentCommand(
    PdfOperatorDescriptor Operator,
    IReadOnlyList<PdfObject> Operands,
    Objects.PdfSourceSpan SourceSpan,
    int Sequence);
