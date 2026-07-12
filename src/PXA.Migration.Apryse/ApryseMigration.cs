using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.Apryse;

public sealed class ApryseMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        // Pre-scan: find which variable holds the PDFDoc and which variable holds the page
        var docVar = FindDocVariable(root);
        var pageVar = FindPageVariable(root);
        var saveTarget = FindSaveTarget(root, docVar);

        var rewriter = new ApryseRewriter(docVar, pageVar, saveTarget);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        newRoot = RemoveAprysedUsings(newRoot);
        newRoot = EnsureCanvasUsing(newRoot);

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(newRoot.NormalizeWhitespace().ToFullString()),
            Diagnostics = rewriter.Diagnostics
        };
    }

    // --- Helpers ---------------------------------------------------------------

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    // Returns the local variable name declared as `new PDFDoc()`, or "doc" as fallback.
    private static string FindDocVariable(CompilationUnitSyntax root)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (GetSimpleName(creation.Type) == "PDFDoc")
            {
                var decl = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "doc";
    }

    // Returns the variable declared by PageCreate(), or "page" as fallback.
    private static string FindPageVariable(CompilationUnitSyntax root)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (GetMethodName(inv) == "PageCreate")
            {
                var decl = inv.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "page";
    }

    // Returns the first argument of doc.Save(...), or null if not found.
    private static string? FindSaveTarget(CompilationUnitSyntax root, string docVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (GetMethodName(inv) != "Save") continue;
            if (inv.Expression is not MemberAccessExpressionSyntax ma) continue;
            if (ma.Expression.ToString() != docVar) continue;
            var args = inv.ArgumentList.Arguments;
            if (args.Count >= 1)
                return args[0].Expression.ToString();
        }
        return null;
    }

    private static CompilationUnitSyntax RemoveAprysedUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(u => !(u.Name?.ToString() ?? "").StartsWith("pdftron", StringComparison.Ordinal))
            .ToArray();
        return root.WithUsings(SyntaxFactory.List(filtered));
    }

    private static CompilationUnitSyntax EnsureCanvasUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(u => u.Name?.ToString() == "PXA.Pdf"))
            return root;
        var canvasUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("PXA.Pdf"))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        return root.WithUsings(root.Usings.Insert(0, canvasUsing));
    }

    private static string GetSimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
        _ => type.ToString()
    };

    private static string? GetMethodName(InvocationExpressionSyntax inv) =>
        inv.Expression is MemberAccessExpressionSyntax ma ? ma.Name.Identifier.ValueText : null;

    // --- Rewriter --------------------------------------------------------------

    private sealed class ApryseRewriter : CSharpSyntaxRewriter
    {
        private readonly string _docVar;
        private readonly string _pageVar;
        private readonly string? _saveTarget;
        private readonly List<MigrationDiagnostic> _diagnostics = [];

        // After we encounter PageCreate we emit AddPage on the next PagePushBack
        private bool _pageCreateRemoved;

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public ApryseRewriter(string docVar, string pageVar, string? saveTarget)
        {
            _docVar  = docVar;
            _pageVar = pageVar;
            _saveTarget = saveTarget;
        }

        // ---- statement-level visit -------------------------------------------

        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            // PDFNet.Initialize(...)  → remove
            if (IsInvocationOf(node, "Initialize"))
            {
                _diagnostics.Add(Info("CANMIGAPRYSE000", "PDFNet.Initialize removed — PXA.Pdf requires no global initialisation."));
                return null;
            }

            // var doc = new PDFDoc() / using var doc = new PDFDoc()  → var document = new PdfDocument()
            if (TryGetPdfDocDeclaration(node, out var docDeclNode))
            {
                _diagnostics.Add(Info("CANMIGAPRYSE001", "new PDFDoc() → new PdfDocument()"));
                return docDeclNode;
            }

            // var page = doc.PageCreate()  → remove (we emit AddPage on PagePushBack)
            if (IsMethodCall(node, "PageCreate") || IsDeclarationWithCall(node, "PageCreate"))
            {
                _pageCreateRemoved = true;
                _diagnostics.Add(Info("CANMIGAPRYSE002", "PageCreate() removed — AddPage() creates and attaches the page in one step."));
                return null;
            }

            // doc.PagePushBack(page)  → var <argName> = document.AddPage()
            if (IsMethodCall(node, "PagePushBack"))
            {
                var varName = GetFirstArgName(node) ?? _pageVar;
                _diagnostics.Add(Info("CANMIGAPRYSE003", "PagePushBack() → document.AddPage()"));
                var addPageStmt = SyntaxFactory.ParseStatement($"var {varName} = document.AddPage();\n");
                return SyntaxFactory.GlobalStatement(addPageStmt)
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }

            // doc.Save(path, flags)  → document.Save(path)
            if (IsMethodCallOn(node, _docVar, "Save"))
            {
                _diagnostics.Add(Info("CANMIGAPRYSE004", $"doc.Save(...) → document.Save({_saveTarget ?? "path"}) — extra Apryse save flags removed."));
                var saveStmt = SyntaxFactory.ParseStatement($"document.Save({_saveTarget ?? "path"});\n");
                return SyntaxFactory.GlobalStatement(saveStmt)
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }

            return base.VisitGlobalStatement(node);
        }

        // ---- helpers ---------------------------------------------------------

        private static bool IsInvocationOf(GlobalStatementSyntax node, string method)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method;
        }

        private static bool IsMethodCall(GlobalStatementSyntax node, string method)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method;
        }

        private static bool IsMethodCallOn(GlobalStatementSyntax node, string variable, string method)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method &&
                   ma.Expression.ToString() == variable;
        }

        private static ExpressionSyntax? ExtractExpression(GlobalStatementSyntax node) =>
            node.Statement is ExpressionStatementSyntax es ? es.Expression : null;

        // Returns the identifier name of the first argument of a method call in a global statement.
        private static string? GetFirstArgName(GlobalStatementSyntax node)
        {
            var expr = ExtractExpression(node);
            if (expr is not InvocationExpressionSyntax inv) return null;
            var first = inv.ArgumentList.Arguments.FirstOrDefault();
            return first?.Expression is IdentifierNameSyntax id ? id.Identifier.ValueText : null;
        }

        // Matches `var x = someObj.Method(...)` — declaration where initializer is a method call
        private static bool IsDeclarationWithCall(GlobalStatementSyntax node, string method)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
            return init is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method;
        }

        private bool TryGetPdfDocDeclaration(GlobalStatementSyntax node, out GlobalStatementSyntax? result)
        {
            result = null;
            var stmt = node.Statement;

            // Both `var doc = new PDFDoc()` and `using var doc = new PDFDoc()`
            VariableDeclarationSyntax? varDecl = stmt switch
            {
                LocalDeclarationStatementSyntax local => local.Declaration,
                _ => null
            };

            if (varDecl == null) return false;

            var firstVar = varDecl.Variables.FirstOrDefault();
            if (firstVar?.Initializer?.Value is not ObjectCreationExpressionSyntax creation) return false;
            if (GetSimpleName(creation.Type) != "PDFDoc") return false;

            var newStmt = SyntaxFactory.ParseStatement("var document = new PdfDocument();\n");
            result = SyntaxFactory.GlobalStatement(newStmt)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
            return true;
        }

        private static MigrationDiagnostic Info(string id, string message) => new()
        {
            Id = id,
            Message = message,
            Severity = MigrationDiagnosticSeverity.Info
        };
    }
}
