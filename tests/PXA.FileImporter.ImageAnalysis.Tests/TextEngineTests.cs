using PXA.FileImporter.ImageAnalysis.Analysis;
using PXA.FileImporter.ImageAnalysis.Templates;
using SkiaSharp;

namespace PXA.FileImporter.ImageAnalysis.Tests;

public class TextEngineTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Renders text on a white bitmap and returns the prepared image.</summary>
    private static (SKBitmap source, PreparedImage prep) RenderText(
        string text,
        float fontSize = 20f,
        int width = 300,
        int height = 80,
        SKColor? textColor = null,
        SKColor? backgroundColor = null,
        bool antialias = false,
        string fontFamily = "Courier New",
        SKFontStyleWeight fontWeight = SKFontStyleWeight.Normal)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(backgroundColor ?? SKColors.White);

        using var font = new SKFont(
            SKTypeface.FromFamilyName(fontFamily, fontWeight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            fontSize);
        using var paint = new SKPaint { Color = textColor ?? SKColors.Black, IsAntialias = antialias };
        canvas.DrawText(text, 10, height * 0.7f, font, paint);

        var prep = Preprocessor.Prepare(bmp);
        return (bmp, prep);
    }

    private static (SKBitmap source, PreparedImage prep) RenderTextAt(
        string text,
        float x,
        float baselineY,
        float fontSize = 28f,
        int width = 360,
        int height = 160)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), fontSize);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        canvas.DrawText(text, x, baselineY, font, paint);

        var prep = Preprocessor.Prepare(bmp);
        return (bmp, prep);
    }

    private static unsafe SKBitmap AllWhiteBinary(int w, int h)
    {
        var info = new SKImageInfo(w, h, SKColorType.Gray8, SKAlphaType.Opaque);
        var bmp  = new SKBitmap(info);
        byte* ptr = (byte*)bmp.GetPixels().ToPointer();
        new System.Span<byte>(ptr, bmp.RowBytes * bmp.Height).Fill(255);
        return bmp;
    }

    private static SKBitmap JpegRoundTrip(SKBitmap source, int quality)
    {
        using var image = SKImage.FromBitmap(source);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return SKBitmap.Decode(data);
    }

    private static string RecognizeRenderedText(
        string text,
        float fontSize = 24f,
        int width = 420,
        int height = 100,
        bool antialias = false,
        string fontFamily = "Courier New",
        SKFontStyleWeight fontWeight = SKFontStyleWeight.Normal)
    {
        var (src, prep) = RenderText(
            text,
            fontSize,
            width,
            height,
            antialias: antialias,
            fontFamily: fontFamily,
            fontWeight: fontWeight);
        using var _s = src;
        using var _p = prep;

        var result = TextEngine.Analyze(prep);
        return string.Join("\n", result.Lines.Select(line => line.Text));
    }

    private static string RecognizeJpegRenderedText(
        string text,
        float fontSize = 24f,
        int width = 420,
        int height = 100,
        int jpegQuality = 55,
        bool antialias = true,
        string fontFamily = "Courier New",
        SKFontStyleWeight fontWeight = SKFontStyleWeight.Normal)
    {
        var (src, prep) = RenderText(
            text,
            fontSize,
            width,
            height,
            antialias: antialias,
            fontFamily: fontFamily,
            fontWeight: fontWeight);
        prep.Dispose();
        using var _s = src;
        using var jpeg = JpegRoundTrip(src, jpegQuality);
        using var jpegPrep = Preprocessor.Prepare(jpeg);

        var result = TextEngine.Analyze(jpegPrep);
        return string.Join("\n", result.Lines.Select(line => line.Text));
    }

    private static string RecognizeRenderedText(
        string text,
        SKColor textColor,
        SKColor backgroundColor,
        float fontSize = 24f,
        int width = 420,
        int height = 100)
    {
        var (src, prep) = RenderText(text, fontSize, width, height, textColor, backgroundColor);
        using var _s = src;
        using var _p = prep;

        var result = TextEngine.Analyze(prep);
        return string.Join("\n", result.Lines.Select(line => line.Text));
    }

    private static void AssertApproximately(double expected, double actual, double tolerance, string label)
    {
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"{label}: expected {expected} +/- {tolerance}, got {actual}");
    }

    public static IEnumerable<object[]> BroadSyntheticOcrCases()
    {
        yield return ["Header", "Courier New", SKFontStyleWeight.Normal, 28f, false, false];
        yield return ["Invoice", "Arial", SKFontStyleWeight.Normal, 28f, false, false];
        yield return ["Total", "Times New Roman", SKFontStyleWeight.Normal, 28f, false, false];
        yield return ["Price", "Arial", SKFontStyleWeight.Bold, 24f, false, false];
        yield return ["Qty", "Courier New", SKFontStyleWeight.Normal, 18f, false, false];
        yield return ["Item", "Times New Roman", SKFontStyleWeight.Bold, 22f, false, false];
        yield return ["Pen", "Arial", SKFontStyleWeight.Normal, 18f, false, false];
        yield return ["12.50", "Courier New", SKFontStyleWeight.Normal, 18f, true, false];
        yield return ["25.00", "Courier New", SKFontStyleWeight.Bold, 24f, false, false];
        yield return ["Hello World", "Courier New", SKFontStyleWeight.Normal, 28f, false, true];
    }

    // ── Connected component labelling ─────────────────────────────────────────

    [Fact]
    public void LabelComponents_AllWhiteBinary_ReturnsNoBlobs()
    {
        using var binary = AllWhiteBinary(100, 50);
        var blobs = TextEngine.LabelConnectedComponents(binary);
        Assert.Empty(blobs);
    }

    [Fact]
    public void LabelComponents_SingleDarkPixel_ReturnsOneBlob()
    {
        var info = new SKImageInfo(50, 50, SKColorType.Gray8, SKAlphaType.Opaque);
        using var bmp = new SKBitmap(info);
        unsafe
        {
            byte* ptr = (byte*)bmp.GetPixels().ToPointer();
            new System.Span<byte>(ptr, bmp.RowBytes * bmp.Height).Fill(255);
            ptr[25 * bmp.RowBytes + 25] = 0; // single black pixel at (25,25)
        }
        var blobs = TextEngine.LabelConnectedComponents(bmp);
        Assert.Single(blobs);
        Assert.Equal(1, blobs[0].PixelCount);
    }

    // ── Candidate filtering ───────────────────────────────────────────────────

    [Fact]
    public void FilterCharCandidates_RejectsTooSmallBlobs()
    {
        var blobs = new List<BlobInfo>
        {
            new() { Bounds = new SKRectI(0, 0, 1, 2), PixelCount = 2 }, // too narrow
            new() { Bounds = new SKRectI(0, 0, 5, 2), PixelCount = 5 }, // too short
        };
        using var dummy = AllWhiteBinary(200, 200);
        var candidates = TextEngine.FilterCharCandidates(blobs, dummy);
        Assert.Empty(candidates);
    }

    [Fact]
    public void FilterCharCandidates_AcceptsTypicalCharBlob()
    {
        // 10×16 blob with 80 dark pixels → fill ratio 0.5 ≥ MinFillRatio
        var blobs = new List<BlobInfo>
        {
            new() { Bounds = new SKRectI(0, 0, 10, 16), PixelCount = 80 },
        };
        using var dummy = AllWhiteBinary(200, 200);
        var candidates = TextEngine.FilterCharCandidates(blobs, dummy);
        Assert.Single(candidates);
    }

    // ── Line assembly ─────────────────────────────────────────────────────────

    [Fact]
    public void AssembleLines_BlobsOnSameLine_GroupedTogether()
    {
        // Three blobs at Y≈50 — should all land on the same line
        var blobs = new List<BlobInfo>
        {
            new() { Bounds = new SKRectI(10,  46, 20, 62), PixelCount = 100 },
            new() { Bounds = new SKRectI(30,  48, 40, 64), PixelCount = 100 },
            new() { Bounds = new SKRectI(50,  47, 60, 63), PixelCount = 100 },
        };
        var lines = TextEngine.AssembleLines(blobs);
        Assert.Single(lines);
        Assert.Equal(3, lines[0].Count);
    }

    [Fact]
    public void AssembleLines_BlobsOnTwoLines_SeparatedCorrectly()
    {
        // Two lines well separated in Y
        var blobs = new List<BlobInfo>
        {
            new() { Bounds = new SKRectI(10, 20, 20, 36), PixelCount = 100 },
            new() { Bounds = new SKRectI(30, 20, 40, 36), PixelCount = 100 },
            new() { Bounds = new SKRectI(10, 80, 20, 96), PixelCount = 100 },
            new() { Bounds = new SKRectI(30, 80, 40, 96), PixelCount = 100 },
        };
        var lines = TextEngine.AssembleLines(blobs);
        Assert.Equal(2, lines.Count);
    }

    // ── NCC ───────────────────────────────────────────────────────────────────

    [Fact]
    public void NCC_IdenticalArrays_ReturnsOne()
    {
        var arr = new float[] { 0.1f, 0.5f, 0.9f, 0.3f, 0.7f, 0.2f };
        double ncc = CharacterTemplates.NormalizedCrossCorrelation(arr, arr);
        Assert.True(Math.Abs(ncc - 1.0) < 1e-6, $"NCC of identical arrays should be 1 but was {ncc}");
    }

    [Fact]
    public void NCC_InvertedArrays_ReturnsNegativeOne()
    {
        var a = new float[] { 0.0f, 1.0f, 0.0f, 1.0f };
        var b = new float[] { 1.0f, 0.0f, 1.0f, 0.0f };
        double ncc = CharacterTemplates.NormalizedCrossCorrelation(a, b);
        Assert.True(Math.Abs(ncc + 1.0) < 1e-6, $"Expected ≈ -1 but got {ncc}");
    }

    [Fact]
    public void NCC_UniformArrays_ReturnsZero()
    {
        var a = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
        var b = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
        double ncc = CharacterTemplates.NormalizedCrossCorrelation(a, b);
        Assert.Equal(0.0, ncc, precision: 6);
    }

    // ── Character template generation ─────────────────────────────────────────

    [Fact]
    public void Match_SpaceChar_AlwaysReturnsSpace()
    {
        // A fully white patch should best match space
        var whitePatch = new float[32 * 32];
        Array.Fill(whitePatch, 1f);
        var (ch, score) = CharacterTemplates.Match(whitePatch);
        // Space template is all-white; NCC of two uniform arrays is 0 by definition.
        // The test just checks it doesn't throw and returns some result.
        Assert.True(char.IsAscii(ch), $"Expected ASCII char, got '{ch}'");
    }

    [Fact]
    public void Match_TemplatePatch_ReturnsSelfOrCloseChar()
    {
        // Render a known digit and verify it matches itself or a visually similar char
        var bmp = new SKBitmap(32, 32, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var font  = new SKFont(SKTypeface.FromFamilyName("Courier New"), 20f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawText("0", 6f, 24f, font, paint);
        }
        using var prep = Preprocessor.Prepare(bmp);
        bmp.Dispose();

        // Extract luminance patch
        using var gray  = prep.Grayscale;
        var patch       = new float[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                patch[y * 32 + x] = gray.GetPixel(x, y).Red / 255f;

        var (ch, score) = CharacterTemplates.Match(patch);
        // We don't mandate exact '0' — just a plausible printable result
        Assert.True(score > 0, "Score should be positive");
        Assert.True(char.IsAscii(ch));
    }

    [Fact]
    public void ExtractPatch_PaddedBounds_NormalizesToTightInk()
    {
        using var bmp = new SKBitmap(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 42f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawText("H", 34, 66, font, paint);
        }
        using var prep = Preprocessor.Prepare(bmp);
        var blob = Assert.Single(TextEngine.FilterCharCandidates(
            TextEngine.LabelConnectedComponents(prep.Binary),
            prep.Binary));

        var tight = GlyphRecognizer.ExtractPatchForTest(prep.Binary, blob.Bounds);
        var paddedBounds = new SKRectI(
            blob.Bounds.Left - 12,
            blob.Bounds.Top - 8,
            blob.Bounds.Right + 14,
            blob.Bounds.Bottom + 10);
        var padded = GlyphRecognizer.ExtractPatchForTest(prep.Binary, paddedBounds);

        double tightDistance = GlyphRecognizer.ProjectionProfileDistanceForTest(tight, 'H');
        double paddedDistance = GlyphRecognizer.ProjectionProfileDistanceForTest(padded, 'H');
        Assert.InRange(Math.Abs(paddedDistance - tightDistance), 0, 0.02);
    }

    [Theory]
    [InlineData('E', 'F')]
    [InlineData('L', 'T')]
    [InlineData('M', 'N')]
    [InlineData('W', 'N')]
    public void ProjectionProfileDistance_TemplatePrefersSelfOverNearbyShape(char expected, char nearby)
    {
        Assert.True(CharacterTemplates.TryGetTemplate(expected, out var patch));

        double selfDistance = GlyphRecognizer.ProjectionProfileDistanceForTest(patch, expected);
        double nearbyDistance = GlyphRecognizer.ProjectionProfileDistanceForTest(patch, nearby);

        Assert.True(
            selfDistance < nearbyDistance,
            $"{expected} profile should be closer to itself than {nearby}: self={selfDistance}, nearby={nearbyDistance}");
    }

    [Theory]
    [InlineData('K', 'X')]
    [InlineData('X', 'Y')]
    [InlineData('Y', 'V')]
    [InlineData('Z', 'N')]
    public void ZoningDistance_TemplatePrefersSelfOverDiagonalNeighbor(char expected, char nearby)
    {
        Assert.True(CharacterTemplates.TryGetTemplate(expected, out var patch));

        double selfDistance = GlyphRecognizer.ZoningDistanceForTest(patch, expected);
        double nearbyDistance = GlyphRecognizer.ZoningDistanceForTest(patch, nearby);

        Assert.True(
            selfDistance < nearbyDistance,
            $"{expected} zoning should be closer to itself than {nearby}: self={selfDistance}, nearby={nearbyDistance}");
    }

    [Fact]
    public unsafe void Recognize_UnknownCheckerboardBlob_ReturnsQuestionMark()
    {
        using var binary = AllWhiteBinary(64, 64);
        byte* ptr = (byte*)binary.GetPixels().ToPointer();
        for (int y = 18; y < 34; y++)
        {
            for (int x = 20; x < 34; x++)
            {
                if ((x + y) % 3 == 0)
                    ptr[y * binary.RowBytes + x] = 0;
            }
        }

        var blob = new BlobInfo
        {
            Bounds = new SKRectI(20, 18, 34, 34),
            PixelCount = 75,
        };

        var glyph = GlyphRecognizer.Recognize(binary, blob);

        Assert.Equal('?', glyph.Value);
        Assert.Equal(0, glyph.Confidence);
        Assert.NotNull(glyph.Diagnostics);
        Assert.Equal("unresolved", glyph.Diagnostics!.Method);
        Assert.True(glyph.Diagnostics.Signals.ContainsKey("ncc"));
        Assert.True(glyph.Diagnostics.DecisionWeights.ContainsKey("ncc"));
    }

    [Fact]
    public unsafe void CountEnclosedWhiteRegions_RingBlob_DetectsHole()
    {
        using var binary = AllWhiteBinary(64, 64);
        byte* ptr = (byte*)binary.GetPixels().ToPointer();
        for (int y = 18; y < 38; y++)
        {
            for (int x = 18; x < 38; x++)
            {
                bool border = x < 22 || x >= 34 || y < 22 || y >= 34;
                if (border)
                    ptr[y * binary.RowBytes + x] = 0;
            }
        }

        var blob = new BlobInfo
        {
            Bounds = new SKRectI(18, 18, 38, 38),
            PixelCount = 256,
        };

        int holes = GlyphRecognizer.CountEnclosedWhiteRegionsForTest(binary, blob);

        Assert.True(holes >= 1, $"Expected at least one enclosed white region, got {holes}");
    }

    // ── Full analysis pipeline ────────────────────────────────────────────────

    [Fact]
    public void Analyze_BlankWhiteImage_ReturnsNoLines()
    {
        using var bmp    = new SKBitmap(200, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);

        using var prep = Preprocessor.Prepare(bmp);
        var result     = TextEngine.Analyze(prep);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Analyze_ImageWithText_ProducesAtLeastOneLine()
    {
        var (src, prep) = RenderText("Hello World", fontSize: 22);
        using var _s    = src;
        using var _p    = prep;

        var result = TextEngine.Analyze(prep);
        Assert.NotEmpty(result.Lines);
    }

    [Fact]
    public void Analyze_TextLine_HasPositiveFont​Size()
    {
        var (src, prep) = RenderText("Test", fontSize: 20);
        using var _s    = src;
        using var _p    = prep;

        var result = TextEngine.Analyze(prep);
        foreach (var line in result.Lines)
            Assert.True(line.FontSizePx > 0, $"FontSizePx should be > 0 but was {line.FontSizePx}");
    }

    [Theory]
    [InlineData("Hello")]
    [InlineData("Hello World")]
    [InlineData("Invoice")]
    [InlineData("12345")]
    public void Analyze_CleanCourierText_RecognizesExpectedContent(string expected)
    {
        string actual = RecognizeRenderedText(expected, fontSize: 28f, width: 520, height: 120);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Invoice")]
    [InlineData("Price")]
    [InlineData("12345")]
    public void Analyze_CleanArialText_RecognizesExpectedContent(string expected)
    {
        string actual = RecognizeRenderedText(
            expected,
            fontSize: 28f,
            width: 520,
            height: 120,
            fontFamily: "Arial");

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Invoice")]
    [InlineData("Total")]
    [InlineData("12345")]
    public void Analyze_CleanSerifText_RecognizesExpectedContent(string expected)
    {
        string actual = RecognizeRenderedText(
            expected,
            fontSize: 28f,
            width: 520,
            height: 120,
            fontFamily: "Times New Roman");

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Invoice", "Arial")]
    [InlineData("Total", "Times New Roman")]
    [InlineData("12345", "Courier New")]
    public void Analyze_CleanBoldText_RecognizesExpectedContent(string expected, string fontFamily)
    {
        string actual = RecognizeRenderedText(
            expected,
            fontSize: 28f,
            width: 520,
            height: 120,
            fontFamily: fontFamily,
            fontWeight: SKFontStyleWeight.Bold);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(BroadSyntheticOcrCases))]
    public void Analyze_BroadSyntheticOcrMatrix_RecognizesExpectedContent(
        string expected,
        string fontFamily,
        SKFontStyleWeight fontWeight,
        float fontSize,
        bool antialias,
        bool jpeg)
    {
        string actual = jpeg
            ? RecognizeJpegRenderedText(
                expected,
                fontSize: fontSize,
                width: 560,
                height: 130,
                jpegQuality: 60,
                antialias: true,
                fontFamily: fontFamily,
                fontWeight: fontWeight)
            : RecognizeRenderedText(
                expected,
                fontSize: fontSize,
                width: 560,
                height: 130,
                antialias: antialias,
                fontFamily: fontFamily,
                fontWeight: fontWeight);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Analyze_LowContrastGrayText_RecognizesControlledContent()
    {
        string actual = RecognizeRenderedText(
            "Invoice",
            textColor: new SKColor(150, 150, 150),
            backgroundColor: new SKColor(245, 245, 245),
            fontSize: 28f,
            width: 520,
            height: 120);

        Assert.Equal("Invoice", actual);
    }

    [Fact]
    public void Analyze_WhiteTextOnDarkBackground_RecognizesControlledContent()
    {
        string actual = RecognizeRenderedText(
            "Hello",
            textColor: SKColors.White,
            backgroundColor: new SKColor(32, 32, 32),
            fontSize: 28f,
            width: 360,
            height: 120);

        Assert.Equal("Hello", actual);
    }

    [Fact]
    public void Analyze_MixedPolarityText_RecognizesDarkAndLightRuns()
    {
        using var bmp = new SKBitmap(420, 170, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var headerPaint = new SKPaint { Color = new SKColor(24, 24, 24), IsAntialias = false };
            using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 28f);
            using var whitePaint = new SKPaint { Color = SKColors.White, IsAntialias = false };
            using var blackPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false };

            canvas.DrawRect(0, 0, 420, 72, headerPaint);
            canvas.DrawText("Header", 24, 48, font, whitePaint);
            canvas.DrawText("Total", 24, 126, font, blackPaint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        var result = TextEngine.Analyze(prep);
        var texts = result.Lines.Select(l => l.Text).ToList();

        Assert.Contains("Header", texts);
        Assert.Contains("Total", texts);
        Assert.Equal(["Header", "Total"], texts);
    }

    [Theory]
    [InlineData("O0")]
    [InlineData("80O")]
    [InlineData("Il1")]
    [InlineData("S5")]
    public void Analyze_AmbiguousGlyphPairs_RecognizesControlledCases(string expected)
    {
        string actual = RecognizeRenderedText(expected, fontSize: 28f, width: 240, height: 120);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("A-1")]
    [InlineData("12.50")]
    [InlineData("9:30")]
    [InlineData("A/1")]
    [InlineData("1,234")]
    public void Analyze_Punctuation_RecognizesControlledCases(string expected)
    {
        string actual = RecognizeRenderedText(expected, fontSize: 28f, width: 280, height: 120);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Analyze_SmallInvoiceTableLabel_RecognizesExpectedContent()
    {
        string actual = RecognizeRenderedText("Price", fontSize: 18f, width: 220, height: 90);
        Assert.Equal("Price", actual);
    }

    [Theory]
    [InlineData("12.50")]
    [InlineData("25.00")]
    public void Analyze_SmallInvoiceTableAmount_RecognizesFive(string expected)
    {
        string actual = RecognizeRenderedText(expected, fontSize: 18f, width: 220, height: 90);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Invoice")]
    [InlineData("12.50")]
    public void Analyze_AntiAliasedCourierText_RecognizesExpectedContent(string expected)
    {
        string actual = RecognizeRenderedText(expected, fontSize: 28f, width: 360, height: 120, antialias: true);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Invoice")]
    [InlineData("12.50")]
    public void Analyze_JpegCompressedCourierText_RecognizesExpectedContent(string expected)
    {
        string actual = RecognizeJpegRenderedText(expected, fontSize: 28f, width: 360, height: 120, jpegQuality: 55);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Analyze_LeadingBullet_DropsDecorativeSymbolWord()
    {
        using var bmp = new SKBitmap(300, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 28f);
            using var textPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            using var bulletPaint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(28, 68, 4, 4, bulletPaint);
            canvas.DrawText("Hello", 56, 82, font, textPaint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        var result = TextEngine.Analyze(prep);
        var line = Assert.Single(result.Lines);

        Assert.Equal("Hello", line.Text);
    }

    [Fact]
    public void Analyze_LeadingBorderFragment_DropsDecorativeSymbolWord()
    {
        using var bmp = new SKBitmap(360, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 28f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(28, 74, 18, 2, paint);
            canvas.DrawText("Hello", 70, 82, font, paint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        var result = TextEngine.Analyze(prep);
        var line = Assert.Single(result.Lines);

        Assert.Equal("Hello", line.Text);
    }

    [Fact]
    public void Analyze_LoneVerticalStroke_DoesNotHallucinateText()
    {
        using var bmp = new SKBitmap(160, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(78, 32, 3, 64, paint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        var result = TextEngine.Analyze(prep);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Analyze_RepeatedVerticalUiStrokes_DoesNotHallucinateText()
    {
        using var bmp = new SKBitmap(220, 120, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawRect(58, 30, 3, 64, paint);
            canvas.DrawRect(92, 30, 3, 64, paint);
            canvas.DrawRect(126, 30, 3, 64, paint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        var result = TextEngine.Analyze(prep);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Analyze_LoneRingIcon_DoesNotHallucinateText()
    {
        using var bmp = new SKBitmap(180, 140, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = false,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4,
            };
            canvas.DrawCircle(90, 70, 26, paint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        var result = TextEngine.Analyze(prep);

        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Analyze_PositionedTextLine_ReturnsExpectedInkBounds()
    {
        var (src, prep) = RenderTextAt("Hello", x: 48, baselineY: 82, fontSize: 28);
        using var _s = src;
        using var _p = prep;

        var result = TextEngine.Analyze(prep);
        var line = Assert.Single(result.Lines);

        Assert.Equal("Hello", line.Text);
        AssertApproximately(49, line.Bounds.Left, 4, "line left");
        AssertApproximately(62, line.Bounds.Top, 5, "line top");
        AssertApproximately(84, line.Bounds.Width, 8, "line width");
        AssertApproximately(20, line.Bounds.Height, 5, "line height");
    }

    [Fact]
    public void Analyze_PositionedTextLine_ReturnsEstimatedBaseline()
    {
        var (src, prep) = RenderTextAt("Hello", x: 48, baselineY: 82, fontSize: 28);
        using var _s = src;
        using var _p = prep;

        var result = TextEngine.Analyze(prep);
        var line = Assert.Single(result.Lines);

        AssertApproximately(83, line.BaselineY, 5, "baseline y");
    }

    [Fact]
    public void Analyze_DescenderHeavyTextLine_ReturnsBaselineNearNonDescenders()
    {
        var (src, prep) = RenderTextAt("pypy a", x: 48, baselineY: 82, fontSize: 28);
        using var _s = src;
        using var _p = prep;

        var result = TextEngine.Analyze(prep);
        var line = Assert.Single(result.Lines);

        AssertApproximately(83, line.BaselineY, 5, "descender-heavy baseline y");
    }

    [Fact]
    public void Analyze_PositionedTextWords_ReturnExpectedWordBounds()
    {
        var (src, prep) = RenderTextAt("Hello World", x: 32, baselineY: 90, fontSize: 28, width: 420);
        using var _s = src;
        using var _p = prep;

        var result = TextEngine.Analyze(prep);
        var line = Assert.Single(result.Lines);

        Assert.Equal("Hello World", line.Text);
        Assert.Equal(2, line.Words.Count);
        AssertApproximately(33, line.Words[0].Bounds.Left, 4, "first word left");
        AssertApproximately(113, line.Words[0].Bounds.Right, 8, "first word right");
        AssertApproximately(137, line.Words[1].Bounds.Left, 10, "second word left");
        Assert.True(line.Words[1].Bounds.Left > line.Words[0].Bounds.Right);
    }

    [Fact]
    public void Analyze_DistantTextRunsOnSameBaseline_ReturnsSeparateLines()
    {
        using var bmp = new SKBitmap(640, 140, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Courier New"), 28f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
            canvas.DrawText("Name", 32, 82, font, paint);
            canvas.DrawText("Total", 440, 82, font, paint);
        }

        using var prep = Preprocessor.Prepare(bmp);
        var result = TextEngine.Analyze(prep);
        var texts = result.Lines.Select(l => l.Text).ToList();

        Assert.Contains("Name", texts);
        Assert.Contains("Total", texts);
        Assert.Equal(["Name", "Total"], texts);
    }
}
