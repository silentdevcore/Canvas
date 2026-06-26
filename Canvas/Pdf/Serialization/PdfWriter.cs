using System.Globalization;
using System.IO.Compression;
using System.Text;
using Canvas.Pdf.Layout;
using Canvas.Pdf.Rendering;

namespace Canvas.Pdf.Serialization;

internal sealed class PdfWriter
{
    internal sealed class PdfWriteResult
    {
        public required byte[] Bytes { get; init; }

        public PdfGenerationDiagnostics? Diagnostics { get; init; }
    }

    public PdfWriteResult Write(PdfDocument document, PdfSaveOptions? options = null)
    {
        options ??= PdfSaveOptions.Default;

        if (document.Pages.Count == 0)
        {
            document.AddPage();
        }

        var nextObjectId = 1;
        var catalogObjectId = nextObjectId++;
        var pagesObjectId = nextObjectId++;
        var infoObjectId = document.Info.HasValues ? nextObjectId++ : (int?)null;

        var usedFonts = document.Pages
            .SelectMany(static page => page.Elements)
            .OfType<TextElement>()
            .Select(static element => element.Font)
            .Distinct()
            .ToList();

        var usedEmbeddedFonts = document.Pages
            .SelectMany(static page => page.Elements)
            .OfType<TextElement>()
            .Where(static el => el.EmbeddedFont is not null)
            .Select(static el => el.EmbeddedFont!)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Cast<PdfEmbeddedFont>()
            .ToList();

        var imagesByKey = document.Pages
            .SelectMany(static page => page.Elements)
            .OfType<ImageElement>()
            .GroupBy(static image => image.CacheKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().Image, StringComparer.OrdinalIgnoreCase);

        var totalImageDrawCalls = document.Pages
            .SelectMany(static page => page.Elements)
            .OfType<ImageElement>()
            .Count();

        if (usedFonts.Count == 0)
        {
            usedFonts.Add(document.DefaultFont);
        }

        var fontObjects = new Dictionary<PdfStandardFont, (string ResourceName, int ObjectId)>();

        for (var i = 0; i < usedFonts.Count; i++)
        {
            var font = usedFonts[i];
            var objectId = nextObjectId++;
            var resourceName = $"F{i + 1}";
            fontObjects[font] = (resourceName, objectId);
        }

        // 5 PDF objects per embedded font: FontStream, FontDescriptor, ToUnicode, CIDFont, Type0
        var embeddedFontObjects = new Dictionary<PdfEmbeddedFont, EmbeddedFontIds>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < usedEmbeddedFonts.Count; i++)
        {
            var ef = usedEmbeddedFonts[i];
            embeddedFontObjects[ef] = new EmbeddedFontIds(
                ResourceName: $"EF{i + 1}",
                FontStreamId: nextObjectId++,
                DescriptorId: nextObjectId++,
                ToUnicodeId: nextObjectId++,
                CidFontId: nextObjectId++,
                Type0Id: nextObjectId++);
        }

        var imageObjects = new Dictionary<string, (string ResourceName, int ObjectId, int? SoftMaskObjectId)>();
        var imageIndex = 1;

        foreach (var image in imagesByKey)
        {
            var objectId = nextObjectId++;
            var softMaskObjectId = image.Value.SoftMask is not null ? nextObjectId++ : (int?)null;
            imageObjects[image.Key] = ($"Im{imageIndex++}", objectId, softMaskObjectId);
        }

        var usedOpacities = document.Pages
            .SelectMany(static page => page.Elements)
            .OfType<ImageElement>()
            .Select(static image => image.Opacity)
            .Where(static opacity => opacity is > 0 and < 1)
            .Distinct()
            .OrderBy(static opacity => opacity)
            .ToList();

        var opacityObjects = new Dictionary<double, (string ResourceName, int ObjectId)>();

        for (var i = 0; i < usedOpacities.Count; i++)
        {
            var opacity = usedOpacities[i];
            opacityObjects[opacity] = ($"Gs{i + 1}", nextObjectId++);
        }

        var pageObjects = new List<int>();
        var objects = new List<PdfIndirectObject>();

        foreach (var fontObject in fontObjects)
        {
            objects.Add(new PdfIndirectObject(
                fontObject.Value.ObjectId,
                $"<< /Type /Font /Subtype /Type1 /BaseFont /{GetBaseFontName(fontObject.Key)} >>\n"));
        }

        // Emit 5-object chain for each embedded TrueType/OpenType font
        foreach (var (ef, ids) in embeddedFontObjects)
        {
            // Collect all code points used by this font across all pages
            var usedCodePoints = document.Pages
                .SelectMany(static p => p.Elements)
                .OfType<TextElement>()
                .Where(el => ReferenceEquals(el.EmbeddedFont, ef))
                .SelectMany(static el => el.Text.EnumerateRunes().Select(static r => r.Value))
                .Distinct()
                .OrderBy(static cp => cp)
                .ToList();

            // 1. Font file stream (FlateDecode compressed)
            var fontBytes = ef.FontBytes.ToArray();
            var compressedFont = CompressZlib(fontBytes);
            objects.Add(new PdfIndirectObject(ids.FontStreamId, BuildFontStreamObject(compressedFont, fontBytes.Length)));

            // 2. FontDescriptor
            objects.Add(new PdfIndirectObject(ids.DescriptorId,
                $"<< /Type /FontDescriptor /FontName /{ef.BaseFontName} /Flags 32 " +
                $"/FontBBox [0 -200 1000 800] /ItalicAngle 0 /Ascent 800 /Descent -200 " +
                $"/CapHeight 700 /StemV 80 /FontFile2 {ids.FontStreamId} 0 R >>\n"));

            // 3. ToUnicode CMap stream (maps CID = Unicode code point back to Unicode for text extraction)
            var cMapBytes = Encoding.ASCII.GetBytes(BuildToUnicodeCMap(usedCodePoints));
            objects.Add(new PdfIndirectObject(ids.ToUnicodeId, BuildContentStreamObject(cMapBytes, compressed: false)));

            // 4. CIDFont with /W widths array
            var widthsArray = BuildCidFontWidthsArray(ef, usedCodePoints);
            objects.Add(new PdfIndirectObject(ids.CidFontId,
                $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{ef.BaseFontName} " +
                $"/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> " +
                $"/DW 1000 /W {widthsArray} /FontDescriptor {ids.DescriptorId} 0 R >>\n"));

            // 5. Type0 composite font
            objects.Add(new PdfIndirectObject(ids.Type0Id,
                $"<< /Type /Font /Subtype /Type0 /BaseFont /{ef.BaseFontName} " +
                $"/Encoding /Identity-H /DescendantFonts [{ids.CidFontId} 0 R] " +
                $"/ToUnicode {ids.ToUnicodeId} 0 R >>\n"));
        }

        foreach (var image in imagesByKey)
        {
            var imageObject = imageObjects[image.Key];
            objects.Add(new PdfIndirectObject(imageObject.ObjectId, BuildImageObject(image.Value, imageObject.SoftMaskObjectId)));

            if (imageObject.SoftMaskObjectId is { } softMaskObjectId && image.Value.SoftMask is { } softMaskImage)
            {
                objects.Add(new PdfIndirectObject(softMaskObjectId, BuildImageObject(softMaskImage)));
            }
        }

        foreach (var opacityObject in opacityObjects)
        {
            var opacityValue = opacityObject.Key.ToString("0.###", CultureInfo.InvariantCulture);
            objects.Add(new PdfIndirectObject(opacityObject.Value.ObjectId, $"<< /Type /ExtGState /ca {opacityValue} /CA {opacityValue} >>\n"));
        }

        var pageCount = document.Pages.Count;
        var pageObjectIds = new int[pageCount];
        var contentObjectIds = new int[pageCount];
        var pageAnnotationObjectIds = new List<int>[pageCount];
        var namedDestinationMap = document.NamedDestinations
            .GroupBy(destination => destination.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var hasAcroFields = document.Pages.Any(static p =>
            p.ComboBoxAnnotations.Count > 0 || p.MultilineTextFields.Count > 0 || p.TextFields.Count > 0 || p.CheckBoxAnnotations.Count > 0);
        var acroFormHelvFontObjectId = hasAcroFields ? nextObjectId++ : (int?)null;
        var pageComboAnnotObjectIds = new List<int>[pageCount];
        var pageMultilineAnnotObjectIds = new List<int>[pageCount];
        var pageTextFieldAnnotObjectIds = new List<int>[pageCount];
        var pageCheckBoxAnnotObjectIds = new List<int>[pageCount];
        var pageReviewAnnotObjectIds = new List<int>[pageCount];
        var allComboFieldObjectIds = new List<int>();
        var allMultilineFieldObjectIds = new List<int>();
        var allTextFieldObjectIds = new List<int>();
        var allCheckBoxObjectIds = new List<int>();

        for (var i = 0; i < pageCount; i++)
        {
            pageObjectIds[i] = nextObjectId++;
            contentObjectIds[i] = nextObjectId++;

            var annotationIds = new List<int>();
            foreach (var _ in document.Pages[i].LinkAnnotations)
            {
                annotationIds.Add(nextObjectId++);
            }

            pageAnnotationObjectIds[i] = annotationIds;

            var comboIds = new List<int>();
            foreach (var _ in document.Pages[i].ComboBoxAnnotations)
            {
                var comboId = nextObjectId++;
                comboIds.Add(comboId);
                allComboFieldObjectIds.Add(comboId);
            }

            pageComboAnnotObjectIds[i] = comboIds;

            var multilineIds = new List<int>();
            foreach (var _ in document.Pages[i].MultilineTextFields)
            {
                var mlId = nextObjectId++;
                multilineIds.Add(mlId);
                allMultilineFieldObjectIds.Add(mlId);
            }

            pageMultilineAnnotObjectIds[i] = multilineIds;

            var textFieldIds = new List<int>();
            foreach (var _ in document.Pages[i].TextFields)
            {
                var tfId = nextObjectId++;
                textFieldIds.Add(tfId);
                allTextFieldObjectIds.Add(tfId);
            }

            pageTextFieldAnnotObjectIds[i] = textFieldIds;

            var checkBoxIds = new List<int>();
            foreach (var _ in document.Pages[i].CheckBoxAnnotations)
            {
                var cbId = nextObjectId++;
                checkBoxIds.Add(cbId);
                allCheckBoxObjectIds.Add(cbId);
            }

            pageCheckBoxAnnotObjectIds[i] = checkBoxIds;

            var reviewIds = new List<int>();
            foreach (var _ in document.Pages[i].ReviewAnnotations)
            {
                reviewIds.Add(nextObjectId++);
            }

            pageReviewAnnotObjectIds[i] = reviewIds;
            pageObjects.Add(pageObjectIds[i]);
        }

        var outlinesRootObjectId = document.Bookmarks.Count > 0 ? nextObjectId++ : (int?)null;
        var bookmarkObjectIds = new List<int>();

        if (outlinesRootObjectId is not null)
        {
            for (var i = 0; i < document.Bookmarks.Count; i++)
            {
                bookmarkObjectIds.Add(nextObjectId++);
            }
        }

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            var pageObjectId = pageObjectIds[pageIndex];
            var contentObjectId = contentObjectIds[pageIndex];
            var pageLinkAnnotationIds = pageAnnotationObjectIds[pageIndex];

            var pageFonts = page.Elements
                .OfType<TextElement>()
                .Select(static element => element.Font)
                .Distinct()
                .ToList();

            if (pageFonts.Count == 0)
            {
                pageFonts.Add(page.DefaultFont);
            }

            var pageFontResources = pageFonts.ToDictionary(
                static font => font,
                font => fontObjects[font].ResourceName);

            var pageEmbeddedFonts = page.Elements
                .OfType<TextElement>()
                .Where(static el => el.EmbeddedFont is not null)
                .Select(static el => el.EmbeddedFont!)
                .Distinct(ReferenceEqualityComparer.Instance)
                .Cast<PdfEmbeddedFont>()
                .ToList();

            var pageEmbeddedFontResources = pageEmbeddedFonts.ToDictionary(
                static ef => ef,
                ef => embeddedFontObjects[ef].ResourceName,
                (IEqualityComparer<PdfEmbeddedFont>)ReferenceEqualityComparer.Instance);

            var pageImages = page.Elements
                .OfType<ImageElement>()
                .Select(static image => image.CacheKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var pageImageResources = pageImages.ToDictionary(
                static key => key,
                key => imageObjects[key].ResourceName,
                StringComparer.OrdinalIgnoreCase);

            var pageOpacities = page.Elements
                .OfType<ImageElement>()
                .Select(static image => image.Opacity)
                .Where(static opacity => opacity is > 0 and < 1)
                .Distinct()
                .ToList();

            var pageOpacityResources = pageOpacities.ToDictionary(
                static opacity => opacity,
                opacity => opacityObjects[opacity].ResourceName);

            var contentStream = PdfCanvasRenderer.RenderPage(
                page, pageFontResources, pageImageResources, pageOpacityResources, pageEmbeddedFontResources);
            var contentBytes = Encoding.ASCII.GetBytes(contentStream);

            if (options.CompressContentStreams)
            {
                contentBytes = CompressZlib(contentBytes);
            }

            objects.Add(new PdfIndirectObject(contentObjectId, BuildContentStreamObject(contentBytes, options.CompressContentStreams)));

            for (var i = 0; i < page.LinkAnnotations.Count; i++)
            {
                var link = page.LinkAnnotations[i];
                var annotationObjectId = pageLinkAnnotationIds[i];
                var rect = string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0} {1} {2} {3}]",
                    FormatNumber(link.X),
                    FormatNumber(link.Y),
                    FormatNumber(link.X + link.Width),
                    FormatNumber(link.Y + link.Height));

                string annotationObject;

                if (link.Url is { Length: > 0 } url)
                {
                    annotationObject = $"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] /A << /S /URI /URI ({EscapeLiteralString(url)}) >> >>\n";
                }
                else if (link.TargetPageNumber is { } targetPageNumber)
                {
                    if (targetPageNumber < 1 || targetPageNumber > pageCount)
                    {
                        throw new InvalidOperationException($"Page link target {targetPageNumber} is out of range for this document.");
                    }

                    var targetPageObjectId = pageObjectIds[targetPageNumber - 1];
                    annotationObject = $"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] /Dest [{targetPageObjectId} 0 R /Fit] >>\n";
                }
                else if (link.NamedDestination is { Length: > 0 } destinationName)
                {
                    if (!namedDestinationMap.TryGetValue(destinationName, out var destination))
                    {
                        throw new InvalidOperationException($"Named destination '{destinationName}' was not found in the document.");
                    }

                    var destinationPageNumber = destination.PageNumber;

                    if (destinationPageNumber < 1 || destinationPageNumber > pageCount)
                    {
                        throw new InvalidOperationException($"Named destination '{destinationName}' points to out-of-range page {destinationPageNumber}.");
                    }

                    var targetPageObjectId = pageObjectIds[destinationPageNumber - 1];
                    annotationObject = destination.Y is { } y
                        ? $"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] /Dest [{targetPageObjectId} 0 R /XYZ null {FormatNumber(y)} null] >>\n"
                        : $"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] /Dest [{targetPageObjectId} 0 R /Fit] >>\n";
                }
                else
                {
                    throw new InvalidOperationException("Link annotation must define Url, TargetPageNumber, or NamedDestination.");
                }

                objects.Add(new PdfIndirectObject(annotationObjectId, annotationObject));
            }

            var pageComboIds = pageComboAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.ComboBoxAnnotations.Count; i++)
            {
                var combo = page.ComboBoxAnnotations[i];
                var comboObjectId = pageComboIds[i];

                var comboRect = string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0} {1} {2} {3}]",
                    FormatNumber(combo.X),
                    FormatNumber(combo.Y),
                    FormatNumber(combo.X + combo.Width),
                    FormatNumber(combo.Y + combo.Height));

                var optArray = string.Join(" ", combo.Options.Select(o => $"({EscapeLiteralString(o)})"));
                var selectedVal = combo.SelectedValue is { Length: > 0 } sv
                    ? sv
                    : (combo.Options.Count > 0 ? combo.Options[0] : string.Empty);

                var widgetObject =
                    $"<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 " +
                    $"/T ({EscapeLiteralString(combo.FieldName)}) " +
                    $"/V ({EscapeLiteralString(selectedVal)}) " +
                    $"/Opt [{optArray}] " +
                    $"/Rect {comboRect} " +
                    $"/P {pageObjectId} 0 R " +
                    $"/DA (/Helv {FormatNumber(combo.FontSize)} Tf 0 g) " +
                    $"/DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> " +
                    $"/MK << /BC [0 0 0] /BG [1 1 1] >> " +
                    $"/BS << /W 1 /S /S >> >>\n";

                objects.Add(new PdfIndirectObject(comboObjectId, widgetObject));
            }

            var pageMultilineIds = pageMultilineAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.MultilineTextFields.Count; i++)
            {
                var ml = page.MultilineTextFields[i];
                var mlObjectId = pageMultilineIds[i];

                var mlRect = string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0} {1} {2} {3}]",
                    FormatNumber(ml.X),
                    FormatNumber(ml.Y),
                    FormatNumber(ml.X + ml.Width),
                    FormatNumber(ml.Y + ml.Height));

                var mlWidgetObject =
                    $"<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 4096 " +
                    $"/T ({EscapeLiteralString(ml.FieldName)}) " +
                    $"/V ({EscapeLiteralString(ml.DefaultValue)}) " +
                    $"/Rect {mlRect} " +
                    $"/P {pageObjectId} 0 R " +
                    $"/DA (/Helv {FormatNumber(ml.FontSize)} Tf 0 g) " +
                    $"/DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> " +
                    $"/MK << /BC [0 0 0] /BG [1 1 1] >> " +
                    $"/BS << /W 1 /S /S >> >>\n";

                objects.Add(new PdfIndirectObject(mlObjectId, mlWidgetObject));
            }

            var pageTextFieldIds = pageTextFieldAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.TextFields.Count; i++)
            {
                var tf = page.TextFields[i];
                var tfObjectId = pageTextFieldIds[i];

                var tfRect = string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0} {1} {2} {3}]",
                    FormatNumber(tf.X),
                    FormatNumber(tf.Y),
                    FormatNumber(tf.X + tf.Width),
                    FormatNumber(tf.Y + tf.Height));

                var tfWidgetObject =
                    $"<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 0 " +
                    $"/T ({EscapeLiteralString(tf.FieldName)}) " +
                    $"/V ({EscapeLiteralString(tf.DefaultValue)}) " +
                    $"/Rect {tfRect} " +
                    $"/P {pageObjectId} 0 R " +
                    $"/DA (/Helv {FormatNumber(tf.FontSize)} Tf 0 g) " +
                    $"/DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> " +
                    $"/MK << /BC [0 0 0] /BG [1 1 1] >> " +
                    $"/BS << /W 1 /S /S >> >>\n";

                objects.Add(new PdfIndirectObject(tfObjectId, tfWidgetObject));
            }

            var pageCheckBoxIds = pageCheckBoxAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.CheckBoxAnnotations.Count; i++)
            {
                var cb = page.CheckBoxAnnotations[i];
                var cbObjectId = pageCheckBoxIds[i];

                var cbRect = string.Format(
                    CultureInfo.InvariantCulture,
                    "[{0} {1} {2} {3}]",
                    FormatNumber(cb.X),
                    FormatNumber(cb.Y),
                    FormatNumber(cb.X + cb.Width),
                    FormatNumber(cb.Y + cb.Height));

                var cbValue = cb.IsChecked ? "/Yes" : "/Off";
                var cbWidgetObject =
                    $"<< /Type /Annot /Subtype /Widget /FT /Btn /Ff 0 " +
                    $"/T ({EscapeLiteralString(cb.FieldName)}) " +
                    $"/V {cbValue} /AS {cbValue} " +
                    $"/Rect {cbRect} " +
                    $"/P {pageObjectId} 0 R " +
                    $"/DA (/Helv 0 Tf 0 g) " +
                    $"/DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> " +
                    $"/MK << /BC [0 0 0] /BG [1 1 1] /CA (8) >> " +
                    $"/BS << /W 1 /S /S >> >>\n";

                objects.Add(new PdfIndirectObject(cbObjectId, cbWidgetObject));
            }

            var pageReviewIds = pageReviewAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.ReviewAnnotations.Count; i++)
            {
                var review = page.ReviewAnnotations[i];
                var reviewObjectId = pageReviewIds[i];
                objects.Add(new PdfIndirectObject(reviewObjectId, BuildReviewAnnotationObject(review, pageObjectId)));
            }

            var fontDictionary = string.Join(
                " ",
                pageFonts.Select(font => $"/{fontObjects[font].ResourceName} {fontObjects[font].ObjectId} 0 R")
                .Concat(pageEmbeddedFonts.Select(ef => $"/{embeddedFontObjects[ef].ResourceName} {embeddedFontObjects[ef].Type0Id} 0 R")));

            var xObjectDictionary = string.Join(
                " ",
                pageImages.Select(key => $"/{imageObjects[key].ResourceName} {imageObjects[key].ObjectId} 0 R"));

            var extGStateDictionary = string.Join(
                " ",
                pageOpacities.Select(opacity => $"/{opacityObjects[opacity].ResourceName} {opacityObjects[opacity].ObjectId} 0 R"));

            var resourcesDictionary = BuildResourcesDictionary(fontDictionary, xObjectDictionary, extGStateDictionary);

            var allAnnotIds = pageLinkAnnotationIds
                .Concat(pageComboAnnotObjectIds[pageIndex])
                .Concat(pageMultilineAnnotObjectIds[pageIndex])
                .Concat(pageTextFieldAnnotObjectIds[pageIndex])
                .Concat(pageCheckBoxAnnotObjectIds[pageIndex])
                .Concat(pageReviewAnnotObjectIds[pageIndex]);
            var annotsSegment = pageLinkAnnotationIds.Count > 0
                || pageComboAnnotObjectIds[pageIndex].Count > 0
                || pageMultilineAnnotObjectIds[pageIndex].Count > 0
                || pageTextFieldAnnotObjectIds[pageIndex].Count > 0
                || pageCheckBoxAnnotObjectIds[pageIndex].Count > 0
                || pageReviewAnnotObjectIds[pageIndex].Count > 0
                ? $" /Annots [{string.Join(" ", allAnnotIds.Select(id => $"{id} 0 R"))}]"
                : string.Empty;

            var rotateSegment = page.RotationDegrees != 0 ? $" /Rotate {page.RotationDegrees}" : string.Empty;
            var cropBoxSegment = BuildPageBoxSegment("CropBox", page.CropBoxLowerLeft, page.CropBoxUpperRight);
            var bleedBoxSegment = BuildPageBoxSegment("BleedBox", page.BleedBoxLowerLeft, page.BleedBoxUpperRight);
            var trimBoxSegment = BuildPageBoxSegment("TrimBox", page.TrimBoxLowerLeft, page.TrimBoxUpperRight);
            var artBoxSegment = BuildPageBoxSegment("ArtBox", page.ArtBoxLowerLeft, page.ArtBoxUpperRight);

            var pageObject = string.Format(
                CultureInfo.InvariantCulture,
                "<< /Type /Page /Parent {0} 0 R /MediaBox [0 0 {1} {2}] /Resources {3} /Contents {4} 0 R{5}{6}{7}{8}{9}{10} >>\n",
                pagesObjectId,
                FormatNumber(page.Width),
                FormatNumber(page.Height),
                resourcesDictionary,
                contentObjectId,
                annotsSegment,
                rotateSegment,
                cropBoxSegment,
                bleedBoxSegment,
                trimBoxSegment,
                artBoxSegment);

            objects.Add(new PdfIndirectObject(pageObjectId, pageObject));
        }

        var kids = string.Join(" ", pageObjects.Select(id => $"{id} 0 R"));

        objects.Add(new PdfIndirectObject(pagesObjectId,
            $"<< /Type /Pages /Kids [{kids}] /Count {pageObjects.Count} >>\n"));

        if (outlinesRootObjectId is { } outlinesId)
        {
            var effectiveLevels = new int[document.Bookmarks.Count];

            for (var i = 0; i < document.Bookmarks.Count; i++)
            {
                var currentLevel = document.Bookmarks[i].Level;
                if (i > 0)
                {
                    currentLevel = Math.Min(currentLevel, effectiveLevels[i - 1] + 1);
                }

                effectiveLevels[i] = Math.Max(1, currentLevel);
            }

            var parentIndexByItem = new int[document.Bookmarks.Count];
            var lastAtLevel = new Dictionary<int, int>();
            var childrenCountByParent = new Dictionary<int, int>();

            for (var i = 0; i < document.Bookmarks.Count; i++)
            {
                var level = effectiveLevels[i];
                var parentIndex = level == 1 && !lastAtLevel.ContainsKey(0)
                    ? -1
                    : lastAtLevel.GetValueOrDefault(level - 1, -1);

                parentIndexByItem[i] = parentIndex;
                if (childrenCountByParent.ContainsKey(parentIndex))
                {
                    childrenCountByParent[parentIndex]++;
                }
                else
                {
                    childrenCountByParent[parentIndex] = 1;
                }

                lastAtLevel[level] = i;

                var pruneLevels = lastAtLevel.Keys.Where(existingLevel => existingLevel > level).ToList();
                foreach (var prune in pruneLevels)
                {
                    lastAtLevel.Remove(prune);
                }
            }

            for (var i = 0; i < document.Bookmarks.Count; i++)
            {
                var bookmark = document.Bookmarks[i];

                if (bookmark.PageNumber > pageCount)
                {
                    throw new InvalidOperationException($"Bookmark target page {bookmark.PageNumber} is out of range for this document.");
                }

                var destinationPageObjectId = pageObjectIds[bookmark.PageNumber - 1];
                var parentIndex = parentIndexByItem[i];
                var parentObjectRef = parentIndex >= 0 ? $"{bookmarkObjectIds[parentIndex]} 0 R" : $"{outlinesId} 0 R";

                var siblingIndexes = Enumerable.Range(0, document.Bookmarks.Count)
                    .Where(index => parentIndexByItem[index] == parentIndex)
                    .ToList();
                var siblingPosition = siblingIndexes.IndexOf(i);
                var prev = siblingPosition > 0 ? $" /Prev {bookmarkObjectIds[siblingIndexes[siblingPosition - 1]]} 0 R" : string.Empty;
                var next = siblingPosition < siblingIndexes.Count - 1 ? $" /Next {bookmarkObjectIds[siblingIndexes[siblingPosition + 1]]} 0 R" : string.Empty;

                var childIndexes = Enumerable.Range(0, document.Bookmarks.Count)
                    .Where(index => parentIndexByItem[index] == i)
                    .ToList();
                var childSegment = childIndexes.Count > 0
                    ? $" /First {bookmarkObjectIds[childIndexes[0]]} 0 R /Last {bookmarkObjectIds[childIndexes[^1]]} 0 R /Count {childIndexes.Count}"
                    : string.Empty;

                var bookmarkObject = $"<< /Title ({EscapeLiteralString(bookmark.Title)}) /Parent {parentObjectRef} /Dest [{destinationPageObjectId} 0 R /Fit]{prev}{next}{childSegment} >>\n";
                objects.Add(new PdfIndirectObject(bookmarkObjectIds[i], bookmarkObject));
            }

            var rootChildren = Enumerable.Range(0, document.Bookmarks.Count)
                .Where(index => parentIndexByItem[index] == -1)
                .ToList();
            var firstBookmark = bookmarkObjectIds[rootChildren[0]];
            var lastBookmark = bookmarkObjectIds[rootChildren[^1]];
            objects.Add(new PdfIndirectObject(
                outlinesId,
                $"<< /Type /Outlines /First {firstBookmark} 0 R /Last {lastBookmark} 0 R /Count {rootChildren.Count} >>\n"));
        }

        if (acroFormHelvFontObjectId is { } helvFontId)
        {
            objects.Add(new PdfIndirectObject(helvFontId,
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\n"));
        }

        var viewerPreferences = BuildViewerPreferences(document.ViewerPreferences);
        var pageMode = BuildPageMode(document.ViewerPreferences.PageMode);
        var pageLayout = BuildPageLayout(document.ViewerPreferences.PageLayout);

        var allAcroFieldIds = allComboFieldObjectIds.Concat(allMultilineFieldObjectIds).Concat(allTextFieldObjectIds).Concat(allCheckBoxObjectIds).ToList();
        var acroFormSegment = allAcroFieldIds.Count > 0
            ? $" /AcroForm << /Fields [{string.Join(" ", allAcroFieldIds.Select(id => $"{id} 0 R"))}]" +
              $" /DA (/Helv 0 Tf 0 g)" +
              $" /DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> >>"
            : string.Empty;

        var openAction = BuildOpenAction(document, pageObjectIds);
        var catalogObject = outlinesRootObjectId is { } outlinesRootId
            ? $"<< /Type /Catalog /Pages {pagesObjectId} 0 R /Outlines {outlinesRootId} 0 R{pageMode}{pageLayout}{viewerPreferences}{openAction}{acroFormSegment} >>\n"
            : $"<< /Type /Catalog /Pages {pagesObjectId} 0 R{pageMode}{pageLayout}{viewerPreferences}{openAction}{acroFormSegment} >>\n";

        objects.Add(new PdfIndirectObject(catalogObjectId, catalogObject));

        if (infoObjectId is { } actualInfoObjectId)
        {
            objects.Add(new PdfIndirectObject(actualInfoObjectId, BuildInfoObject(document.Info)));
        }

        Security.StandardSecurityHandler? security = null;
        int? encryptObjectId = null;
        if (options.Encryption is { } encryptionOptions)
        {
            var documentId = Security.StandardSecurityHandler.GenerateDocumentId(document);
            security = Security.StandardSecurityHandler.Create(encryptionOptions, documentId);
            encryptObjectId = objects.Max(static o => o.Id) + 1;
            objects.Add(new PdfIndirectObject(encryptObjectId.Value, security.BuildEncryptDictionary()));
        }

        var bytes = Serialize(objects, catalogObjectId, infoObjectId, security, encryptObjectId);
        var webLinkCount = document.Pages
            .SelectMany(static page => page.LinkAnnotations)
            .Count(static link => link.Url is { Length: > 0 });
        var pageLinkCount = document.Pages
            .SelectMany(static page => page.LinkAnnotations)
            .Count(static link => link.TargetPageNumber is not null);
        var namedDestinationLinkCount = document.Pages
            .SelectMany(static page => page.LinkAnnotations)
            .Count(static link => link.NamedDestination is { Length: > 0 });
        var bookmarkTargetPageCount = document.Bookmarks
            .Select(static bookmark => bookmark.PageNumber)
            .Distinct()
            .Count();
        var namedDestinationPageCount = document.NamedDestinations
            .Select(static destination => destination.PageNumber)
            .Distinct()
            .Count();
        var pagesWithTextCount = document.GetPagesWithText().Count;
        var pagesWithImageCount = document.GetPagesWithImages().Count;
        var pagesWithLinkCount = document.GetPagesWithLinks().Count;
        var pagesWithShapeCount = document.GetPagesWithShapes().Count;
        var pagesWithCropBoxCount = document.GetPagesWithCropBox().Count;
        var pagesWithBleedBoxCount = document.GetPagesWithBleedBox().Count;
        var pagesWithTrimBoxCount = document.GetPagesWithTrimBox().Count;
        var pagesWithArtBoxCount = document.GetPagesWithArtBox().Count;
        var pagesWithAnyBoundaryBoxCount = document.GetPagesWithAnyBoundaryBox().Count;
        var pagesWithAnyTransparencyCount = document.GetPagesWithAnyTransparency().Count;
        var pagesWithImageTransparencyCount = document.GetPagesWithImageTransparency().Count;
        var pagesWithTextDecorationsCount = document.GetPagesWithTextDecorations().Count;
        var pagesWithFlowContentCount = document.GetPagesWithFlowContent().Count;
        var pagesWithWebLinksCount = document.GetPagesWithWebLinks().Count;
        var pagesWithPageLinksCount = document.GetPagesWithPageLinks().Count;
        var pagesWithNamedDestinationLinksCount = document.GetPagesWithNamedDestinationLinks().Count;
        var pagesWithBookmarksCount = document.GetPagesWithBookmarks().Count;
        var pagesWithNamedDestinationsByPageCount = document.GetPagesWithNamedDestinations().Count;
        var pagesWithMixedContentCount = document.GetPagesWithMixedContent().Count;
        var pagesWithLineCount = document.GetPagesWithLines().Count;
        var pagesWithRectangleCount = document.GetPagesWithRectangles().Count;
        var pagesWithRoundedRectangleCount = document.GetPagesWithRoundedRectangles().Count;
        var pagesWithCircleCount = document.GetPagesWithCircles().Count;
        var pagesWithPolygonCount = document.GetPagesWithPolygons().Count;
        var pagesWithBezierCurveCount = document.GetPagesWithBezierCurves().Count;
        var pagesWithUnderlinedTextCount = document.GetPagesWithUnderlinedText().Count;
        var pagesWithStrikethroughTextCount = document.GetPagesWithStrikethroughText().Count;
        var pagesWithRotatedTextCount = document.GetPagesWithRotatedText().Count;
        var pagesWithCharacterSpacedTextCount = document.GetPagesWithCharacterSpacedText().Count;
        var pagesWithHorizontallyScaledTextCount = document.GetPagesWithHorizontallyScaledText().Count;
        var pagesWithJustifiedTextCount = document.GetPagesWithJustifiedText().Count;
        var pagesWithOpaqueImagesOnlyCount = document.GetPagesWithOpaqueImagesOnly().Count;
        var pagesWithoutLinksCount = document.GetPagesWithoutLinks().Count;
        var pagesWithoutImagesCount = document.GetPagesWithoutImages().Count;
        var pagesWithoutTextCount = document.GetPagesWithoutText().Count;
        var pagesWithoutShapesCount = document.GetPagesWithoutShapes().Count;
        var pagesWithOnlyTextCount = document.GetPagesWithOnlyText().Count;
        var pagesWithOnlyImagesCount = document.GetPagesWithOnlyImages().Count;
        var pagesWithAnyElementsCount = document.GetPagesWithAnyElements().Count;
        var pagesWithMultipleLinksCount = document.GetPagesWithMultipleLinks().Count;
        var pagesWithMultipleImagesCount = document.GetPagesWithMultipleImages().Count;
        var pagesWithMultipleTextElementsCount = document.GetPagesWithMultipleTextElements().Count;
        var pagesWithMultipleShapesCount = document.GetPagesWithMultipleShapes().Count;
        var pagesWithAtLeastFiveElementsCount = document.GetPagesWithAtLeastElementCount(5).Count;
        var pagesWithAtMostOneElementCount = document.GetPagesWithAtMostElementCount(1).Count;
        var pagesWithExactlyOneLinkCount = document.GetPagesWithExactlyOneLink().Count;
        var pagesWithExactlyOneImageCount = document.GetPagesWithExactlyOneImage().Count;
        var pagesWithExactlyOneTextElementCount = document.GetPagesWithExactlyOneTextElement().Count;
        var pagesWithExactlyOneShapeCount = document.GetPagesWithExactlyOneShape().Count;
        var pagesWithExactlyOneLineCount = document.GetPagesWithExactlyOneLine().Count;
        var pagesWithExactlyOneRectangleCount = document.GetPagesWithExactlyOneRectangle().Count;
        var pagesWithExactlyOneRoundedRectangleCount = document.GetPagesWithExactlyOneRoundedRectangle().Count;
        var pagesWithExactlyOneCircleCount = document.GetPagesWithExactlyOneCircle().Count;
        var pagesWithExactlyOnePolygonCount = document.GetPagesWithExactlyOnePolygon().Count;
        var pagesWithExactlyOneBezierCurveCount = document.GetPagesWithExactlyOneBezierCurve().Count;
        var pagesWithAnyTextSpacingAdjustmentsCount = document.GetPagesWithAnyTextSpacingAdjustments().Count;
        var pagesWithOnlyVectorShapesCount = document.GetPagesWithOnlyVectorShapes().Count;
        var pagesWithOnlyLinksCount = document.GetPagesWithOnlyLinks().Count;
        var pagesWithElementsAndLinksCount = document.GetPagesWithElementsAndLinks().Count;
        var pagesWithoutElementsButWithLinksCount = document.GetPagesWithoutElementsButWithLinks().Count;
        var pagesWithLandscapeOrientationCount = document.GetPagesWithLandscapeOrientation().Count;
        var pagesWithPortraitOrientationCount = document.GetPagesWithPortraitOrientation().Count;
        var pagesWithSquareOrientationCount = document.GetPagesWithSquareOrientation().Count;
        var pagesUsingA4SizeCount = document.GetPagesUsingA4Size().Count;
        var pagesUsingNonA4SizeCount = document.GetPagesUsingNonA4Size().Count;
        var pagesUsingLetterSizeCount = document.GetPagesUsingLetterSize().Count;
        var pagesUsingA3SizeCount = document.GetPagesUsingA3Size().Count;
        var pagesWithPageRotation0Count = document.GetPagesWithPageRotation0().Count;
        var pagesWithPageRotation90Count = document.GetPagesWithPageRotation90().Count;
        var pagesWithPageRotation180Count = document.GetPagesWithPageRotation180().Count;
        var pagesWithPageRotation270Count = document.GetPagesWithPageRotation270().Count;
        var pagesWithAnyPageRotationCount = document.GetPagesWithAnyPageRotation().Count;
        var diagnostics = options.CollectDiagnostics
            ? new PdfGenerationDiagnostics
            {
                PageCount = document.Pages.Count,
                ObjectCount = objects.Count,
                ByteSize = bytes.Length,
                ContentStreamsCompressed = options.CompressContentStreams,
                BookmarkCount = document.Bookmarks.Count,
                TableOfContentsPageCount = document.TableOfContentsPageCount,
                NamedDestinationCount = document.NamedDestinations.Count,
                LinkAnnotationCount = document.Pages.Sum(static page => page.LinkAnnotations.Count),
                SectionCount = document.Sections.Count,
                NestedBookmarkCount = document.Bookmarks.Count(static bookmark => bookmark.Level > 1),
                ImageResourceCount = imagesByKey.Count,
                ImageOpacityResourceCount = opacityObjects.Count,
                ImageDrawCallCount = totalImageDrawCalls,
                ImageCacheHitCount = Math.Max(0, totalImageDrawCalls - imagesByKey.Count),
                WatermarkPageCount = document.LastWatermarkPageCount,
                EmptyPageCount = document.GetPagesWithoutContent().Count,
                RotatedPageCount = document.Pages.Count(static page => page.RotationDegrees != 0),
                HeaderRenderedPageCount = document.LastHeaderRenderedPageCount,
                FooterRenderedPageCount = document.LastFooterRenderedPageCount,
                PageNumberRenderedPageCount = document.LastPageNumberRenderedPageCount,
                TocEntryCount = document.LastTocEntryCount,
                TocPageLinkCount = document.LastTocPageLinkCount,
                WebLinkAnnotationCount = webLinkCount,
                PageLinkAnnotationCount = pageLinkCount,
                NamedDestinationLinkAnnotationCount = namedDestinationLinkCount,
                PagesWithTextCount = pagesWithTextCount,
                PagesWithImageCount = pagesWithImageCount,
                PagesWithLinkCount = pagesWithLinkCount,
                PagesWithBookmarkTargetCount = bookmarkTargetPageCount,
                PagesWithNamedDestinationCount = namedDestinationPageCount,
                PagesWithShapeCount = pagesWithShapeCount,
                WatermarkedUniquePageCount = document.LastWatermarkedUniquePageCount,
                HeaderRenderedUniquePageCount = document.LastHeaderRenderedUniquePageCount,
                FooterRenderedUniquePageCount = document.LastFooterRenderedUniquePageCount,
                PageNumberRenderedUniquePageCount = document.LastPageNumberRenderedUniquePageCount,
                PagesWithCropBoxCount = pagesWithCropBoxCount,
                PagesWithBleedBoxCount = pagesWithBleedBoxCount,
                PagesWithTrimBoxCount = pagesWithTrimBoxCount,
                PagesWithArtBoxCount = pagesWithArtBoxCount,
                PagesWithAnyBoundaryBoxCount = pagesWithAnyBoundaryBoxCount,
                PagesWithAnyTransparencyCount = pagesWithAnyTransparencyCount,
                PagesWithImageTransparencyCount = pagesWithImageTransparencyCount,
                PagesWithTextDecorationsCount = pagesWithTextDecorationsCount,
                PagesWithFlowContentCount = pagesWithFlowContentCount,
                PagesWithWebLinkCount = pagesWithWebLinksCount,
                PagesWithPageLinkCount = pagesWithPageLinksCount,
                PagesWithNamedDestinationLinkCount = pagesWithNamedDestinationLinksCount,
                PagesWithBookmarkCount = pagesWithBookmarksCount,
                PagesWithNamedDestinationCountByPage = pagesWithNamedDestinationsByPageCount,
                PagesWithMixedContentCount = pagesWithMixedContentCount,
                PagesWithLineCount = pagesWithLineCount,
                PagesWithRectangleCount = pagesWithRectangleCount,
                PagesWithRoundedRectangleCount = pagesWithRoundedRectangleCount,
                PagesWithCircleCount = pagesWithCircleCount,
                PagesWithPolygonCount = pagesWithPolygonCount,
                PagesWithBezierCurveCount = pagesWithBezierCurveCount,
                PagesWithUnderlinedTextCount = pagesWithUnderlinedTextCount,
                PagesWithStrikethroughTextCount = pagesWithStrikethroughTextCount,
                PagesWithRotatedTextCount = pagesWithRotatedTextCount,
                PagesWithCharacterSpacedTextCount = pagesWithCharacterSpacedTextCount,
                PagesWithHorizontallyScaledTextCount = pagesWithHorizontallyScaledTextCount,
                PagesWithJustifiedTextCount = pagesWithJustifiedTextCount,
                PagesWithOpaqueImagesOnlyCount = pagesWithOpaqueImagesOnlyCount,
                PagesWithoutLinksCount = pagesWithoutLinksCount,
                PagesWithoutImagesCount = pagesWithoutImagesCount,
                PagesWithoutTextCount = pagesWithoutTextCount,
                PagesWithoutShapesCount = pagesWithoutShapesCount,
                PagesWithOnlyTextCount = pagesWithOnlyTextCount,
                PagesWithOnlyImagesCount = pagesWithOnlyImagesCount,
                PagesWithAnyElementsCount = pagesWithAnyElementsCount,
                PagesWithMultipleLinksCount = pagesWithMultipleLinksCount,
                PagesWithMultipleImagesCount = pagesWithMultipleImagesCount,
                PagesWithMultipleTextElementsCount = pagesWithMultipleTextElementsCount,
                PagesWithMultipleShapesCount = pagesWithMultipleShapesCount,
                PagesWithAtLeastFiveElementsCount = pagesWithAtLeastFiveElementsCount,
                PagesWithAtMostOneElementCount = pagesWithAtMostOneElementCount,
                PagesWithExactlyOneLinkCount = pagesWithExactlyOneLinkCount,
                PagesWithExactlyOneImageCount = pagesWithExactlyOneImageCount,
                PagesWithExactlyOneTextElementCount = pagesWithExactlyOneTextElementCount,
                PagesWithExactlyOneShapeCount = pagesWithExactlyOneShapeCount,
                PagesWithExactlyOneLineCount = pagesWithExactlyOneLineCount,
                PagesWithExactlyOneRectangleCount = pagesWithExactlyOneRectangleCount,
                PagesWithExactlyOneRoundedRectangleCount = pagesWithExactlyOneRoundedRectangleCount,
                PagesWithExactlyOneCircleCount = pagesWithExactlyOneCircleCount,
                PagesWithExactlyOnePolygonCount = pagesWithExactlyOnePolygonCount,
                PagesWithExactlyOneBezierCurveCount = pagesWithExactlyOneBezierCurveCount,
                PagesWithAnyTextSpacingAdjustmentsCount = pagesWithAnyTextSpacingAdjustmentsCount,
                PagesWithOnlyVectorShapesCount = pagesWithOnlyVectorShapesCount,
                PagesWithOnlyLinksCount = pagesWithOnlyLinksCount,
                PagesWithElementsAndLinksCount = pagesWithElementsAndLinksCount,
                PagesWithoutElementsButWithLinksCount = pagesWithoutElementsButWithLinksCount,
                PagesWithLandscapeOrientationCount = pagesWithLandscapeOrientationCount,
                PagesWithPortraitOrientationCount = pagesWithPortraitOrientationCount,
                PagesWithSquareOrientationCount = pagesWithSquareOrientationCount,
                PagesUsingA4SizeCount = pagesUsingA4SizeCount,
                PagesUsingNonA4SizeCount = pagesUsingNonA4SizeCount,
                PagesUsingLetterSizeCount = pagesUsingLetterSizeCount,
                PagesUsingA3SizeCount = pagesUsingA3SizeCount,
                PagesWithPageRotation0Count = pagesWithPageRotation0Count,
                PagesWithPageRotation90Count = pagesWithPageRotation90Count,
                PagesWithPageRotation180Count = pagesWithPageRotation180Count,
                PagesWithPageRotation270Count = pagesWithPageRotation270Count,
                PagesWithAnyPageRotationCount = pagesWithAnyPageRotationCount
            }
            : null;

        return new PdfWriteResult
        {
            Bytes = bytes,
            Diagnostics = diagnostics
        };
    }

    private static string BuildPageBoxSegment(string name, PdfPoint? lowerLeft, PdfPoint? upperRight)
    {
        if (lowerLeft is null || upperRight is null)
        {
            return string.Empty;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            " /{0} [{1} {2} {3} {4}]",
            name,
            FormatNumber(lowerLeft.Value.X),
            FormatNumber(lowerLeft.Value.Y),
            FormatNumber(upperRight.Value.X),
            FormatNumber(upperRight.Value.Y));
    }

    private static string BuildOpenAction(PdfDocument document, IReadOnlyList<int> pageObjectIds)
    {
        var prefs = document.ViewerPreferences;

        if (prefs.OpenPageNumber is null)
        {
            return string.Empty;
        }

        var pageNumber = prefs.OpenPageNumber.Value;
        if (pageNumber < 1 || pageNumber > pageObjectIds.Count)
        {
            return string.Empty;
        }

        var pageObjectId = pageObjectIds[pageNumber - 1];

        if (prefs.OpenZoomPercent is { } zoom)
        {
            var zoomScale = zoom / 100.0;
            return $" /OpenAction [{pageObjectId} 0 R /XYZ null null {zoomScale.ToString("0.###", CultureInfo.InvariantCulture)}]";
        }

        return $" /OpenAction [{pageObjectId} 0 R /Fit]";
    }

    private static string BuildReviewAnnotationObject(PdfReviewAnnotation annotation, int pageObjectId)
    {
        var rect = FormatRect(annotation.X, annotation.Y, annotation.Width, annotation.Height);
        var color = FormatColorArray(annotation.Color);
        var opacitySegment = annotation.Opacity < 1 ? $" /CA {FormatNumber(annotation.Opacity)}" : string.Empty;
        var contents = EscapeLiteralString(annotation.Contents ?? string.Empty);

        return annotation.Type switch
        {
            PdfReviewAnnotationType.StickyNote =>
                $"<< /Type /Annot /Subtype /Text /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /Name /Comment{opacitySegment} >>\n",
            PdfReviewAnnotationType.FreeText =>
                $"<< /Type /Annot /Subtype /FreeText /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /DA (/Helv 10 Tf 0 g) /Border [0 0 1]{opacitySegment} >>\n",
            PdfReviewAnnotationType.Highlight =>
                BuildMarkupAnnotationObject("Highlight", annotation, pageObjectId, rect, color, opacitySegment, contents),
            PdfReviewAnnotationType.Underline =>
                BuildMarkupAnnotationObject("Underline", annotation, pageObjectId, rect, color, opacitySegment, contents),
            PdfReviewAnnotationType.StrikeOut =>
                BuildMarkupAnnotationObject("StrikeOut", annotation, pageObjectId, rect, color, opacitySegment, contents),
            PdfReviewAnnotationType.Square =>
                $"<< /Type /Annot /Subtype /Square /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /BS << /W 2 /S /S >>{opacitySegment} >>\n",
            PdfReviewAnnotationType.Circle =>
                $"<< /Type /Annot /Subtype /Circle /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /BS << /W 2 /S /S >>{opacitySegment} >>\n",
            PdfReviewAnnotationType.Redaction =>
                $"<< /Type /Annot /Subtype /Redact /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /IC {color}{opacitySegment} >>\n",
            _ => throw new ArgumentOutOfRangeException(nameof(annotation), annotation.Type, "Unsupported review annotation type.")
        };
    }

    private static string BuildMarkupAnnotationObject(
        string subtype,
        PdfReviewAnnotation annotation,
        int pageObjectId,
        string rect,
        string color,
        string opacitySegment,
        string contents)
    {
        var quadPoints = FormatQuadPoints(annotation.X, annotation.Y, annotation.Width, annotation.Height);
        return $"<< /Type /Annot /Subtype /{subtype} /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /QuadPoints {quadPoints}{opacitySegment} >>\n";
    }

    private static string FormatRect(double x, double y, double width, double height)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0} {1} {2} {3}]",
            FormatNumber(x),
            FormatNumber(y),
            FormatNumber(x + width),
            FormatNumber(y + height));
    }

    private static string FormatQuadPoints(double x, double y, double width, double height)
    {
        var left = x;
        var right = x + width;
        var bottom = y;
        var top = y + height;
        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0} {1} {2} {3} {4} {5} {6} {7}]",
            FormatNumber(left),
            FormatNumber(top),
            FormatNumber(right),
            FormatNumber(top),
            FormatNumber(left),
            FormatNumber(bottom),
            FormatNumber(right),
            FormatNumber(bottom));
    }

    private static string FormatColorArray(PdfColor color)
    {
        return $"[{FormatNumber(color.Red)} {FormatNumber(color.Green)} {FormatNumber(color.Blue)}]";
    }

    private static byte[] Serialize(
        List<PdfIndirectObject> objects,
        int rootObjectId,
        int? infoObjectId,
        Security.StandardSecurityHandler? security = null,
        int? encryptObjectId = null)
    {
        objects.Sort(static (x, y) => x.Id.CompareTo(y.Id));

        using var stream = new MemoryStream();

        static void WriteAscii(MemoryStream destination, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            destination.Write(bytes, 0, bytes.Length);
        }

        WriteAscii(stream, "%PDF-1.4\n");

        var offsets = new Dictionary<int, long>();

        foreach (var obj in objects)
        {
            offsets[obj.Id] = stream.Position;

            // Encrypt every object except the /Encrypt dictionary itself.
            var contentBytes = security is not null && obj.Id != encryptObjectId
                ? security.EncryptObjectBody(obj.Id, 0, obj.ContentBytes)
                : obj.ContentBytes;

            WriteAscii(stream, $"{obj.Id} 0 obj\n");
            stream.Write(contentBytes, 0, contentBytes.Length);

            if (contentBytes.Length == 0 || contentBytes[^1] != (byte)'\n')
            {
                WriteAscii(stream, "\n");
            }

            WriteAscii(stream, "endobj\n");
        }

        var maxObjectId = objects[^1].Id;
        var xrefOffset = stream.Position;

        WriteAscii(stream, "xref\n");
        WriteAscii(stream, $"0 {maxObjectId + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");

        for (var id = 1; id <= maxObjectId; id++)
        {
            if (offsets.TryGetValue(id, out var offset))
            {
                WriteAscii(stream, $"{offset:D10} 00000 n \n");
            }
            else
            {
                WriteAscii(stream, "0000000000 00000 f \n");
            }
        }

        WriteAscii(stream, "trailer\n");

        var infoSegment = infoObjectId is { } actualInfoObjectId
            ? $" /Info {actualInfoObjectId} 0 R"
            : string.Empty;

        if (security is not null && encryptObjectId is { } encryptId)
        {
            // /ID is required for encrypted documents (its first element feeds key derivation).
            var idHex = Convert.ToHexString(security.DocumentId);
            WriteAscii(stream,
                $"<< /Size {maxObjectId + 1} /Root {rootObjectId} 0 R{infoSegment} /Encrypt {encryptId} 0 R /ID [<{idHex}> <{idHex}>] >>\n");
        }
        else
        {
            WriteAscii(stream, $"<< /Size {maxObjectId + 1} /Root {rootObjectId} 0 R{infoSegment} >>\n");
        }

        WriteAscii(stream, "startxref\n");
        WriteAscii(stream, $"{xrefOffset}\n");
        WriteAscii(stream, "%%EOF\n");

        return stream.ToArray();
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string GetBaseFontName(PdfStandardFont font)
    {
        return font switch
        {
            PdfStandardFont.Helvetica => "Helvetica",
            PdfStandardFont.HelveticaBold => "Helvetica-Bold",
            PdfStandardFont.HelveticaOblique => "Helvetica-Oblique",
            PdfStandardFont.HelveticaBoldOblique => "Helvetica-BoldOblique",
            PdfStandardFont.TimesRoman => "Times-Roman",
            PdfStandardFont.TimesBold => "Times-Bold",
            PdfStandardFont.TimesItalic => "Times-Italic",
            PdfStandardFont.TimesBoldItalic => "Times-BoldItalic",
            PdfStandardFont.Courier => "Courier",
            PdfStandardFont.CourierBold => "Courier-Bold",
            PdfStandardFont.CourierOblique => "Courier-Oblique",
            PdfStandardFont.CourierBoldOblique => "Courier-BoldOblique",
            _ => throw new ArgumentOutOfRangeException(nameof(font), font, "Unsupported standard font.")
        };
    }

    private static string BuildInfoObject(PdfDocumentInfo info)
    {
        var entries = new List<string>();

        AppendIfValue(entries, "Title", info.Title);
        AppendIfValue(entries, "Author", info.Author);
        AppendIfValue(entries, "Subject", info.Subject);
        AppendIfValue(entries, "Keywords", info.Keywords);
        AppendIfValue(entries, "Creator", info.Creator);
        AppendIfValue(entries, "Producer", info.Producer);
        AppendIfDate(entries, "CreationDate", info.CreationDate);
        AppendIfDate(entries, "ModDate", info.ModificationDate);

        return $"<< {string.Join(" ", entries)} >>\n";
    }

    private static void AppendIfValue(List<string> entries, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add($"/{key} ({EscapeLiteralString(value)})");
        }
    }

    private static void AppendIfDate(List<string> entries, string key, DateTimeOffset? value)
    {
        if (value is { } date)
        {
            entries.Add($"/{key} ({FormatPdfDate(date)})");
        }
    }

    private static string FormatPdfDate(DateTimeOffset value)
    {
        var offset = value.Offset;
        var sign = offset >= TimeSpan.Zero ? "+" : "-";
        var absolute = offset.Duration();
        return $"D:{value:yyyyMMddHHmmss}{sign}{absolute.Hours:00}'{absolute.Minutes:00}'";
    }

    private static string BuildViewerPreferences(PdfViewerPreferencesOptions options)
    {
        var entries = new List<string>();

        if (options.HideToolbar)
        {
            entries.Add("/HideToolbar true");
        }

        if (options.HideMenubar)
        {
            entries.Add("/HideMenubar true");
        }

        if (options.HideWindowUI)
        {
            entries.Add("/HideWindowUI true");
        }

        if (options.FitWindow)
        {
            entries.Add("/FitWindow true");
        }

        if (options.CenterWindow)
        {
            entries.Add("/CenterWindow true");
        }

        if (options.DisplayDocTitle)
        {
            entries.Add("/DisplayDocTitle true");
        }

        if (options.ReadingDirection is { } direction)
        {
            entries.Add(direction == PdfReadingDirection.RightToLeft ? "/Direction /R2L" : "/Direction /L2R");
        }

        if (options.DisablePrintScaling)
        {
            entries.Add("/PrintScaling /None");
        }

        if (options.DuplexFlipLongEdge)
        {
            entries.Add("/Duplex /DuplexFlipLongEdge");
        }
        else if (options.DuplexFlipShortEdge)
        {
            entries.Add("/Duplex /DuplexFlipShortEdge");
        }

        return entries.Count > 0 ? $" /ViewerPreferences << {string.Join(" ", entries)} >>" : string.Empty;
    }

    private static string BuildPageMode(PdfPageMode? mode)
    {
        return mode switch
        {
            PdfPageMode.UseNone => " /PageMode /UseNone",
            PdfPageMode.UseOutlines => " /PageMode /UseOutlines",
            PdfPageMode.UseThumbs => " /PageMode /UseThumbs",
            PdfPageMode.FullScreen => " /PageMode /FullScreen",
            _ => string.Empty
        };
    }

    private static string BuildPageLayout(PdfPageLayoutMode? layout)
    {
        return layout switch
        {
            PdfPageLayoutMode.SinglePage => " /PageLayout /SinglePage",
            PdfPageLayoutMode.OneColumn => " /PageLayout /OneColumn",
            PdfPageLayoutMode.TwoColumnLeft => " /PageLayout /TwoColumnLeft",
            PdfPageLayoutMode.TwoColumnRight => " /PageLayout /TwoColumnRight",
            _ => string.Empty
        };
    }

    private static string EscapeLiteralString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static byte[] BuildImageObject(PdfImageData image, int? softMaskObjectId = null)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, Encoding.ASCII, leaveOpen: true);

        var softMaskSegment = softMaskObjectId is { } id ? $" /SMask {id} 0 R" : string.Empty;
        var header = image.DecodeParameters is { Length: > 0 }
            ? $"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /{image.ColorSpaceName} /BitsPerComponent {image.BitsPerComponent} /Filter /{image.FilterName} /DecodeParms << {image.DecodeParameters} >>{softMaskSegment} /Length {image.Data.Length} >>\nstream\n"
            : $"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /{image.ColorSpaceName} /BitsPerComponent {image.BitsPerComponent} /Filter /{image.FilterName}{softMaskSegment} /Length {image.Data.Length} >>\nstream\n";

        writer.Write(Encoding.ASCII.GetBytes(header));
        writer.Write(image.Data);
        writer.Write(Encoding.ASCII.GetBytes("\nendstream\n"));
        writer.Flush();

        return memory.ToArray();
    }

    private static string BuildResourcesDictionary(string fontDictionary, string xObjectDictionary, string extGStateDictionary)
    {
        var parts = new List<string>
        {
            $"/Font << {fontDictionary} >>"
        };

        if (!string.IsNullOrWhiteSpace(xObjectDictionary))
        {
            parts.Add($"/XObject << {xObjectDictionary} >>");
        }

        if (!string.IsNullOrWhiteSpace(extGStateDictionary))
        {
            parts.Add($"/ExtGState << {extGStateDictionary} >>");
        }

        return $"<< {string.Join(" ", parts)} >>";
    }

    private static byte[] BuildContentStreamObject(byte[] contentData, bool compressed)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, Encoding.ASCII, leaveOpen: true);

        var header = compressed
            ? $"<< /Length {contentData.Length} /Filter /FlateDecode >>\nstream\n"
            : $"<< /Length {contentData.Length} >>\nstream\n";

        writer.Write(Encoding.ASCII.GetBytes(header));
        writer.Write(contentData);
        writer.Write(Encoding.ASCII.GetBytes("\nendstream\n"));
        writer.Flush();

        return memory.ToArray();
    }

    private static byte[] CompressZlib(byte[] data)
    {
        using var destination = new MemoryStream();
        using (var zlib = new ZLibStream(destination, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return destination.ToArray();
    }

    private static byte[] BuildFontStreamObject(byte[] compressedFontData, int originalLength)
    {
        using var memory = new MemoryStream();
        var header = $"<< /Length {compressedFontData.Length} /Length1 {originalLength} /Filter /FlateDecode >>\nstream\n";
        memory.Write(Encoding.ASCII.GetBytes(header));
        memory.Write(compressedFontData);
        memory.Write(Encoding.ASCII.GetBytes("\nendstream\n"));
        return memory.ToArray();
    }

    private static string BuildToUnicodeCMap(IReadOnlyList<int> codePoints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("/CIDInit /ProcSet findresource begin");
        sb.AppendLine("12 dict begin");
        sb.AppendLine("begincmap");
        sb.AppendLine("/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> def");
        sb.AppendLine("/CMapName /Adobe-Identity-H def");
        sb.AppendLine("/CMapType 1 def");
        sb.AppendLine("1 begincodespacerange");
        sb.AppendLine("<0000> <FFFF>");
        sb.AppendLine("endcodespacerange");

        const int chunkSize = 100;
        for (var i = 0; i < codePoints.Count; i += chunkSize)
        {
            var chunk = codePoints.Skip(i).Take(chunkSize).ToList();
            sb.AppendLine($"{chunk.Count} beginbfchar");
            foreach (var cp in chunk)
            {
                var cidHex = cp.ToString("X4", CultureInfo.InvariantCulture);
                var unicodeHex = cp.ToString("X4", CultureInfo.InvariantCulture);
                sb.AppendLine($"<{cidHex}> <{unicodeHex}>");
            }
            sb.AppendLine("endbfchar");
        }

        sb.AppendLine("endcmap");
        sb.AppendLine("CMapName currentdict /CMap defineresource pop");
        sb.AppendLine("end");
        sb.AppendLine("end");
        return sb.ToString();
    }

    private static string BuildCidFontWidthsArray(PdfEmbeddedFont font, IReadOnlyList<int> codePoints)
    {
        if (codePoints.Count == 0) return "[]";

        var sb = new StringBuilder("[");
        foreach (var cp in codePoints)
        {
            var w = font.GetPdfAdvanceWidth(cp);
            sb.Append(CultureInfo.InvariantCulture, $"{cp} [{w}] ");
        }
        sb.Append(']');
        return sb.ToString();
    }

    private sealed record EmbeddedFontIds(
        string ResourceName,
        int FontStreamId,
        int DescriptorId,
        int ToUnicodeId,
        int CidFontId,
        int Type0Id);
}
