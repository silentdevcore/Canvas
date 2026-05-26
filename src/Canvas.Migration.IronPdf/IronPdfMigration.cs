using System.Text;
using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.IronPdf;

public sealed class IronPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));
        }

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var analysis = IronPdfAnalyzer.Analyze(root);

        return new MigrationResult
        {
            MigratedCode = BuildMigrationReport(sourceCode, analysis),
            Diagnostics = analysis.Diagnostics
        };
    }

    private static string BuildMigrationReport(string sourceCode, IronPdfAnalysis analysis)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// Canvas.Pdf migration report: IronPDF");
        builder.AppendLine("// IronPDF commonly renders HTML/CSS through Chromium. Canvas.Pdf is a drawing API,");
        builder.AppendLine("// so v1 keeps the original code and reports the manual rewrite work instead of");
        builder.AppendLine("// producing misleading Canvas draw calls.");

        if (analysis.HasChromePdfRenderer)
        {
            builder.AppendLine("// - Detected ChromePdfRenderer/HtmlToPdf renderer construction.");
        }

        foreach (var renderCall in analysis.RenderCalls)
        {
            builder.Append("// - ");
            builder.Append(renderCall.MethodName);
            builder.Append(": ");
            builder.Append(renderCall.Message);
            builder.AppendLine();
        }

        foreach (var saveCall in analysis.SaveCalls)
        {
            builder.Append("// - ");
            builder.Append(saveCall);
            builder.AppendLine(" detected. Keep this as the final Canvas document.Save(...) target after manual rewrite.");
        }

        if (analysis.HasUnsupportedEditingApi)
        {
            builder.AppendLine("// - PDF editing/merge/security/signing APIs require manual migration outside v1.");
        }

        builder.AppendLine();
        builder.Append(NormalizeLineEndings(sourceCode));
        return builder.ToString();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class IronPdfAnalyzer
    {
        private readonly List<MigrationDiagnostic> _diagnostics = new();
        private readonly List<IronPdfRenderCall> _renderCalls = new();
        private readonly List<string> _saveCalls = new();
        private bool _hasChromePdfRenderer;
        private bool _hasUnsupportedEditingApi;

        public static IronPdfAnalysis Analyze(CompilationUnitSyntax root)
        {
            var analyzer = new IronPdfAnalyzer();
            analyzer.AnalyzeRoot(root);

            return new IronPdfAnalysis(
                analyzer._diagnostics,
                analyzer._renderCalls,
                analyzer._saveCalls,
                analyzer._hasChromePdfRenderer,
                analyzer._hasUnsupportedEditingApi);
        }

        private void AnalyzeRoot(CompilationUnitSyntax root)
        {
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeName = GetSimpleTypeName(creation.Type);
                if (typeName is "ChromePdfRenderer" or "HtmlToPdf")
                {
                    _hasChromePdfRenderer = true;
                    _diagnostics.Add(Info("CANMIGIRONPDF001", "IronPDF renderer construction was detected. v1 reports manual Canvas.Pdf rewrite work."));
                }
            }

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                AnalyzeInvocation(invocation);
            }

            AddUnsupportedApiDiagnostics(root);
        }

        private void AnalyzeInvocation(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access)
            {
                return;
            }

            var methodName = access.Name.Identifier.ValueText;
            switch (methodName)
            {
                case "RenderHtmlAsPdf":
                    AddRenderDiagnostic(
                        "CANMIGIRONPDF002",
                        methodName,
                        "HTML/CSS rendering requires manual Canvas layout migration.",
                        IsLiteralArgument(invocation, out var literalHtml)
                            ? $"Literal HTML detected for manual extraction: {Truncate(literalHtml)}"
                            : "HTML source is dynamic; inspect template/data flow before rewriting.");
                    break;

                case "RenderHtmlFileAsPdf":
                    AddRenderDiagnostic(
                        "CANMIGIRONPDF003",
                        methodName,
                        "HTML file rendering requires manual template review before Canvas rewrite.",
                        "HTML file rendering requires manual template review before Canvas rewrite. Map file template content to Canvas draw calls manually.");
                    break;

                case "RenderUrlAsPdf":
                    AddRenderDiagnostic(
                        "CANMIGIRONPDF004",
                        methodName,
                        "URL-to-PDF rendering is outside direct Canvas.Pdf source migration.",
                        "URL-to-PDF rendering is outside direct Canvas.Pdf source migration. Capture or recreate the page content explicitly before migrating.");
                    break;

                case "RenderRazorToPdf":
                case "RenderRazorViewToPdf":
                    AddRenderDiagnostic(
                        "CANMIGIRONPDF005",
                        methodName,
                        "Razor-to-PDF rendering requires manual view/template migration.",
                        "Razor-to-PDF rendering requires manual view/template migration. Review Razor model binding and layout before Canvas rewrite.");
                    break;

                case "SaveAs":
                    _saveCalls.Add("SaveAs(...)");
                    _diagnostics.Add(Info("CANMIGIRONPDF006", "IronPDF SaveAs(...) target was detected for later Canvas document.Save(...) mapping."));
                    break;

                case "SaveAsAsync":
                    _saveCalls.Add("SaveAsAsync(...)");
                    _diagnostics.Add(Info("CANMIGIRONPDF007", "IronPDF SaveAsAsync(...) target was detected for later async save strategy review."));
                    break;
            }
        }

        private void AddRenderDiagnostic(string id, string methodName, string message, string reportMessage)
        {
            _renderCalls.Add(new IronPdfRenderCall(methodName, reportMessage));
            _diagnostics.Add(Warning(id, message));
        }

        private void AddUnsupportedApiDiagnostics(CompilationUnitSyntax root)
        {
            var names = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(static identifier => identifier.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);

            if (!names.Overlaps(new[]
                {
                    "PdfDocument",
                    "PdfDocumentBase",
                    "PdfFile",
                    "PdfSignature",
                    "SecuritySettings",
                    "PdfMerger",
                    "Merge",
                    "AppendPdf",
                    "CopyPages",
                    "ExtractAllText"
                }))
            {
                return;
            }

            _hasUnsupportedEditingApi = true;
            _diagnostics.Add(Warning("CANMIGIRONPDF020", "IronPDF PDF editing, merge, text extraction, security, or signing APIs are outside the v1 migration scope."));
        }

        private static bool IsLiteralArgument(InvocationExpressionSyntax invocation, out string literalValue)
        {
            literalValue = string.Empty;

            if (invocation.ArgumentList.Arguments.Count == 0)
            {
                return false;
            }

            if (invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal
                && literal.Kind() == SyntaxKind.StringLiteralExpression
                && literal.Token.ValueText is { Length: > 0 } value)
            {
                literalValue = value;
                return true;
            }

            return false;
        }

        private static string Truncate(string value)
        {
            const int maxLength = 80;
            var normalized = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
        }

        private static string GetSimpleTypeName(TypeSyntax type)
        {
            return type switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText,
                AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
                _ => type.ToString()
            };
        }

        private static MigrationDiagnostic Info(string id, string message)
        {
            return new MigrationDiagnostic
            {
                Id = id,
                Message = message,
                Severity = MigrationDiagnosticSeverity.Info
            };
        }

        private static MigrationDiagnostic Warning(string id, string message)
        {
            return new MigrationDiagnostic
            {
                Id = id,
                Message = message,
                Severity = MigrationDiagnosticSeverity.Warning
            };
        }
    }

    private sealed record IronPdfAnalysis(
        IReadOnlyList<MigrationDiagnostic> Diagnostics,
        IReadOnlyList<IronPdfRenderCall> RenderCalls,
        IReadOnlyList<string> SaveCalls,
        bool HasChromePdfRenderer,
        bool HasUnsupportedEditingApi);

    private sealed record IronPdfRenderCall(string MethodName, string Message);
}
