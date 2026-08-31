using System.Text;
using PXA.Importer.Objects;

namespace PXA.Importer.Trust;

internal static class PdfA2bConformanceValidator
{
    public const string Name = "PXA PDF/A-2b structural validator 1.0";

    private static readonly HashSet<string> ForbiddenDictionaryKeys = new(StringComparer.Ordinal)
    {
        "JS", "JavaScript", "Launch", "RichMediaContent", "RichMediaSettings"
    };

    private static readonly HashSet<string> ForbiddenAnnotationSubtypes = new(StringComparer.Ordinal)
    {
        "RichMedia", "Movie", "Sound", "FileAttachment"
    };

    public static bool CanValidate(PdfObjectGraph graph)
    {
        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        return catalog is not null && HasPdfA2bMetadata(graph, catalog);
    }

    public static bool Validate(
        PdfObjectGraph graph,
        bool isEncrypted,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        var valid = true;
        if (isEncrypted)
        {
            Error("PXAPDFTRUST310", "PDF/A-2b does not permit encryption.");
        }

        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        if (catalog is null)
        {
            Error("PXAPDFTRUST311", "The PDF catalog could not be resolved for PDF/A validation.");
            return false;
        }

        if (!HasPdfA2bMetadata(graph, catalog))
        {
            Error("PXAPDFTRUST312", "The catalog does not reference XMP metadata identifying PDF/A-2b.");
        }

        if (!HasRgbOutputIntent(graph, catalog))
        {
            Error("PXAPDFTRUST313", "A valid RGB PDF/A output intent with an ICC profile was not found.");
        }

        foreach (var indirectObject in graph.Objects.Values)
        {
            if (indirectObject.Value is PdfDictionary dictionary)
            {
                ValidateDictionary(dictionary, indirectObject.Id);
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

        if (valid)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST300",
                PdfTrustDiagnosticSeverity.Info,
                "The document passed the PXA structural PDF/A-2b validator. Independent validation is still recommended for release evidence."));
        }

        return valid;

        void ValidateDictionary(PdfDictionary dictionary, PdfObjectId objectId)
        {
            foreach (var key in dictionary.Values.Keys)
            {
                if (ForbiddenDictionaryKeys.Contains(key))
                {
                    Error("PXAPDFTRUST315", $"Forbidden PDF/A action or multimedia key '/{key}' was found.", objectId);
                }
            }

            if (string.Equals(ReadName(graph, dictionary["Type"]), "Annot", StringComparison.Ordinal) &&
                ReadName(graph, dictionary["Subtype"]) is { } subtype &&
                ForbiddenAnnotationSubtypes.Contains(subtype))
            {
                Error("PXAPDFTRUST316", $"Annotation subtype '/{subtype}' is not accepted by the PXA PDF/A-2b validator.", objectId);
            }

            foreach (var nested in dictionary.Values.Values.OfType<PdfDictionary>())
            {
                ValidateDictionary(nested, objectId);
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

    private static bool HasPdfA2bMetadata(PdfObjectGraph graph, PdfDictionary catalog)
    {
        if (Resolve(graph, catalog["Metadata"]) is not PdfStreamObject metadata)
        {
            return false;
        }

        var bytes = metadata.IsDecoded ? metadata.DecodedBytes.Span : metadata.EncodedBytes.Span;
        var xml = Encoding.UTF8.GetString(bytes);
        return xml.Contains("<pdfaid:part>2</pdfaid:part>", StringComparison.OrdinalIgnoreCase) &&
               xml.Contains("<pdfaid:conformance>B</pdfaid:conformance>", StringComparison.OrdinalIgnoreCase);
    }

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
