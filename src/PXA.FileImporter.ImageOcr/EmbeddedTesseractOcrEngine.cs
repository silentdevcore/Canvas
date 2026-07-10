using Tesseract;

namespace PXA.FileImporter.ImageOcr;

public sealed class EmbeddedTesseractOcrEngine : IOcrEngine
{
    private readonly string _tessDataPath;
    private readonly string? _nativeLibraryPath;

    public EmbeddedTesseractOcrEngine(string? tessDataPath = null, string? nativeLibraryPath = null)
    {
        _tessDataPath = string.IsNullOrWhiteSpace(tessDataPath)
            ? Path.Combine(AppContext.BaseDirectory, "tessdata")
            : tessDataPath;
        var bundledNativePath = Path.Combine(AppContext.BaseDirectory, "native");
        _nativeLibraryPath = string.IsNullOrWhiteSpace(nativeLibraryPath) && Directory.Exists(bundledNativePath)
            ? bundledNativePath
            : nativeLibraryPath;
    }

    public string Name => "Tesseract";

    public string Version => "5.2.0";

    public Task<IReadOnlyList<OcrPage>> RecognizeAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(options);
        EnsureLanguageDataExists(options.Languages);

        var timeout = TimeSpan.FromSeconds(Math.Clamp(options.MaxOcrRuntimeSeconds, 5, 180));
        return RecognizeCoreAsync(pages, options, timeout, cancellationToken);
    }

    private async Task<IReadOnlyList<OcrPage>> RecognizeCoreAsync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await Task
                .Run(() => RecognizeSync(pages, options, timeoutCts.Token), CancellationToken.None)
                .WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            throw new InvalidOperationException(
                $"OCR did not finish within {Math.Round(timeout.TotalSeconds)} seconds. Try enabling preprocessing, using a smaller scan, or selecting fewer OCR languages.",
                ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"OCR did not finish within {Math.Round(timeout.TotalSeconds)} seconds. Try enabling preprocessing, using a smaller scan, or selecting fewer OCR languages.",
                ex);
        }
    }

    private IReadOnlyList<OcrPage> RecognizeSync(
        IReadOnlyList<OcrImagePage> pages,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken)
    {
        var result = new List<OcrPage>();
        var nativePath = options.NativeLibraryPath ?? _nativeLibraryPath;

        try
        {
            if (!string.IsNullOrWhiteSpace(nativePath))
                TesseractEnviornment.CustomSearchPath = nativePath;

            using var engine = new TesseractEngine(_tessDataPath, options.Languages, EngineMode.Default);

            foreach (var imagePage in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var pix = Pix.LoadFromMemory(imagePage.EncodedImageBytes);
                using var page = engine.Process(pix);
                result.Add(ReadPage(imagePage, page));
            }
        }
        catch (DllNotFoundException ex)
        {
            throw BuildNativeDependencyException(ex);
        }
        catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException dllEx)
        {
            throw BuildNativeDependencyException(dllEx);
        }

        return result;
    }

    private static OcrNativeDependencyMissingException BuildNativeDependencyException(Exception ex)
    {
        return new OcrNativeDependencyMissingException(
            "OCR native binaries could not be loaded. Bundle matching Tesseract and Leptonica native libraries with the app, or configure Ocr:NativeLibraryPath to an app-owned native library folder.",
            ex);
    }

    private void EnsureLanguageDataExists(string languages)
    {
        if (!Directory.Exists(_tessDataPath))
        {
            throw new OcrLanguageDataMissingException(
                $"OCR language data folder was not found: '{_tessDataPath}'. Bundle tessdata files with the app deployment.");
        }

        var missing = languages
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(lang => !File.Exists(Path.Combine(_tessDataPath, $"{lang}.traineddata")))
            .ToList();

        if (missing.Count > 0)
        {
            throw new OcrLanguageDataMissingException(
                $"Missing OCR language data: {string.Join(", ", missing)}. Expected .traineddata files in '{_tessDataPath}'.");
        }
    }

    private static OcrPage ReadPage(OcrImagePage imagePage, Page page)
    {
        var words = new List<OcrWord>();
        using var iterator = page.GetIterator();
        iterator.Begin();

        do
        {
            var wordText = CleanText(iterator.GetText(PageIteratorLevel.Word));
            if (string.IsNullOrWhiteSpace(wordText))
                continue;

            if (!iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var wordRect))
                continue;

            words.Add(new OcrWord
            {
                Text = wordText,
                Bounds = ToBounds(wordRect),
                Confidence = ClampConfidence(iterator.GetConfidence(PageIteratorLevel.Word)),
            });
        }
        while (iterator.Next(PageIteratorLevel.Word));

        var lines = GroupWordsIntoLines(words);

        var bounds = Union(lines.Select(l => l.Bounds), imagePage.WidthPx, imagePage.HeightPx);
        var avg = lines.Count == 0 ? 0 : lines.Average(l => l.Confidence);

        return new OcrPage
        {
            PageIndex = imagePage.PageIndex,
            WidthPx = imagePage.WidthPx,
            HeightPx = imagePage.HeightPx,
            Confidence = Math.Round(avg, 4),
            Blocks =
            [
                new OcrBlock
                {
                    Bounds = bounds,
                    Confidence = Math.Round(avg, 4),
                    Lines = lines,
                }
            ],
        };
    }

    private static List<OcrLine> GroupWordsIntoLines(IReadOnlyList<OcrWord> words)
    {
        var lines = new List<List<OcrWord>>();

        foreach (var word in words.OrderBy(w => w.Bounds.Y).ThenBy(w => w.Bounds.X))
        {
            var matched = lines.FirstOrDefault(line => BelongsToLine(word, line));
            if (matched is null)
                lines.Add([word]);
            else
                matched.Add(word);
        }

        return lines
            .Select(line =>
            {
                var ordered = line.OrderBy(w => w.Bounds.X).ToList();
                var bounds = Union(ordered.Select(w => w.Bounds), 0, 0);
                return new OcrLine
                {
                    Text = string.Join(" ", ordered.Select(w => w.Text)),
                    Bounds = bounds,
                    Confidence = Math.Round(ordered.Average(w => w.Confidence), 4),
                    Words = ordered,
                };
            })
            .OrderBy(l => l.Bounds.Y)
            .ThenBy(l => l.Bounds.X)
            .ToList();
    }

    private static bool BelongsToLine(OcrWord word, IReadOnlyList<OcrWord> line)
    {
        var lineTop = line.Min(w => w.Bounds.Y);
        var lineBottom = line.Max(w => w.Bounds.Y + w.Bounds.Height);
        var wordCenter = word.Bounds.Y + word.Bounds.Height / 2.0;
        var medianHeight = line.Select(w => w.Bounds.Height).Order().ElementAt(line.Count / 2);

        return wordCenter >= lineTop - medianHeight * 0.35
            && wordCenter <= lineBottom + medianHeight * 0.35;
    }

    private static string CleanText(string? value) =>
        string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static double ClampConfidence(float confidence)
    {
        var normalized = confidence > 1 ? confidence / 100.0 : confidence;
        return Math.Round(Math.Clamp(normalized, 0, 1), 4);
    }

    private static OcrBoundingBox ToBounds(Rect rect) =>
        new(rect.X1, rect.Y1, Math.Max(0, rect.X2 - rect.X1), Math.Max(0, rect.Y2 - rect.Y1));

    private static OcrBoundingBox Union(IEnumerable<OcrBoundingBox> boxes, int width, int height)
    {
        var list = boxes.ToList();
        if (list.Count == 0)
            return new OcrBoundingBox(0, 0, width, height);

        var left = list.Min(b => b.X);
        var top = list.Min(b => b.Y);
        var right = list.Max(b => b.X + b.Width);
        var bottom = list.Max(b => b.Y + b.Height);
        return new OcrBoundingBox(left, top, right - left, bottom - top);
    }
}
