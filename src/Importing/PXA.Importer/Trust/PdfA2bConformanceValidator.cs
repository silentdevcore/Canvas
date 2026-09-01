using System.Text;
using PXA.Importer.Content;
using PXA.Importer.Objects;
using PXA.Importer.Parsing;
using PXA.Importer.Streams;

namespace PXA.Importer.Trust;

internal static class PdfA2bConformanceValidator
{
    public const string Name = "PXA PDF/A-2b/3b structural validator 1.3";

    private static readonly HashSet<string> AllowedAfRelationships = new(StringComparer.Ordinal)
    {
        "Source", "Data", "Alternative", "Supplement", "Unspecified"
    };

    private static readonly HashSet<string> ForbiddenActionTypes = new(StringComparer.Ordinal)
    {
        "JavaScript", "Launch", "Sound", "Movie", "ResetForm", "ImportData", "Hide",
        "Rendition", "Trans", "SetOCGState", "GoTo3DView", "SetState", "NoOp"
    };

    private static readonly HashSet<string> ForbiddenAnnotationSubtypes = new(StringComparer.Ordinal)
    {
        "RichMedia", "Movie", "Sound", "Screen", "3D"
    };

    private static readonly HashSet<string> AllowedAnnotationSubtypes = new(StringComparer.Ordinal)
    {
        "Text", "Link", "FreeText", "Line", "Square", "Circle", "Polygon", "PolyLine",
        "Highlight", "Underline", "Squiggly", "StrikeOut", "Stamp", "Caret", "Ink", "Popup",
        "FileAttachment", "Widget", "PrinterMark", "TrapNet", "Watermark", "Redact"
    };

    private static readonly HashSet<string> AllowedBlendModes = new(StringComparer.Ordinal)
    {
        "Normal", "Compatible", "Multiply", "Screen", "Overlay", "Darken", "Lighten",
        "ColorDodge", "ColorBurn", "HardLight", "SoftLight", "Difference", "Exclusion",
        "Hue", "Saturation", "Color", "Luminosity"
    };

    private static readonly HashSet<string> AllowedRenderingIntents = new(StringComparer.Ordinal)
    {
        "RelativeColorimetric", "AbsoluteColorimetric", "Perceptual", "Saturation"
    };

    private static readonly HashSet<string> ForbiddenMultimediaKeys = new(StringComparer.Ordinal)
    {
        "RichMediaContent", "RichMediaSettings", "3DD", "3DV", "Movie", "Sound"
    };

    public static bool CanValidate(PdfObjectGraph graph, PdfArchiveProfile profile)
    {
        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        return catalog is not null && HasPdfAMetadata(graph, catalog, profile);
    }

    public static bool Validate(
        PdfObjectGraph graph,
        bool isEncrypted,
        ICollection<PdfTrustDiagnostic> diagnostics,
        PdfArchiveProfile profile = PdfArchiveProfile.PdfA2b)
    {
        var valid = true;
        if (isEncrypted)
        {
            Error("PXAPDFTRUST310", $"{PdfA2bConverter.ProfileName(profile)} does not permit encryption.");
        }

        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        if (catalog is null)
        {
            Error("PXAPDFTRUST311", "The PDF catalog could not be resolved for PDF/A validation.");
            return false;
        }

        if (!HasPdfAMetadata(graph, catalog, profile))
        {
            Error("PXAPDFTRUST312", $"The catalog does not reference XMP metadata identifying {PdfA2bConverter.ProfileName(profile)}.");
        }

        if (!HasRgbOutputIntent(graph, catalog))
        {
            Error("PXAPDFTRUST313", "A valid RGB PDF/A output intent with an ICC profile was not found.");
        }

        ValidateOptionalContent(catalog);
        ValidateInteractiveForms(catalog);

        foreach (var indirectObject in graph.Objects.Values)
        {
            var dictionary = indirectObject.Value switch
            {
                PdfDictionary value => value,
                PdfStreamObject stream => stream.Dictionary,
                _ => null
            };
            if (dictionary is not null)
            {
                ValidateDictionary(dictionary, indirectObject.Id, indirectObject.Value is PdfStreamObject);
                if (string.Equals(ReadName(graph, dictionary["Type"]), "Font", StringComparison.Ordinal) &&
                    !HasEmbeddedFontProgram(graph, dictionary))
                {
                    Error(
                        "PXAPDFTRUST314",
                        "A font dictionary does not reference an embedded font program.",
                        indirectObject.Id);
                }
            }
        }


        ValidateContentRenderingIntents();

        ValidateEmbeddedFiles(catalog);

        if (valid)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST300",
                PdfTrustDiagnosticSeverity.Info,
                $"The document passed the PXA structural {PdfA2bConverter.ProfileName(profile)} validator. Independent validation is still recommended for release evidence."));
        }

        return valid;

        void ValidateDictionary(
            PdfDictionary dictionary,
            PdfObjectId objectId,
            bool isStreamDictionary = false,
            int depth = 0)
        {
            if (depth > 64)
            {
                Error("PXAPDFTRUST330", "A direct PDF object structure exceeds the supported PDF/A validation depth.", objectId);
                return;
            }

            if (dictionary["JS"] is not null || dictionary["JavaScript"] is not null)
            {
                Error("PXAPDFTRUST315", "PDF/A-2 and PDF/A-3 do not permit JavaScript actions or name trees.", objectId);
            }

            if (dictionary["AA"] is not null)
            {
                Error("PXAPDFTRUST324", "Additional-action dictionaries are not accepted by the PXA PDF/A-2b/3b profile policy.", objectId);
            }

            if (ReadName(graph, dictionary["S"]) is { } actionType &&
                ForbiddenActionTypes.Contains(actionType))
            {
                Error("PXAPDFTRUST325", $"Action type '/{actionType}' is forbidden by PDF/A-2 and PDF/A-3.", objectId);
            }


            ValidateRenderingIntent(dictionary["RI"], objectId);
            ValidateBlendModes(dictionary["BM"], objectId);

            foreach (var key in dictionary.Values.Keys.Where(ForbiddenMultimediaKeys.Contains))
            {
                Error("PXAPDFTRUST326", $"Multimedia key '/{key}' is not permitted by the supported PDF/A profiles.", objectId);
            }

            var annotationSubtype = ReadName(graph, dictionary["Subtype"]);
            var isAnnotation = string.Equals(ReadName(graph, dictionary["Type"]), "Annot", StringComparison.Ordinal) ||
                               annotationSubtype is "Widget" or "Text" or "Link" or "FreeText" or "Line" or "Square" or
                                   "Circle" or "Polygon" or "PolyLine" or "Highlight" or "Underline" or "Squiggly" or
                                   "StrikeOut" or "Stamp" or "Caret" or "Ink" or "Popup" or "FileAttachment" or
                                   "Sound" or "Movie" or "Screen" or "3D" or "RichMedia";
            if (isAnnotation && annotationSubtype is null)
            {
                Error("PXAPDFTRUST316", "An annotation dictionary has no recognized /Subtype.", objectId);
            }
            if (isAnnotation && annotationSubtype is { } subtype)
            {
                if (subtype == "Widget" && (dictionary["A"] is not null || dictionary["AA"] is not null))
                {
                    Error("PXAPDFTRUST335", "A Widget annotation must not contain /A or /AA actions.", objectId);
                }

                if (ForbiddenAnnotationSubtypes.Contains(subtype) || !AllowedAnnotationSubtypes.Contains(subtype) ||
                    profile == PdfArchiveProfile.PdfA2b && subtype == "FileAttachment")
                {
                    Error("PXAPDFTRUST316", $"Annotation subtype '/{subtype}' is not accepted for {PdfA2bConverter.ProfileName(profile)}.", objectId);
                }
                else if (subtype == "FileAttachment" &&
                         (Resolve(graph, dictionary["FS"]) is not PdfDictionary specification ||
                          Resolve(graph, specification["EF"]) is not PdfDictionary))
                {
                    Error("PXAPDFTRUST331", "A PDF/A-3 file-attachment annotation must reference an embedded file specification through /FS.", objectId);
                }

                var flags = ReadInteger(graph, dictionary["F"]);
                if (subtype != "Popup" && flags is null ||
                    flags is { } annotationFlags &&
                    ((annotationFlags & 1) != 0 || (annotationFlags & 2) != 0 ||
                     (annotationFlags & 32) != 0 || (annotationFlags & 256) != 0 ||
                     (annotationFlags & 4) == 0))
                {
                    var code = subtype == "Widget" && ResolveFieldType(graph, dictionary) == "Sig"
                        ? "PXAPDFTRUST332"
                        : "PXAPDFTRUST328";
                    Error(code, "PDF/A annotations must be printable and must not be Invisible, Hidden, or NoView.", objectId);
                }

                ValidateAnnotationAppearance(dictionary, subtype, objectId);
            }

            if (dictionary["FT"] is not null && (dictionary["A"] is not null || dictionary["AA"] is not null))
            {
                Error("PXAPDFTRUST335", "A form field dictionary must not contain /A or /AA actions.", objectId);
            }

            if ((isStreamDictionary && dictionary["F"] is not null) ||
                dictionary["OPI"] is not null ||
                string.Equals(ReadName(graph, dictionary["Type"]), "XObject", StringComparison.Ordinal) && dictionary["Ref"] is not null)
            {
                Error("PXAPDFTRUST327", "The document depends on external stream or reference content that is not self-contained.", objectId);
            }

            if (dictionary["TR"] is { } transfer && ReadName(graph, transfer) is not ("Identity" or "Default") ||
                dictionary["TR2"] is { } transfer2 && ReadName(graph, transfer2) is not ("Identity" or "Default"))
            {
                Error("PXAPDFTRUST329", "A non-default transfer function is not permitted by the supported PDF/A profiles.", objectId);
            }

            foreach (var nested in dictionary.Values.Values)
            {
                ValidateNested(nested, objectId, depth + 1);
            }
        }


        void ValidateAnnotationAppearance(PdfDictionary annotation, string subtype, PdfObjectId objectId)
        {
            var fieldType = subtype == "Widget" ? ResolveFieldType(graph, annotation) : null;
            var appearanceRequired = subtype is not ("Popup" or "Link") && !HasZeroAreaRectangle(annotation["Rect"]);
            var appearance = Resolve(graph, annotation["AP"]) as PdfDictionary;
            if (appearanceRequired && appearance is null)
            {
                var code = fieldType == "Sig" ? "PXAPDFTRUST332" : "PXAPDFTRUST333";
                Error(code, "The annotation requires a normal appearance dictionary.", objectId);
                return;
            }

            if (annotation["AP"] is not null && appearance is null)
            {
                Error("PXAPDFTRUST333", "The annotation /AP entry is not an appearance dictionary.", objectId);
                return;
            }

            if (appearance is null)
            {
                return;
            }

            var unsupportedKeys = appearance.Values.Keys.Where(static key => key != "N").ToArray();
            if (unsupportedKeys.Length > 0)
            {
                Error("PXAPDFTRUST334", $"The annotation appearance dictionary contains entries other than /N: {string.Join(", ", unsupportedKeys.Select(static key => $"/{key}"))}.", objectId);
            }

            var normal = Resolve(graph, appearance["N"]);
            if (subtype == "Widget" && fieldType == "Btn")
            {
                if (normal is not PdfDictionary states || states.Values.Count == 0 ||
                    states.Values.Values.Any(value => Resolve(graph, value) is not PdfStreamObject))
                {
                    Error("PXAPDFTRUST334", "A button Widget /N appearance must be a non-empty state dictionary containing only appearance streams.", objectId);
                }
            }
            else if (normal is not PdfStreamObject)
            {
                Error("PXAPDFTRUST334", "The annotation /N entry must resolve to an appearance stream.", objectId);
            }
        }

        void ValidateInteractiveForms(PdfDictionary rootCatalog)
        {
            if (ReadBoolean(graph, rootCatalog["NeedsRendering"]) == true)
            {
                Error("PXAPDFTRUST335", "The document catalog must not require dynamic XFA rendering.", ObjectId(rootCatalog));
            }

            if (Resolve(graph, rootCatalog["AcroForm"]) is not PdfDictionary acroForm)
            {
                return;
            }

            var acroFormId = ObjectId(acroForm) ?? ObjectId(rootCatalog);
            if (ReadBoolean(graph, acroForm["NeedAppearances"]) == true)
            {
                Error("PXAPDFTRUST335", "The AcroForm /NeedAppearances flag must be absent or false.", acroFormId);
            }

            if (acroForm["XFA"] is not null)
            {
                Error("PXAPDFTRUST335", "PDF/A-2 and PDF/A-3 do not permit XFA form content.", acroFormId);
            }
        }

        void ValidateOptionalContent(PdfDictionary rootCatalog)
        {
            if (Resolve(graph, rootCatalog["OCProperties"]) is not PdfDictionary properties)
            {
                return;
            }

            var ownerId = ObjectId(properties) ?? ObjectId(rootCatalog);
            var groups = Resolve(graph, properties["OCGs"]) as PdfArray;
            var ocgIds = groups is not null
                ? groups.Items.OfType<PdfReference>().Select(static item => item.Id).ToHashSet()
                : [];
            if (groups is null || groups.Items.Any(static item => item is not PdfReference))
            {
                Error("PXAPDFTRUST338", "The optional-content /OCGs entry must be an array of indirect OCG references.", ownerId);
            }

            var configurations = new List<PdfDictionary>();
            if (Resolve(graph, properties["D"]) is PdfDictionary defaultConfiguration)
            {
                configurations.Add(defaultConfiguration);
            }
            else
            {
                Error("PXAPDFTRUST336", "The optional-content properties require a default /D configuration dictionary.", ownerId);
            }

            if (Resolve(graph, properties["Configs"]) is PdfArray additionalConfigurations)
            {
                foreach (var item in additionalConfigurations.Items)
                {
                    if (Resolve(graph, item) is PdfDictionary configuration)
                    {
                        configurations.Add(configuration);
                    }
                    else
                    {
                        Error("PXAPDFTRUST336", "Every /Configs item must resolve to an optional-content configuration dictionary.", ownerId);
                    }
                }
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var configuration in configurations)
            {
                var configurationId = ObjectId(configuration) ?? ownerId;
                var name = ReadText(graph, configuration["Name"]);
                if (string.IsNullOrWhiteSpace(name))
                {
                    Error("PXAPDFTRUST336", "Every optional-content configuration requires a non-empty /Name.", configurationId);
                }
                else if (!names.Add(name))
                {
                    Error("PXAPDFTRUST337", $"Optional-content configuration name '{name}' is duplicated.", configurationId);
                }

                if (configuration["AS"] is not null)
                {
                    Error("PXAPDFTRUST339", "The /AS key is not permitted in an optional-content configuration.", configurationId);
                }

                if (configuration["Order"] is not null)
                {
                    if (Resolve(graph, configuration["Order"]) is not PdfArray order)
                    {
                        Error("PXAPDFTRUST338", "The optional-content /Order entry must be an array.", configurationId);
                    }
                    else
                    {
                        var ordered = EnumerateReferences(order).ToHashSet();
                        if (!ocgIds.IsSubsetOf(ordered))
                        {
                            Error("PXAPDFTRUST338", "The optional-content /Order entry does not contain every OCG reference.", configurationId);
                        }
                    }
                }
            }
        }

        void ValidateRenderingIntent(PdfObject? value, PdfObjectId objectId)
        {
            if (value is null)
            {
                return;
            }

            var intent = ReadName(graph, value);
            if (intent is null || !AllowedRenderingIntents.Contains(intent))
            {
                Error("PXAPDFTRUST340", $"Rendering intent '{intent ?? "<invalid>"}' is not one of the four ISO 32000-1 values.", objectId);
            }
        }

        void ValidateBlendModes(PdfObject? value, PdfObjectId objectId)
        {
            if (value is null)
            {
                return;
            }

            var values = Resolve(graph, value) switch
            {
                PdfName name => new[] { name.Value },
                PdfArray array => array.Items.Select(item => ReadName(graph, item)).ToArray(),
                _ => [null]
            };
            foreach (var mode in values)
            {
                if (mode is null || !AllowedBlendModes.Contains(mode))
                {
                    Error("PXAPDFTRUST341", $"Blend mode '{mode ?? "<invalid>"}' is not defined by ISO 32000-1.", objectId);
                }
            }
        }

        void ValidateContentRenderingIntents()
        {
            var decoder = new PdfStreamDecoderRegistry(new PdfParseLimits());
            foreach (var (objectId, stream) in EnumerateContentStreams(graph))
            {
                try
                {
                    var support = decoder.Evaluate(stream.Dictionary["Filter"]);
                    if (support.Any(static item => item.Status != PdfStreamDecoderSupportStatus.Supported))
                    {
                        Error("PXAPDFTRUST343", "A content stream uses a filter that the structural validator cannot decode safely.", objectId);
                        continue;
                    }

                    var content = stream.IsDecoded ? stream.DecodedBytes : decoder.Decode(stream);
                    foreach (var command in new PdfContentStreamParser().Parse(content).Where(static item => item.Operator.Name == "ri"))
                    {
                        ValidateRenderingIntent(command.Operands.LastOrDefault(), objectId);
                    }
                }
                catch (Exception exception) when (exception is InvalidDataException or NotSupportedException or OverflowException)
                {
                    Error("PXAPDFTRUST343", "A content stream could not be decoded and inspected for PDF/A graphics-state rules.", objectId);
                }
            }
        }

        void ValidateNested(PdfObject value, PdfObjectId objectId, int depth)
        {
            if (depth > 64)
            {
                Error("PXAPDFTRUST330", "A direct PDF object structure exceeds the supported PDF/A validation depth.", objectId);
                return;
            }

            switch (value)
            {
                case PdfDictionary nested:
                    ValidateDictionary(nested, objectId, depth: depth);
                    break;
                case PdfStreamObject stream:
                    ValidateDictionary(stream.Dictionary, objectId, isStreamDictionary: true, depth: depth);
                    break;
                case PdfArray array:
                    foreach (var item in array.Items)
                    {
                        ValidateNested(item, objectId, depth + 1);
                    }
                    break;
            }
        }

        void ValidateEmbeddedFiles(PdfDictionary rootCatalog)
        {
            var embedded = EnumerateEmbeddedFileSpecifications(graph).ToArray();

            if (profile == PdfArchiveProfile.PdfA2b)
            {
                foreach (var item in embedded)
                {
                    Error("PXAPDFTRUST317", "PDF/A-2b does not permit embedded files.", item.Id);
                }
                return;
            }

            var associated = Resolve(graph, rootCatalog["AF"]) is PdfArray af
                ? af.Items.OfType<PdfReference>().Select(static item => item.Id).ToHashSet()
                : [];
            foreach (var item in embedded)
            {
                if (!item.IsIndirect)
                {
                    Error("PXAPDFTRUST323", "A direct embedded file specification cannot be associated safely; materialize it as an indirect object before PDF/A-3 conversion.", item.Id);
                    continue;
                }

                if (!AllowedAfRelationships.Contains(ReadName(graph, item.Dictionary["AFRelationship"]) ?? string.Empty))
                {
                    Error("PXAPDFTRUST318", "A PDF/A-3 embedded file specification has no valid /AFRelationship.", item.Id);
                }

                if (item.Dictionary["F"] is not PdfString && item.Dictionary["UF"] is not PdfString)
                {
                    Error("PXAPDFTRUST319", "A PDF/A-3 embedded file specification has no /F or /UF filename.", item.Id);
                }

                if (!associated.Contains(item.Id))
                {
                    Error("PXAPDFTRUST320", "A PDF/A-3 embedded file specification is not associated through the catalog /AF array.", item.Id);
                }

                var fileStreams = Resolve(graph, item.Dictionary["EF"]) as PdfDictionary;
                var references = fileStreams?.Values.Values.OfType<PdfReference>().ToArray() ?? [];
                if (references.Length == 0)
                {
                    Error("PXAPDFTRUST321", "A PDF/A-3 /EF dictionary does not reference an embedded-file stream.", item.Id);
                    continue;
                }

                foreach (var reference in references)
                {
                    if (graph.Resolve(reference.Id)?.Value is not PdfStreamObject stream ||
                        !IsMimeTypeName(ReadName(graph, stream.Dictionary["Subtype"])))
                    {
                        Error("PXAPDFTRUST322", "A PDF/A-3 embedded-file stream has no valid MIME /Subtype name.", reference.Id);
                    }
                }
            }
        }

        void Error(string code, string message, PdfObjectId? objectId = null)
        {
            valid = false;
            diagnostics.Add(new PdfTrustDiagnostic(
                code,
                PdfTrustDiagnosticSeverity.Error,
                message,
                objectId?.Number,
                objectId?.Generation));
        }
    }

    private static bool HasPdfAMetadata(PdfObjectGraph graph, PdfDictionary catalog, PdfArchiveProfile profile)
    {
        if (Resolve(graph, catalog["Metadata"]) is not PdfStreamObject metadata)
        {
            return false;
        }

        var bytes = metadata.IsDecoded ? metadata.DecodedBytes.Span : metadata.EncodedBytes.Span;
        var xml = Encoding.UTF8.GetString(bytes);
        var part = profile == PdfArchiveProfile.PdfA3b ? 3 : 2;
        return xml.Contains($"<pdfaid:part>{part}</pdfaid:part>", StringComparison.OrdinalIgnoreCase) &&
               xml.Contains("<pdfaid:conformance>B</pdfaid:conformance>", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMimeTypeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var slash = value.IndexOf('/');
        return slash > 0 && slash == value.LastIndexOf('/') && slash < value.Length - 1 &&
               IsMimeToken(value.AsSpan(0, slash)) && IsMimeToken(value.AsSpan(slash + 1));
    }

    private static bool IsMimeToken(ReadOnlySpan<char> value)
    {
        if (value.Length == 0 || value.IndexOfAny("()<>@,;:\\\"/[]?= \t\r\n".AsSpan()) >= 0)
        {
            return false;
        }

        foreach (var current in value)
        {
            if (current is <= '\u001f' or >= '\u007f')
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<EmbeddedFileSpecification> EnumerateEmbeddedFileSpecifications(PdfObjectGraph graph)
    {
        foreach (var indirectObject in graph.Objects.Values)
        {
            if (indirectObject.Value is not PdfDictionary root)
            {
                continue;
            }

            foreach (var result in Visit(root, indirectObject.Id, isIndirect: true))
            {
                yield return result;
            }
        }

        static IEnumerable<EmbeddedFileSpecification> Visit(PdfDictionary dictionary, PdfObjectId owner, bool isIndirect)
        {
            if (dictionary["EF"] is not null)
            {
                yield return new EmbeddedFileSpecification(owner, dictionary, isIndirect);
            }

            foreach (var value in dictionary.Values.Values)
            {
                if (value is PdfDictionary nested)
                {
                    foreach (var result in Visit(nested, owner, isIndirect: false))
                    {
                        yield return result;
                    }
                }
                else if (value is PdfArray array)
                {
                    foreach (var nestedDictionary in array.Items.OfType<PdfDictionary>())
                    {
                        foreach (var result in Visit(nestedDictionary, owner, isIndirect: false))
                        {
                            yield return result;
                        }
                    }
                }
            }
        }
    }

    private sealed record EmbeddedFileSpecification(PdfObjectId Id, PdfDictionary Dictionary, bool IsIndirect);

    private static bool HasRgbOutputIntent(PdfObjectGraph graph, PdfDictionary catalog)
    {
        if (Resolve(graph, catalog["OutputIntents"]) is not PdfArray outputIntents)
        {
            return false;
        }

        foreach (var item in outputIntents.Items)
        {
            if (Resolve(graph, item) is not PdfDictionary intent ||
                !string.Equals(ReadName(graph, intent["S"]), "GTS_PDFA1", StringComparison.Ordinal) ||
                Resolve(graph, intent["DestOutputProfile"]) is not PdfStreamObject profile ||
                ReadInteger(graph, profile.Dictionary["N"]) != 3)
            {
                continue;
            }

            var bytes = profile.IsDecoded ? profile.DecodedBytes.Span : profile.EncodedBytes.Span;
            if (bytes.Length >= 40 && bytes.Slice(36, 4).SequenceEqual("acsp"u8))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasEmbeddedFontProgram(PdfObjectGraph graph, PdfDictionary font)
    {
        if (string.Equals(ReadName(graph, font["Subtype"]), "Type0", StringComparison.Ordinal))
        {
            if (Resolve(graph, font["DescendantFonts"]) is not PdfArray descendants || descendants.Items.Count == 0 ||
                Resolve(graph, descendants.Items[0]) is not PdfDictionary cidFont)
            {
                return false;
            }

            font = cidFont;
        }

        if (Resolve(graph, font["FontDescriptor"]) is not PdfDictionary descriptor)
        {
            return false;
        }

        return descriptor["FontFile"] is not null || descriptor["FontFile2"] is not null || descriptor["FontFile3"] is not null;
    }

    private static bool HasZeroAreaRectangle(PdfObject? value)
    {
        if (value is not PdfArray { Items.Count: >= 4 } rectangle)
        {
            return false;
        }

        var values = rectangle.Items.Take(4).Select(ReadNumber).ToArray();
        return values.All(static item => item is not null) &&
               values[0] == values[2] && values[1] == values[3];
    }

    private static IEnumerable<PdfObjectId> EnumerateReferences(PdfArray array)
    {
        foreach (var item in array.Items)
        {
            switch (item)
            {
                case PdfReference reference:
                    yield return reference.Id;
                    break;
                case PdfArray nested:
                    foreach (var nestedReference in EnumerateReferences(nested))
                    {
                        yield return nestedReference;
                    }
                    break;
            }
        }
    }

    private static IEnumerable<(PdfObjectId Id, PdfStreamObject Stream)> EnumerateContentStreams(PdfObjectGraph graph)
    {
        var emitted = new HashSet<PdfObjectId>();
        foreach (var indirectObject in graph.Objects.Values)
        {
            if (indirectObject.Value is PdfDictionary page && ReadName(graph, page["Type"]) == "Page")
            {
                foreach (var reference in EnumerateContentReferences(page["Contents"]))
                {
                    if (emitted.Add(reference.Id) && graph.Resolve(reference.Id)?.Value is PdfStreamObject content)
                    {
                        yield return (reference.Id, content);
                    }
                }

                if (page["Contents"] is PdfStreamObject directContent)
                {
                    yield return (indirectObject.Id, directContent);
                }
            }

            if (indirectObject.Value is PdfStreamObject stream &&
                (ReadName(graph, stream.Dictionary["Subtype"]) == "Form" ||
                 ReadInteger(graph, stream.Dictionary["PatternType"]) == 1) &&
                emitted.Add(indirectObject.Id))
            {
                yield return (indirectObject.Id, stream);
            }
        }

        static IEnumerable<PdfReference> EnumerateContentReferences(PdfObject? value)
        {
            switch (value)
            {
                case PdfReference reference:
                    yield return reference;
                    break;
                case PdfArray array:
                    foreach (var item in array.Items)
                    {
                        foreach (var reference in EnumerateContentReferences(item))
                        {
                            yield return reference;
                        }
                    }
                    break;
            }
        }
    }

    private static PdfObjectId? ObjectId(PdfObject value) => value.OriginalId;

    private static bool? ReadBoolean(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfBoolean boolean ? boolean.Value : null;

    private static string? ReadText(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfString text ? text.ToLatin1String() : null;

    private static double? ReadNumber(PdfObject value) => value switch
    {
        PdfInteger integer => integer.Value,
        PdfNumber number => number.Value,
        _ => null
    };

    private static string? ResolveFieldType(PdfObjectGraph graph, PdfDictionary field)
    {
        var visited = new HashSet<PdfObjectId>();
        PdfDictionary? current = field;
        while (current is not null)
        {
            if (ReadName(graph, current["FT"]) is { } fieldType)
            {
                return fieldType;
            }

            if (current["Parent"] is not PdfReference parent ||
                !visited.Add(parent.Id) ||
                graph.Resolve(parent.Id)?.Value is not PdfDictionary parentField)
            {
                return null;
            }

            current = parentField;
        }

        return null;
    }

    private static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    private static string? ReadName(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfName name ? name.Value : null;

    private static int? ReadInteger(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) switch
        {
            PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
            PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
            _ => null
        };
}
