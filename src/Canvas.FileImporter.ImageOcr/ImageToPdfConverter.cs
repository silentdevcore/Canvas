using Canvas.Core.Contracts;
using SkiaSharp;
using System.Diagnostics;

namespace Canvas.FileImporter.ImageOcr;

public sealed class ImageToPdfConverter
{
    private const double DefaultDpi = 300;

    private readonly IOcrEngine _ocrEngine;

    public ImageToPdfConverter(IOcrEngine ocrEngine)
    {
        _ocrEngine = ocrEngine;
    }

    public async Task<ImageToPdfConversionResult> ConvertAsync(
        Stream stream,
        string? fileName,
        ImageToPdfConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        var memoryBefore = GC.GetTotalMemory(false);
        var stopwatch = Stopwatch.StartNew();
        var raw = await ReadAllBytesAsync(stream, cancellationToken);
        if (raw.LongLength > options.MaxFileBytes)
            throw new InvalidOperationException($"Image file is too large. Maximum allowed size is {options.MaxFileBytes} bytes.");

        var sourceName = string.IsNullOrWhiteSpace(fileName) ? "image-ocr" : Path.GetFileNameWithoutExtension(fileName);

        using var codec = SKCodec.Create(new SKMemoryStream(raw))
            ?? throw new InvalidOperationException("Unable to decode image. The file is unsupported or corrupt.");

        using var decoded = SKBitmap.Decode(codec)
            ?? throw new InvalidOperationException("Unable to decode image. The file is unsupported or corrupt.");

        using var bitmap = ApplyOrientation(decoded, codec.EncodedOrigin);
        var pixelCount = (long)bitmap.Width * bitmap.Height;
        if (pixelCount > options.MaxPixels)
            throw new InvalidOperationException($"Image pixel count is too large. Maximum allowed pixel count is {options.MaxPixels}.");

        var (dataUri, encodedForOcr) = EncodeImage(bitmap);

        var dpiX = options.SourceDpiX is > 0 ? options.SourceDpiX.Value : DefaultDpi;
        var dpiY = options.SourceDpiY is > 0 ? options.SourceDpiY.Value : DefaultDpi;
        var (pageWidth, pageHeight) = ResolvePageSize(bitmap.Width, bitmap.Height, dpiX, dpiY, options);

        IReadOnlyList<OcrPage> ocrPages;
        try
        {
            ocrPages = await _ocrEngine.RecognizeAsync(
                [new OcrImagePage(0, bitmap.Width, bitmap.Height, encodedForOcr)],
                options,
                cancellationToken);
        }
        catch (DllNotFoundException ex)
        {
            throw new OcrNativeDependencyMissingException(
                "OCR native binaries could not be loaded. Bundle matching Tesseract and Leptonica native libraries with the app, or configure Ocr:NativeLibraryPath to an app-owned native library folder.",
                ex);
        }
        catch (TypeInitializationException ex) when (ex.InnerException is DllNotFoundException dllEx)
        {
            throw new OcrNativeDependencyMissingException(
                "OCR native binaries could not be loaded. Bundle matching Tesseract and Leptonica native libraries with the app, or configure Ocr:NativeLibraryPath to an app-owned native library folder.",
                dllEx);
        }

        var design = BuildDesign(sourceName, dataUri, bitmap.Width, bitmap.Height, pageWidth, pageHeight, ocrPages, options);
        stopwatch.Stop();
        var memoryAfter = GC.GetTotalMemory(false);

        var words = ocrPages.SelectMany(p => p.Blocks).SelectMany(b => b.Lines).SelectMany(l => l.Words).ToList();
        var lines = ocrPages.SelectMany(p => p.Blocks).SelectMany(b => b.Lines).ToList();
        var lowConfidenceWords = words.Count(w => w.Confidence < options.LowConfidenceThreshold);
        var warnings = BuildWarnings(dpiX, dpiY, words, lowConfidenceWords, options);

        return new ImageToPdfConversionResult
        {
            Design = design,
            OcrPages = ocrPages,
            Warnings = warnings,
            Diagnostics = new ImageToPdfDiagnostics
            {
                SourceWidthPx = bitmap.Width,
                SourceHeightPx = bitmap.Height,
                EffectiveDpiX = Math.Round(dpiX, 2),
                EffectiveDpiY = Math.Round(dpiY, 2),
                PageWidthPt = Math.Round(pageWidth, 2),
                PageHeightPt = Math.Round(pageHeight, 2),
                PageCount = ocrPages.Count,
                OcrEngine = _ocrEngine.Name,
                OcrEngineVersion = _ocrEngine.Version,
                Languages = options.Languages,
                WordCount = words.Count,
                LineCount = lines.Count,
                AverageConfidence = words.Count == 0 ? 0 : Math.Round(words.Average(w => w.Confidence), 4),
                LowConfidenceWordCount = lowConfidenceWords,
                RuntimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                MemoryDeltaBytes = memoryAfter - memoryBefore,
            },
            DebugOverlayPng = options.IncludeDebugOverlay
                ? OcrDebugOverlayRenderer.Render(bitmap, ocrPages)
                : null,
        };
    }

    private static (double Width, double Height) ResolvePageSize(
        int imageWidthPx,
        int imageHeightPx,
        double dpiX,
        double dpiY,
        ImageToPdfConversionOptions options)
    {
        if (options.PageWidthPt is > 0 && options.PageHeightPt is > 0)
            return (options.PageWidthPt.Value, options.PageHeightPt.Value);

        if (string.Equals(options.PageSizingMode, "a4-fit", StringComparison.OrdinalIgnoreCase))
            return imageWidthPx >= imageHeightPx ? (842, 595) : (595, 842);

        return (imageWidthPx / dpiX * 72.0, imageHeightPx / dpiY * 72.0);
    }

    private static DesignExportDto BuildDesign(
        string name,
        string dataUri,
        int imageWidthPx,
        int imageHeightPx,
        double pageWidth,
        double pageHeight,
        IReadOnlyList<OcrPage> ocrPages,
        ImageToPdfConversionOptions options)
    {
        var elements = new List<ElementDto>();

        if (options.IncludeBackgroundImage)
        {
            elements.Add(new ElementDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "image",
                Name = "Original image background",
                X = 0,
                Y = 0,
                Width = pageWidth,
                Height = pageHeight,
                Content = dataUri,
                FitMode = "fill",
                Locked = true,
                Style = new Dictionary<string, object>
                {
                    ["imageOcrRole"] = "background",
                },
            });
        }

        foreach (var line in ocrPages.SelectMany(p => p.Blocks).SelectMany(b => b.Lines))
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            var x = line.Bounds.X / (double)imageWidthPx * pageWidth;
            var y = line.Bounds.Y / (double)imageHeightPx * pageHeight;
            var width = Math.Max(1, line.Bounds.Width / (double)imageWidthPx * pageWidth);
            var height = Math.Max(1, line.Bounds.Height / (double)imageHeightPx * pageHeight);
            var fontSize = Math.Clamp(height * 0.78, 6, 72);

            elements.Add(new ElementDto
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "text",
                Name = "OCR text",
                X = Math.Round(x, 2),
                Y = Math.Round(y, 2),
                Width = Math.Round(width, 2),
                Height = Math.Round(height, 2),
                Content = line.Text,
                Style = new Dictionary<string, object>
                {
                    ["fontSize"] = Math.Round(fontSize, 2),
                    ["color"] = "#111827",
                    ["imageOcrConfidence"] = line.Confidence,
                    ["sourceBoundsPx"] = $"{line.Bounds.X},{line.Bounds.Y},{line.Bounds.Width},{line.Bounds.Height}",
                },
            });
        }

        return new DesignExportDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Pages =
            [
                new PageDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Elements = elements,
                }
            ],
            PageSettings = new PageSettingsDto
            {
                Width = Math.Round(pageWidth, 2),
                Height = Math.Round(pageHeight, 2),
                Orientation = pageWidth > pageHeight ? "landscape" : "portrait",
                Unit = "pt",
                Metadata = new PdfMetadataDto
                {
                    Title = name,
                    Subject = "Converted with Canvas Image OCR Converter",
                },
            },
        };
    }

    private static IReadOnlyList<string> BuildWarnings(
        double dpiX,
        double dpiY,
        IReadOnlyList<OcrWord> words,
        int lowConfidenceWords,
        ImageToPdfConversionOptions options)
    {
        var warnings = new List<string>();
        if (dpiX < 150 || dpiY < 150)
            warnings.Add("Input DPI is low; OCR accuracy may be reduced.");
        if (words.Count == 0)
            warnings.Add("No OCR words were detected.");
        if (lowConfidenceWords > 0)
            warnings.Add($"{lowConfidenceWords} OCR words are below the configured confidence threshold.");
        if (!options.IncludeBackgroundImage)
            warnings.Add("Background image layer is disabled; visual fidelity depends entirely on reconstructed elements.");
        return warnings;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static (string DataUri, byte[] EncodedBytes) EncodeImage(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();
        return ($"data:image/png;base64,{Convert.ToBase64String(bytes)}", bytes);
    }

    private static SKBitmap ApplyOrientation(SKBitmap src, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return src.Copy();

        var swap = origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

        var dstW = swap ? src.Height : src.Width;
        var dstH = swap ? src.Width : src.Height;
        var dst = new SKBitmap(dstW, dstH, src.ColorType, src.AlphaType);
        using var canvas = new SKCanvas(dst);

        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(dstW, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(dstW, dstH);
                canvas.Scale(-1, -1);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, dstH);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(dstW, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(dstW, dstH);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, dstH);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(src, 0, 0);
        canvas.Flush();
        return dst;
    }
}
