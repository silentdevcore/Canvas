using System.Text;
using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.DsPdf;

public sealed class DsPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));
        }

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var analysis = DsPdfAnalyzer.Analyze(root);

        return new MigrationResult
        {
            MigratedCode = BuildMigrationReport(sourceCode, analysis),
            Diagnostics = analysis.Diagnostics
        };
    }

    private static string BuildMigrationReport(string sourceCode, DsPdfAnalysis analysis)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// Canvas.Pdf migration report: DsPdf / Document Solutions");
        builder.AppendLine("// DsPdf/GcPdf exposes document, page, graphics, layout, and PDF processing APIs.");
        builder.AppendLine("// v1 keeps the original source and reports deterministic Canvas.Pdf rewrite candidates.");

        foreach (var item in analysis.ReportItems)
        {
            builder.Append("// - ");
            builder.Append(item);
            builder.AppendLine();
        }

        if (analysis.HasUnsupportedApi)
        {
            builder.AppendLine("// - Advanced layout, AcroForms, annotations, PDF/A/compliance, redaction, signatures, security, or existing-PDF editing require manual migration outside v1.");
        }

        builder.AppendLine();
        builder.Append(NormalizeLineEndings(sourceCode));
        return builder.ToString();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class DsPdfAnalyzer
    {
        private readonly List<MigrationDiagnostic> _diagnostics = new();
        private readonly List<string> _reportItems = new();
        private bool _hasUnsupportedApi;

        public static DsPdfAnalysis Analyze(CompilationUnitSyntax root)
        {
            var analyzer = new DsPdfAnalyzer();
            analyzer.AnalyzeRoot(root);

            return new DsPdfAnalysis(analyzer._diagnostics, analyzer._reportItems, analyzer._hasUnsupportedApi);
        }

        private void AnalyzeRoot(CompilationUnitSyntax root)
        {
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeName = GetSimpleTypeName(creation.Type);
                switch (typeName)
                {
                    case "GcPdfDocument":
                        _reportItems.Add("new GcPdfDocument(...) detected. Candidate Canvas rewrite starts with `var document = new PdfDocument();`.");
                        _diagnostics.Add(Info("CANMIGDSPDF001", "DsPdf GcPdfDocument construction was detected."));
                        break;

                    case "TextFormat":
                        _reportItems.Add("TextFormat detected. Map font family, size, style, and color to Canvas text parameters where possible.");
                        _diagnostics.Add(Info("CANMIGDSPDF004", "DsPdf TextFormat usage was detected."));
                        break;

                    case "TableRenderer":
                    case "LayoutHost":
                    case "TextLayout":
                        _hasUnsupportedApi = true;
                        _diagnostics.Add(Warning("CANMIGDSPDF020", "DsPdf advanced layout/table APIs require manual migration outside v1."));
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
                case "NewPage":
                case "AddPage":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is `var page = document.AddPage(...)` after page size/orientation review.");
                    _diagnostics.Add(Info("CANMIGDSPDF002", "DsPdf page creation was detected as a Canvas page candidate."));
                    break;

                case "DrawString":
                    _reportItems.Add("DrawString(...) detected. Candidate Canvas rewrite is `page.DrawText(...)` or `page.DrawTextFromTop(...)` after coordinate/layout review.");
                    _diagnostics.Add(Info("CANMIGDSPDF003", "DsPdf text drawing was detected."));
                    break;

                case "DrawImage":
                    _reportItems.Add("DrawImage(...) detected. Candidate Canvas rewrite is `page.DrawImage(...)` after image sizing/resource review.");
                    _diagnostics.Add(Info("CANMIGDSPDF005", "DsPdf image drawing was detected."));
                    break;

                case "DrawLine":
                case "DrawRectangle":
                case "FillRectangle":
                case "DrawEllipse":
                case "DrawPolygon":
                case "DrawPath":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is a Canvas shape/path drawing call after geometry review.");
                    _diagnostics.Add(Info("CANMIGDSPDF006", "DsPdf shape/path drawing was detected."));
                    break;

                case "Save":
                    _reportItems.Add("Save(...) detected. Candidate Canvas rewrite ends with `document.Save(...)`; review DsPdf save options.");
                    _diagnostics.Add(Info("CANMIGDSPDF007", "DsPdf save/export target was detected for Canvas document.Save(...) mapping."));
                    break;

                case "Load":
                case "LoadFromFile":
                case "DeletePage":
                case "MovePage":
                case "ClonePage":
                case "MergeWithDocument":
                case "ImportPage":
                    _hasUnsupportedApi = true;
                    _diagnostics.Add(Warning("CANMIGDSPDF021", "DsPdf existing-PDF editing and page import/merge APIs require manual migration outside v1."));
                    break;

                case "Sign":
                case "Encrypt":
                case "SetPermissions":
                case "SetSecurity":
                case "SaveAsPdfA":
                case "ConvertToPdfA":
                case "Redact":
                case "ApplyRedactions":
                    _hasUnsupportedApi = true;
                    _diagnostics.Add(Warning("CANMIGDSPDF022", "DsPdf compliance, security, signature, or redaction APIs require manual migration outside v1."));
                    break;

                case "Add":
                    if (access.Expression.ToString().Contains("AcroForm", StringComparison.Ordinal)
                        || access.Expression.ToString().Contains("Annotations", StringComparison.Ordinal))
                    {
                        _hasUnsupportedApi = true;
                        _diagnostics.Add(Warning("CANMIGDSPDF023", "DsPdf AcroForm or annotation APIs require manual migration outside v1."));
                    }

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
                    "AcroForm",
                    "Field",
                    "SignatureProperties",
                    "Security",
                    "Redact",
                    "PdfA",
                    "TableRenderer",
                    "LayoutHost",
                    "TextLayout",
                    "Annotation",
                    "LinkAnnotation",
                    "FileAttachmentAnnotation"
                }))
            {
                return;
            }

            _hasUnsupportedApi = true;
            _diagnostics.Add(Warning("CANMIGDSPDF023", "DsPdf forms, annotations, advanced layout, PDF/A, signatures, security, or redaction APIs are outside the v1 migration scope."));
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

    private sealed record DsPdfAnalysis(
        IReadOnlyList<MigrationDiagnostic> Diagnostics,
        IReadOnlyList<string> ReportItems,
        bool HasUnsupportedApi);
}
