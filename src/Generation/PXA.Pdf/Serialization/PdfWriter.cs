using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security;
using System.Text;
using PXA.Pdf.Layout;
using PXA.Pdf.Rendering;

namespace PXA.Pdf.Serialization;

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

        var compliance = PdfCompliancePreflight.Validate(document, options);
        if (!compliance.IsValid)
        {
            throw new PdfComplianceException(compliance);
        }

        var accessibility = PdfAccessibilityPreflight.Validate(document, options);
        if (!accessibility.IsValid)
            throw new PdfAccessibilityException(accessibility);

        var isPdfA = options.ComplianceProfile is PdfComplianceProfile.PdfA2B or PdfComplianceProfile.PdfA3B;
        var isPdfUa = options.AccessibilityProfile == PdfAccessibilityProfile.PdfUa1;
        var pdfAPart = options.ComplianceProfile == PdfComplianceProfile.PdfA3B ? 3 : 2;

        var nextObjectId = 1;
        var catalogObjectId = nextObjectId++;
        var pagesObjectId = nextObjectId++;
        var infoObjectId = document.Info.HasValues ? nextObjectId++ : (int?)null;
        var metadataObjectId = isPdfA || isPdfUa ? nextObjectId++ : (int?)null;
        var outputIntentObjectId = isPdfA ? nextObjectId++ : (int?)null;
        var iccProfileObjectId = isPdfA ? nextObjectId++ : (int?)null;
        var associatedFileObjects = document.AssociatedFiles
            .Select(file => new AssociatedFileObjectIds(file, nextObjectId++, nextObjectId++))
            .ToList();

        var usedFonts = document.Pages
            .SelectMany(static page => page.Elements)
            .OfType<TextElement>()
            .Where(static element => element.EmbeddedFont is null)
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

            // 3. ToUnicode CMap stream (maps TrueType glyph-ID CIDs back to Unicode for text extraction)
            var cMapBytes = Encoding.ASCII.GetBytes(BuildToUnicodeCMap(ef, usedCodePoints));
            objects.Add(new PdfIndirectObject(ids.ToUnicodeId, BuildContentStreamObject(cMapBytes, compressed: false)));

            // 4. CIDFont with /W widths array
            var widthsArray = BuildCidFontWidthsArray(ef, usedCodePoints);
            objects.Add(new PdfIndirectObject(ids.CidFontId,
                $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{ef.BaseFontName} " +
                $"/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> " +
                $"/DW 1000 /W {widthsArray} /CIDToGIDMap /Identity " +
                $"/FontDescriptor {ids.DescriptorId} 0 R >>\n"));

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
            p.ComboBoxAnnotations.Count > 0 || p.MultilineTextFields.Count > 0 || p.TextFields.Count > 0
            || p.CheckBoxAnnotations.Count > 0 || p.ListBoxAnnotations.Count > 0
            || p.RadioButtonAnnotations.Count > 0 || p.SignatureFieldAnnotations.Count > 0
            || p.PushButtonAnnotations.Count > 0);
        var hasFreeTextReviewAnnotations = document.Pages.Any(static p =>
            p.ReviewAnnotations.Any(static annotation => annotation.Type == PdfReviewAnnotationType.FreeText));
        var acroFormHelvFontObjectId = hasAcroFields || hasFreeTextReviewAnnotations ? nextObjectId++ : (int?)null;
        var pageComboAnnotObjectIds = new List<int>[pageCount];
        var pageMultilineAnnotObjectIds = new List<int>[pageCount];
        var pageTextFieldAnnotObjectIds = new List<int>[pageCount];
        var pageCheckBoxAnnotObjectIds = new List<int>[pageCount];
        var pageListBoxAnnotObjectIds = new List<int>[pageCount];
        var pageRadioButtonAnnotObjectIds = new List<int>[pageCount];
        var pageSignatureFieldAnnotObjectIds = new List<int>[pageCount];
        var pagePushButtonAnnotObjectIds = new List<int>[pageCount];
        var pageReviewAnnotObjectIds = new List<int>[pageCount];
        var pageReviewAppearanceObjectIds = new List<int>[pageCount];
        var allComboFieldObjectIds = new List<int>();
        var allMultilineFieldObjectIds = new List<int>();
        var allTextFieldObjectIds = new List<int>();
        var allCheckBoxObjectIds = new List<int>();
        var allListBoxObjectIds = new List<int>();
        var allSignatureFieldObjectIds = new List<int>();
        var allPushButtonObjectIds = new List<int>();
        var radioGroupParentObjectIds = document.Pages
            .SelectMany(static page => page.RadioButtonAnnotations)
            .Select(static radio => radio.FieldName)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(static fieldName => fieldName, _ => nextObjectId++, StringComparer.Ordinal);

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

            var listBoxIds = new List<int>();
            foreach (var _ in document.Pages[i].ListBoxAnnotations)
            {
                var listBoxId = nextObjectId++;
                listBoxIds.Add(listBoxId);
                allListBoxObjectIds.Add(listBoxId);
            }
            pageListBoxAnnotObjectIds[i] = listBoxIds;

            var radioButtonIds = new List<int>();
            foreach (var _ in document.Pages[i].RadioButtonAnnotations)
                radioButtonIds.Add(nextObjectId++);
            pageRadioButtonAnnotObjectIds[i] = radioButtonIds;

            var signatureFieldIds = new List<int>();
            foreach (var _ in document.Pages[i].SignatureFieldAnnotations)
            {
                var signatureId = nextObjectId++;
                signatureFieldIds.Add(signatureId);
                allSignatureFieldObjectIds.Add(signatureId);
            }
            pageSignatureFieldAnnotObjectIds[i] = signatureFieldIds;

            var pushButtonIds = new List<int>();
            foreach (var _ in document.Pages[i].PushButtonAnnotations)
            {
                var pushButtonId = nextObjectId++;
                pushButtonIds.Add(pushButtonId);
                allPushButtonObjectIds.Add(pushButtonId);
            }
            pagePushButtonAnnotObjectIds[i] = pushButtonIds;

            var reviewIds = new List<int>();
            var reviewAppearanceIds = new List<int>();
            foreach (var _ in document.Pages[i].ReviewAnnotations)
            {
                reviewIds.Add(nextObjectId++);
                reviewAppearanceIds.Add(nextObjectId++);
            }

            pageReviewAnnotObjectIds[i] = reviewIds;
            pageReviewAppearanceObjectIds[i] = reviewAppearanceIds;
            pageObjects.Add(pageObjectIds[i]);
        }

        var nextStructureParentKey = pageCount;
        var accessibleAnnotations = new List<AccessibleAnnotationReference>();
        if (isPdfUa)
        {
            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                RegisterAnnotations(document.Pages[pageIndex].LinkAnnotations, pageAnnotationObjectIds[pageIndex], pageIndex);
                RegisterAnnotations(document.Pages[pageIndex].ComboBoxAnnotations, pageComboAnnotObjectIds[pageIndex], pageIndex);
                RegisterAnnotations(document.Pages[pageIndex].MultilineTextFields, pageMultilineAnnotObjectIds[pageIndex], pageIndex);
                RegisterAnnotations(document.Pages[pageIndex].TextFields, pageTextFieldAnnotObjectIds[pageIndex], pageIndex);
                RegisterAnnotations(document.Pages[pageIndex].CheckBoxAnnotations, pageCheckBoxAnnotObjectIds[pageIndex], pageIndex);
                RegisterAnnotations(document.Pages[pageIndex].ListBoxAnnotations, pageListBoxAnnotObjectIds[pageIndex], pageIndex);
                RegisterAnnotations(document.Pages[pageIndex].RadioButtonAnnotations, pageRadioButtonAnnotObjectIds[pageIndex], pageIndex);
                RegisterAnnotations(document.Pages[pageIndex].SignatureFieldAnnotations, pageSignatureFieldAnnotObjectIds[pageIndex], pageIndex);
                RegisterAnnotations(document.Pages[pageIndex].PushButtonAnnotations, pagePushButtonAnnotObjectIds[pageIndex], pageIndex);
            }
        }

        void RegisterAnnotations<T>(IReadOnlyList<T> annotations, IReadOnlyList<int> objectIds, int pageIndex)
            where T : IPdfAccessibleAnnotation
        {
            for (var annotationIndex = 0; annotationIndex < annotations.Count; annotationIndex++)
            {
                var annotation = annotations[annotationIndex];
                if (annotation.Accessibility is null)
                    continue;
                accessibleAnnotations.Add(new AccessibleAnnotationReference(
                    pageIndex, annotation, objectIds[annotationIndex], nextStructureParentKey++));
            }
        }
        var accessibleAnnotationMap = accessibleAnnotations.ToDictionary(
            reference => reference.Annotation,
            (IEqualityComparer<IPdfAccessibleAnnotation>)ReferenceEqualityComparer.Instance);
        var accessibleAnnotationByObjectId = accessibleAnnotations.ToDictionary(reference => reference.ObjectId);

        var structureTreeRootObjectId = isPdfUa ? nextObjectId++ : (int?)null;
        var parentTreeObjectId = isPdfUa ? nextObjectId++ : (int?)null;
        var documentStructureObjectId = isPdfUa ? nextObjectId++ : (int?)null;
        var structureObjectIds = new Dictionary<PdfAccessibilityProperties, int>(ReferenceEqualityComparer.Instance);
        var pageMarkedContentIds = new Dictionary<PdfPageElement, int>[pageCount];
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var markedContentIds = new Dictionary<PdfPageElement, int>(ReferenceEqualityComparer.Instance);
            var markedContentId = 0;
            foreach (var semantic in document.Pages[pageIndex].SemanticElements)
            {
                if (!structureObjectIds.ContainsKey(semantic))
                    structureObjectIds[semantic] = nextObjectId++;
            }
            foreach (var element in document.Pages[pageIndex].Elements.Where(element => element.Accessibility is not null))
            {
                markedContentIds[element] = markedContentId++;
                if (!structureObjectIds.ContainsKey(element.Accessibility!))
                    structureObjectIds[element.Accessibility!] = nextObjectId++;
            }
            pageMarkedContentIds[pageIndex] = markedContentIds;
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
                .Where(static element => element.EmbeddedFont is null)
                .Select(static element => element.Font)
                .Distinct()
                .ToList();

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

            var contentStream = PdfPageContentRenderer.RenderPage(
                page, pageFontResources, pageImageResources, pageOpacityResources, pageEmbeddedFontResources,
                isPdfUa ? pageMarkedContentIds[pageIndex] : null, isPdfUa);
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
                var linkStructure = BuildAnnotationStructureSegment(link, accessibleAnnotationMap);
                var linkDescription = BuildAnnotationDescriptionSegment(link.Accessibility);
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
                    annotationObject = $"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] /A << /S /URI /URI ({EscapeLiteralString(url)}) >>{linkDescription}{linkStructure} >>\n";
                }
                else if (link.TargetPageNumber is { } targetPageNumber)
                {
                    if (targetPageNumber < 1 || targetPageNumber > pageCount)
                    {
                        throw new InvalidOperationException($"Page link target {targetPageNumber} is out of range for this document.");
                    }

                    var targetPageObjectId = pageObjectIds[targetPageNumber - 1];
                    annotationObject = $"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] /Dest [{targetPageObjectId} 0 R /Fit]{linkDescription}{linkStructure} >>\n";
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
                        ? $"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] /Dest [{targetPageObjectId} 0 R /XYZ null {FormatNumber(y)} null]{linkDescription}{linkStructure} >>\n"
                        : $"<< /Type /Annot /Subtype /Link /Rect {rect} /Border [0 0 0] /Dest [{targetPageObjectId} 0 R /Fit]{linkDescription}{linkStructure} >>\n";
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
                var comboStructure = BuildAnnotationStructureSegment(combo, accessibleAnnotationMap);
                var comboDescription = OptionalHexEntry("TU", AccessibleDescription(combo.Accessibility, combo.FieldName));

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
                    $"/BS << /W 1 /S /S >>{comboDescription}{comboStructure} >>\n";

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
                var multilineStructure = BuildAnnotationStructureSegment(ml, accessibleAnnotationMap);
                var multilineDescription = OptionalHexEntry("TU", AccessibleDescription(ml.Accessibility, ml.FieldName));

                var mlWidgetObject =
                    $"<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 4096 " +
                    $"/T ({EscapeLiteralString(ml.FieldName)}) " +
                    $"/V ({EscapeLiteralString(ml.DefaultValue)}) " +
                    $"/Rect {mlRect} " +
                    $"/P {pageObjectId} 0 R " +
                    $"/DA (/Helv {FormatNumber(ml.FontSize)} Tf 0 g) " +
                    $"/DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> " +
                    $"/MK << /BC [0 0 0] /BG [1 1 1] >> " +
                    $"/BS << /W 1 /S /S >>{multilineDescription}{multilineStructure} >>\n";

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
                var textFieldStructure = BuildAnnotationStructureSegment(tf, accessibleAnnotationMap);
                var textFieldDescription = OptionalHexEntry("TU", AccessibleDescription(tf.Accessibility, tf.FieldName));

                var tfWidgetObject =
                    $"<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 0 " +
                    $"/T ({EscapeLiteralString(tf.FieldName)}) " +
                    $"/V ({EscapeLiteralString(tf.DefaultValue)}) " +
                    $"/Rect {tfRect} " +
                    $"/P {pageObjectId} 0 R " +
                    $"/DA (/Helv {FormatNumber(tf.FontSize)} Tf 0 g) " +
                    $"/DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> " +
                    $"/MK << /BC [0 0 0] /BG [1 1 1] >> " +
                    $"/BS << /W 1 /S /S >>{textFieldDescription}{textFieldStructure} >>\n";

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
                var checkBoxStructure = BuildAnnotationStructureSegment(cb, accessibleAnnotationMap);
                var checkBoxDescription = OptionalHexEntry("TU", AccessibleDescription(cb.Accessibility, cb.FieldName));
                var cbWidgetObject =
                    $"<< /Type /Annot /Subtype /Widget /FT /Btn /Ff 0 " +
                    $"/T ({EscapeLiteralString(cb.FieldName)}) " +
                    $"/V {cbValue} /AS {cbValue} " +
                    $"/Rect {cbRect} " +
                    $"/P {pageObjectId} 0 R " +
                    $"/DA (/Helv 0 Tf 0 g) " +
                    $"/DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> " +
                    $"/MK << /BC [0 0 0] /BG [1 1 1] /CA (8) >> " +
                    $"/BS << /W 1 /S /S >>{checkBoxDescription}{checkBoxStructure} >>\n";

                objects.Add(new PdfIndirectObject(cbObjectId, cbWidgetObject));
            }

            var pageListBoxIds = pageListBoxAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.ListBoxAnnotations.Count; i++)
            {
                var listBox = page.ListBoxAnnotations[i];
                var objectId = pageListBoxIds[i];
                var rect = BuildAnnotationRectangle(listBox.X, listBox.Y, listBox.Width, listBox.Height);
                var listOptions = string.Join(" ", listBox.Options.Select(PdfHexString));
                var values = listBox.SelectedValues.Where(value => listBox.Options.Contains(value, StringComparer.Ordinal)).ToList();
                var valueSegment = values.Count switch
                {
                    0 => string.Empty,
                    1 => $" /V {PdfHexString(values[0])}",
                    _ => $" /V [{string.Join(" ", values.Select(PdfHexString))}]"
                };
                var flags = listBox.MultiSelect ? 2097152 : 0;
                var structure = BuildAnnotationStructureSegment(listBox, accessibleAnnotationMap);
                var description = OptionalHexEntry("TU", AccessibleDescription(listBox.Accessibility, listBox.FieldName));
                var widget = $"<< /Type /Annot /Subtype /Widget /FT /Ch /Ff {flags} " +
                    $"/T ({EscapeLiteralString(listBox.FieldName)}) /Opt [{listOptions}]{valueSegment} " +
                    $"/Rect {rect} /P {pageObjectId} 0 R /DA (/Helv {FormatNumber(listBox.FontSize)} Tf 0 g) " +
                    $"/DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> " +
                    $"/MK << /BC [0 0 0] /BG [1 1 1] >> /BS << /W 1 /S /S >>{description}{structure} >>\n";
                objects.Add(new PdfIndirectObject(objectId, widget));
            }

            var pageRadioIds = pageRadioButtonAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.RadioButtonAnnotations.Count; i++)
            {
                var radio = page.RadioButtonAnnotations[i];
                var objectId = pageRadioIds[i];
                var rect = BuildAnnotationRectangle(radio.X, radio.Y, radio.Width, radio.Height);
                var state = $"R{radio.OptionIndex + 1}";
                var structure = BuildAnnotationStructureSegment(radio, accessibleAnnotationMap);
                var description = OptionalHexEntry("TU", AccessibleDescription(radio.Accessibility, radio.ExportValue));
                var widget = $"<< /Type /Annot /Subtype /Widget /Parent {radioGroupParentObjectIds[radio.FieldName]} 0 R " +
                    $"/Rect {rect} /P {pageObjectId} 0 R /AS /{(radio.IsSelected ? state : "Off")} " +
                    $"/Border [0 0 0]{description}{structure} >>\n";
                objects.Add(new PdfIndirectObject(objectId, widget));
            }

            var pageSignatureIds = pageSignatureFieldAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.SignatureFieldAnnotations.Count; i++)
            {
                var signature = page.SignatureFieldAnnotations[i];
                var objectId = pageSignatureIds[i];
                var rect = BuildAnnotationRectangle(signature.X, signature.Y, signature.Width, signature.Height);
                var structure = BuildAnnotationStructureSegment(signature, accessibleAnnotationMap);
                var description = OptionalHexEntry("TU", AccessibleDescription(signature.Accessibility, signature.FieldName));
                var widget = $"<< /Type /Annot /Subtype /Widget /FT /Sig /Ff 0 " +
                    $"/T ({EscapeLiteralString(signature.FieldName)}) /Rect {rect} /P {pageObjectId} 0 R " +
                    $"/Border [0 0 0]{description}{structure} >>\n";
                objects.Add(new PdfIndirectObject(objectId, widget));
            }

            var pagePushButtonIds = pagePushButtonAnnotObjectIds[pageIndex];
            for (var i = 0; i < page.PushButtonAnnotations.Count; i++)
            {
                var button = page.PushButtonAnnotations[i];
                var objectId = pagePushButtonIds[i];
                var rect = BuildAnnotationRectangle(button.X, button.Y, button.Width, button.Height);
                var structure = BuildAnnotationStructureSegment(button, accessibleAnnotationMap);
                var description = OptionalHexEntry("TU", AccessibleDescription(button.Accessibility, button.Caption));
                var action = button.Action switch
                {
                    PdfPushButtonAction.Submit => " /A << /S /SubmitForm /Flags 4 >>",
                    PdfPushButtonAction.Reset => " /A << /S /ResetForm >>",
                    _ => string.Empty
                };
                var widget = $"<< /Type /Annot /Subtype /Widget /FT /Btn /Ff 65536 " +
                    $"/T ({EscapeLiteralString(button.FieldName)}) /Rect {rect} /P {pageObjectId} 0 R " +
                    $"/MK << /CA {PdfHexString(button.Caption)} >> /Border [0 0 0]{action}{description}{structure} >>\n";
                objects.Add(new PdfIndirectObject(objectId, widget));
            }

            var pageReviewIds = pageReviewAnnotObjectIds[pageIndex];
            var pageReviewAppearanceIds = pageReviewAppearanceObjectIds[pageIndex];
            for (var i = 0; i < page.ReviewAnnotations.Count; i++)
            {
                var review = page.ReviewAnnotations[i];
                var reviewObjectId = pageReviewIds[i];
                var appearanceObjectId = pageReviewAppearanceIds[i];
                objects.Add(new PdfIndirectObject(reviewObjectId, BuildReviewAnnotationObject(review, pageObjectId, appearanceObjectId)));
                objects.Add(new PdfIndirectObject(appearanceObjectId, BuildReviewAnnotationAppearanceObject(review, acroFormHelvFontObjectId)));
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
                .Concat(pageListBoxAnnotObjectIds[pageIndex])
                .Concat(pageRadioButtonAnnotObjectIds[pageIndex])
                .Concat(pageSignatureFieldAnnotObjectIds[pageIndex])
                .Concat(pagePushButtonAnnotObjectIds[pageIndex])
                .Concat(pageReviewAnnotObjectIds[pageIndex]);
            if (isPdfUa)
            {
                allAnnotIds = allAnnotIds.Select((objectId, originalIndex) => (objectId, originalIndex))
                    .OrderBy(item => accessibleAnnotationByObjectId.TryGetValue(item.objectId, out var reference)
                        ? reference.Annotation.Accessibility?.ReadingOrder ?? int.MaxValue
                        : int.MaxValue)
                    .ThenBy(item => item.originalIndex)
                    .Select(item => item.objectId);
            }
            var allAnnotIdList = allAnnotIds.ToList();
            var annotsSegment = allAnnotIdList.Count > 0
                ? $" /Annots [{string.Join(" ", allAnnotIdList.Select(id => $"{id} 0 R"))}]"
                : string.Empty;

            var rotateSegment = page.RotationDegrees != 0 ? $" /Rotate {page.RotationDegrees}" : string.Empty;
            var cropBoxSegment = BuildPageBoxSegment("CropBox", page.CropBoxLowerLeft, page.CropBoxUpperRight);
            var bleedBoxSegment = BuildPageBoxSegment("BleedBox", page.BleedBoxLowerLeft, page.BleedBoxUpperRight);
            var trimBoxSegment = BuildPageBoxSegment("TrimBox", page.TrimBoxLowerLeft, page.TrimBoxUpperRight);
            var artBoxSegment = BuildPageBoxSegment("ArtBox", page.ArtBoxLowerLeft, page.ArtBoxUpperRight);

            var structureParentsSegment = isPdfUa ? $" /StructParents {pageIndex} /Tabs /S" : string.Empty;
            var pageObject = string.Format(
                CultureInfo.InvariantCulture,
                "<< /Type /Page /Parent {0} 0 R /MediaBox [0 0 {1} {2}] /Resources {3} /Contents {4} 0 R{5}{6}{7}{8}{9}{10}{11} >>\n",
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
                artBoxSegment,
                structureParentsSegment);

            objects.Add(new PdfIndirectObject(pageObjectId, pageObject));
        }

        var kids = string.Join(" ", pageObjects.Select(id => $"{id} 0 R"));

        objects.Add(new PdfIndirectObject(pagesObjectId,
            $"<< /Type /Pages /Kids [{kids}] /Count {pageObjects.Count} >>\n"));

        if (isPdfUa)
        {
            var taggedOperations = document.Pages
                .SelectMany((page, pageIndex) => page.Elements.Select((element, drawIndex) => (element, pageIndex, drawIndex)))
                .Where(item => item.element.Accessibility is not null)
                .ToList();
            var logicalElements = document.Pages
                .SelectMany((page, pageIndex) => page.SemanticElements.Select((semantic, semanticIndex) => (semantic, pageIndex, semanticIndex)))
                .GroupBy(item => item.semantic, (IEqualityComparer<PdfAccessibilityProperties>)ReferenceEqualityComparer.Instance)
                .Select(group => new
                {
                    Semantic = group.Key,
                    First = group.OrderBy(item => item.pageIndex).ThenBy(item => item.semanticIndex).First(),
                    Operations = taggedOperations
                        .Where(operation => ReferenceEquals(operation.element.Accessibility, group.Key))
                        .OrderBy(operation => operation.pageIndex)
                        .ThenBy(operation => operation.drawIndex)
                        .ToList(),
                    Annotations = accessibleAnnotations
                        .Where(reference => ReferenceEquals(reference.Annotation.Accessibility, group.Key))
                        .OrderBy(reference => reference.PageIndex)
                        .ThenBy(reference => reference.StructParentKey)
                        .ToList()
                })
                .OrderBy(item => item.First.pageIndex)
                .ThenBy(item => item.Semantic.ReadingOrder ?? int.MaxValue)
                .ThenBy(item => item.First.semanticIndex)
                .ToList();
            var logicalById = logicalElements
                .Where(item => item.Semantic.ElementId is { Length: > 0 })
                .ToDictionary(item => item.Semantic.ElementId!, StringComparer.Ordinal);

            foreach (var item in logicalElements)
            {
                var semantic = item.Semantic;
                var structureAttributes = new List<string>();
                var tableAttributes = new List<string>();
                if (semantic.Role == PdfStructureRole.TH)
                    tableAttributes.Add($"/Scope /{semantic.TableHeaderScope}");
                if (semantic.TableHeaderIds is { Count: > 0 })
                    tableAttributes.Add($"/Headers [{string.Join(" ", semantic.TableHeaderIds.Select(PdfHexString))}]");
                if (tableAttributes.Count > 0)
                    structureAttributes.Add($"<< /O /Table {string.Join(" ", tableAttributes)} >>");
                if (semantic.Role == PdfStructureRole.Form && item.Annotations.Count > 0)
                    structureAttributes.Add($"<< /O /PrintField /Role /{ResolvePrintFieldRole(item.Annotations.Select(reference => reference.Annotation))} >>");
                var attributes = structureAttributes.Count switch
                {
                    0 => string.Empty,
                    1 => $" /A {structureAttributes[0]}",
                    _ => $" /A [{string.Join(" ", structureAttributes)}]"
                };
                var markedContentReferences = item.Operations.Select(operation =>
                    $"<< /Type /MCR /Pg {pageObjectIds[operation.pageIndex]} 0 R /MCID {pageMarkedContentIds[operation.pageIndex][operation.element]} >>").ToList();
                var childReferences = logicalElements
                    .Where(candidate => string.Equals(candidate.Semantic.ParentElementId, semantic.ElementId, StringComparison.Ordinal))
                    .Select(candidate => $"{structureObjectIds[candidate.Semantic]} 0 R")
                    .ToList();
                var annotationReferences = item.Annotations
                    .Select(reference => $"<< /Type /OBJR /Pg {pageObjectIds[reference.PageIndex]} 0 R /Obj {reference.ObjectId} 0 R >>")
                    .ToList();
                var allKids = markedContentReferences.Concat(childReferences).Concat(annotationReferences).ToList();
                var structureKids = item.Operations.Count == 1 && childReferences.Count == 0 && annotationReferences.Count == 0
                    ? pageMarkedContentIds[item.Operations[0].pageIndex][item.Operations[0].element].ToString(CultureInfo.InvariantCulture)
                    : $"[{string.Join(" ", allKids)}]";
                var parentObjectId = semantic.ParentElementId is { Length: > 0 } parentId
                    ? structureObjectIds[logicalById[parentId].Semantic]
                    : documentStructureObjectId!.Value;
                var structureObject = $"<< /Type /StructElem /S /{semantic.StructureTypeName} /P {parentObjectId} 0 R " +
                    $"/Pg {pageObjectIds[item.First.pageIndex]} 0 R /K {structureKids}" +
                    OptionalHexEntry("ID", semantic.ElementId) + OptionalHexEntry("Lang", semantic.Language) +
                    OptionalHexEntry("Alt", semantic.AlternateText) + OptionalHexEntry("ActualText", semantic.ActualText) +
                    attributes + " >>\n";
                objects.Add(new PdfIndirectObject(structureObjectIds[semantic], structureObject));
            }

            var pageParentTreeNumbers = Enumerable.Range(0, pageCount).Select(pageIndex =>
            {
                var refs = document.Pages[pageIndex].Elements
                    .Where(element => pageMarkedContentIds[pageIndex].ContainsKey(element))
                    .OrderBy(element => pageMarkedContentIds[pageIndex][element])
                    .Select(element => $"{structureObjectIds[element.Accessibility!]} 0 R");
                return $"{pageIndex} [{string.Join(" ", refs)}]";
            });
            var annotationParentTreeNumbers = accessibleAnnotations.Select(reference =>
                $"{reference.StructParentKey} {structureObjectIds[reference.Annotation.Accessibility!]} 0 R");
            var parentTreeNumbers = string.Join(" ", pageParentTreeNumbers.Concat(annotationParentTreeNumbers));
            objects.Add(new PdfIndirectObject(parentTreeObjectId!.Value, $"<< /Nums [{parentTreeNumbers}] >>\n"));
            var rootKids = string.Join(" ", logicalElements
                .Where(item => string.IsNullOrWhiteSpace(item.Semantic.ParentElementId))
                .Select(item => $"{structureObjectIds[item.Semantic]} 0 R"));
            objects.Add(new PdfIndirectObject(documentStructureObjectId!.Value,
                $"<< /Type /StructElem /S /Document /P {structureTreeRootObjectId} 0 R /Lang {PdfHexString(document.Language!)} /K [{rootKids}] >>\n"));
            var roleMappings = logicalElements
                .Where(item => !string.IsNullOrWhiteSpace(item.Semantic.CustomRole))
                .GroupBy(item => item.Semantic.CustomRole!, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"/{group.Key} /{group.First().Semantic.Role}")
                .ToList();
            var roleMapSegment = roleMappings.Count == 0
                ? string.Empty
                : $" /RoleMap << {string.Join(" ", roleMappings)} >>";
            objects.Add(new PdfIndirectObject(structureTreeRootObjectId!.Value,
                $"<< /Type /StructTreeRoot /K [{documentStructureObjectId.Value} 0 R] /ParentTree {parentTreeObjectId.Value} 0 R /ParentTreeNextKey {nextStructureParentKey}{roleMapSegment} >>\n"));
        }

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

        foreach (var (fieldName, parentObjectId) in radioGroupParentObjectIds)
        {
            var widgets = document.Pages
                .SelectMany((page, pageIndex) => page.RadioButtonAnnotations.Select((radio, annotationIndex) => new
                {
                    Radio = radio,
                    ObjectId = pageRadioButtonAnnotObjectIds[pageIndex][annotationIndex]
                }))
                .Where(item => string.Equals(item.Radio.FieldName, fieldName, StringComparison.Ordinal))
                .OrderBy(item => item.Radio.OptionIndex)
                .ToList();
            var selected = widgets.FirstOrDefault(item => item.Radio.IsSelected)?.Radio;
            var value = selected is null ? "/Off" : $"/R{selected.OptionIndex + 1}";
            var radioKids = string.Join(" ", widgets.Select(item => $"{item.ObjectId} 0 R"));
            var radioOptions = string.Join(" ", widgets.Select(item => PdfHexString(item.Radio.ExportValue)));
            var description = OptionalHexEntry("TU", AccessibleDescription(widgets[0].Radio.Accessibility, fieldName));
            objects.Add(new PdfIndirectObject(parentObjectId,
                $"<< /FT /Btn /Ff 32768 /T ({EscapeLiteralString(fieldName)}) /V {value} /Opt [{radioOptions}] " +
                $"/Kids [{radioKids}]{description} >>\n"));
        }

        var viewerPreferences = BuildViewerPreferences(document.ViewerPreferences, isPdfUa);
        var pageMode = BuildPageMode(document.ViewerPreferences.PageMode);
        var pageLayout = BuildPageLayout(document.ViewerPreferences.PageLayout);

        var allAcroFieldIds = allComboFieldObjectIds
            .Concat(allMultilineFieldObjectIds)
            .Concat(allTextFieldObjectIds)
            .Concat(allCheckBoxObjectIds)
            .Concat(allListBoxObjectIds)
            .Concat(radioGroupParentObjectIds.Values)
            .Concat(allSignatureFieldObjectIds)
            .Concat(allPushButtonObjectIds)
            .ToList();
        var acroFormSegment = allAcroFieldIds.Count > 0
            ? $" /AcroForm << /Fields [{string.Join(" ", allAcroFieldIds.Select(id => $"{id} 0 R"))}]" +
              $" /DA (/Helv 0 Tf 0 g)" +
              $" /DR << /Font << /Helv {acroFormHelvFontObjectId!.Value} 0 R >> >> >>"
            : string.Empty;

        if (isPdfA)
        {
            var iccBytes = PdfComplianceResources.CreateSrgbIccProfile();
            objects.Add(new PdfIndirectObject(
                iccProfileObjectId!.Value,
                BuildBinaryStreamObject(iccBytes, $"/N 3 /Alternate /DeviceRGB /Range [0 1 0 1 0 1]")));
            objects.Add(new PdfIndirectObject(
                outputIntentObjectId!.Value,
                $"<< /Type /OutputIntent /S /GTS_PDFA1 /OutputConditionIdentifier (sRGB IEC61966-2.1) " +
                $"/RegistryName (http://www.color.org) /Info (PXA sRGB output intent) " +
                $"/DestOutputProfile {iccProfileObjectId.Value} 0 R >>\n"));
        }

        if (metadataObjectId is { } actualMetadataObjectId)
            objects.Add(new PdfIndirectObject(actualMetadataObjectId,
                BuildBinaryStreamObject(
                    BuildComplianceXmp(document.Info, isPdfA ? pdfAPart : null, isPdfUa, document.AssociatedFiles),
                    "/Type /Metadata /Subtype /XML")));

        foreach (var associated in associatedFileObjects)
        {
            var checksum = Convert.ToHexString(MD5.HashData(associated.File.ContentBytes));
            var mimeType = EscapePdfName(associated.File.MimeType);
            objects.Add(new PdfIndirectObject(
                associated.StreamObjectId,
                BuildBinaryStreamObject(
                    associated.File.ContentBytes,
                    $"/Type /EmbeddedFile /Subtype /{mimeType} /Params << /Size {associated.File.ContentBytes.Length} /CheckSum <{checksum}> >>")));

            var fallbackName = EscapeLiteralString(AsciiFileName(associated.File.FileName));
            var description = OptionalHexEntry("Desc", associated.File.Description);
            objects.Add(new PdfIndirectObject(
                associated.FileSpecificationObjectId,
                $"<< /Type /Filespec /F ({fallbackName}) /UF {PdfHexString(associated.File.FileName)}" +
                $" /AFRelationship /{associated.File.Relationship}{description}" +
                $" /EF << /F {associated.StreamObjectId} 0 R /UF {associated.StreamObjectId} 0 R >> >>\n"));
        }

        var metadataSegment = metadataObjectId is not null ? $" /Metadata {metadataObjectId.Value} 0 R" : string.Empty;
        var outputIntentSegment = isPdfA ? $" /OutputIntents [{outputIntentObjectId!.Value} 0 R]" : string.Empty;
        var associatedFilesSegment = BuildAssociatedFilesCatalogSegment(associatedFileObjects);
        var openAction = BuildOpenAction(document, pageObjectIds);
        var accessibilitySegment = isPdfUa
            ? $" /StructTreeRoot {structureTreeRootObjectId} 0 R /MarkInfo << /Marked true >> /Lang {PdfHexString(document.Language!)}"
            : string.Empty;
        var catalogObject = outlinesRootObjectId is { } outlinesRootId
            ? $"<< /Type /Catalog /Pages {pagesObjectId} 0 R /Outlines {outlinesRootId} 0 R{pageMode}{pageLayout}{viewerPreferences}{openAction}{acroFormSegment}{metadataSegment}{outputIntentSegment}{associatedFilesSegment}{accessibilitySegment} >>\n"
            : $"<< /Type /Catalog /Pages {pagesObjectId} 0 R{pageMode}{pageLayout}{viewerPreferences}{openAction}{acroFormSegment}{metadataSegment}{outputIntentSegment}{associatedFilesSegment}{accessibilitySegment} >>\n";

        objects.Add(new PdfIndirectObject(catalogObjectId, catalogObject));

        if (infoObjectId is { } actualInfoObjectId)
        {
            objects.Add(new PdfIndirectObject(actualInfoObjectId, BuildInfoObject(document.Info)));
        }

        if (options.Encryption is not null && options.CertificateEncryption is not null)
        {
            throw new ArgumentException(
                "Password and certificate encryption cannot be enabled for the same PDF save operation.",
                nameof(options));
        }

        Security.IPdfSecurityHandler? security = null;
        int? encryptObjectId = null;
        if (options.Encryption is { } encryptionOptions)
        {
            var documentId = Security.StandardSecurityHandler.GenerateDocumentId(document);
            security = encryptionOptions.Algorithm switch
            {
                PdfEncryptionAlgorithm.Aes256 => Security.Aes256SecurityHandler.Create(encryptionOptions, documentId),
                PdfEncryptionAlgorithm.Rc4_128 => Security.StandardSecurityHandler.Create(encryptionOptions, documentId),
                PdfEncryptionAlgorithm.Aes128 => Security.Aes128SecurityHandler.Create(encryptionOptions, documentId),
                _ => throw new NotSupportedException($"Unsupported PDF encryption algorithm '{encryptionOptions.Algorithm}'.")
            };
            encryptObjectId = objects.Max(static o => o.Id) + 1;
            objects.Add(new PdfIndirectObject(encryptObjectId.Value, security.BuildEncryptDictionary()));
        }
        else if (options.CertificateEncryption is { } certificateOptions)
        {
            var documentId = Security.StandardSecurityHandler.GenerateDocumentId(document);
            security = Security.PublicKeySecurityHandler.Create(certificateOptions, documentId);
            encryptObjectId = objects.Max(static o => o.Id) + 1;
            objects.Add(new PdfIndirectObject(encryptObjectId.Value, security.BuildEncryptDictionary()));
        }

        byte[] bytes;
        try
        {
            bytes = Serialize(objects, catalogObjectId, infoObjectId, security, encryptObjectId, isPdfA || isPdfUa ? "1.7" : null);
        }
        finally
        {
            security?.Dispose();
        }

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

    private static string BuildReviewAnnotationObject(PdfReviewAnnotation annotation, int pageObjectId, int appearanceObjectId)
    {
        var rect = FormatRect(annotation.X, annotation.Y, annotation.Width, annotation.Height);
        var color = FormatColorArray(annotation.Color);
        var opacitySegment = annotation.Opacity < 1 ? $" /CA {FormatNumber(annotation.Opacity)}" : string.Empty;
        var appearanceSegment = $" /AP << /N {appearanceObjectId} 0 R >>";
        var contents = EscapeLiteralString(annotation.Contents ?? string.Empty);

        return annotation.Type switch
        {
            PdfReviewAnnotationType.StickyNote =>
                $"<< /Type /Annot /Subtype /Text /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /Name /Comment{opacitySegment}{appearanceSegment} >>\n",
            PdfReviewAnnotationType.FreeText =>
                $"<< /Type /Annot /Subtype /FreeText /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /DA (/Helv 10 Tf 0 g) /Border [0 0 1]{opacitySegment}{appearanceSegment} >>\n",
            PdfReviewAnnotationType.Highlight =>
                BuildMarkupAnnotationObject("Highlight", annotation, pageObjectId, rect, color, opacitySegment, appearanceSegment, contents),
            PdfReviewAnnotationType.Underline =>
                BuildMarkupAnnotationObject("Underline", annotation, pageObjectId, rect, color, opacitySegment, appearanceSegment, contents),
            PdfReviewAnnotationType.StrikeOut =>
                BuildMarkupAnnotationObject("StrikeOut", annotation, pageObjectId, rect, color, opacitySegment, appearanceSegment, contents),
            PdfReviewAnnotationType.Square =>
                $"<< /Type /Annot /Subtype /Square /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /BS << /W 2 /S /S >>{opacitySegment}{appearanceSegment} >>\n",
            PdfReviewAnnotationType.Circle =>
                $"<< /Type /Annot /Subtype /Circle /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /BS << /W 2 /S /S >>{opacitySegment}{appearanceSegment} >>\n",
            PdfReviewAnnotationType.Redaction =>
                $"<< /Type /Annot /Subtype /Redact /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /IC {color}{opacitySegment}{appearanceSegment} >>\n",
            _ => throw new ArgumentOutOfRangeException(nameof(annotation), annotation.Type, "Unsupported review annotation type.")
        };
    }

    private static byte[] BuildReviewAnnotationAppearanceObject(PdfReviewAnnotation annotation, int? helvFontObjectId)
    {
        var width = Math.Max(1, annotation.Width);
        var height = Math.Max(1, annotation.Height);
        var resourceParts = new List<string>();
        if (annotation.Type == PdfReviewAnnotationType.FreeText && helvFontObjectId is { } fontId)
        {
            resourceParts.Add($"/Font << /Helv {fontId} 0 R >>");
        }

        if (annotation.Opacity < 1)
        {
            var opacity = FormatNumber(annotation.Opacity);
            resourceParts.Add($"/ExtGState << /GS1 << /ca {opacity} /CA {opacity} >> >>");
        }

        var resources = resourceParts.Count > 0
            ? $" /Resources << {string.Join(" ", resourceParts)} >>"
            : string.Empty;
        var stream = BuildReviewAppearanceStream(annotation, width, height);
        var bytes = Encoding.ASCII.GetBytes(stream);
        var header = $"<< /Type /XObject /Subtype /Form /BBox [0 0 {FormatNumber(width)} {FormatNumber(height)}]{resources} /Length {bytes.Length} >>\nstream\n";

        using var memory = new MemoryStream();
        memory.Write(Encoding.ASCII.GetBytes(header));
        memory.Write(bytes);
        memory.Write(Encoding.ASCII.GetBytes("\nendstream\n"));
        return memory.ToArray();
    }

    private static string BuildReviewAppearanceStream(PdfReviewAnnotation annotation, double width, double height)
    {
        var color = FormatColorOperands(annotation.Color);
        var opacity = Math.Clamp(annotation.Opacity, 0.1, 1);
        return annotation.Type switch
        {
            PdfReviewAnnotationType.StickyNote => BuildStickyNoteAppearance(color, width, height),
            PdfReviewAnnotationType.FreeText => BuildFreeTextAppearance(annotation, color, width, height),
            PdfReviewAnnotationType.Highlight => BuildFilledRectAppearance(color, width, height, opacity),
            PdfReviewAnnotationType.Underline => BuildLineAppearance(color, 0, Math.Max(1, height * 0.15), width, Math.Max(1, height * 0.15)),
            PdfReviewAnnotationType.StrikeOut => BuildLineAppearance(color, 0, height / 2, width, height / 2),
            PdfReviewAnnotationType.Square => BuildRectangleOutlineAppearance(color, width, height),
            PdfReviewAnnotationType.Circle => BuildCircleOutlineAppearance(color, width, height),
            PdfReviewAnnotationType.Redaction => BuildFilledRectAppearance(color, width, height, 1),
            _ => string.Empty
        };
    }

    private static string BuildStickyNoteAppearance(string color, double width, double height)
    {
        var fold = Math.Min(width, height) * 0.28;
        return string.Format(
            CultureInfo.InvariantCulture,
            "q\n{0} rg\n0 0 {1} {2} re f\n1 1 1 rg\n{3} {4} m {1} {2} l {1} {4} l h f\n0.18 0.18 0.18 RG\n1 w\n0 0 {1} {2} re S\nQ",
            color,
            FormatNumber(width),
            FormatNumber(height),
            FormatNumber(width - fold),
            FormatNumber(height - fold));
    }

    private static string BuildFreeTextAppearance(PdfReviewAnnotation annotation, string color, double width, double height)
    {
        var text = EscapeLiteralString(annotation.Contents ?? string.Empty);
        var baseline = Math.Max(3, height - 14);
        return string.Format(
            CultureInfo.InvariantCulture,
            "q\n{0} RG\n1 w\n0.5 0.5 {1} {2} re S\nBT\n/Helv 10 Tf\n0 0 0 rg\n4 {3} Td\n({4}) Tj\nET\nQ",
            color,
            FormatNumber(Math.Max(0, width - 1)),
            FormatNumber(Math.Max(0, height - 1)),
            FormatNumber(baseline),
            text);
    }

    private static string BuildFilledRectAppearance(string color, double width, double height, double opacity)
    {
        var opacityPrefix = opacity < 1 ? $"/GS1 gs\n" : string.Empty;
        return string.Format(
            CultureInfo.InvariantCulture,
            "q\n{0}{1} rg\n0 0 {2} {3} re f\nQ",
            opacityPrefix,
            color,
            FormatNumber(width),
            FormatNumber(height));
    }

    private static string BuildLineAppearance(string color, double x1, double y1, double x2, double y2)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "q\n{0} RG\n2 w\n{1} {2} m {3} {4} l S\nQ",
            color,
            FormatNumber(x1),
            FormatNumber(y1),
            FormatNumber(x2),
            FormatNumber(y2));
    }

    private static string BuildRectangleOutlineAppearance(string color, double width, double height)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "q\n{0} RG\n2 w\n1 1 {1} {2} re S\nQ",
            color,
            FormatNumber(Math.Max(0, width - 2)),
            FormatNumber(Math.Max(0, height - 2)));
    }

    private static string BuildCircleOutlineAppearance(string color, double width, double height)
    {
        var cx = width / 2;
        var cy = height / 2;
        var rx = Math.Max(0.5, width / 2 - 1);
        var ry = Math.Max(0.5, height / 2 - 1);
        const double k = 0.5522847498;
        return string.Format(
            CultureInfo.InvariantCulture,
            "q\n{0} RG\n2 w\n{1} {2} m\n{3} {2} {4} {5} {4} {6} c\n{4} {7} {3} {8} {1} {8} c\n{9} {8} {10} {7} {10} {6} c\n{10} {5} {9} {2} {1} {2} c\nS\nQ",
            color,
            FormatNumber(cx),
            FormatNumber(cy + ry),
            FormatNumber(cx + rx * k),
            FormatNumber(cx + rx),
            FormatNumber(cy + ry * k),
            FormatNumber(cy),
            FormatNumber(cy - ry * k),
            FormatNumber(cy - ry),
            FormatNumber(cx - rx * k),
            FormatNumber(cx - rx));
    }

    private static string BuildMarkupAnnotationObject(
        string subtype,
        PdfReviewAnnotation annotation,
        int pageObjectId,
        string rect,
        string color,
        string opacitySegment,
        string appearanceSegment,
        string contents)
    {
        var quadPoints = annotation.QuadPoints.Count > 0
            ? FormatQuadPoints(annotation.QuadPoints)
            : FormatQuadPoints(annotation.X, annotation.Y, annotation.Width, annotation.Height);
        return $"<< /Type /Annot /Subtype /{subtype} /Rect {rect} /P {pageObjectId} 0 R /Contents ({contents}) /C {color} /QuadPoints {quadPoints}{opacitySegment}{appearanceSegment} >>\n";
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

    private static string FormatQuadPoints(IReadOnlyList<PdfMarkupQuadPoint> quadPoints)
    {
        var values = new List<string>(quadPoints.Count * 8);
        foreach (var point in quadPoints)
        {
            values.Add(FormatNumber(point.X1));
            values.Add(FormatNumber(point.Y1));
            values.Add(FormatNumber(point.X2));
            values.Add(FormatNumber(point.Y2));
            values.Add(FormatNumber(point.X3));
            values.Add(FormatNumber(point.Y3));
            values.Add(FormatNumber(point.X4));
            values.Add(FormatNumber(point.Y4));
        }

        return $"[{string.Join(" ", values)}]";
    }

    private static string FormatColorArray(PdfColor color)
    {
        return $"[{FormatNumber(color.Red)} {FormatNumber(color.Green)} {FormatNumber(color.Blue)}]";
    }

    private static string FormatColorOperands(PdfColor color)
    {
        return $"{FormatNumber(color.Red)} {FormatNumber(color.Green)} {FormatNumber(color.Blue)}";
    }

    private static byte[] Serialize(
        List<PdfIndirectObject> objects,
        int rootObjectId,
        int? infoObjectId,
        Security.IPdfSecurityHandler? security = null,
        int? encryptObjectId = null,
        string? pdfVersion = null)
    {
        objects.Sort(static (x, y) => x.Id.CompareTo(y.Id));

        using var stream = new MemoryStream();

        static void WriteAscii(MemoryStream destination, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            destination.Write(bytes, 0, bytes.Length);
        }

        WriteAscii(stream, $"%PDF-{pdfVersion ?? security?.PdfVersion ?? "1.4"}\n");
        stream.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);

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
            var idHex = Convert.ToHexString(CreateDocumentId(objects, rootObjectId, infoObjectId));
            WriteAscii(stream,
                $"<< /Size {maxObjectId + 1} /Root {rootObjectId} 0 R{infoSegment} /ID [<{idHex}> <{idHex}>] >>\n");
        }

        WriteAscii(stream, "startxref\n");
        WriteAscii(stream, $"{xrefOffset}\n");
        WriteAscii(stream, "%%EOF\n");

        return stream.ToArray();
    }

    private static byte[] CreateDocumentId(
        IReadOnlyList<PdfIndirectObject> objects,
        int rootObjectId,
        int? infoObjectId)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(BitConverter.GetBytes(rootObjectId));
        hash.AppendData(BitConverter.GetBytes(infoObjectId ?? 0));

        foreach (var obj in objects)
        {
            hash.AppendData(BitConverter.GetBytes(obj.Id));
            hash.AppendData(obj.ContentBytes);
        }

        return hash.GetHashAndReset()[..16];
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

        foreach (var customProperty in info.CustomProperties.OrderBy(static item => item.Key, StringComparer.Ordinal))
        {
            var key = SanitizeInfoKey(customProperty.Key);
            if (!string.IsNullOrWhiteSpace(key))
            {
                AppendIfValue(entries, key, customProperty.Value);
            }
        }

        return $"<< {string.Join(" ", entries)} >>\n";
    }

    private static byte[] BuildBinaryStreamObject(byte[] bytes, string dictionaryEntries)
    {
        using var memory = new MemoryStream();
        var header = $"<< {dictionaryEntries} /Length {bytes.Length} >>\nstream\n";
        memory.Write(Encoding.ASCII.GetBytes(header));
        memory.Write(bytes);
        memory.Write(Encoding.ASCII.GetBytes("\nendstream\n"));
        return memory.ToArray();
    }

    private static byte[] BuildComplianceXmp(
        PdfDocumentInfo info,
        int? pdfAPart,
        bool isPdfUa,
        IReadOnlyList<PdfAssociatedFile> associatedFiles)
    {
        static string Xml(string? value) => SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        static string OptionalElement(string name, string? value) => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"<{name}>{Xml(value)}</{name}>";
        static string OptionalDate(string name, DateTimeOffset? value) => value is null
            ? string.Empty
            : $"<{name}>{value.Value.ToUniversalTime():yyyy-MM-dd'T'HH:mm:ss'Z'}</{name}>";

        var title = string.IsNullOrWhiteSpace(info.Title)
            ? string.Empty
            : $"<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{Xml(info.Title)}</rdf:li></rdf:Alt></dc:title>";
        var author = string.IsNullOrWhiteSpace(info.Author)
            ? string.Empty
            : $"<dc:creator><rdf:Seq><rdf:li>{Xml(info.Author)}</rdf:li></rdf:Seq></dc:creator>";
        var subject = string.IsNullOrWhiteSpace(info.Subject)
            ? string.Empty
            : $"<dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">{Xml(info.Subject)}</rdf:li></rdf:Alt></dc:description>";

        var pdfAIdentification = pdfAPart is null ? string.Empty : $"""
                <rdf:Description rdf:about="" xmlns:pdfaid="http://www.aiim.org/pdfa/ns/id/">
                  <pdfaid:part>{pdfAPart}</pdfaid:part>
                  <pdfaid:conformance>B</pdfaid:conformance>
                </rdf:Description>
            """;
        var pdfUaIdentification = isPdfUa ? """
                <rdf:Description rdf:about="" xmlns:pdfuaid="http://www.aiim.org/pdfua/ns/id/">
                  <pdfuaid:part>1</pdfuaid:part>
                </rdf:Description>
            """ : string.Empty;
        var associatedFileMetadata = BuildAssociatedFileXmp(associatedFiles, Xml);

        var xmp = $"""
            <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                {pdfAIdentification}{pdfUaIdentification}{associatedFileMetadata}
                <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/">
                  {title}{author}{subject}
                </rdf:Description>
                <rdf:Description rdf:about="" xmlns:pdf="http://ns.adobe.com/pdf/1.3/">
                  {OptionalElement("pdf:Keywords", info.Keywords)}{OptionalElement("pdf:Producer", info.Producer)}
                </rdf:Description>
                <rdf:Description rdf:about="" xmlns:xmp="http://ns.adobe.com/xap/1.0/">
                  {OptionalElement("xmp:CreatorTool", info.Creator)}{OptionalDate("xmp:CreateDate", info.CreationDate)}{OptionalDate("xmp:ModifyDate", info.ModificationDate)}
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            <?xpacket end="w"?>
            """;

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xmp);
    }

    private static string BuildAssociatedFileXmp(
        IReadOnlyList<PdfAssociatedFile> associatedFiles,
        Func<string?, string> xml)
    {
        var metadata = associatedFiles
            .Select(static file => file.XmpMetadata)
            .SingleOrDefault(static value => value is not null);
        if (metadata is null)
            return string.Empty;

        var propertyDefinitions = string.Join(
            "\n",
            metadata.Properties.Select(property => $"""
                      <rdf:li rdf:parseType="Resource">
                        <pdfaProperty:name>{xml(property.Name)}</pdfaProperty:name>
                        <pdfaProperty:valueType>{xml(property.ValueType)}</pdfaProperty:valueType>
                        <pdfaProperty:category>{xml(property.Category)}</pdfaProperty:category>
                        <pdfaProperty:description>{xml(property.Description)}</pdfaProperty:description>
                      </rdf:li>
                """));
        var propertyValues = string.Join(
            "\n",
            metadata.Properties.Select(property =>
                $"<{metadata.Prefix}:{property.Name}>{xml(property.Value)}</{metadata.Prefix}:{property.Name}>"));

        return $"""
                <rdf:Description rdf:about=""
                    xmlns:pdfaExtension="http://www.aiim.org/pdfa/ns/extension/"
                    xmlns:pdfaSchema="http://www.aiim.org/pdfa/ns/schema#"
                    xmlns:pdfaProperty="http://www.aiim.org/pdfa/ns/property#">
                  <pdfaExtension:schemas>
                    <rdf:Bag>
                      <rdf:li rdf:parseType="Resource">
                        <pdfaSchema:schema>{xml(metadata.SchemaName)}</pdfaSchema:schema>
                        <pdfaSchema:namespaceURI>{xml(metadata.NamespaceUri)}</pdfaSchema:namespaceURI>
                        <pdfaSchema:prefix>{xml(metadata.Prefix)}</pdfaSchema:prefix>
                        <pdfaSchema:property>
                          <rdf:Seq>
                {propertyDefinitions}
                          </rdf:Seq>
                        </pdfaSchema:property>
                      </rdf:li>
                    </rdf:Bag>
                  </pdfaExtension:schemas>
                </rdf:Description>
                <rdf:Description rdf:about="" xmlns:{metadata.Prefix}="{xml(metadata.NamespaceUri)}">
                  {propertyValues}
                </rdf:Description>
            """;
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

    private static string OptionalHexEntry(string key, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $" /{key} {PdfHexString(value)}";

    private static string BuildAnnotationRectangle(double x, double y, double width, double height) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "[{0} {1} {2} {3}]",
            FormatNumber(x),
            FormatNumber(y),
            FormatNumber(x + width),
            FormatNumber(y + height));

    private static string BuildAnnotationStructureSegment(
        IPdfAccessibleAnnotation annotation,
        IReadOnlyDictionary<IPdfAccessibleAnnotation, AccessibleAnnotationReference> references) =>
        references.TryGetValue(annotation, out var reference)
            ? $" /StructParent {reference.StructParentKey}"
            : string.Empty;

    private static string BuildAnnotationDescriptionSegment(PdfAccessibilityProperties? accessibility) =>
        OptionalHexEntry("Contents", AccessibleDescription(accessibility, null));

    private static string ResolvePrintFieldRole(IEnumerable<IPdfAccessibleAnnotation> annotations)
    {
        var types = annotations.Select(static annotation => annotation.GetType()).Distinct().ToList();
        if (types.Count == 1 && types[0] == typeof(PdfRadioButtonAnnotation))
            return "rb";
        if (types.Count == 1 && types[0] == typeof(PdfCheckBoxAnnotation))
            return "cb";
        if (types.Count == 1 && types[0] == typeof(PdfPushButtonAnnotation))
            return "pb";

        return "tv";
    }

    private static string? AccessibleDescription(PdfAccessibilityProperties? accessibility, string? fallback) =>
        !string.IsNullOrWhiteSpace(accessibility?.AlternateText)
            ? accessibility.AlternateText
            : !string.IsNullOrWhiteSpace(accessibility?.ActualText)
                ? accessibility.ActualText
                : fallback;

    private static string PdfHexString(string value)
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes(value);
        return $"<FEFF{Convert.ToHexString(bytes)}>";
    }

    private static string BuildAssociatedFilesCatalogSegment(
        IReadOnlyList<AssociatedFileObjectIds> associatedFiles)
    {
        if (associatedFiles.Count == 0)
            return string.Empty;

        var names = string.Join(" ", associatedFiles
            .OrderBy(static file => file.File.FileName, StringComparer.Ordinal)
            .Select(file =>
            $"{PdfHexString(file.File.FileName)} {file.FileSpecificationObjectId} 0 R"));
        var references = string.Join(" ", associatedFiles.Select(file =>
            $"{file.FileSpecificationObjectId} 0 R"));
        return $" /Names << /EmbeddedFiles << /Names [{names}] >> >> /AF [{references}]";
    }

    private static string EscapePdfName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var current in Encoding.ASCII.GetBytes(value))
        {
            if (current is >= 33 and <= 126 && current is not (byte)'#' and not (byte)'/' &&
                current is not (byte)'(' and not (byte)')' and not (byte)'<' and not (byte)'>' &&
                current is not (byte)'[' and not (byte)']' and not (byte)'{' and not (byte)'}' and
                not (byte)'%')
            {
                builder.Append((char)current);
            }
            else
            {
                builder.Append('#').Append(current.ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return builder.ToString();
    }

    private static string AsciiFileName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var current in value)
            builder.Append(current is >= ' ' and <= '~' ? current : '_');
        return builder.ToString();
    }

    private static string SanitizeInfoKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string BuildViewerPreferences(PdfViewerPreferencesOptions options, bool requireDocumentTitle = false)
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

        if (options.DisplayDocTitle || requireDocumentTitle)
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

    private sealed record AssociatedFileObjectIds(
        PdfAssociatedFile File,
        int StreamObjectId,
        int FileSpecificationObjectId);

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

    private static string BuildToUnicodeCMap(PdfEmbeddedFont font, IReadOnlyList<int> codePoints)
    {
        var mappings = codePoints
            .Select(codePoint => (CodePoint: codePoint, GlyphId: font.GetGlyphId(codePoint)))
            .Where(static mapping => mapping.GlyphId != 0)
            .GroupBy(static mapping => mapping.GlyphId)
            .Select(static group => group.First())
            .OrderBy(static mapping => mapping.GlyphId)
            .ToList();
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
        for (var i = 0; i < mappings.Count; i += chunkSize)
        {
            var chunk = mappings.Skip(i).Take(chunkSize).ToList();
            sb.AppendLine($"{chunk.Count} beginbfchar");
            foreach (var mapping in chunk)
            {
                var cidHex = mapping.GlyphId.ToString("X4", CultureInfo.InvariantCulture);
                var unicodeHex = mapping.CodePoint <= 0xFFFF
                    ? mapping.CodePoint.ToString("X4", CultureInfo.InvariantCulture)
                    : string.Concat(char.ConvertFromUtf32(mapping.CodePoint)
                        .Select(character => ((int)character).ToString("X4", CultureInfo.InvariantCulture)));
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
        foreach (var glyphId in codePoints.Select(font.GetGlyphId).Where(static glyphId => glyphId != 0).Distinct().Order())
        {
            var advance = font.GetGlyphAdvance(glyphId);
            var width = font.UnitsPerEm == 0 ? 500 : (int)Math.Round(advance / font.UnitsPerEm * 1000);
            sb.Append(CultureInfo.InvariantCulture, $"{glyphId} [{width}] ");
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

    private sealed record AccessibleAnnotationReference(
        int PageIndex,
        IPdfAccessibleAnnotation Annotation,
        int ObjectId,
        int StructParentKey);
}
