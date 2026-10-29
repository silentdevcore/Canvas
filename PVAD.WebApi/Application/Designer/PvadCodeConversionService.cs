using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Observability;

namespace PVAD.WebApi.Application.Designer;

public interface IPvadCodeConversionService
{
    Task<PvadCodeConversionResultDto> ConvertAsync(string sourceLanguage, string targetLanguage, string source, CancellationToken cancellationToken);
    Task<PvadCodeWorkerResponse> ExecuteAsync(string language, string source, CancellationToken cancellationToken);
    PvadCodeConversionResultDto ValidateJson(string source);
}

public sealed class PvadCodeConversionService(
    IPvadCodeWorkerClient worker,
    PVAD.Pdf.PdfFontLoader? fontLoader = null) : IPvadCodeConversionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly IReadOnlyDictionary<string, RepresentationCodec> Codecs = new RepresentationCodec[]
    {
        new JsonRepresentationCodec(),
        new ModelRepresentationCodec(),
        new PdfRepresentationCodec(),
        new Base64RepresentationCodec(),
    }.ToDictionary(codec => codec.Language, StringComparer.Ordinal);

    public async Task<PvadCodeConversionResultDto> ConvertAsync(
        string sourceLanguage, string targetLanguage, string source, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        ValidateLanguage(sourceLanguage);
        ValidateLanguage(targetLanguage);
        var decoded = await Codecs[sourceLanguage].DecodeAsync(source, worker, cancellationToken);
        var design = decoded.Design;
        var diagnostics = decoded.Diagnostics;
        var sourceMap = decoded.SourceMap;
        var fidelity = decoded.Fidelity;

        var normalized = design is null ? "" : JsonSerializer.Serialize(design, JsonOptions);
        sourceMap = [];
        var generated = sourceLanguage == targetLanguage
            ? source
            : design is null ? "" : Codecs[targetLanguage].Encode(design, normalized);
        AddSourceMap(generated, targetLanguage, sourceMap, design);
        if (diagnostics.Any(value => value.Severity == "error"))
            fidelity = PvadCodeFidelity.Unsupported;
        if (design is not null && !diagnostics.Any(value => value.Severity == "error") &&
            sourceLanguage != PvadCodeLanguages.CSharpPdf)
            fidelity = PvadCodeFidelity.Exact;
        var sourcePreservation = sourceLanguage == targetLanguage
            ? PvadCodeSourcePreservation.Preserved
            : sourceLanguage is PvadCodeLanguages.Json or PvadCodeLanguages.CSharpBase64
                ? PvadCodeSourcePreservation.Regenerated
                : PvadCodeSourcePreservation.StructureLost;
        var result = Result(sourceLanguage, targetLanguage, source, generated, normalized, design, fidelity,
            sourcePreservation, diagnostics, sourceMap);
        PvadTelemetry.RecordCodeOperation("convert", sourceLanguage,
            diagnostics.Any(value => value.Severity == "error") ? "rejected" : "succeeded",
            fidelity, Stopwatch.GetElapsedTime(started), diagnostics.Select(value => value.Code).ToArray());
        return result;
    }

    public async Task<PvadCodeWorkerResponse> ExecuteAsync(string language, string source, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        ValidateLanguage(language);
        PvadCodeWorkerResponse result;
        if (language == PvadCodeLanguages.Json)
        {
            var (design, diagnostics) = ParseJson(source);
            result = new PvadCodeWorkerResponse
            {
                Success = design is not null, CanonicalDesign = design,
                Fidelity = design is null ? PvadCodeFidelity.Unsupported : PvadCodeFidelity.Exact,
                Diagnostics = diagnostics,
            };
        }
        else
        {
            result = await worker.RunAsync(new PvadCodeWorkerRequest { Language = language, Source = source, Operation = "execute" }, cancellationToken);
        }
        if (language == PvadCodeLanguages.CSharpPdf && result.Success && result.PdfBytes is null && result.CanonicalDesign is not null)
        {
            var document = DesignJsonMapper.MapToPdfDocument(result.CanonicalDesign, fontLoader);
            result.PdfBytes = document.ToBytes(DesignJsonMapper.BuildSaveOptions(result.CanonicalDesign));
        }
        PvadTelemetry.RecordCodeOperation("execute", language, result.Success ? "succeeded" : "rejected",
            result.Fidelity, Stopwatch.GetElapsedTime(started), result.Diagnostics.Select(value => value.Code).ToArray());
        return result;
    }

    public PvadCodeConversionResultDto ValidateJson(string source)
    {
        var started = Stopwatch.GetTimestamp();
        var (design, diagnostics) = ParseJson(source);
        var normalized = design is null ? "" : JsonSerializer.Serialize(design, JsonOptions);
        var result = Result(PvadCodeLanguages.Json, PvadCodeLanguages.Json, source, normalized, normalized, design,
            design is null ? PvadCodeFidelity.Unsupported : PvadCodeFidelity.Exact,
            PvadCodeSourcePreservation.Preserved, diagnostics, []);
        PvadTelemetry.RecordCodeOperation("validate", PvadCodeLanguages.Json, design is null ? "rejected" : "succeeded",
            result.Fidelity, Stopwatch.GetElapsedTime(started), diagnostics.Select(value => value.Code).ToArray());
        return result;
    }

    private static (DesignExportDto? Design, List<PvadCodeDiagnosticDto> Diagnostics) ParseJson(string source)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(source) > PvadCodeLimits.MaximumDesignBytes)
                return (null, [Diagnostic("PXACODE103", "The canonical design exceeds the 10 MiB limit.")]);
            var design = JsonSerializer.Deserialize<DesignExportDto>(source, JsonOptions);
            if (design is null || design.Pages.Count == 0)
                return (null, [Diagnostic("PXACODE100", "A design must contain at least one page.")]);
            if (design.Pages.SelectMany(value => value.Elements).Any(value => string.IsNullOrWhiteSpace(value.Id) || string.IsNullOrWhiteSpace(value.Type)))
                return (null, [Diagnostic("PXACODE101", "Every element requires a stable id and type.")]);
            if (design.Pages.Any(value => string.IsNullOrWhiteSpace(value.Id)) ||
                design.Pages.GroupBy(value => value.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
                return (null, [Diagnostic("PXACODE104", "Every page requires a unique stable id.")]);
            var elements = design.Pages.SelectMany(value => value.Elements).Concat(design.SharedElements).ToArray();
            if (elements.GroupBy(value => value.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
                return (null, [Diagnostic("PXACODE105", "Every element id must be unique in the design.")]);
            var trustIssues = PdfTrustSettingsValidator.Validate(design.PageSettings);
            if (trustIssues.Count > 0)
                return (null, trustIssues.Select(value => Diagnostic(value.Code, value.Message)).ToList());
            return (design, []);
        }
        catch (JsonException exception)
        {
            return (null, [new PvadCodeDiagnosticDto { Code = "PXACODE102", Severity = "error", Message = exception.Message, Line = (int?)exception.LineNumber + 1, Column = (int?)exception.BytePositionInLine + 1 }]);
        }
    }

    private static string GenerateModel(DesignExportDto design)
    {
        var writer = new CSharpDesignSourceWriter();
        return "// Generated by PVAD. This is an editable, strongly typed design model.\nreturn " +
            writer.Write(design) + ";";
    }

    private static string GenerateBase64(string normalizedJson)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedJson));
        return string.Join(Environment.NewLine,
        [
            "// Generated by PVAD. Compact lossless transport representation.",
            $"var json = new UTF8Encoding(false, true).GetString(Convert.FromBase64String({Literal(encoded)}));",
            "return JsonSerializer.Deserialize<DesignExportDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;",
        ]);
    }

    private static string GeneratePdfBuilder(DesignExportDto design)
    {
        var writer = new CSharpDesignSourceWriter();
        var lines = new List<string>
        {
            "// Generated by PVAD. Semantic builder calls preserve Designer elements exactly.",
            $"var document = new PvadPdfCodeBuilder({writer.WriteDocumentEnvelope(design)});",
        };
        for (var pageIndex = 0; pageIndex < design.Pages.Count; pageIndex++)
        {
            var page = design.Pages[pageIndex];
            var pageVariable = $"page{pageIndex + 1}";
            lines.Add($"var {pageVariable} = document.AddPage({writer.WritePageEnvelope(page)});");
            foreach (var element in page.Elements)
            {
                lines.Add($"// {PvadCodeElementMarker.Canonical} {SafeComment(element.Id)}");
                lines.Add($"{pageVariable}.Add({writer.WriteElement(element)});");
            }
        }
        lines.Add("return document.Build();");
        return string.Join(Environment.NewLine, lines);
    }

    private static PvadCodeConversionResultDto Result(string sourceLanguage, string targetLanguage, string source,
        string generated, string normalized, DesignExportDto? design, string fidelity, string sourcePreservation,
        List<PvadCodeDiagnosticDto> diagnostics, List<PvadCodeSourceMapEntryDto> sourceMap) => new()
    {
        SourceLanguage = sourceLanguage, TargetLanguage = targetLanguage, GeneratedSource = generated,
        CanonicalDesign = design, Fidelity = fidelity, DocumentFidelity = fidelity,
        SourcePreservation = sourcePreservation, Diagnostics = diagnostics, SourceMap = sourceMap,
        SourceChecksum = Hash(source), ResultChecksum = Hash(generated), CanonicalChecksum = Hash(normalized),
    };

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Literal(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    private static string SafeComment(string value) => value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal)[..Math.Min(value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal).Length, 200)];
    private static void AddSourceMap(string source, string language, List<PvadCodeSourceMapEntryDto> map, DesignExportDto? design)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (language == PvadCodeLanguages.CSharpBase64)
        {
            map.Add(new PvadCodeSourceMapEntryDto
            {
                ElementId = "__payload__", Language = language, StartLine = Math.Min(2, lines.Length),
                EndLine = Math.Min(2, lines.Length), StartColumn = 1, EndColumn = lines[Math.Min(1, lines.Length - 1)].Length + 1,
            });
            return;
        }
        if (language == PvadCodeLanguages.Json && design is not null)
        {
            foreach (var element in design.Pages.SelectMany(page => page.Elements).Concat(design.SharedElements))
            {
                var token = $"\"id\": {JsonSerializer.Serialize(element.Id)}";
                var line = Array.FindIndex(lines, value => value.Contains(token, StringComparison.Ordinal));
                if (line >= 0)
                    map.Add(new PvadCodeSourceMapEntryDto
                    {
                        ElementId = element.Id, Language = language, StartLine = line + 1,
                        EndLine = line + 1, StartColumn = lines[line].IndexOf(token, StringComparison.Ordinal) + 1,
                        EndColumn = lines[line].Length + 1,
                    });
            }
            return;
        }
        for (var index = 0; index < lines.Length; index++)
        {
            var id = PvadCodeElementMarker.Read(lines[index]);
            if (id is not null)
                map.Add(new PvadCodeSourceMapEntryDto
                {
                    ElementId = id, Language = language, StartLine = index + 1,
                    EndLine = Math.Min(lines.Length, index + 2), StartColumn = 1, EndColumn = 1,
                });
        }
    }
    private static PvadCodeDiagnosticDto Diagnostic(string code, string message) => new() { Code = code, Severity = "error", Message = message };
    private static void ValidateLanguage(string language) { if (!PvadCodeLanguages.Supported.Contains(language)) throw new ArgumentException($"Unsupported code language '{language}'.", nameof(language)); }

    private sealed record DecodedRepresentation(
        DesignExportDto? Design,
        List<PvadCodeDiagnosticDto> Diagnostics,
        List<PvadCodeSourceMapEntryDto> SourceMap,
        string Fidelity);

    private abstract class RepresentationCodec(string language)
    {
        public string Language { get; } = language;

        public virtual async Task<DecodedRepresentation> DecodeAsync(
            string source, IPvadCodeWorkerClient codeWorker, CancellationToken cancellationToken)
        {
            var response = await codeWorker.RunAsync(new PvadCodeWorkerRequest
            {
                Language = Language, Source = source, Operation = "convert",
            }, cancellationToken);
            return new DecodedRepresentation(
                response.CanonicalDesign, response.Diagnostics, response.SourceMap, response.Fidelity);
        }

        public abstract string Encode(DesignExportDto design, string normalizedJson);
    }

    private sealed class JsonRepresentationCodec() : RepresentationCodec(PvadCodeLanguages.Json)
    {
        public override Task<DecodedRepresentation> DecodeAsync(
            string source, IPvadCodeWorkerClient codeWorker, CancellationToken cancellationToken)
        {
            var (design, diagnostics) = ParseJson(source);
            return Task.FromResult(new DecodedRepresentation(
                design, diagnostics, [], design is null ? PvadCodeFidelity.Unsupported : PvadCodeFidelity.Exact));
        }

        public override string Encode(DesignExportDto design, string normalizedJson) => normalizedJson;
    }

    private sealed class ModelRepresentationCodec() : RepresentationCodec(PvadCodeLanguages.CSharpModel)
    {
        public override string Encode(DesignExportDto design, string normalizedJson) => GenerateModel(design);
    }

    private sealed class PdfRepresentationCodec() : RepresentationCodec(PvadCodeLanguages.CSharpPdf)
    {
        public override string Encode(DesignExportDto design, string normalizedJson) => GeneratePdfBuilder(design);
    }

    private sealed class Base64RepresentationCodec() : RepresentationCodec(PvadCodeLanguages.CSharpBase64)
    {
        public override string Encode(DesignExportDto design, string normalizedJson) => GenerateBase64(normalizedJson);
    }
}
