using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PVAD.Migration.Abstractions;
using Esprima;
using Esprima.Ast;

namespace PVAD.Migration.Pdf.Viewer.Nutrient;

public sealed class NutrientViewerSourceConverter
{
    public const int MaximumSourceBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> PackageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "@nutrient-sdk/viewer",
        "pspdfkit"
    };

    public NutrientViewerSourceResult Convert(string source, string sourceLanguage = "javascript", string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!string.Equals(sourceLanguage, "javascript", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sourceLanguage, "js", StringComparison.OrdinalIgnoreCase))
        {
            throw new NutrientViewerSourceException(
                "CANMIGNUTWEB003",
                "TypeScript-specific syntax requires the planned TypeScript Compiler API worker. Submit transpiled JavaScript for this migration phase.");
        }

        return ConvertParsedSource(source, source, "javascript", sourceName ?? "nutrient-viewer.js", []);
    }

    public NutrientViewerSourceResult ConvertTranspiledTypeScript(
        string source,
        string transpiledJavaScript,
        string? sourceName = null,
        IReadOnlyList<string>? compilerDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transpiledJavaScript);
        ValidateSize(source, "TypeScript source");
        ValidateSize(transpiledJavaScript, "Transpiled Viewer source");

        if (string.IsNullOrWhiteSpace(transpiledJavaScript))
        {
            throw new NutrientViewerSourceException(
                "CANMIGNUTWEB007",
                "The TypeScript compiler returned no JavaScript source.");
        }

        var diagnostics = (compilerDiagnostics ?? [])
            .Where(static message => !string.IsNullOrWhiteSpace(message))
            .Take(50)
            .Select(static message => Warning("CANMIGNUTWEB008", $"TypeScript compiler: {message}"))
            .ToArray();

        return ConvertParsedSource(
            transpiledJavaScript,
            source,
            "typescript",
            Path.ChangeExtension(sourceName ?? "nutrient-viewer.ts", ".js"),
            diagnostics);
    }

    private static NutrientViewerSourceResult ConvertParsedSource(
        string parsedSource,
        string checksumSource,
        string sourceLanguage,
        string sourceName,
        IEnumerable<NutrientViewerDiagnostic> initialDiagnostics)
    {
        ValidateSize(parsedSource, "Viewer source");

        Program program;
        try
        {
            var parser = new JavaScriptParser(new ParserOptions
            {
                MaxAssignmentDepth = 100,
                Tolerant = false
            });
            program = parser.ParseModule(parsedSource, sourceName);
        }
        catch (ParserException exception)
        {
            throw new NutrientViewerSourceException("CANMIGNUTWEB004", $"Viewer source is not valid ECMAScript: {exception.Description}", exception);
        }

        var aliases = FindViewerAliases(program);
        var variables = FindVariables(program);
        var loadCall = program.DescendantNodesAndSelf()
            .OfType<CallExpression>()
            .FirstOrDefault(call => IsViewerLoadCall(call.Callee, aliases));

        if (loadCall is null)
        {
            throw new NutrientViewerSourceException(
                "CANMIGNUTWEB005",
                "No NutrientViewer.load(...) or PSPDFKit.load(...) call was found.");
        }

        var detectedProduct = DetectProduct(loadCall.Callee, aliases);
        var configExpression = loadCall.Arguments.Count > 0 ? ResolveExpression(loadCall.Arguments[0], variables) : null;
        if (configExpression is NewExpression newExpression && newExpression.Arguments.Count > 0)
        {
            configExpression = ResolveExpression(newExpression.Arguments[0], variables);
        }

        if (configExpression is not ObjectExpression configObject)
        {
            throw new NutrientViewerSourceException(
                "CANMIGNUTWEB006",
                "The Viewer load configuration must be an object literal or a const variable initialized with an object literal.");
        }

        var diagnostics = new List<NutrientViewerDiagnostic>(initialDiagnostics);
        var config = ReadObject(configObject, variables, diagnostics, "config");
        var normalized = Normalize(config, diagnostics);
        if (program.DescendantNodesAndSelf().OfType<CallExpression>().Any(IsEventRegistration))
        {
            diagnostics.Add(Warning("CANMIGNUTWEB034", "Viewer event integration requires review. Application-owned callback logic was not regenerated."));
        }
        var trustReport = NutrientViewerTrustEvidence.FromViewerSource(parsedSource);
        foreach (var evidence in trustReport.Evidence)
        {
            if (diagnostics.Any(item => string.Equals(item.Code, evidence.DiagnosticId, StringComparison.Ordinal)))
                continue;
            diagnostics.Add(Warning(
                evidence.DiagnosticId,
                $"Nutrient {evidence.Capability} usage requires an explicit PVADE Trust migration. See {evidence.DocumentationPath}."));
        }
        var hasReview = diagnostics.Any(item => string.Equals(item.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
        var sourceLine = FindLoadLine(parsedSource, detectedProduct.StartsWith("PSPDFKit", StringComparison.Ordinal) ? "PSPDFKit" : "NutrientViewer", aliases);
        var generated = GenerateIntegrationModule(normalized);
        var normalizedJson = normalized.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var capabilityReport = NutrientMigrationCapabilityReport.FromDiagnostics(
            migrated: Math.Max(1, normalized.Count - 3),
            diagnostics);

        return new NutrientViewerSourceResult(
            "Nutrient",
            "nutrient-viewer",
            detectedProduct,
            sourceLanguage,
            hasReview ? "reviewRequired" : "compatible",
            "regenerated",
            normalizedJson,
            generated,
            diagnostics,
            [new NutrientViewerSourceMap(sourceLine, 1, "openMigratedPdfViewer", "/pdf/viewer")],
            Sha256(checksumSource),
            Sha256(normalizedJson),
            Sha256(generated),
            capabilityReport)
        {
            PdfTrust = trustReport
        };
    }

    private static void ValidateSize(string source, string label)
    {
        if (Encoding.UTF8.GetByteCount(source) > MaximumSourceBytes)
        {
            throw new NutrientViewerSourceException("CANMIGNUTWEB002", $"{label} exceeds the 2 MiB migration limit.");
        }
    }

    private static HashSet<string> FindViewerAliases(Program program)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal) { "NutrientViewer", "PSPDFKit" };
        foreach (var import in program.Body.OfType<ImportDeclaration>())
        {
            if (import.Source.StringValue is not { } package || !PackageNames.Contains(package))
            {
                continue;
            }

            foreach (var specifier in import.Specifiers)
            {
                aliases.Add(specifier.Local.Name);
            }
        }

        return aliases;
    }

    private static Dictionary<string, Expression> FindVariables(Program program)
    {
        var variables = new Dictionary<string, Expression>(StringComparer.Ordinal);
        foreach (var declaration in program.DescendantNodesAndSelf().OfType<VariableDeclarator>())
        {
            if (declaration.Id is Identifier identifier && declaration.Init is not null)
            {
                variables[identifier.Name] = declaration.Init;
            }
        }

        return variables;
    }

    private static bool IsViewerLoadCall(Expression callee, IReadOnlySet<string> aliases) =>
        callee is MemberExpression
        {
            Object: Identifier owner,
            Property: Identifier { Name: "load" }
        } && aliases.Contains(owner.Name);

    private static string DetectProduct(Expression callee, IReadOnlySet<string> aliases)
    {
        if (callee is MemberExpression { Object: Identifier owner })
        {
            if (string.Equals(owner.Name, "PSPDFKit", StringComparison.Ordinal))
            {
                return "PSPDFKit Web SDK";
            }

            if (string.Equals(owner.Name, "NutrientViewer", StringComparison.Ordinal))
            {
                return "Nutrient Web SDK";
            }

            return aliases.Contains(owner.Name) ? "Nutrient Web SDK" : "Nutrient/PSPDFKit Web SDK";
        }

        return "Nutrient/PSPDFKit Web SDK";
    }

    private static Expression? ResolveExpression(Node? node, IReadOnlyDictionary<string, Expression> variables, int depth = 0)
    {
        if (depth > 12)
        {
            return null;
        }

        return node switch
        {
            Identifier identifier when variables.TryGetValue(identifier.Name, out var expression) => ResolveExpression(expression, variables, depth + 1),
            Expression expression => expression,
            _ => null
        };
    }

    private static JsonObject ReadObject(
        ObjectExpression expression,
        IReadOnlyDictionary<string, Expression> variables,
        ICollection<NutrientViewerDiagnostic> diagnostics,
        string path)
    {
        var result = new JsonObject();
        foreach (var item in expression.Properties)
        {
            if (item is not Property property || PropertyName(property.Key) is not { } name)
            {
                diagnostics.Add(Warning("CANMIGNUTWEB020", $"Dynamic or spread property at '{path}' requires manual migration."));
                continue;
            }

            var value = ReadValue(ResolveExpression(property.Value, variables), variables, diagnostics, $"{path}.{name}");
            if (value is not null)
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static JsonNode? ReadValue(
        Expression? expression,
        IReadOnlyDictionary<string, Expression> variables,
        ICollection<NutrientViewerDiagnostic> diagnostics,
        string path)
    {
        switch (expression)
        {
            case Literal literal when literal.Value is null:
                return JsonValue.Create((string?) null);
            case Literal literal when literal.Value is string text:
                return JsonValue.Create(text);
            case Literal literal when literal.Value is bool boolean:
                return JsonValue.Create(boolean);
            case Literal literal when literal.NumericValue is { } number:
                return JsonValue.Create(number);
            case ObjectExpression objectExpression:
                return ReadObject(objectExpression, variables, diagnostics, path);
            case NewExpression newExpression when newExpression.Arguments.Count > 0:
                return ReadValue(ResolveExpression(newExpression.Arguments[0], variables), variables, diagnostics, path);
            case ArrayExpression arrayExpression:
            {
                var array = new JsonArray();
                foreach (var item in arrayExpression.Elements)
                {
                    var value = ReadValue(ResolveExpression(item, variables), variables, diagnostics, $"{path}[]");
                    if (value is not null)
                    {
                        array.Add(value);
                    }
                }

                return array;
            }
            case UnaryExpression { Operator: UnaryOperator.Minus, Argument: Literal literal } when literal.NumericValue is { } number:
                return JsonValue.Create(-number);
            default:
                diagnostics.Add(Warning("CANMIGNUTWEB021", $"Dynamic value at '{path}' was not copied and requires manual migration."));
                return null;
        }
    }

    private static JsonObject Normalize(JsonObject source, ICollection<NutrientViewerDiagnostic> diagnostics)
    {
        var normalized = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["providerKey"] = "nutrient-viewer",
            ["route"] = "/pdf/viewer"
        };

        CopyString(source, normalized, "container", "mountSelector");
        CopyString(source, normalized, "locale", "locale");
        CopyString(source, normalized, "theme", "theme");

        var document = StringValue(source["document"] ?? source["documentUrl"]);
        if (!string.IsNullOrWhiteSpace(document))
        {
            if (!IsApplicationOwnedDocumentSource(document))
            {
                diagnostics.Add(Warning("CANMIGNUTWEB030", "The external or unsafe document path was not copied. Resolve the PDF through the authenticated PVADE application or asset service."));
                normalized["sourceReviewRequired"] = true;
            }
            else
            {
                normalized["documentSource"] = document;
            }
        }

        if (source.ContainsKey("licenseKey"))
        {
            diagnostics.Add(Warning("CANMIGNUTWEB031", "A Nutrient license key was detected and removed. Configure PVADE licensing separately."));
        }

        if (source.ContainsKey("serverUrl") || source.ContainsKey("jwt") || source.ContainsKey("authPayload"))
        {
            diagnostics.Add(Warning("CANMIGNUTWEB032", "Document Engine or server authentication settings were detected and removed. Review deployment, tenant, and data-flow integration manually."));
            normalized["documentEngineReviewRequired"] = true;
        }

        var viewState = source["initialViewState"] as JsonObject;
        if (NumberValue(viewState?["currentPageIndex"]) is { } pageIndex)
        {
            normalized["initialPageNumber"] = Math.Max(1, (int) pageIndex + 1);
        }

        var zoom = StringValue(viewState?["zoom"] ?? source["initialZoom"]);
        if (!string.IsNullOrWhiteSpace(zoom))
        {
            normalized["initialFitMode"] = zoom.Contains("WIDTH", StringComparison.OrdinalIgnoreCase) ? "width" : "page";
        }

        if (source["toolbarItems"] is JsonArray toolbar)
        {
            normalized["requestedToolbarItems"] = toolbar.DeepClone();
            diagnostics.Add(Warning("CANMIGNUTWEB033", "Toolbar items were retained as UI requests only. PVADE permissions and entitlements remain authoritative."));
        }

        var reviewKeys = new[]
        {
            "scrollMode", "pageTransition", "spreadMode", "layoutMode", "customUI", "customRenderers", "renderers"
        }.Where(source.ContainsKey).ToArray();
        if (reviewKeys.Length > 0)
        {
            normalized["configurationReviewItems"] = new JsonArray(reviewKeys.Select(value => JsonValue.Create(value)).ToArray());
            diagnostics.Add(Warning("CANMIGNUTWEB035", $"Viewer configuration requires manual review: {string.Join(", ", reviewKeys)}."));
        }

        return normalized;
    }

    private static string GenerateIntegrationModule(JsonObject normalized)
    {
        var json = normalized.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        return $$"""
            import { openMigratedPdfViewer } from '@/features/pdf-viewer/migrationAdapter';
            import type { MigratedPdfViewerConfig } from '@/features/pdf-viewer/migrationAdapter';

            const viewerConfig = {{json}} satisfies MigratedPdfViewerConfig;

            export const openViewer = (): void => {
              openMigratedPdfViewer(viewerConfig);
            };
            """;
    }

    private static string? PropertyName(Expression key) => key switch
    {
        Identifier identifier => identifier.Name,
        Literal literal => literal.StringValue,
        _ => null
    };

    private static bool IsEventRegistration(CallExpression call) => call.Callee is MemberExpression
    {
        Property: Identifier { Name: "addEventListener" or "addListener" or "on" }
    };

    private static void CopyString(JsonObject source, JsonObject target, string sourceName, string targetName)
    {
        if (StringValue(source[sourceName]) is { Length: > 0 } value)
        {
            target[targetName] = value;
        }
    }

    private static string? StringValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;

    private static bool IsApplicationOwnedDocumentSource(string source) =>
        (source.StartsWith("/", StringComparison.Ordinal) && !source.StartsWith("//", StringComparison.Ordinal))
        || (source.StartsWith("./", StringComparison.Ordinal) && !source.Contains("../", StringComparison.Ordinal));

    private static double? NumberValue(JsonNode? node) => node is JsonValue value && value.TryGetValue<double>(out var result) ? result : null;

    private static int FindLoadLine(string source, string expectedOwner, IReadOnlySet<string> aliases)
    {
        var owners = new[] { expectedOwner }.Concat(aliases).Distinct(StringComparer.Ordinal);
        var bestIndex = owners.Select(owner => source.IndexOf($"{owner}.load", StringComparison.Ordinal)).Where(index => index >= 0).DefaultIfEmpty(0).Min();
        return source.AsSpan(0, bestIndex).Count('\n') + 1;
    }

    private static string Sha256(string value) => System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static NutrientViewerDiagnostic Warning(string code, string message) => new(code, "Warning", message);
}

public sealed record NutrientViewerSourceResult(
    string Provider,
    string ProviderKey,
    string DetectedProduct,
    string SourceLanguage,
    string DocumentFidelity,
    string SourcePreservation,
    string NormalizedViewerSettingsJson,
    string GeneratedIntegrationModule,
    IReadOnlyList<NutrientViewerDiagnostic> Diagnostics,
    IReadOnlyList<NutrientViewerSourceMap> SourceMap,
    string SourceChecksum,
    string NormalizedChecksum,
    string GeneratedChecksum,
    NutrientMigrationCapabilityReport CapabilityReport)
{
    public PdfTrustMigrationReport? PdfTrust { get; init; }
}

public sealed record NutrientViewerSourceMap(int SourceLine, int SourceColumn, string TargetSymbol, string TargetRoute);

public sealed class NutrientViewerSourceException : Exception
{
    public NutrientViewerSourceException(string code, string message, Exception? innerException = null)
        : base(message, innerException) => Code = code;

    public string Code { get; }
}
