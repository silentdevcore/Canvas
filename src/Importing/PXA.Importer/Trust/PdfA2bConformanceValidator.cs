using System.Text;
using PXA.Importer.Objects;

namespace PXA.Importer.Trust;

internal static class PdfA2bConformanceValidator
{
    public const string Name = "PXA PDF/A-2b/3b structural validator 1.2";

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
            if (isAnnotation && annotationSubtype is { } subtype)
            {
                if (ForbiddenAnnotationSubtypes.Contains(subtype) ||
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

                var flags = ReadInteger(graph, dictionary["F"]) ?? 0;
                if ((flags & 1) != 0 || (flags & 2) != 0 || (flags & 32) != 0 || (flags & 4) == 0)
                {
                    var code = subtype == "Widget" && ResolveFieldType(graph, dictionary) == "Sig"
                        ? "PXAPDFTRUST332"
                        : "PXAPDFTRUST328";
                    Error(code, "PDF/A annotations must be printable and must not be Invisible, Hidden, or NoView.", objectId);
                }
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
