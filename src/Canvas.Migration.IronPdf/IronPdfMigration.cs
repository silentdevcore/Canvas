using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.IronPdf;

public sealed class IronPdfMigration : CSharpSourceMigration
{
    private static readonly string[] RenderMethods =
    [
        "RenderHtmlAsPdf", "RenderHtmlFileAsPdf", "RenderUrlAsPdf",
        "RenderRazorToPdf", "RenderRazorViewToPdf"
    ];

    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        var rendererVar = FindRendererVariable(root);
        var pdfVar = FindPdfVariable(root, rendererVar);
        var saveTarget = FindSaveTarget(root, pdfVar);

        var rewriter = new IronPdfRewriter(rendererVar, pdfVar, saveTarget);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        newRoot = RemoveIronPdfUsings(newRoot);
        newRoot = EnsureCanvasUsing(newRoot);

        var diagnostics = rewriter.Diagnostics.ToList();
        diagnostics.AddRange(ScanForUnsupportedIdentifiers(root));

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(newRoot.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics
        };
    }

    private static string FindRendererVariable(CompilationUnitSyntax root)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (GetSimpleName(creation.Type) is "ChromePdfRenderer" or "HtmlToPdf")
            {
                var decl = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "renderer";
    }

    private static string FindPdfVariable(CompilationUnitSyntax root, string rendererVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                RenderMethods.Contains(ma.Name.Identifier.ValueText) &&
                (ma.Expression.ToString() == rendererVar ||
                 ma.Expression is ObjectCreationExpressionSyntax chainedCreation &&
                 GetSimpleName(chainedCreation.Type) is "ChromePdfRenderer" or "HtmlToPdf"))
            {
                var decl = inv.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "pdf";
    }

    private static string? FindSaveTarget(CompilationUnitSyntax root, string pdfVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText is "SaveAs" or "SaveAsAsync" &&
                ma.Expression.ToString() == pdfVar)
            {
                var args = inv.ArgumentList.Arguments;
                if (args.Count >= 1) return args[0].Expression.ToString();
            }
        }
        return null;
    }

    private static IEnumerable<MigrationDiagnostic> ScanForUnsupportedIdentifiers(CompilationUnitSyntax root)
    {
        var names = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(static id => id.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        if (names.Overlaps(new[]
            {
                "PdfMerger", "Merge", "AppendPdf", "CopyPages",
                "PdfSignature", "SecuritySettings", "ExtractAllText"
            }))
        {
            yield return Warning("CANMIGIRONPDF020",
                "PDF editing, merge, text extraction, security, or signing APIs require manual migration outside v1.");
        }
    }

    private static CompilationUnitSyntax RemoveIronPdfUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static u => !(u.Name?.ToString() ?? "").StartsWith("IronPdf", StringComparison.Ordinal))
            .ToArray();
        return root.WithUsings(SyntaxFactory.List(filtered));
    }

    private static CompilationUnitSyntax EnsureCanvasUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static u => u.Name?.ToString() == "Canvas.Pdf"))
            return root;
        var canvasUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Canvas.Pdf"))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        return root.WithUsings(root.Usings.Insert(0, canvasUsing));
    }

    private static string GetSimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
        _ => type.ToString()
    };

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static MigrationDiagnostic Info(string id, string message) => new()
    {
        Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info
    };

    private static MigrationDiagnostic Warning(string id, string message) => new()
    {
        Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning
    };

    // --- Rewriter ---------------------------------------------------------------

    private sealed class IronPdfRewriter : CSharpSyntaxRewriter
    {
        private readonly string _rendererVar;
        private readonly string _pdfVar;
        private readonly string? _saveTarget;
        private readonly List<MigrationDiagnostic> _diagnostics = [];
        private bool _scaffoldEmitted;

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public IronPdfRewriter(string rendererVar, string pdfVar, string? saveTarget)
        {
            _rendererVar = rendererVar;
            _pdfVar = pdfVar;
            _saveTarget = saveTarget;
        }

        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var newMembers = new List<MemberDeclarationSyntax>();
            foreach (var member in node.Members)
            {
                if (member is not GlobalStatementSyntax gs)
                {
                    newMembers.Add(member);
                    continue;
                }
                newMembers.AddRange(TransformGlobal(gs));
            }
            return node.WithMembers(SyntaxFactory.List(newMembers));
        }

        private IEnumerable<MemberDeclarationSyntax> TransformGlobal(GlobalStatementSyntax node)
        {
            // `var renderer = new ChromePdfRenderer()` or `new HtmlToPdf()` →
            //   var document = new PdfDocument();
            //   var page = document.AddPage();
            if (IsRendererCreation(node))
            {
                _diagnostics.Add(Info("CANMIGIRONPDF001",
                    "ChromePdfRenderer/HtmlToPdf → PdfDocument + AddPage. HTML rendering requires manual Canvas draw call migration."));
                _scaffoldEmitted = true;
                return
                [
                    MakeGlobal("var document = new PdfDocument();", node),
                    MakeGlobal("var page = document.AddPage();", node)
                ];
            }

            // `var pdf = renderer.RenderHtmlAsPdf(html)` or chained `new ChromePdfRenderer().RenderHtmlAsPdf(...)`
            if (TryGetRenderCall(node, "RenderHtmlAsPdf", out var htmlArg))
            {
                var truncated = htmlArg != null ? Truncate(htmlArg) : "html";
                _diagnostics.Add(Warning("CANMIGIRONPDF002",
                    $"RenderHtmlAsPdf({truncated}) — HTML rendering requires manual Canvas draw call migration. Add draw calls after document.AddPage()."));
                // Emit scaffold here if the renderer was created inline (chained call, no prior creation statement)
                if (!_scaffoldEmitted)
                {
                    _scaffoldEmitted = true;
                    _diagnostics.Add(Info("CANMIGIRONPDF001",
                        "Inline ChromePdfRenderer → PdfDocument + AddPage scaffold generated."));
                    return
                    [
                        MakeGlobal("var document = new PdfDocument();", node),
                        MakeGlobal("var page = document.AddPage();", node)
                    ];
                }
                return [];
            }

            if (TryGetRenderCall(node, "RenderHtmlFileAsPdf", out var fileArg))
            {
                _diagnostics.Add(Warning("CANMIGIRONPDF003",
                    $"RenderHtmlFileAsPdf({fileArg ?? "..."}) — HTML file rendering requires manual Canvas migration. Review the HTML template and replace with Canvas draw calls."));
                return [];
            }

            if (TryGetRenderCall(node, "RenderUrlAsPdf", out var urlArg))
            {
                _diagnostics.Add(Warning("CANMIGIRONPDF004",
                    $"RenderUrlAsPdf({urlArg ?? "..."}) — URL-based rendering is outside Canvas.Pdf scope. Recreate the page content with Canvas draw calls."));
                return [];
            }

            if (TryGetRenderCall(node, "RenderRazorToPdf", out _) ||
                TryGetRenderCall(node, "RenderRazorViewToPdf", out _))
            {
                _diagnostics.Add(Warning("CANMIGIRONPDF005",
                    "Razor-to-PDF rendering requires manual view/model migration. Review Razor template and replace with Canvas draw calls."));
                return [];
            }

            // `pdf.SaveAs(path)` or `await pdf.SaveAsAsync(path)` → `document.Save(path)`
            if (IsMethodCallOn(node, _pdfVar, "SaveAs", out var savePath))
            {
                var path = savePath ?? _saveTarget ?? "path";
                _diagnostics.Add(Info("CANMIGIRONPDF006", $"SaveAs({path}) → document.Save({path})"));
                return [MakeGlobal($"document.Save({path});", node)];
            }

            if (IsMethodCallOn(node, _pdfVar, "SaveAsAsync", out var asyncSavePath))
            {
                var path = asyncSavePath ?? _saveTarget ?? "path";
                _diagnostics.Add(Info("CANMIGIRONPDF007",
                    $"SaveAsAsync({path}) → document.Save({path}) — Canvas.Pdf uses synchronous Save in v1."));
                return [MakeGlobal($"document.Save({path});", node)];
            }

            // Editing/security APIs — keep with warning
            if (IsMethodCallOn(node, _pdfVar, "Merge", out _) ||
                IsMethodCallOn(node, _pdfVar, "AppendPdf", out _) ||
                IsMethodCallOn(node, _pdfVar, "CopyPages", out _) ||
                IsMethodCallOn(node, _pdfVar, "SignWithDigitalSignature", out _) ||
                IsMethodCallOn(node, _pdfVar, "SignPdfWithDigitalSignature", out _))
            {
                _diagnostics.Add(Warning("CANMIGIRONPDF020",
                    "PDF editing, merge, or signing APIs require manual migration outside v1."));
                return [node];
            }

            // Renderer option assignments (`renderer.RenderingOptions.X = ...`) → remove silently
            // (the renderer is gone; keeping these would cause compile errors)
            if (IsPropertySetOnVar(node, _rendererVar))
                return [];

            return [node];
        }

        private static bool IsRendererCreation(GlobalStatementSyntax node)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
            return init is ObjectCreationExpressionSyntax creation &&
                   GetSimpleName(creation.Type) is "ChromePdfRenderer" or "HtmlToPdf";
        }

        private bool TryGetRenderCall(
            GlobalStatementSyntax node,
            string method,
            out string? firstArg)
        {
            firstArg = null;

            // `var pdf = renderer.RenderXxx(...)` — declaration form
            if (node.Statement is LocalDeclarationStatementSyntax local)
            {
                var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
                if (init is InvocationExpressionSyntax inv &&
                    inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name.Identifier.ValueText == method &&
                    (ma.Expression.ToString() == _rendererVar ||
                     ma.Expression is ObjectCreationExpressionSyntax chainCreation &&
                     GetSimpleName(chainCreation.Type) is "ChromePdfRenderer" or "HtmlToPdf"))
                {
                    firstArg = inv.ArgumentList.Arguments.Count > 0
                        ? inv.ArgumentList.Arguments[0].Expression.ToString()
                        : null;
                    return true;
                }
                return false;
            }

            // Expression-statement form: `renderer.RenderXxx(...)` without assignment
            var expr = ExtractInnerExpression(node);
            if (expr is InvocationExpressionSyntax exprInv &&
                exprInv.Expression is MemberAccessExpressionSyntax exprMa &&
                exprMa.Name.Identifier.ValueText == method &&
                exprMa.Expression.ToString() == _rendererVar)
            {
                firstArg = exprInv.ArgumentList.Arguments.Count > 0
                    ? exprInv.ArgumentList.Arguments[0].Expression.ToString()
                    : null;
                return true;
            }

            return false;
        }

        private static bool IsMethodCallOn(
            GlobalStatementSyntax node,
            string variable,
            string method,
            out string? firstArg)
        {
            firstArg = null;
            var expr = ExtractInnerExpression(node);
            if (expr is not InvocationExpressionSyntax inv) return false;
            if (inv.Expression is not MemberAccessExpressionSyntax ma) return false;
            if (ma.Name.Identifier.ValueText != method) return false;
            if (ma.Expression.ToString() != variable) return false;
            firstArg = inv.ArgumentList.Arguments.Count > 0
                ? inv.ArgumentList.Arguments[0].Expression.ToString()
                : null;
            return true;
        }

        private static bool IsPropertySetOnVar(GlobalStatementSyntax node, string variable)
        {
            if (node.Statement is not ExpressionStatementSyntax es) return false;
            if (es.Expression is not AssignmentExpressionSyntax assign) return false;
            // Match `variable.SomeProp = ...` or `variable.SomeProp.SubProp = ...`
            var lhs = assign.Left;
            while (lhs is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Expression is IdentifierNameSyntax id &&
                    id.Identifier.ValueText == variable)
                    return true;
                lhs = memberAccess.Expression;
            }
            return false;
        }

        // Unwraps `await expr` → `expr` for async call matching
        private static ExpressionSyntax? ExtractInnerExpression(GlobalStatementSyntax node)
        {
            if (node.Statement is not ExpressionStatementSyntax es) return null;
            return es.Expression is AwaitExpressionSyntax awaitExpr ? awaitExpr.Expression : es.Expression;
        }

        private static GlobalStatementSyntax MakeGlobal(string code, GlobalStatementSyntax original)
        {
            var stmt = SyntaxFactory.ParseStatement(code + "\n");
            return SyntaxFactory.GlobalStatement(stmt)
                .WithLeadingTrivia(original.GetLeadingTrivia())
                .WithTrailingTrivia(original.GetTrailingTrivia());
        }

        private static string GetSimpleName(TypeSyntax type) => type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
            _ => type.ToString()
        };

        private static string Truncate(string value)
        {
            const int max = 80;
            var normalized = value.Replace("\"", "", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);
            return normalized.Length <= max ? normalized : normalized[..max] + "...";
        }

        private static MigrationDiagnostic Info(string id, string message) => new()
        {
            Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info
        };

        private static MigrationDiagnostic Warning(string id, string message) => new()
        {
            Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning
        };
    }
}
