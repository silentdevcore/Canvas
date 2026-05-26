using System.Text;
using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.Apryse;

public sealed class ApryseMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));
        }

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var analysis = ApryseAnalyzer.Analyze(root);

        return new MigrationResult
        {
            MigratedCode = BuildMigrationReport(sourceCode, analysis),
            Diagnostics = analysis.Diagnostics
        };
    }

    private static string BuildMigrationReport(string sourceCode, ApryseAnalysis analysis)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// Canvas.Pdf migration report: Apryse SDK");
        builder.AppendLine("// Apryse/PDFNet exposes low-level PDF document, page, and content stream APIs.");
        builder.AppendLine("// v1 keeps the original source and reports deterministic Canvas.Pdf rewrite candidates.");

        foreach (var item in analysis.ReportItems)
        {
            builder.Append("// - ");
            builder.Append(item);
            builder.AppendLine();
        }

        if (analysis.HasUnsupportedApi)
        {
            builder.AppendLine("// - Existing-PDF editing, SDF object manipulation, forms, annotations, redaction, OCR/conversion, or signatures require manual migration outside v1.");
        }

        builder.AppendLine();
        builder.Append(NormalizeLineEndings(sourceCode));
        return builder.ToString();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class ApryseAnalyzer
    {
        private readonly List<MigrationDiagnostic> _diagnostics = new();
        private readonly List<string> _reportItems = new();
        private bool _hasUnsupportedApi;

        public static ApryseAnalysis Analyze(CompilationUnitSyntax root)
        {
            var analyzer = new ApryseAnalyzer();
            analyzer.AnalyzeRoot(root);

            return new ApryseAnalysis(analyzer._diagnostics, analyzer._reportItems, analyzer._hasUnsupportedApi);
        }

        private void AnalyzeRoot(CompilationUnitSyntax root)
        {
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeName = GetSimpleTypeName(creation.Type);
                switch (typeName)
                {
                    case "PDFDoc":
                        _reportItems.Add("new PDFDoc(...) detected. Candidate Canvas rewrite starts with `var document = new PdfDocument();`.");
                        _diagnostics.Add(Info("CANMIGAPRYSE001", "Apryse PDFDoc construction was detected."));
                        break;

                    case "ElementBuilder":
                        _reportItems.Add("ElementBuilder detected. Map text/image/path element creation to explicit Canvas page draw calls.");
                        _diagnostics.Add(Info("CANMIGAPRYSE005", "Apryse ElementBuilder construction was detected."));
                        break;

                    case "ElementWriter":
                        _reportItems.Add("ElementWriter detected. Map written content stream elements to Canvas page draw calls.");
                        _diagnostics.Add(Info("CANMIGAPRYSE006", "Apryse ElementWriter construction was detected."));
                        break;
                }
            }

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                AnalyzeInvocation(invocation);
            }

            AddIdentifierDiagnostics(root);
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
                case "Initialize" when access.Expression.ToString().EndsWith("PDFNet", StringComparison.Ordinal):
                    _reportItems.Add("PDFNet.Initialize(...) detected. Canvas.Pdf does not require a global SDK initialization call.");
                    _diagnostics.Add(Info("CANMIGAPRYSE000", "Apryse PDFNet.Initialize(...) was detected."));
                    break;

                case "PageCreate":
                    _reportItems.Add("PageCreate(...) detected. Candidate Canvas rewrite is `var page = document.AddPage(...)` after media box review.");
                    _diagnostics.Add(Info("CANMIGAPRYSE002", "Apryse PageCreate(...) was detected as a Canvas page candidate."));
                    break;

                case "PagePushBack":
                    _reportItems.Add("PagePushBack(page) detected. Canvas `document.AddPage(...)` creates and attaches the page in one step.");
                    _diagnostics.Add(Info("CANMIGAPRYSE003", "Apryse PagePushBack(...) was detected as a page append candidate."));
                    break;

                case "Save":
                    _reportItems.Add("doc.Save(...) detected. Candidate Canvas rewrite ends with `document.Save(...)`; review Apryse save flags.");
                    _diagnostics.Add(Info("CANMIGAPRYSE004", "Apryse doc.Save(...) target was detected for Canvas document.Save(...) mapping."));
                    break;

                case "Begin":
                    _reportItems.Add("ElementWriter.Begin(page) detected. Canvas drawing should target the `PdfPage` returned by `document.AddPage(...)`.");
                    _diagnostics.Add(Info("CANMIGAPRYSE007", "Apryse ElementWriter.Begin(...) was detected."));
                    break;

                case "WriteElement":
                    _reportItems.Add("WriteElement(...) detected. Review the ElementBuilder source and map each element to Canvas drawing calls.");
                    _diagnostics.Add(Info("CANMIGAPRYSE008", "Apryse WriteElement(...) was detected as content stream work."));
                    break;

                case "CreateTextBegin":
                case "CreateTextRun":
                case "CreateTextEnd":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is `page.DrawText(...)` after text matrix/font review.");
                    _diagnostics.Add(Info("CANMIGAPRYSE009", "Apryse text element creation was detected."));
                    break;

                case "CreateImage":
                case "CreateImageFromFile":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is `page.DrawImage(...)` after image resource review.");
                    _diagnostics.Add(Info("CANMIGAPRYSE010", "Apryse image element creation was detected."));
                    break;

                case "CreateRect":
                case "CreatePath":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is `page.DrawRectangle(...)` or path drawing after geometry review.");
                    _diagnostics.Add(Info("CANMIGAPRYSE011", "Apryse path/shape element creation was detected."));
                    break;

                case "InitSecurityHandler":
                case "Lock":
                case "GetSDFDoc":
                case "GetAcroForm":
                case "FlattenAnnotations":
                case "SaveViewerOptimized":
                case "Convert":
                case "OCRModule":
                    _hasUnsupportedApi = true;
                    _diagnostics.Add(Warning("CANMIGAPRYSE020", "Apryse processing, conversion, OCR, forms, or low-level SDF APIs require manual migration outside v1."));
                    break;
            }
        }

        private void AddIdentifierDiagnostics(CompilationUnitSyntax root)
        {
            var names = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(static identifier => identifier.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);

            if (!names.Overlaps(new[]
                {
                    "SDFDoc",
                    "Obj",
                    "ElementReader",
                    "Annot",
                    "Field",
                    "DigitalSignatureField",
                    "Redactor",
                    "PDFDraw",
                    "PDFViewCtrl",
                    "Convert",
                    "OCRModule"
                }))
            {
                return;
            }

            _hasUnsupportedApi = true;
            _diagnostics.Add(Warning("CANMIGAPRYSE021", "Apryse SDF, reader, annotation, field, redaction, rendering, viewer, conversion, or OCR APIs are outside the v1 migration scope."));
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

    private sealed record ApryseAnalysis(
        IReadOnlyList<MigrationDiagnostic> Diagnostics,
        IReadOnlyList<string> ReportItems,
        bool HasUnsupportedApi);
}
