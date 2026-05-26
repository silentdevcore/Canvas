using System.Text;
using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.DevExpressPdf;

public sealed class DevExpressPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));
        }

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var analysis = DevExpressPdfAnalyzer.Analyze(root);

        return new MigrationResult
        {
            MigratedCode = BuildMigrationReport(sourceCode, analysis),
            Diagnostics = analysis.Diagnostics
        };
    }

    private static string BuildMigrationReport(string sourceCode, DevExpressPdfAnalysis analysis)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// Canvas.Pdf migration report: DevExpress PDF");
        builder.AppendLine("// DevExpress PDF APIs can mean PDF processing/editing, direct drawing, or report export.");
        builder.AppendLine("// v1 keeps the original source and reports deterministic Canvas.Pdf rewrite candidates.");

        if (analysis.HasProcessor)
        {
            builder.AppendLine("// - Detected PdfDocumentProcessor. Review whether this code creates new PDFs or edits existing PDFs.");
        }

        foreach (var item in analysis.ReportItems)
        {
            builder.Append("// - ");
            builder.Append(item);
            builder.AppendLine();
        }

        if (analysis.HasUnsupportedProcessingApi)
        {
            builder.AppendLine("// - Existing-PDF editing, forms, signatures, encryption, or document operations require manual migration outside v1.");
        }

        if (analysis.HasReportExportApi)
        {
            builder.AppendLine("// - DevExpress reporting/export APIs require report template review before Canvas.Pdf rewrite.");
        }

        builder.AppendLine();
        builder.Append(NormalizeLineEndings(sourceCode));
        return builder.ToString();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class DevExpressPdfAnalyzer
    {
        private readonly List<MigrationDiagnostic> _diagnostics = new();
        private readonly List<string> _reportItems = new();
        private bool _hasProcessor;
        private bool _hasUnsupportedProcessingApi;
        private bool _hasReportExportApi;

        public static DevExpressPdfAnalysis Analyze(CompilationUnitSyntax root)
        {
            var analyzer = new DevExpressPdfAnalyzer();
            analyzer.AnalyzeRoot(root);

            return new DevExpressPdfAnalysis(
                analyzer._diagnostics,
                analyzer._reportItems,
                analyzer._hasProcessor,
                analyzer._hasUnsupportedProcessingApi,
                analyzer._hasReportExportApi);
        }

        private void AnalyzeRoot(CompilationUnitSyntax root)
        {
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeName = GetSimpleTypeName(creation.Type);
                if (typeName == "PdfDocumentProcessor")
                {
                    _hasProcessor = true;
                    _diagnostics.Add(Info("CANMIGDEVEXP001", "DevExpress PdfDocumentProcessor construction was detected."));
                }

                if (typeName is "XtraReport" or "PdfExportOptions")
                {
                    _hasReportExportApi = true;
                    _diagnostics.Add(Warning("CANMIGDEVEXP020", "DevExpress report export workflows require manual report-to-Canvas migration."));
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
                case "CreateEmptyDocument":
                    _reportItems.Add("CreateEmptyDocument(...) detected. Candidate Canvas rewrite starts with `var document = new PdfDocument();`.");
                    _diagnostics.Add(Info("CANMIGDEVEXP002", "DevExpress CreateEmptyDocument(...) was detected as a generated-document candidate."));
                    break;

                case "CreateGraphics":
                    _reportItems.Add("CreateGraphics(...) detected. Canvas drawing should move to a `PdfPage` returned by `document.AddPage()`.");
                    _diagnostics.Add(Info("CANMIGDEVEXP003", "DevExpress CreateGraphics(...) was detected as a drawing candidate."));
                    break;

                case "RenderNewPage":
                    _reportItems.Add("RenderNewPage(...) detected. Candidate Canvas rewrite is `var page = document.AddPage(...)` before drawing calls.");
                    _diagnostics.Add(Info("CANMIGDEVEXP004", "DevExpress RenderNewPage(...) was detected as a page creation candidate."));
                    break;

                case "DrawString":
                    _reportItems.Add(CreateDrawStringReport(invocation));
                    _diagnostics.Add(Info("CANMIGDEVEXP005", "DevExpress DrawString(...) was detected as a Canvas text drawing candidate."));
                    break;

                case "DrawLine":
                    _reportItems.Add("DrawLine(...) detected. Candidate Canvas rewrite is `page.DrawLine(...)` after coordinate review.");
                    _diagnostics.Add(Info("CANMIGDEVEXP006", "DevExpress DrawLine(...) was detected as a Canvas line drawing candidate."));
                    break;

                case "DrawRectangle":
                    _reportItems.Add("DrawRectangle(...) detected. Candidate Canvas rewrite is `page.DrawRectangle(...)` after coordinate review.");
                    _diagnostics.Add(Info("CANMIGDEVEXP007", "DevExpress DrawRectangle(...) was detected as a Canvas rectangle drawing candidate."));
                    break;

                case "SaveDocument":
                    _reportItems.Add("SaveDocument(...) detected. Candidate Canvas rewrite ends with `document.Save(...)`.");
                    _diagnostics.Add(Info("CANMIGDEVEXP008", "DevExpress SaveDocument(...) target was detected for Canvas document.Save(...) mapping."));
                    break;

                case "LoadDocument":
                case "AppendDocument":
                case "CreateEmptyDocumentAsync":
                case "DeletePage":
                case "InsertPage":
                    _hasUnsupportedProcessingApi = true;
                    _diagnostics.Add(Warning("CANMIGDEVEXP021", "DevExpress existing-PDF processing or page editing APIs require manual migration outside v1."));
                    break;

                case "ExportToPdf":
                case "ExportToPdfAsync":
                case "CreateDocument":
                    _hasReportExportApi = true;
                    _diagnostics.Add(Warning("CANMIGDEVEXP020", "DevExpress report export workflows require manual report-to-Canvas migration."));
                    break;
            }
        }

        private static string CreateDrawStringReport(InvocationExpressionSyntax invocation)
        {
            if (invocation.ArgumentList.Arguments.Count > 0)
            {
                var textExpression = invocation.ArgumentList.Arguments[0].Expression.ToString();
                return $"DrawString(...) detected for `{textExpression}`. Candidate Canvas rewrite is `page.DrawTextFromTop({textExpression}, x, y, fontSize)` after coordinate/font review.";
            }

            return "DrawString(...) detected. Candidate Canvas rewrite is `page.DrawTextFromTop(...)` after coordinate/font review.";
        }

        private void AddIdentifierDiagnostics(CompilationUnitSyntax root)
        {
            var names = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(static identifier => identifier.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);

            if (names.Overlaps(new[]
                {
                    "PdfAcroForm",
                    "PdfFormField",
                    "PdfSignature",
                    "PdfDocumentSigner",
                    "PdfEncryptionOptions",
                    "PdfAnnotation",
                    "PdfBookmark",
                    "PdfDocumentProcessor"
                }))
            {
                if (names.Contains("PdfDocumentProcessor"))
                {
                    _hasProcessor = true;
                }

                if (names.Overlaps(new[]
                    {
                        "PdfAcroForm",
                        "PdfFormField",
                        "PdfSignature",
                        "PdfDocumentSigner",
                        "PdfEncryptionOptions",
                        "PdfAnnotation",
                        "PdfBookmark"
                    }))
                {
                    _hasUnsupportedProcessingApi = true;
                    _diagnostics.Add(Warning("CANMIGDEVEXP022", "DevExpress forms, signatures, encryption, annotations, or bookmarks require manual migration outside v1."));
                }
            }

            if (names.Overlaps(new[] { "XtraReport", "PdfExportOptions", "PrintingSystemBase" }))
            {
                _hasReportExportApi = true;
                _diagnostics.Add(Warning("CANMIGDEVEXP020", "DevExpress report export workflows require manual report-to-Canvas migration."));
            }
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

    private sealed record DevExpressPdfAnalysis(
        IReadOnlyList<MigrationDiagnostic> Diagnostics,
        IReadOnlyList<string> ReportItems,
        bool HasProcessor,
        bool HasUnsupportedProcessingApi,
        bool HasReportExportApi);
}
