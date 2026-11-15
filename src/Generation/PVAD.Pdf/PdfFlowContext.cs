namespace PVAD.Pdf;

public sealed class PdfFlowContext
{
    private readonly PdfDocument _document;
    private readonly PdfFlowOptions _options;

    internal PdfFlowContext(PdfDocument document, PdfFlowOptions options)
    {
        _document = document;
        _options = options;
        CurrentPage = _document.AddPage(_options.PageWidth, _options.PageHeight);
        CurrentY = CurrentPage.Height - _options.MarginTop;
    }

    public PdfPage CurrentPage { get; private set; }

    public double CurrentY { get; private set; }

    public double ContentWidth => CurrentPage.Width - _options.MarginLeft - _options.MarginRight;

    public PdfPage NextPage()
    {
        CurrentPage = _document.AddPage(_options.PageWidth, _options.PageHeight);
        CurrentY = CurrentPage.Height - _options.MarginTop;
        return CurrentPage;
    }

    public void AddBookmark(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Bookmark title cannot be null or empty.", nameof(title));
        }

        var pageNumber = GetCurrentPageNumber();
        _document.AddBookmark(title, pageNumber);
    }

    public void AddNamedDestination(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Destination name cannot be null or empty.", nameof(name));
        }

        var pageNumber = GetCurrentPageNumber();
        _document.AddNamedDestination(name, pageNumber, CurrentY);
    }

    public void AddTextLine(string text, PdfDrawTextOptions? options = null)
    {
        options ??= PdfDrawTextOptions.Default;

        if (_options.KeepHeadingsWithNextBlock && options.FontSize >= _options.HeadingMinFontSize)
        {
            EnsureSpace(options.FontSize + _options.LineSpacing + _options.KeepWithNextRequiredSpace);
        }

        EnsureSpace(options.FontSize);

        CurrentPage.DrawText(text, _options.MarginLeft, CurrentY, options);
        CurrentY -= options.FontSize + _options.LineSpacing;
    }

    public PdfParagraphLayoutResult AddParagraph(string text, PdfParagraphOptions? options = null)
    {
        options ??= PdfParagraphOptions.Default;

        if (_options.KeepParagraphsWithNextBlock)
        {
            EnsureSpace(options.FontSize + _options.KeepParagraphWithNextRequiredSpace);
        }

        EnsureSpace(options.FontSize);

        var maxWidth = CurrentPage.Width - _options.MarginLeft - _options.MarginRight;
        var result = CurrentPage.DrawParagraph(text, _options.MarginLeft, CurrentY, maxWidth, options);

        CurrentY = result.BottomY - _options.ParagraphSpacing;

        if (CurrentY < _options.MarginBottom)
        {
            NextPage();
        }

        return result;
    }

    public PdfTableLayoutResult AddSimpleTable(IReadOnlyList<IReadOnlyList<string>> rows, PdfTableOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            throw new ArgumentException("Table requires at least one row.", nameof(rows));
        }

        options ??= PdfTableOptions.Default;

        var width = ContentWidth;
        var topY = CurrentY;
        var heights = CurrentPage.MeasureSimpleTableRows(width, rows, options);
        var chunks = PlanTableChunks(heights, options);
        PdfTableLayoutResult? lastChunk = null;
        var totalHeight = 0d;
        foreach (var chunk in chunks)
        {
            if (chunk.NewPage) NextPage();
            if (options.ShowContinuationMarkers && lastChunk is not null)
            {
                DrawTableContinuation(options.ContinuationHeaderText, options);
            }
            var chunkRows = chunk.Indices.Select(index => rows[index]).ToArray();
            var chunkOptions = SliceTableOptions(options, chunk, heights);
            lastChunk = CurrentPage.DrawSimpleTable(_options.MarginLeft, CurrentY, width, chunkRows, chunkOptions);
            totalHeight += lastChunk.Height;
            CurrentY = lastChunk.BottomY - _options.ParagraphSpacing;
            if (options.ShowContinuationMarkers && !chunk.IsFinal)
                DrawTableContinuation(options.ContinuationFooterText, options);
        }

        return new PdfTableLayoutResult
        {
            X = _options.MarginLeft, TopY = topY, Width = width, Height = totalHeight,
            BottomY = lastChunk!.BottomY, RowCount = rows.Count, ColumnCount = rows[0].Count
        };
    }

    private sealed record TableChunk(int[] Indices, int DataOffset, bool NewPage, bool IsFinal, bool HasFooter);

    private List<TableChunk> PlanTableChunks(double[] heights, PdfTableOptions options)
    {
        if (heights.Any(height => !double.IsFinite(height) || height <= 0))
            throw new ArgumentOutOfRangeException(nameof(options), "Measured table row heights must be finite and positive.");
        if (options.HasHeaderRow && options.HasFooterRow && heights.Length < 2)
            throw new ArgumentException("Header and footer require separate rows.", nameof(options));
        var freshHeight = _options.PageHeight - _options.MarginTop - _options.MarginBottom;
        var available = CurrentY - _options.MarginBottom;
        var marker = options.ShowContinuationMarkers ? options.ContinuationFontSize + _options.LineSpacing : 0;
        if (!double.IsFinite(freshHeight) || freshHeight <= 0 || !double.IsFinite(available)
            || !double.IsFinite(marker) || marker < 0 || options.ShowContinuationMarkers && options.ContinuationFontSize <= 0
            || !double.IsFinite(_options.ParagraphSpacing) || _options.ParagraphSpacing < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Flow table margins, spacing and continuation size must define a finite printable area.");
        var headerHeight = options.HasHeaderRow ? heights[0] : 0;
        var footerHeight = options.HasFooterRow ? heights[^1] : 0;
        var start = options.HasHeaderRow ? 1 : 0;
        var end = heights.Length - (options.HasFooterRow ? 1 : 0);
        var chunks = new List<TableChunk>();
        var newPage = false;

        // Plan every chunk before painting: an oversized row must not leave a partial table.
        while (start < end || chunks.Count == 0)
        {
            var headerMarker = chunks.Count > 0 ? marker : 0;
            var bestEnd = -1;
            var dataHeight = 0d;
            if (start == end && headerHeight + footerHeight + headerMarker <= available)
                bestEnd = end;
            for (var candidate = start; candidate < end; candidate++)
            {
                dataHeight += heights[candidate];
                var isFinal = candidate + 1 == end;
                var total = headerMarker + headerHeight + dataHeight
                    + (options.HasFooterRow && (options.RepeatFooterOnEachChunk || isFinal) ? footerHeight : 0)
                    + (!isFinal && options.ShowContinuationMarkers ? _options.ParagraphSpacing + marker : 0);
                if (total <= available) bestEnd = candidate + 1;
                if (headerMarker + headerHeight + dataHeight > available) break;
            }
            if (bestEnd < 0)
            {
                if (newPage || available >= freshHeight)
                    throw new InvalidOperationException($"Flow table row {start} cannot fit on a fresh page with its header, footer and continuation markers. Reduce its content, font size or padding, or increase the page size.");
                available = freshHeight;
                newPage = true;
                continue;
            }
            var final = bestEnd == end;
            var footer = options.HasFooterRow && (options.RepeatFooterOnEachChunk || final);
            var indices = new List<int>();
            if (options.HasHeaderRow) indices.Add(0);
            for (var index = start; index < bestEnd; index++) indices.Add(index);
            if (footer) indices.Add(heights.Length - 1);
            chunks.Add(new TableChunk(indices.ToArray(), start - (options.HasHeaderRow ? 1 : 0), newPage, final, footer));
            if (final) break;
            start = bestEnd;
            newPage = true;
            available = freshHeight;
        }
        return chunks;
    }

    private void DrawTableContinuation(string text, PdfTableOptions options)
    {
        CurrentPage.DrawText(text, _options.MarginLeft, CurrentY, new PdfDrawTextOptions
        {
            FontSize = options.ContinuationFontSize, FontFamily = options.FontFamily,
            FillColor = options.TextColor ?? PdfColor.Gray
        });
        CurrentY -= options.ContinuationFontSize + _options.LineSpacing;
    }

    private static PdfTableOptions SliceTableOptions(PdfTableOptions options, TableChunk chunk, double[] heights)
    {
        var rowMap = chunk.Indices.Select((original, local) => (original, local))
            .ToDictionary(pair => pair.original, pair => pair.local);
        return new PdfTableOptions
        {
            RowHeight = options.RowHeight,
            AutoRowHeight = false,
            CellPadding = options.CellPadding,
            CellPaddingLeft = options.CellPaddingLeft,
            CellPaddingRight = options.CellPaddingRight,
            CellPaddingTop = options.CellPaddingTop,
            CellPaddingBottom = options.CellPaddingBottom,
            HasHeaderRow = options.HasHeaderRow,
            HasFooterRow = chunk.HasFooter,
            RepeatFooterOnEachChunk = options.RepeatFooterOnEachChunk,
            RowHeights = chunk.Indices.Select(index => heights[index]).ToArray(),
            RowLineHeights = options.RowLineHeights is null ? null : chunk.Indices.Select(index => options.RowLineHeights[index]).ToArray(),
            CellStyles = options.CellStyles?.Where(cell => rowMap.ContainsKey(cell.Row))
                .Select(cell => cell.AtRow(rowMap[cell.Row])).ToArray(),
            DataRowOffset = options.DataRowOffset + chunk.DataOffset,
            ColumnWidths = options.ColumnWidths,
            MinColumnWidth = options.MinColumnWidth,
            MaxColumnWidth = options.MaxColumnWidth,
            ColumnAlignments = options.ColumnAlignments,
            ColumnVerticalAlignments = options.ColumnVerticalAlignments,
            ColumnValueFormatters = options.ColumnValueFormatters,
            CellAlignment = options.CellAlignment,
            CellVerticalAlignment = options.CellVerticalAlignment,
            WrapCellText = options.WrapCellText,
            FontSize = options.FontSize,
            CellLineHeight = options.CellLineHeight,
            Font = options.Font,
            FontFamily = options.FontFamily,
            Bold = options.Bold,
            Italic = options.Italic,
            TextColor = options.TextColor,
            HeaderFillColor = options.HeaderFillColor,
            FooterFillColor = options.FooterFillColor,
            AlternateRowFillColor = options.AlternateRowFillColor,
            CellCornerRadius = options.CellCornerRadius,
            BorderColor = options.BorderColor,
            BorderLineWidth = options.BorderLineWidth,
            DrawOuterBorder = options.DrawOuterBorder,
            DrawInnerVerticalBorders = options.DrawInnerVerticalBorders,
            DrawInnerHorizontalBorders = options.DrawInnerHorizontalBorders,
            OuterBorderColor = options.OuterBorderColor,
            InnerBorderColor = options.InnerBorderColor,
            OuterBorderLineWidth = options.OuterBorderLineWidth,
            InnerBorderLineWidth = options.InnerBorderLineWidth,
            OuterBorderStrokeStyle = options.OuterBorderStrokeStyle,
            InnerBorderStrokeStyle = options.InnerBorderStrokeStyle
        };
    }

    public PdfImageLayoutResult AddImage(string imagePath, double width, double height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be greater than zero.");
        }

        EnsureSpace(height);

        var x = _options.MarginLeft;
        var y = CurrentY - height;
        CurrentPage.DrawImage(imagePath, x, y, width, height);

        CurrentY = y - _options.ParagraphSpacing;

        if (CurrentY < _options.MarginBottom)
        {
            NextPage();
        }

        return new PdfImageLayoutResult
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }

    public PdfImageLayoutResult AddImage(string imagePath, double width, double height, double opacity)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be greater than zero.");
        }

        if (opacity < 0 || opacity > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        EnsureSpace(height);

        var x = _options.MarginLeft;
        var y = CurrentY - height;
        CurrentPage.DrawImage(imagePath, x, y, width, height, opacity);

        CurrentY = y - _options.ParagraphSpacing;

        if (CurrentY < _options.MarginBottom)
        {
            NextPage();
        }

        return new PdfImageLayoutResult
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }

    public PdfImageLayoutResult AddImageScaledToContentWidth(string imagePath, double? maxHeight = null)
    {
        if (maxHeight is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "Max height must be greater than zero when provided.");
        }

        var image = PdfImageReader.Read(imagePath);
        var contentWidth = CurrentPage.Width - _options.MarginLeft - _options.MarginRight;
        var width = contentWidth;
        var height = width * image.Height / image.Width;

        if (maxHeight is { } constrainedHeight && height > constrainedHeight)
        {
            var scale = constrainedHeight / height;
            height = constrainedHeight;
            width *= scale;
        }

        EnsureSpace(height);
        var x = _options.MarginLeft + ((ContentWidth - width) / 2);
        var y = CurrentY - height;
        CurrentPage.DrawImage(imagePath, x, y, width, height);

        CurrentY = y - _options.ParagraphSpacing;

        if (CurrentY < _options.MarginBottom)
        {
            NextPage();
        }

        return new PdfImageLayoutResult
        {
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }

    public PdfImageLayoutResult AddImageFit(string imagePath, double maxHeight, PdfImageFitMode fitMode = PdfImageFitMode.Contain)
    {
        if (maxHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "Max height must be greater than zero.");
        }

        var image = PdfImageReader.Read(imagePath);
        var targetWidth = ContentWidth;
        var targetHeight = maxHeight;
        var imageAspect = image.Width / image.Height;
        var boxAspect = targetWidth / targetHeight;

        var drawWidth = targetWidth;
        var drawHeight = targetHeight;
        var drawX = _options.MarginLeft;
        var drawY = CurrentY - targetHeight;

        if (fitMode == PdfImageFitMode.Contain)
        {
            if (imageAspect > boxAspect)
            {
                drawHeight = targetWidth / imageAspect;
                drawY += (targetHeight - drawHeight) / 2;
            }
            else
            {
                drawWidth = targetHeight * imageAspect;
                drawX += (targetWidth - drawWidth) / 2;
            }

            return AddImage(imagePath, drawWidth, drawHeight);
        }

        if (fitMode == PdfImageFitMode.Cover)
        {
            if (imageAspect > boxAspect)
            {
                drawWidth = targetHeight * imageAspect;
                drawX -= (drawWidth - targetWidth) / 2;
            }
            else
            {
                drawHeight = targetWidth / imageAspect;
                drawY -= (drawHeight - targetHeight) / 2;
            }

            EnsureSpace(targetHeight);
            CurrentPage.DrawImageClipped(
                imagePath,
                drawX,
                drawY,
                drawWidth,
                drawHeight,
                _options.MarginLeft,
                CurrentY - targetHeight,
                targetWidth,
                targetHeight);
            CurrentY = (CurrentY - targetHeight) - _options.ParagraphSpacing;

            if (CurrentY < _options.MarginBottom)
            {
                NextPage();
            }

            return new PdfImageLayoutResult
            {
                X = _options.MarginLeft,
                Y = (CurrentY + _options.ParagraphSpacing),
                Width = targetWidth,
                Height = targetHeight
            };
        }

        return AddImage(imagePath, targetWidth, targetHeight);
    }

    public IReadOnlyList<PdfParagraphLayoutResult> AddList(IReadOnlyList<string> items, PdfListOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        options ??= PdfListOptions.Default;

        if (options.FontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "List font size must be greater than zero.");
        }

        if (options.MarkerFontSize is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "List MarkerFontSize must be greater than zero when provided.");
        }

        if (options.StartIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "List StartIndex must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.NumberFormat))
        {
            throw new ArgumentException("List NumberFormat cannot be null or empty.", nameof(options));
        }

        if (options.Indent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "List indent cannot be negative.");
        }

        if (options.MarkerWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "List MarkerWidth cannot be negative.");
        }

        if (options.MarkerGap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "List MarkerGap cannot be negative.");
        }

        if (options.ItemSpacing < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "List item spacing cannot be negative.");
        }

        var results = new List<PdfParagraphLayoutResult>(items.Count);
        var listFont = options.Font
            ?? (options.FontFamily is { } family
                ? PdfStandardFonts.FromStyle(family, options.Bold, options.Italic)
                : CurrentPage.DefaultFont);
        var markerFont = options.MarkerFont
            ?? (options.MarkerFontFamily is { } markerFamily
                ? PdfStandardFonts.FromStyle(markerFamily, options.MarkerBold, options.MarkerItalic)
                : listFont);
        var markerFontSize = options.MarkerFontSize ?? options.FontSize;

        var markers = new string[items.Count];
        var widestMarkerWidth = 0d;

        for (var i = 0; i < items.Count; i++)
        {
            var marker = options.Ordered
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture, options.NumberFormat, options.StartIndex + i)
                : options.Bullet;

            markers[i] = marker;

            var markerWidth = EstimateTextWidth(marker, markerFontSize, markerFont);
            if (markerWidth > widestMarkerWidth)
            {
                widestMarkerWidth = markerWidth;
            }
        }

        var effectiveMarkerWidth = options.MarkerWidth > 0 ? options.MarkerWidth : widestMarkerWidth;

        for (var i = 0; i < items.Count; i++)
        {
            var itemText = items[i] ?? string.Empty;
            var marker = markers[i];
            var markerTextWidth = EstimateTextWidth(marker, markerFontSize, markerFont);

            EnsureSpace(options.FontSize + options.ItemSpacing);

            var markerX = options.AlignMarkersRight
                ? _options.MarginLeft + (effectiveMarkerWidth - markerTextWidth)
                : _options.MarginLeft;

            CurrentPage.DrawText(marker, markerX, CurrentY, new PdfDrawTextOptions
            {
                FontSize = markerFontSize,
                Font = markerFont,
                FillColor = options.MarkerColor ?? options.FillColor
            });

            var textX = _options.MarginLeft + effectiveMarkerWidth + options.MarkerGap + options.Indent;
            var maxWidth = CurrentPage.Width - textX - _options.MarginRight;
            var result = CurrentPage.DrawParagraph(itemText, textX, CurrentY, maxWidth, new PdfParagraphOptions
            {
                FontSize = options.FontSize,
                LineHeight = options.LineHeight,
                Font = options.Font,
                FontFamily = options.FontFamily,
                Bold = options.Bold,
                Italic = options.Italic,
                FillColor = options.FillColor,
                Alignment = options.ItemAlignment
            });

            results.Add(result);
            CurrentY = result.BottomY - options.ItemSpacing;

            if (CurrentY < _options.MarginBottom)
            {
                NextPage();
            }
        }

        return results;
    }

    private static double EstimateTextWidth(string text, double fontSize, PdfStandardFont font)
    {
        var avgCharacterWidthFactor = font switch
        {
            PdfStandardFont.Courier or PdfStandardFont.CourierBold or PdfStandardFont.CourierOblique or PdfStandardFont.CourierBoldOblique => 0.6,
            PdfStandardFont.TimesRoman or PdfStandardFont.TimesBold or PdfStandardFont.TimesItalic or PdfStandardFont.TimesBoldItalic => 0.5,
            _ => 0.52
        };

        return text.Length * fontSize * avgCharacterWidthFactor;
    }

    private void EnsureSpace(double requiredHeight)
    {
        if (CurrentY - requiredHeight < _options.MarginBottom)
        {
            NextPage();
        }
    }

    private int GetCurrentPageNumber()
    {
        for (var i = 0; i < _document.Pages.Count; i++)
        {
            if (ReferenceEquals(_document.Pages[i], CurrentPage))
            {
                return i + 1;
            }
        }

        throw new InvalidOperationException("Current flow page was not found in document.");
    }
}
