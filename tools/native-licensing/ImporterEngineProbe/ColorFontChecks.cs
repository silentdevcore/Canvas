using System.Collections;
using System.Reflection;
using System.Text;
using PVAD.Importer.Color;
using PVAD.Importer.Fonts;
using PVAD.Importer.Objects;
using PVAD.Licensing;

internal sealed class ColorFontChecks
{
    private readonly PdfToUnicodeMap unicode = new();
    private readonly CountingEncoding encoding = new();
    private readonly CountingFunction function = new();
    private readonly CountingComponents components = new();
    private readonly PdfFontResource font;
    private readonly PdfResolvedColorSpace cmyk = new(PdfColorSpaceFamily.DeviceCmyk, 4);
    private readonly byte[] content = "0 1 1 0 k BT (Color text) Tj ET"u8.ToArray();
    private readonly byte[] originalContent;
    private readonly PdfDictionary exponential;
    private readonly List<(string Name, PdfFunction Function, double Expected)> retainedFunctions = [];
    private PdfFontResource? parsedFont;
    private PdfResolvedColorSpace? icc;
    private int resolutions;

    internal ColorFontChecks(Dictionary<string, Action> calls, HashSet<Type> types)
    {
        originalContent = content.ToArray();
        unicode.Add(65, "A");
        unicode.Add(0x1234, 2, "Z");
        font = new PdfFontResource
        {
            ResourceName = "F1", Kind = PdfFontKind.Type0, CodeByteLength = 2,
            Encoding = encoding, Widths = new Dictionary<int, double> { [65] = 600 }, MissingWidth = 500,
        };
        exponential = new PdfDictionary
        {
            ["FunctionType"] = new PdfInteger(2), ["Domain"] = Numbers(0, 1),
            ["C0"] = Numbers(0), ["C1"] = Numbers(1), ["N"] = new PdfInteger(1),
        };
        var rewriter = new PdfContentColorRewriter(Resolve, new Dictionary<string, PdfObject>());
        Add(typeof(PdfSrgbColorConverter), nameof(PdfSrgbColorConverter.Resolve),
            () => Equal(PdfSrgbColorConverter.Resolve(new PdfName("DeviceCMYK"), Resolve).Family == PdfColorSpaceFamily.DeviceCmyk),
            typeof(PdfObject), typeof(Func<PdfObject?, PdfObject?>), typeof(int));
        Add(typeof(PdfSrgbColorConverter), nameof(PdfSrgbColorConverter.ToSrgb),
            () => Equal(PdfSrgbColorConverter.ToSrgb(cmyk, components)!.SequenceEqual(new[] { 1d, 0d, 0d })),
            typeof(PdfResolvedColorSpace), typeof(IReadOnlyList<double>), typeof(int));
        Add(typeof(PdfFunction), nameof(PdfFunction.TryCreate),
            () => Equal(PdfFunction.TryCreate(exponential, Resolve)!.Evaluate([0.5])[0] == 0.5),
            typeof(PdfObject), typeof(Func<PdfObject?, PdfObject?>), typeof(int));
        Add(typeof(PdfFunction), nameof(PdfFunction.Evaluate),
            () => Equal(function.Evaluate([0.5]).SequenceEqual(new[] { 0.5 })), typeof(IReadOnlyList<double>));
        Add(typeof(PdfContentColorRewriter), nameof(PdfContentColorRewriter.Rewrite), () =>
        {
            var result = rewriter.Rewrite(content);
            var text = Encoding.ASCII.GetString(result.Content);
            Equal(result.ConvertedOperatorCount == 1 && text.Contains("1 0 0 rg", StringComparison.Ordinal) && text.Contains("BT (Color text) Tj ET", StringComparison.Ordinal));
        }, typeof(ReadOnlyMemory<byte>));
        Add(typeof(PdfFontResource), nameof(PdfFontResource.Decode),
            () => Equal(font.Decode("A"u8) == "A"), typeof(ReadOnlySpan<byte>));
        Add(typeof(PdfFontResource), nameof(PdfFontResource.GetGlyphCodes),
            () => Equal(font.GetGlyphCodes(new byte[] { 0x12, 0x34, 0x56, 0x78 }).SequenceEqual(new[] { 0x1234, 0x5678 })), typeof(ReadOnlySpan<byte>));
        Add(typeof(PdfEncodingMap), nameof(PdfEncodingMap.Decode),
            () => Equal(PdfEncodingMap.Identity.Decode("A"u8) == "A"), typeof(ReadOnlySpan<byte>));
        Add(typeof(PdfToUnicodeMap), nameof(PdfToUnicodeMap.Decode),
            () => Equal(unicode.Decode(new byte[] { 65, 0x12, 0x34 }) == "AZ"), typeof(ReadOnlySpan<byte>));

        void Add(Type type, string name, Action action, params Type[] parameters)
        {
            types.Add(type);
            var method = type.GetMethod(name, parameters) ?? throw new InvalidOperationException("Missing color/font entry.");
            NativeSurfaceCoverage.Record(method);
            var key = $"{method.DeclaringType!.Name}.{method.Name}({string.Join(',', method.GetParameters().Select(parameter => parameter.ParameterType.ToString()))})";
            calls.Add(key, action);
            NativeSurfaceCoverage.BindPhases(method, "direct", key);
        }
    }

    internal (int Resolutions, int Encodings, int Evaluations, int Components) Snapshot() => (resolutions, encoding.Calls, function.Calls, components.Reads);

    internal void VerifyUnchanged((int Resolutions, int Encodings, int Evaluations, int Components) before)
    {
        Equal(Snapshot() == before && content.SequenceEqual(originalContent));
    }

    internal void VerifyMetadata()
    {
        Equal(cmyk.IsConvertible && cmyk.ComponentCount == 4 && function.Domain.Count == 2 && function.OutputCount == 1);
        Equal(font.ResourceName == "F1" && font.GetGlyphWidth(65) == 600 && font.GetGlyphWidth(66) == 500 && unicode.MaxCodeLength == 2);
        // Building DTO mapping entries is not glyph decoding.
        var empty = new PdfToUnicodeMap();
        empty.Add(66, "B");
        empty.Add(0x1234, 2, "Z");
        Equal(empty.MaxCodeLength == 2);
        if (icc is not null) Equal(icc.Family == PdfColorSpaceFamily.IccBased && icc.ComponentCount == 3);
    }

    internal void VerifyFunctionFamilies(List<string> checks)
    {
        foreach (var (name, fixture, expected) in new[]
        {
            ("exponential", (PdfObject)exponential, 0.5),
            ("sampled", Sampled(), 0.5),
            ("postscript", PostScript(), 0.5),
            ("array", new PdfArray([exponential]), 0.5),
            ("stitching", new PdfDictionary
            {
                ["FunctionType"] = new PdfInteger(3), ["Domain"] = Numbers(0, 1), ["Bounds"] = Numbers(0.5),
                ["Encode"] = Numbers(0, 1, 0, 1), ["Functions"] = new PdfArray([exponential, exponential]),
            }, 0d),
        })
        {
            var retained = PdfFunction.TryCreate(fixture, Resolve)!;
            Equal(retained is not null && Math.Abs(retained.Evaluate([0.5])[0] - expected) < 0.005);
            retainedFunctions.Add((name, retained!, expected));
            checks.Add("color-font:valid-function-family:" + name);
        }
        foreach (var (name, space, input, expected) in new[]
        {
            ("gray", new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceGray, 1), new[] { 0.5 }, new[] { 0.5, 0.5, 0.5 }),
            ("rgb", new PdfResolvedColorSpace(PdfColorSpaceFamily.DeviceRgb, 3), new[] { 1d, 0d, 0d }, new[] { 1d, 0d, 0d }),
            ("cmyk", cmyk, new[] { 0d, 1d, 1d, 0d }, new[] { 1d, 0d, 0d }),
            ("indexed", new PdfResolvedColorSpace(PdfColorSpaceFamily.Indexed, 1,
                new(PdfColorSpaceFamily.DeviceRgb, 3), IndexedLookup: [255, 0, 0], IndexedHighValue: 0), new[] { 0d }, new[] { 1d, 0d, 0d }),
        })
        {
            Equal(PdfSrgbColorConverter.ToSrgb(space, input)!.SequenceEqual(expected));
            checks.Add("color-font:valid-color-family:" + name);
        }
        parsedFont = new PdfSimpleFontParser().Parse("F1", new PdfDictionary
        {
            ["Subtype"] = new PdfName("Type1"), ["BaseFont"] = new PdfName("Helvetica"),
        }, new DirectFontResolver());
        Equal(parsedFont.Decode("A"u8) == "A" && parsedFont.GetGlyphCodes("A"u8).SequenceEqual(new[] { 65 }));
        checks.Add("color-font:parsed-font-content");
        // Reuse the same synthetic profile factory as the existing color regression suite.
        var resources = typeof(PVAD.Pdf.PdfDocument).Assembly.GetType("PVAD.Pdf.PdfComplianceResources", throwOnError: true)!;
        var profile = (byte[])resources.GetMethod("CreateSrgbIccProfile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        icc = PdfSrgbColorConverter.Resolve(new PdfArray([new PdfName("ICCBased"), new PdfStreamObject(new PdfDictionary { ["N"] = new PdfInteger(3) }, profile)]), Resolve);
        if (Environment.GetEnvironmentVariable("PVAD_REQUIRE_LCMS2") == "1") Equal(icc.IsConvertible);
        if (icc.IsConvertible)
        {
            VerifyIccRed();
            checks.Add("color-font:real-icc-transform");
        }
        else
        {
            Equal(PdfSrgbColorConverter.ToSrgb(icc, [1, 0, 0]) is null);
            checks.Add("color-font:icc-unavailable-fails-closed");
        }
    }

    internal void VerifyRetainedDenials(List<string> checks)
    {
        foreach (var (name, retained, _) in retainedFunctions)
            Denied(() => retained.Evaluate([0.5]), "color-font:retained-function-denied:" + name);
        Denied(() => parsedFont!.Decode("A"u8), "color-font:retained-parsed-font-decode-denied");
        Denied(() => parsedFont!.GetGlyphCodes("A"u8), "color-font:retained-parsed-font-codes-denied");
        Denied(() => PdfSrgbColorConverter.ToSrgb(icc!, [1, 0, 0]), "color-font:retained-icc-transform-denied");
        void Denied(Action action, string name)
        {
            try { action(); }
            catch (PvadLicenseException error) when (error.Code == "PVADLIC012") { checks.Add(name); return; }
            throw new InvalidOperationException("Retained color/font state bypassed admission.");
        }
    }

    internal void VerifyRetainedRenewal(List<string> checks)
    {
        foreach (var (name, retained, expected) in retainedFunctions)
        {
            Equal(Math.Abs(retained.Evaluate([0.5])[0] - expected) < 0.005);
            checks.Add("color-font:retained-function-renewed:" + name);
        }
        Equal(parsedFont!.Decode("A"u8) == "A" && parsedFont.GetGlyphCodes("A"u8).SequenceEqual(new[] { 65 }));
        checks.Add("color-font:retained-parsed-font-renewed");
        if (icc!.IsConvertible)
        {
            VerifyIccRed();
            checks.Add("color-font:retained-icc-transform-renewed");
        }
    }

    internal void VerifyNestedContinuations(string[] licenses, List<string> checks)
    {
        foreach (var kind in new[] { "sampled", "postscript", "icc" })
        foreach (var callbackFailure in new[] { false, true })
        {
            try
            {
                var switched = false;
                PdfObject? Revoke(PdfObject? value)
                {
                    var target = kind switch
                    {
                        "sampled" => value is PdfInteger { Value: 8 },
                        "postscript" => value is PdfInteger { Value: 4 },
                        _ => value is PdfStreamObject,
                    };
                    if (target && !switched)
                    {
                        switched = true; PvadLicense.LoadFromFile(licenses[1]);
                        if (callbackFailure) PvadLicense.Require(PvadLicenseProducts.Importer);
                    }
                    return value;
                }
                try
                {
                    if (kind == "icc")
                        Equal(PdfSrgbColorConverter.Resolve(new PdfArray([new PdfName("ICCBased"), new PdfStreamObject(new PdfDictionary { ["N"] = new PdfInteger(3) }, new byte[128])]), Revoke).ComponentCount == 3);
                    else
                        Equal(PdfFunction.TryCreate(kind == "sampled" ? Sampled() : PostScript(), Revoke) is not null);
                }
                catch (PvadLicenseException error) when (error.Code == "PVADLIC012" && switched && callbackFailure)
                {
                    checks.Add("color-font:resolver-native-denial-not-hidden:" + kind);
                    continue;
                }
                Equal(switched && !callbackFailure);
                checks.Add("color-font:admitted-resolution-survives-license-replacement:" + kind);
                try { PdfSrgbColorConverter.Resolve(new PdfName("DeviceRGB"), value => value); }
                catch (PvadLicenseException error) when (error.Code == "PVADLIC012")
                {
                    checks.Add("color-font:new-resolution-stays-fresh:" + kind);
                    continue;
                }
                throw new InvalidOperationException("A new color resolution reused a closed admission.");
            }
            finally { PvadLicense.LoadFromFile(licenses[0]); }
        }
    }

    internal static bool IsFreeMethod(MethodInfo method) =>
        method == typeof(PdfFontResource).GetMethod(nameof(PdfFontResource.GetGlyphWidth), [typeof(int)]) ||
        method == typeof(PdfToUnicodeMap).GetMethod(nameof(PdfToUnicodeMap.Add), [typeof(int), typeof(string)]) ||
        method == typeof(PdfToUnicodeMap).GetMethod(nameof(PdfToUnicodeMap.Add), [typeof(int), typeof(int), typeof(string)]);

    private void VerifyIccRed()
    {
        var rgb = PdfSrgbColorConverter.ToSrgb(icc!, [1, 0, 0]);
        Equal(rgb is not null && rgb[0] is >= 0.99 and <= 1 && rgb[1] is >= 0 and <= 0.01 && rgb[2] is >= 0 and <= 0.01);
    }

    private PdfObject? Resolve(PdfObject? value) { resolutions++; return value; }
    private static PdfArray Numbers(params double[] values) => new(values.Select(value => (PdfObject)new PdfNumber(value)));
    private static PdfStreamObject Sampled() => new(new PdfDictionary
    {
        ["FunctionType"] = new PdfInteger(0), ["Domain"] = Numbers(0, 1), ["Range"] = Numbers(0, 1),
        ["Size"] = new PdfArray([new PdfInteger(2)]), ["BitsPerSample"] = new PdfInteger(8),
    }, new byte[] { 0, 255 });
    private static PdfStreamObject PostScript() => new(new PdfDictionary
    {
        ["FunctionType"] = new PdfInteger(4), ["Domain"] = Numbers(0, 1), ["Range"] = Numbers(0, 1),
    }, "{ 1 exch sub }"u8.ToArray());
    private static void Equal(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Color/font admission or content check failed.");
    }
    private sealed class CountingEncoding : PdfEncodingMap
    {
        public int Calls { get; private set; }
        public override string Decode(ReadOnlySpan<byte> bytes) { Calls++; return base.Decode(bytes); }
    }
    private sealed class DirectFontResolver : IPdfObjectResolver
    {
        public PdfObject? Resolve(PdfObject value) => value;
    }
    private sealed class CountingFunction() : PdfFunction([0, 1], null)
    {
        public int Calls { get; private set; }
        public override int OutputCount => 1;
        protected override double[] EvaluateCore(double[] inputs) { Calls++; return inputs; }
    }
    private sealed class CountingComponents : IReadOnlyList<double>
    {
        public int Reads { get; private set; }
        public int Count { get { Reads++; return 4; } }
        public double this[int index] { get { Reads++; return index is 1 or 2 ? 1 : 0; } }
        public IEnumerator<double> GetEnumerator() { Reads++; return new List<double> { 0, 1, 1, 0 }.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
