using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.PdfTools;

public sealed class PdfToolsMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var rewriter = new PdfToolsRewriter();
        var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;

        var diagnostics = new List<MigrationDiagnostic>
        {
            Warning("CANMIGPDFTOOLS000",
                "Pdftools SDK is primarily a PDF conversion, optimization, validation, signing, rendering, and processing SDK; direct PDF generation belongs to the separate PDF Toolbox SDK/add-on and is not automatically migrated here.")
        };
        diagnostics.AddRange(rewriter.Diagnostics);
        diagnostics.AddRange(ScanForUnsupportedSdkUsage(root));

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(rewritten.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics
        };
    }

    private static IEnumerable<MigrationDiagnostic> ScanForUnsupportedSdkUsage(CompilationUnitSyntax root)
    {
        var usingNames = root.Usings
            .Select(static directive => directive.Name?.ToString() ?? "")
            .ToArray();

        if (usingNames.Any(static name => name.Equals("PdfTools.Toolbox", StringComparison.Ordinal)
            || name.StartsWith("PdfTools.Toolbox.", StringComparison.Ordinal)))
        {
            yield return Warning("CANMIGPDFTOOLS022",
                "PDF Toolbox SDK direct-generation APIs are a separate product/add-on; collect Toolbox-specific samples before adding automatic PXA.Pdf rewrites.");
        }

        var names = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(static identifier => identifier.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        if (names.Overlaps(new[]
            {
                "Document",
                "DocumentAssembler",
                "Converter",
                "Conversion",
                "Renderer",
                "Render",
                "Image",
                "Optimizer",
                "Validator",
                "Repair",
                "Archive",
                "Conformance",
                "Signature",
                "Signer",
                "Encryption",
                "Security",
                "Form",
                "Annotation",
                "Bookmark",
                "Outline",
                "Ocr"
            }))
        {
            yield return Warning("CANMIGPDFTOOLS020",
                "Pdftools SDK document assembly, conversion, rendering, optimization, validation, signing, security, forms, annotations, outlines, or OCR workflows require manual migration outside v1.");
        }

        if (root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsManualWorkflowCall))
        {
            yield return Warning("CANMIGPDFTOOLS021",
                "Pdftools SDK existing-PDF processing calls require manual migration; PXA.Pdf output should be recreated as document composition code.");
        }
    }

    private static bool IsManualWorkflowCall(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return false;

        return access.Name.Identifier.ValueText is
            "Open" or
            "Load" or
            "Save" or
            "Convert" or
            "ConvertToPdf" or
            "ConvertToImage" or
            "Render" or
            "Optimize" or
            "Validate" or
            "Repair" or
            "Sign" or
            "Encrypt" or
            "Decrypt" or
            "Merge" or
            "Split" or
            "ExtractPages" or
            "Assemble" or
            "Append";
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static MigrationDiagnostic Info(string id, string message) => new()
    {
        Id = id,
        Message = message,
        Severity = MigrationDiagnosticSeverity.Info
    };

    private static MigrationDiagnostic Warning(string id, string message) => new()
    {
        Id = id,
        Message = message,
        Severity = MigrationDiagnosticSeverity.Warning
    };

    private sealed class PdfToolsRewriter : CSharpSyntaxRewriter
    {
        private readonly List<MigrationDiagnostic> _diagnostics = [];

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            if (IsSdkInitializeCall(node))
            {
                _diagnostics.Add(Info("CANMIGPDFTOOLS001",
                    "Pdftools Sdk.Initialize(...) removed; PXA.Pdf does not require SDK initialization."));
                return null;
            }

            return base.VisitGlobalStatement(node);
        }

        private static bool IsSdkInitializeCall(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(static invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText == "Initialize"
                    && access.Expression.ToString() == "Sdk");
        }
    }
}
