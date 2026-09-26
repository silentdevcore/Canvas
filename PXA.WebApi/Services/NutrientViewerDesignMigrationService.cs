using System.Globalization;
using System.Text.Json;
using PXA.Core.Contracts;
using PXA.FileImporter;
using PXA.Migration.Pdf.Viewer.Nutrient;

namespace PXA.WebApi.Services;

public sealed class NutrientViewerDesignMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<NutrientViewerDesignMigrationResult> ConvertAsync(
        Stream pdf,
        string pdfName,
        string inputType,
        string source,
        string? sourceName,
        string? transpiledSource,
        IReadOnlyList<string>? compilerDiagnostics,
        CancellationToken cancellationToken)
    {
        var design = await PdfFileImporter.DoImportAsync(
            pdf,
            Path.GetFileNameWithoutExtension(pdfName),
            new PdfFileImportOptions { ChartRecognition = PdfChartRecognitionMode.Safe },
            cancellationToken);
        var pages = design.Pages.Select((page, index) => ToGeometry(design, page, index)).ToArray();

        PxaViewerAnnotationSidecar? annotations = null;
        PxaViewerFormValuePayload? formValues = null;
        JsonElement? viewerSettings = null;
        string fidelity;
        string sourcePreservation;
        NutrientMigrationCapabilityReport capability;
        var diagnostics = new List<NutrientViewerDiagnostic>();

        if (inputType.Equals("instantJson", StringComparison.OrdinalIgnoreCase))
        {
            var converted = new NutrientInstantJsonConverter().Convert(source, pages, sourceName ?? pdfName);
            annotations = JsonSerializer.Deserialize<PxaViewerAnnotationSidecar>(converted.AnnotationSidecarJson, JsonOptions);
            formValues = JsonSerializer.Deserialize<PxaViewerFormValuePayload>(converted.FormValuePayloadJson, JsonOptions);
            diagnostics.AddRange(converted.Diagnostics);
            fidelity = converted.DocumentFidelity;
            sourcePreservation = converted.SourcePreservation;
            capability = converted.CapabilityReport;
        }
        else
        {
            var converted = inputType.Equals("typescript", StringComparison.OrdinalIgnoreCase)
                ? new NutrientViewerSourceConverter().ConvertTranspiledTypeScript(
                    source,
                    transpiledSource ?? throw new ArgumentException("TypeScript compiler output is required."),
                    sourceName ?? pdfName,
                    compilerDiagnostics)
                : new NutrientViewerSourceConverter().Convert(source, "javascript", sourceName ?? pdfName);
            viewerSettings = JsonDocument.Parse(converted.NormalizedViewerSettingsJson).RootElement.Clone();
            diagnostics.AddRange(converted.Diagnostics);
            fidelity = converted.DocumentFidelity;
            sourcePreservation = converted.SourcePreservation;
            capability = converted.CapabilityReport;
        }

        var mappedAnnotations = ApplyAnnotations(design, annotations, diagnostics);
        var appliedFormValues = ApplyFormValues(design, formValues, diagnostics);
        PreserveMigrationMetadata(design, inputType, viewerSettings, annotations, formValues);

        design.ImportDiagnostics ??= [];
        design.ImportDiagnostics.AddRange(diagnostics.Select(static diagnostic => new ImportDiagnosticDto
        {
            Code = diagnostic.Code,
            Severity = diagnostic.Severity.ToLowerInvariant(),
            Message = diagnostic.Message,
            Source = "Nutrient Viewer migration"
        }));

        return new NutrientViewerDesignMigrationResult(
            design,
            diagnostics,
            fidelity,
            sourcePreservation,
            capability,
            mappedAnnotations,
            appliedFormValues);
    }

    private static NutrientPageGeometry ToGeometry(DesignExportDto design, PageDto page, int index)
    {
        var width = page.Layout?.Width ?? design.PageSettings?.Width ?? 595;
        var height = page.Layout?.Height ?? design.PageSettings?.Height ?? 842;
        return new NutrientPageGeometry(width, height);
    }

    private static int ApplyAnnotations(
        DesignExportDto design,
        PxaViewerAnnotationSidecar? sidecar,
        ICollection<NutrientViewerDiagnostic> diagnostics)
    {
        if (sidecar is null) return 0;
        var usedIds = design.Pages.SelectMany(static page => page.Elements)
            .Select(static element => element.Id)
            .ToHashSet(StringComparer.Ordinal);
        var count = 0;

        foreach (var annotation in sidecar.Annotations)
        {
            if (annotation.PageNumber < 1 || annotation.PageNumber > design.Pages.Count)
            {
                diagnostics.Add(new("CANMIGNUTDESIGN001", "Warning", $"Annotation '{annotation.Id}' references a missing PDF page and was omitted."));
                continue;
            }

            var page = design.Pages[annotation.PageNumber - 1];
            var width = page.Layout?.Width ?? design.PageSettings?.Width ?? 595;
            var height = page.Layout?.Height ?? design.PageSettings?.Height ?? 842;
            var id = UniqueId($"nutrient-{annotation.Id}", usedIds);
            var element = MapAnnotation(annotation, id, width, height, diagnostics);
            if (element is null) continue;
            page.Elements.Add(element);
            count++;
        }

        return count;
    }

    private static ElementDto? MapAnnotation(
        PxaViewerAnnotation annotation,
        string id,
        double pageWidth,
        double pageHeight,
        ICollection<NutrientViewerDiagnostic> diagnostics)
    {
        var x = pageWidth * annotation.XPct / 100d;
        var y = pageHeight * annotation.YPct / 100d;
        var width = Math.Max(1, pageWidth * annotation.WidthPct / 100d);
        var height = Math.Max(1, pageHeight * annotation.HeightPct / 100d);
        var style = new Dictionary<string, object>
        {
            ["color"] = annotation.Color,
            ["borderColor"] = annotation.Color,
            ["borderWidth"] = annotation.StrokeWidth ?? 1,
            ["opacity"] = annotation.Opacity ?? 1,
            ["nutrientAnnotationType"] = annotation.Type,
        };
        if (!string.IsNullOrWhiteSpace(annotation.FillColor)) style["backgroundColor"] = annotation.FillColor;

        var element = new ElementDto
        {
            Id = id,
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2),
            Locked = annotation.Locked,
            Style = style,
        };

        switch (annotation.Type)
        {
            case "note":
                element.Type = "note";
                element.NoteTitle = annotation.Metadata?.Subject ?? "Nutrient note";
                element.NoteBody = annotation.Text;
                element.NoteAuthor = annotation.Author;
                break;
            case "freeText":
            case "stamp":
                element.Type = "text";
                element.Content = annotation.Text;
                if (annotation.Type == "stamp") style["fontWeight"] = "bold";
                break;
            case "rectangle":
            case "redaction":
                element.Type = "shape";
                if (annotation.Type == "redaction") style["backgroundColor"] = annotation.FillColor ?? "#000000";
                break;
            case "circle":
                element.Type = "circle";
                break;
            case "line":
            case "underline":
            case "strikeout":
                element.Type = "line";
                element.StartMarker = annotation.LineEndingStart;
                element.EndMarker = annotation.LineEndingEnd;
                break;
            case "highlight":
                element.Type = "highlight";
                style["backgroundColor"] = annotation.FillColor ?? annotation.Color;
                style["opacity"] = annotation.Opacity ?? 0.4;
                break;
            case "ink":
                element.Type = "draw";
                element.DrawTool = "pen";
                element.PathData = BuildInkPath(annotation, x, y, pageWidth, pageHeight);
                break;
            case "image":
                diagnostics.Add(new("CANMIGNUTDESIGN002", "Warning", $"Image annotation '{annotation.Id}' has no transferable image payload and remains in migration metadata only."));
                return null;
            default:
                diagnostics.Add(new("CANMIGNUTDESIGN003", "Warning", $"Annotation type '{annotation.Type}' cannot be represented by the Designer and was omitted."));
                return null;
        }

        return element;
    }

    private static string BuildInkPath(PxaViewerAnnotation annotation, double x, double y, double pageWidth, double pageHeight)
    {
        var lines = annotation.PointLines is { Count: > 0 }
            ? annotation.PointLines
            : annotation.Points is { Count: > 0 } ? [annotation.Points] : [];
        return string.Join(" ", lines.Select(line => string.Join(" ", line.Select((point, index) =>
        {
            var localX = Math.Round((point.XPct / 100d * pageWidth) - x, 2);
            var localY = Math.Round((point.YPct / 100d * pageHeight) - y, 2);
            return $"{(index == 0 ? "M" : "L")}{localX.ToString(CultureInfo.InvariantCulture)},{localY.ToString(CultureInfo.InvariantCulture)}";
        }))));
    }

    private static int ApplyFormValues(
        DesignExportDto design,
        PxaViewerFormValuePayload? payload,
        ICollection<NutrientViewerDiagnostic> diagnostics)
    {
        if (payload is null) return 0;
        var elements = design.Pages.SelectMany(static page => page.Elements)
            .Concat(design.SharedElements)
            .Where(static element => !string.IsNullOrWhiteSpace(element.FieldName))
            .GroupBy(static element => element.FieldName!, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var applied = 0;

        foreach (var field in payload.Fields)
        {
            if (!elements.TryGetValue(field.Name, out var matches))
            {
                diagnostics.Add(new("CANMIGNUTDESIGN010", "Warning", $"Form value '{field.Name}' has no matching imported Designer field and remains in migration metadata."));
                continue;
            }

            foreach (var element in matches)
            {
                element.Required = field.Required;
                element.Options = field.Options.Count > 0 ? field.Options.ToArray() : element.Options;
                switch (field.Kind)
                {
                    case "checkbox":
                        element.CheckState = field.Value is bool boolean && boolean ? "checked" : "unchecked";
                        break;
                    case "radio":
                    case "dropdown":
                    case "list":
                        element.SelectedValue = JsonValueText(field.Value);
                        break;
                    default:
                        element.Content = JsonValueText(field.Value);
                        break;
                }
                applied++;
            }
        }

        return applied;
    }

    private static string JsonValueText(object? value) => value switch
    {
        null => string.Empty,
        JsonElement json when json.ValueKind == JsonValueKind.Array => string.Join(", ", json.EnumerateArray().Select(static item => item.ToString())),
        JsonElement json => json.ToString(),
        IEnumerable<string> values => string.Join(", ", values),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static void PreserveMigrationMetadata(
        DesignExportDto design,
        string inputType,
        JsonElement? viewerSettings,
        PxaViewerAnnotationSidecar? annotations,
        PxaViewerFormValuePayload? formValues)
    {
        design.Extensions ??= [];
        design.Extensions["nutrientViewerMigration"] = JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1,
            provider = "nutrient",
            inputType,
            viewerSettings,
            annotationSidecar = annotations,
            formValues,
        }, JsonOptions);
    }

    private static string UniqueId(string requested, ISet<string> used)
    {
        var safe = new string(requested.Where(static character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(safe)) safe = $"nutrient-{Guid.NewGuid():N}";
        var candidate = safe;
        var suffix = 2;
        while (!used.Add(candidate)) candidate = $"{safe}-{suffix++}";
        return candidate;
    }
}

public sealed record NutrientViewerDesignMigrationResult(
    DesignExportDto Design,
    IReadOnlyList<NutrientViewerDiagnostic> Diagnostics,
    string DocumentFidelity,
    string SourcePreservation,
    NutrientMigrationCapabilityReport CapabilityReport,
    int MappedAnnotations,
    int AppliedFormValues);
