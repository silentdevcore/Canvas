using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using PVAD.CodeWorker;
using PVAD.Core.Contracts;
using PVAD.Generator;
using PVAD.Pdf;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    if (args.Length != 2) return 64;
    var response = new PvadCodeWorkerResponse();
    try
    {
        var requestBytes = await File.ReadAllBytesAsync(args[0]);
        if (requestBytes.Length > PvadCodeLimits.MaximumWorkerRequestBytes) throw new InvalidOperationException("PXACODE009: Worker request is too large.");
        var request = JsonSerializer.Deserialize<PvadCodeWorkerRequest>(requestBytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("PXACODE010: Worker request is invalid.");
        if (request.Language is not (PvadCodeLanguages.CSharpModel or PvadCodeLanguages.CSharpPdf or PvadCodeLanguages.CSharpBase64))
            throw new InvalidOperationException("PXACODE011: Worker language is unsupported.");

        response.Diagnostics = SandboxPolicy.Analyze(request.Source);
        if (response.Diagnostics.Any(value => value.Severity == "error"))
            return await WriteAsync(args[1], response);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, 15)));
        var references = TrustedReferences();
        if (request.Language is PvadCodeLanguages.CSharpModel or PvadCodeLanguages.CSharpBase64)
        {
            var options = ScriptOptions.Default.WithReferences(references)
                .AddReferences(typeof(DesignExportDto).Assembly)
                .AddReferences(typeof(JsonSerializer).Assembly)
                .WithImports("PVAD.Core.Contracts", "System", "System.Collections.Generic", "System.Globalization", "System.Linq", "System.Text", "System.Text.Json");
            response.CanonicalDesign = await CSharpScript.EvaluateAsync<DesignExportDto>(
                request.Source.Trim(), options, cancellationToken: timeout.Token);
            ValidateDesignLimits(response.CanonicalDesign, request);
            response.SourceMap = ParseSourceMap(request.Source, request.Language, response.CanonicalDesign);
            response.Fidelity = PvadCodeFidelity.Exact;
        }
        else
        {
            var options = ScriptOptions.Default.WithReferences(references)
                .AddReferences(typeof(PdfDocument).Assembly)
                .AddReferences(typeof(PvadPdfCodeBuilder).Assembly)
                .AddReferences(typeof(DesignExportDto).Assembly)
                .AddReferences(typeof(JsonSerializer).Assembly)
                .WithImports("PVAD.Core.Contracts", "PVAD.Generator", "PVAD.Pdf", "System", "System.Collections.Generic", "System.Globalization", "System.Linq", "System.Text", "System.Text.Json");
            var evaluated = await CSharpScript.EvaluateAsync<object>(
                request.Source.Trim(), options, cancellationToken: timeout.Token);
            if (evaluated is PvadPdfCodeDocument semantic)
            {
                response.CanonicalDesign = semantic.Design;
                ValidateDesignLimits(response.CanonicalDesign, request);
                response.SourceMap = ParseSourceMap(request.Source, request.Language, response.CanonicalDesign);
                response.Fidelity = PvadCodeFidelity.Exact;
            }
            else if (evaluated is PdfDocument document)
            {
                if (document.Pages.Count > Math.Clamp(request.MaximumPages, 1, 500))
                    throw new InvalidOperationException("PXACODE020: Result exceeds the page limit.");
                response.CanonicalDesign = PdfSnapshotDesignMapper.Map(
                    document.CreateDesignSnapshot(), request.Source,
                    Math.Clamp(request.MaximumElements, 1, 50_000), response.Diagnostics, response.SourceMap);
                response.PdfBytes = request.Operation == "execute" ? document.ToBytes() : null;
                if (response.PdfBytes is { Length: > 25_000_000 })
                    throw new InvalidOperationException("PXACODE023: PDF output exceeds 25 MB.");
                response.Fidelity = response.Diagnostics.Any(value => value.Severity == "warning")
                    ? PvadCodeFidelity.ReviewRequired : PvadCodeFidelity.Compatible;
            }
            else
            {
                throw new InvalidOperationException("PXACODE012: C# PDF code must return PvadPdfCodeDocument or legacy PdfDocument.");
            }
        }
        response.Success = response.CanonicalDesign is not null;
    }
    catch (CompilationErrorException exception)
    {
        response.Diagnostics.AddRange(exception.Diagnostics.Where(value => value.Severity == DiagnosticSeverity.Error)
            .Select(value =>
            {
                var point = value.Location.GetLineSpan().StartLinePosition;
                return new PvadCodeDiagnosticDto { Code = "PXACODE030", Severity = "error", Message = value.GetMessage(), Line = point.Line + 1, Column = point.Character + 1 };
            }));
    }
    catch (OperationCanceledException)
    {
        response.Diagnostics.Add(new PvadCodeDiagnosticDto { Code = "PXACODE031", Severity = "error", Message = "Execution exceeded the sandbox time limit." });
    }
    catch (FormatException)
    {
        response.Diagnostics.Add(new PvadCodeDiagnosticDto { Code = "PXACODE130", Severity = "error", Message = "The Base64 payload is invalid." });
    }
    catch (DecoderFallbackException)
    {
        response.Diagnostics.Add(new PvadCodeDiagnosticDto { Code = "PXACODE131", Severity = "error", Message = "The decoded Base64 payload is not valid UTF-8." });
    }
    catch (JsonException exception)
    {
        response.Diagnostics.Add(new PvadCodeDiagnosticDto { Code = "PXACODE132", Severity = "error", Message = exception.Message });
    }
    catch (Exception exception)
    {
        var message = exception.Message.StartsWith("PXACODE", StringComparison.Ordinal)
            ? exception.Message : $"PXACODE032: {exception.GetType().Name}.";
        response.Diagnostics.Add(new PvadCodeDiagnosticDto { Code = message.Split(':')[0], Severity = "error", Message = message });
    }
    return await WriteAsync(args[1], response);
}

static void ValidateDesignLimits(DesignExportDto design, PvadCodeWorkerRequest request)
{
    if (design.Pages.Count == 0)
        throw new InvalidOperationException("PXACODE100: A design must contain at least one page.");
    if (design.Pages.Any(page => string.IsNullOrWhiteSpace(page.Id)) ||
        design.Pages.GroupBy(page => page.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        throw new InvalidOperationException("PXACODE104: Every page requires a unique stable id.");
    var elements = design.Pages.SelectMany(page => page.Elements).Concat(design.SharedElements).ToArray();
    if (elements.Any(element => string.IsNullOrWhiteSpace(element.Id) || string.IsNullOrWhiteSpace(element.Type)))
        throw new InvalidOperationException("PXACODE101: Every element requires a stable id and type.");
    if (elements.GroupBy(element => element.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        throw new InvalidOperationException("PXACODE105: Every element id must be unique in the design.");
    if (design.Pages.Count > Math.Clamp(request.MaximumPages, 1, 500))
        throw new InvalidOperationException("PXACODE020: Result exceeds the page limit.");
    if (design.Pages.Sum(page => page.Elements.Count) > Math.Clamp(request.MaximumElements, 1, 50_000))
        throw new InvalidOperationException("PXACODE021: The result exceeds the element limit.");
    if (JsonSerializer.SerializeToUtf8Bytes(design).Length > PvadCodeLimits.MaximumDesignBytes)
        throw new InvalidOperationException("PXACODE103: The canonical design exceeds the 10 MiB limit.");
    var trustIssue = PdfTrustSettingsValidator.Validate(design.PageSettings).FirstOrDefault();
    if (trustIssue is not null)
        throw new InvalidOperationException($"{trustIssue.Code}: {trustIssue.Message}");
}

static List<PvadCodeSourceMapEntryDto> ParseSourceMap(string source, string language, DesignExportDto design)
{
    var result = new List<PvadCodeSourceMapEntryDto>();
    var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    if (language == PvadCodeLanguages.CSharpBase64)
    {
        result.Add(new PvadCodeSourceMapEntryDto
        {
            ElementId = "__payload__", Language = language, StartLine = Math.Min(2, lines.Length),
            EndLine = Math.Min(2, lines.Length), StartColumn = 1, EndColumn = lines[Math.Min(1, lines.Length - 1)].Length + 1,
        });
        return result;
    }
    var knownElementIds = design.Pages.SelectMany(page => page.Elements).Concat(design.SharedElements)
        .Select(element => element.Id).ToHashSet(StringComparer.Ordinal);
    for (var index = 0; index < lines.Length; index++)
    {
        var id = PvadCodeElementMarker.Read(lines[index]);
        if (id is not null && knownElementIds.Contains(id))
            result.Add(new PvadCodeSourceMapEntryDto { ElementId = id, Language = language, StartLine = index + 1, EndLine = Math.Min(index + 2, lines.Length), StartColumn = 1, EndColumn = 1 });
    }
    return result;
}

static List<MetadataReference> TrustedReferences() =>
    (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
    .Where(path =>
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name is "System.Private.CoreLib" or "System.Runtime" or "System.Collections" or
            "System.Collections.Concurrent" or "System.Linq" or "System.Console" or "System.Memory" or
            "System.Text.Json" or "System.Text.Encodings.Web" or "System.Text.RegularExpressions" or "netstandard";
    })
    .Select(path => MetadataReference.CreateFromFile(path)).Cast<MetadataReference>().ToList();

static async Task<int> WriteAsync(string path, PvadCodeWorkerResponse response)
{
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(response, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    }));
    return response.Success ? 0 : 2;
}
