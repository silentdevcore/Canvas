using System.Text;
using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.FoxitPdf;

public sealed class FoxitPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));
        }

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var analysis = FoxitPdfAnalyzer.Analyze(root);

        return new MigrationResult
        {
            MigratedCode = BuildMigrationReport(sourceCode, analysis),
            Diagnostics = analysis.Diagnostics
        };
    }

    private static string BuildMigrationReport(string sourceCode, FoxitPdfAnalysis analysis)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// Canvas.Pdf migration report: Foxit PDF SDK");
        builder.AppendLine("// Foxit PDF SDK exposes document, page, graphics/content, and processing APIs.");
        builder.AppendLine("// v1 keeps the original source and reports deterministic Canvas.Pdf rewrite candidates.");

        foreach (var item in analysis.ReportItems)
        {
            builder.Append("// - ");
            builder.Append(item);
            builder.AppendLine();
        }

        if (analysis.HasUnsupportedApi)
        {
            builder.AppendLine("// - Existing-PDF editing, forms, annotations, security/signing, rendering, viewer, OCR/conversion, or redaction APIs require manual migration outside v1.");
        }

        builder.AppendLine();
        builder.Append(NormalizeLineEndings(sourceCode));
        return builder.ToString();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class FoxitPdfAnalyzer
    {
        private readonly List<MigrationDiagnostic> _diagnostics = new();
        private readonly List<string> _reportItems = new();
        private bool _hasUnsupportedApi;

        public static FoxitPdfAnalysis Analyze(CompilationUnitSyntax root)
        {
            var analyzer = new FoxitPdfAnalyzer();
            analyzer.AnalyzeRoot(root);

            return new FoxitPdfAnalysis(analyzer._diagnostics, analyzer._reportItems, analyzer._hasUnsupportedApi);
        }

        private void AnalyzeRoot(CompilationUnitSyntax root)
        {
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeName = GetSimpleTypeName(creation.Type);
                switch (typeName)
                {
                    case "PDFDoc":
                        _reportItems.Add("new PDFDoc(...) detected. Candidate Canvas rewrite starts with `var document = new PdfDocument();`; input-file constructors need manual review.");
                        _diagnostics.Add(Info("CANMIGFOXIT001", "Foxit PDFDoc construction was detected."));
                        break;

                    case "Graphics":
                    case "PDFGraphics":
                        _reportItems.Add(typeName + " detected. Map text/image/shape drawing to Canvas page draw calls.");
                        _diagnostics.Add(Info("CANMIGFOXIT003", "Foxit graphics/content object construction was detected."));
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
                case "Initialize" when access.Expression.ToString().EndsWith("Library", StringComparison.Ordinal):
                    _reportItems.Add("Library.Initialize(...) detected. Canvas.Pdf does not require a global Foxit SDK initialization call.");
                    _diagnostics.Add(Info("CANMIGFOXIT000", "Foxit Library.Initialize(...) was detected."));
                    break;

                case "InsertPage":
                case "CreatePage":
                case "NewPage":
                case "AddPage":
                case "PageCreate":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is `var page = document.AddPage(...)` after page size/orientation review.");
                    _diagnostics.Add(Info("CANMIGFOXIT002", "Foxit page creation/insertion was detected as a Canvas page candidate."));
                    break;

                case "GetGraphics":
                case "StartGenerateContents":
                case "GenerateContent":
                    _reportItems.Add(methodName + "(...) detected. Canvas drawing should target the `PdfPage` returned by `document.AddPage(...)`.");
                    _diagnostics.Add(Info("CANMIGFOXIT003", "Foxit graphics/content workflow was detected."));
                    break;

                case "DrawText":
                case "ShowText":
                case "DrawString":
                case "TextOut":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is `page.DrawText(...)` or `page.DrawTextFromTop(...)` after coordinate review.");
                    _diagnostics.Add(Info("CANMIGFOXIT004", "Foxit text drawing was detected."));
                    break;

                case "DrawImage":
                case "AddImage":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is `page.DrawImage(...)` after image sizing/resource review.");
                    _diagnostics.Add(Info("CANMIGFOXIT005", "Foxit image drawing was detected."));
                    break;

                case "DrawLine":
                case "DrawRect":
                case "DrawRectangle":
                case "FillRect":
                case "DrawPath":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite is `page.DrawLine(...)`, `page.DrawRectangle(...)`, or path drawing after geometry review.");
                    _diagnostics.Add(Info("CANMIGFOXIT006", "Foxit shape/path drawing was detected."));
                    break;

                case "Save":
                case "SaveAs":
                    _reportItems.Add(methodName + "(...) detected. Candidate Canvas rewrite ends with `document.Save(...)`; review Foxit save flags.");
                    _diagnostics.Add(Info("CANMIGFOXIT007", "Foxit save/export target was detected for Canvas document.Save(...) mapping."));
                    break;

                case "Load":
                case "LoadFromFile":
                case "ImportFromFile":
                case "DeletePage":
                case "MovePage":
                case "GetAnnot":
                case "GetForm":
                case "GetFormFiller":
                case "Sign":
                case "SetSecurity":
                case "Encrypt":
                case "Decrypt":
                    _hasUnsupportedApi = true;
                    _diagnostics.Add(Warning("CANMIGFOXIT020", "Foxit existing-PDF editing, annotations, forms, signing, or security APIs require manual migration outside v1."));
                    break;

                case "AddAnnot":
                case "StartOCR":
                case "ToPdf":
                case "RenderPageToBitmap":
                case "Redact":
                    _hasUnsupportedApi = true;
                    _diagnostics.Add(Warning("CANMIGFOXIT021", "Foxit annotation, OCR, conversion, rendering, viewer, or redaction APIs are outside the v1 migration scope."));
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
                    "Annot",
                    "Field",
                    "Form",
                    "PDFForm",
                    "Signature",
                    "SecurityHandler",
                    "Redaction",
                    "OCR",
                    "PDFViewCtrl",
                    "Renderer",
                    "Conversion",
                    "Attachment"
                }))
            {
                return;
            }

            _hasUnsupportedApi = true;
            _diagnostics.Add(Warning("CANMIGFOXIT021", "Foxit form, annotation, signing/security, redaction, OCR, rendering, viewer, conversion, or attachment APIs are outside the v1 migration scope."));
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

    private sealed record FoxitPdfAnalysis(
        IReadOnlyList<MigrationDiagnostic> Diagnostics,
        IReadOnlyList<string> ReportItems,
        bool HasUnsupportedApi);
}
