using System.Text;
using PXA.Importer.Color;
using PXA.Importer.Objects;

namespace PXA.Importer.Tests;

public sealed class PdfColorConversionTests
{
    [Fact]
    public void ContentRewriter_ShouldRestoreColorSpaceAcrossGraphicsState()
    {
        var rewriter = new PdfContentColorRewriter(Direct, new Dictionary<string, PdfObject>());
        var source = Encoding.ASCII.GetBytes(
            "/DeviceCMYK cs 0 0 0 1 scn q /DeviceRGB cs 1 0 0 scn Q 0 0 0 1 scn");

        var result = rewriter.Rewrite(source);
        var rewritten = Encoding.ASCII.GetString(result.Content);

        Assert.Empty(result.UnconvertibleColorSpaceNames);
        Assert.DoesNotContain("0 0 0 1 scn", rewritten, StringComparison.Ordinal);
        Assert.Equal(3, result.ConvertedOperatorCount);
    }

    [Fact]
    public void ContentRewriter_ShouldBlockUnknownNamedColorSpace()
    {
        var rewriter = new PdfContentColorRewriter(Direct, new Dictionary<string, PdfObject>());

        var result = rewriter.Rewrite(Encoding.ASCII.GetBytes("/Missing cs 0.5 scn"));

        Assert.Contains("Missing", result.UnconvertibleColorSpaceNames);
        Assert.Equal(0, result.ConvertedOperatorCount);
    }

    [Fact]
    public void ContentRewriter_ShouldBlockShadingUntilItsResourceIsConverted()
    {
        var rewriter = new PdfContentColorRewriter(Direct, new Dictionary<string, PdfObject>());

        var result = rewriter.Rewrite(Encoding.ASCII.GetBytes("/Sh1 sh"));

        Assert.Contains("Shading:Sh1", result.UnconvertibleColorSpaceNames);
    }

    private static readonly Func<PdfObject?, PdfObject?> Direct = static value => value;

    [Theory]
    // ISO 32000-1, 8.6.4.4: R = 1 - min(1, C + K), and so on.
    [InlineData(0, 0, 0, 0, 1, 1, 1)]
    [InlineData(0, 0, 0, 1, 0, 0, 0)]
    [InlineData(1, 0, 0, 0, 0, 1, 1)]
    [InlineData(0, 1, 0, 0, 1, 0, 1)]
    [InlineData(0, 0, 1, 0, 1, 1, 0)]
    [InlineData(0.5, 0.25, 0, 0.25, 0.25, 0.5, 0.75)]
    public void Cmyk_ShouldFollowTheSpecifiedConversion(
        double c, double m, double y, double k, double r, double g, double b)
    {
        var rgb = PdfSrgbColorConverter.ToSrgb(
            new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceCmyk, 4), [c, m, y, k]);

        Assert.NotNull(rgb);
        Assert.Equal(r, rgb[0], precision: 6);
        Assert.Equal(g, rgb[1], precision: 6);
        Assert.Equal(b, rgb[2], precision: 6);
    }

    [Fact]
    public void Separation_ShouldConvertThroughItsTintTransform()
    {
        // A spot colour whose full tint is pure cyan in the CMYK alternate space.
        var space = PdfSrgbColorConverter.Resolve(Separation("Spot", ExponentialToCmyk()), Direct);

        Assert.True(space.IsConvertible);
        var full = PdfSrgbColorConverter.ToSrgb(space, [1.0]);
        var none = PdfSrgbColorConverter.ToSrgb(space, [0.0]);

        Assert.NotNull(full);
        Assert.NotNull(none);
        Assert.Equal(0d, full[0], precision: 5);
        Assert.Equal(1d, full[1], precision: 5);
        Assert.Equal(1d, none[0], precision: 5);
    }

    [Fact]
    public void DeviceN_ShouldConvertThroughItsTintTransform()
    {
        var deviceN = new PdfArray([
            new PdfName("DeviceN"),
            new PdfArray([new PdfName("SpotA"), new PdfName("SpotB")]),
            new PdfName("DeviceRGB"),
            ExponentialFunction(inputs: 2, c0: [1, 1, 1], c1: [0, 0, 0])
        ]);

        var space = PdfSrgbColorConverter.Resolve(deviceN, Direct);

        Assert.True(space.IsConvertible);
        Assert.Equal(2, space.ComponentCount);
        var rgb = PdfSrgbColorConverter.ToSrgb(space, [1.0, 1.0]);
        Assert.NotNull(rgb);
        Assert.Equal(0d, rgb[0], precision: 5);
    }

    [Fact]
    public void IccBased_WithoutAnExplicitAlternate_ShouldRemainUnconvertible()
    {
        var icc = new PdfArray([
            new PdfName("ICCBased"),
            new PdfStreamObject(new PdfDictionary { ["N"] = new PdfInteger(4) }, ReadOnlyMemory<byte>.Empty)
        ]);

        var space = PdfSrgbColorConverter.Resolve(icc, Direct);

        Assert.Equal(PdfColorSpaceFamily.IccBased, space.Family);
        Assert.False(space.IsConvertible);
        Assert.Null(space.Alternate);
        Assert.Null(PdfSrgbColorConverter.ToSrgb(space, [0, 0, 0, 1]));
    }

    [Fact]
    public void Indexed_ShouldResolveThroughItsLookupTable()
    {
        var indexed = new PdfArray([
            new PdfName("Indexed"),
            new PdfName("DeviceRGB"),
            new PdfInteger(1),
            new PdfString(new byte[] { 255, 0, 0, 0, 0, 255 }, IsHex: false)
        ]);

        var space = PdfSrgbColorConverter.Resolve(indexed, Direct);

        Assert.True(space.IsConvertible);
        Assert.Equal([1d, 0d, 0d], PdfSrgbColorConverter.ToSrgb(space, [0])!.Select(static v => Math.Round(v, 5)));
        Assert.Equal([0d, 0d, 1d], PdfSrgbColorConverter.ToSrgb(space, [1])!.Select(static v => Math.Round(v, 5)));
    }

    [Fact]
    public void Pattern_ShouldRemainUnconvertible()
    {
        var space = PdfSrgbColorConverter.Resolve(new PdfName("Pattern"), Direct);

        Assert.Equal(PdfColorSpaceFamily.Pattern, space.Family);
        Assert.False(space.IsConvertible);
        Assert.Null(PdfSrgbColorConverter.ToSrgb(space, [0.5]));
    }

    [Fact]
    public void Separation_WithoutATintTransformIsNotConvertible()
    {
        var space = PdfSrgbColorConverter.Resolve(
            new PdfArray([new PdfName("Separation"), new PdfName("Spot"), new PdfName("DeviceCMYK")]),
            Direct);

        Assert.False(space.IsConvertible);
    }

    [Fact]
    public void Rewriter_ShouldReplaceCmykOperatorsWithRgb()
    {
        var content = "0 0 0 1 k 10 10 100 100 re f"u8.ToArray();

        var result = new PdfContentColorRewriter(Direct, new Dictionary<string, PdfObject>()).Rewrite(content);

        var rewritten = Encoding.ASCII.GetString(result.Content);
        Assert.Equal(1, result.ConvertedOperatorCount);
        Assert.Contains("0 0 0 rg", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain(" k ", rewritten, StringComparison.Ordinal);
        // Everything outside the colour command is byte-identical.
        Assert.Contains("10 10 100 100 re f", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void Rewriter_ShouldReplaceStrokeAndFillIndependently()
    {
        var content = "1 0 0 0 K 0 0 0 0 k"u8.ToArray();

        var result = new PdfContentColorRewriter(Direct, new Dictionary<string, PdfObject>()).Rewrite(content);

        var rewritten = Encoding.ASCII.GetString(result.Content);
        Assert.Equal(2, result.ConvertedOperatorCount);
        Assert.Contains("0 1 1 RG", rewritten, StringComparison.Ordinal);
        Assert.Contains("1 1 1 rg", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void Rewriter_ShouldConvertASeparationFillThroughItsResource()
    {
        var resources = new Dictionary<string, PdfObject>
        {
            ["CS0"] = Separation("Spot", ExponentialToCmyk())
        };
        var content = "/CS0 cs 1 scn 0 0 10 10 re f"u8.ToArray();

        var result = new PdfContentColorRewriter(Direct, resources).Rewrite(content);

        var rewritten = Encoding.ASCII.GetString(result.Content);
        Assert.Equal(2, result.ConvertedOperatorCount);
        Assert.Contains("/DeviceRGB cs", rewritten, StringComparison.Ordinal);
        Assert.Contains("0 1 1 rg", rewritten, StringComparison.Ordinal);
        Assert.Empty(result.UnconvertibleColorSpaceNames);
    }

    [Fact]
    public void Rewriter_ShouldReportAPatternSpaceInsteadOfConvertingIt()
    {
        var resources = new Dictionary<string, PdfObject> { ["P0"] = new PdfName("Pattern") };
        var content = "/P0 cs /Pat0 scn 0 0 10 10 re f"u8.ToArray();

        var result = new PdfContentColorRewriter(Direct, resources).Rewrite(content);

        Assert.Equal(0, result.ConvertedOperatorCount);
        Assert.Contains("Pattern:Pat0", result.UnconvertibleColorSpaceNames);
        Assert.Equal(content, result.Content);
    }

    [Fact]
    public void Rewriter_ShouldAcceptAColoredTilingPatternSelection()
    {
        var pattern = new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("Pattern"),
                ["PatternType"] = new PdfInteger(1),
                ["PaintType"] = new PdfInteger(1)
            },
            "0 0 0 1 k"u8.ToArray());
        var rewriter = new PdfContentColorRewriter(
            Direct,
            new Dictionary<string, PdfObject> { ["P0"] = new PdfName("Pattern") },
            new Dictionary<string, PdfObject> { ["Pat0"] = pattern });
        var content = "/P0 cs /Pat0 scn 0 0 10 10 re f"u8.ToArray();

        var result = rewriter.Rewrite(content);

        Assert.Empty(result.UnconvertibleColorSpaceNames);
        Assert.Equal(0, result.ConvertedOperatorCount);
        Assert.Equal(content, result.Content);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public void Rewriter_ShouldBlockUnsupportedPatternKinds(int patternType, int paintType)
    {
        var pattern = new PdfStreamObject(
            new PdfDictionary
            {
                ["Type"] = new PdfName("Pattern"),
                ["PatternType"] = new PdfInteger(patternType),
                ["PaintType"] = new PdfInteger(paintType)
            },
            ReadOnlyMemory<byte>.Empty);
        var rewriter = new PdfContentColorRewriter(
            Direct,
            new Dictionary<string, PdfObject> { ["P0"] = new PdfName("Pattern") },
            new Dictionary<string, PdfObject> { ["Pat0"] = pattern });

        var result = rewriter.Rewrite("/P0 cs /Pat0 scn 0 0 10 10 re f"u8.ToArray());

        Assert.Contains("Pattern:Pat0", result.UnconvertibleColorSpaceNames);
        Assert.Equal(0, result.ConvertedOperatorCount);
    }

    [Fact]
    public void Rewriter_ShouldLeaveContentWithoutColorOperatorsByteIdentical()
    {
        var content = "BT /F1 12 Tf (hello) Tj ET"u8.ToArray();

        var result = new PdfContentColorRewriter(Direct, new Dictionary<string, PdfObject>()).Rewrite(content);

        Assert.Equal(content, result.Content);
        Assert.Equal(0, result.ConvertedOperatorCount);
    }

    [Fact]
    public void PostScriptFunction_ShouldEvaluateTheCalculatorSubset()
    {
        // { 1 exch sub } inverts its input.
        var function = PdfFunction.TryCreate(
            new PdfStreamObject(
                new PdfDictionary
                {
                    ["FunctionType"] = new PdfInteger(4),
                    ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                    ["Range"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)])
                },
                "{ 1 exch sub }"u8.ToArray()),
            Direct);

        Assert.NotNull(function);
        Assert.Equal(0.25d, function.Evaluate([0.75])[0], precision: 6);
        Assert.Equal(1d, function.Evaluate([0])[0], precision: 6);
    }

    [Fact]
    public void PostScriptFunction_ShouldRejectUnknownOperators()
    {
        var function = PdfFunction.TryCreate(
            new PdfStreamObject(
                new PdfDictionary
                {
                    ["FunctionType"] = new PdfInteger(4),
                    ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                    ["Range"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)])
                },
                "{ 1 unsupported-op }"u8.ToArray()),
            Direct);

        Assert.Null(function);
    }

    [Fact]
    public void SampledFunction_ShouldInterpolateBetweenSamples()
    {
        var function = PdfFunction.TryCreate(
            new PdfStreamObject(
                new PdfDictionary
                {
                    ["FunctionType"] = new PdfInteger(0),
                    ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                    ["Range"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
                    ["Size"] = new PdfArray([new PdfInteger(2)]),
                    ["BitsPerSample"] = new PdfInteger(8)
                },
                new byte[] { 0, 255 }),
            Direct);

        Assert.NotNull(function);
        Assert.Equal(0.5d, function.Evaluate([0.5])[0], precision: 2);
    }

    [Fact]
    public void StitchingFunction_ShouldSelectTheSubfunctionForTheInput()
    {
        var stitching = new PdfDictionary
        {
            ["FunctionType"] = new PdfInteger(3),
            ["Domain"] = new PdfArray([new PdfInteger(0), new PdfInteger(1)]),
            ["Bounds"] = new PdfArray([new PdfNumber(0.5)]),
            ["Encode"] = new PdfArray([new PdfInteger(0), new PdfInteger(1), new PdfInteger(0), new PdfInteger(1)]),
            ["Functions"] = new PdfArray([
                ExponentialFunction(1, [0], [0]),
                ExponentialFunction(1, [1], [1])
            ])
        };

        var function = PdfFunction.TryCreate(stitching, Direct);

        Assert.NotNull(function);
        Assert.Equal(0d, function.Evaluate([0.25])[0], precision: 6);
        Assert.Equal(1d, function.Evaluate([0.75])[0], precision: 6);
    }

    private static PdfObject Separation(string name, PdfObject tint) =>
        new PdfArray([new PdfName("Separation"), new PdfName(name), new PdfName("DeviceCMYK"), tint]);

    private static PdfObject ExponentialToCmyk() => ExponentialFunction(1, [0, 0, 0, 0], [1, 0, 0, 0]);

    private static PdfObject ExponentialFunction(int inputs, double[] c0, double[] c1)
    {
        var domain = new List<PdfObject>();
        for (var index = 0; index < inputs; index++)
        {
            domain.Add(new PdfInteger(0));
            domain.Add(new PdfInteger(1));
        }

        var range = new List<PdfObject>();
        for (var index = 0; index < c0.Length; index++)
        {
            range.Add(new PdfInteger(0));
            range.Add(new PdfInteger(1));
        }

        return new PdfDictionary
        {
            ["FunctionType"] = new PdfInteger(2),
            ["Domain"] = new PdfArray(domain),
            ["Range"] = new PdfArray(range),
            ["C0"] = new PdfArray(c0.Select(static value => (PdfObject)new PdfNumber(value))),
            ["C1"] = new PdfArray(c1.Select(static value => (PdfObject)new PdfNumber(value))),
            ["N"] = new PdfInteger(1)
        };
    }
}
