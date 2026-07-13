using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.Pdf.Code.Foxit;

public sealed class FoxitPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        var docVar = FindDocumentVariable(root);
        var pageVars = FindPageVariables(root, docVar);
        var graphicsVars = FindGraphicsVariables(root, pageVars);
        var saveTarget = FindSaveTarget(root, docVar);

        var rewriter = new FoxitRewriter(docVar, pageVars, graphicsVars, saveTarget);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        newRoot = RemoveFoxitUsings(newRoot);
        newRoot = EnsurePxaUsing(newRoot);

        var diagnostics = rewriter.Diagnostics.ToList();
        diagnostics.AddRange(ScanForUnsupportedIdentifiers(root));

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(newRoot.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics
        };
    }

    private static string FindDocumentVariable(CompilationUnitSyntax root)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (GetSimpleName(creation.Type) == "PDFDoc")
            {
                var decl = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "document";
    }

    private static HashSet<string> FindPageVariables(CompilationUnitSyntax root, string docVar)
    {
        var pageVars = new HashSet<string>(StringComparer.Ordinal);
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText is "InsertPage" or "CreatePage" or "AddPage" or "NewPage" or "PageCreate" &&
                ma.Expression.ToString() == docVar)
            {
                var decl = inv.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) pageVars.Add(decl.Identifier.ValueText);
            }
        }
        if (pageVars.Count == 0) pageVars.Add("page");
        return pageVars;
    }

    private static HashSet<string> FindGraphicsVariables(CompilationUnitSyntax root, HashSet<string> pageVars)
    {
        var graphicsVars = new HashSet<string>(StringComparer.Ordinal);
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText is "GetGraphics" or "StartGenerateContents" &&
                pageVars.Contains(ma.Expression.ToString()))
            {
                var decl = inv.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) graphicsVars.Add(decl.Identifier.ValueText);
            }
        }
        // Also catch bare `graphics` used without an explicit assignment in scope
        if (graphicsVars.Count == 0) graphicsVars.Add("graphics");
        return graphicsVars;
    }

    private static string? FindSaveTarget(CompilationUnitSyntax root, string docVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText is "Save" or "SaveAs" &&
                ma.Expression.ToString() == docVar)
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
                "Annot", "Field", "Form", "PDFForm", "Signature",
                "SecurityHandler", "Redaction", "OCR", "PDFViewCtrl",
                "Renderer", "Conversion", "Attachment"
            }))
        {
            yield return Warning("CANMIGFOXIT021",
                "Forms, annotations, signing/security, redaction, OCR, rendering, viewer, conversion, or attachment APIs are outside the v1 migration scope.");
        }
    }

    private static CompilationUnitSyntax RemoveFoxitUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static u => !(u.Name?.ToString() ?? "").StartsWith("foxit", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return root.WithUsings(SyntaxFactory.List(filtered));
    }

    private static CompilationUnitSyntax EnsurePxaUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static u => u.Name?.ToString() == "PXA.Pdf"))
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

    private sealed class FoxitRewriter : CSharpSyntaxRewriter
    {
        private readonly string _docVar;
        private readonly HashSet<string> _pageVars;
        private readonly HashSet<string> _graphicsVars;
        private readonly string? _saveTarget;
        private readonly string _pageVar;
        private readonly List<MigrationDiagnostic> _diagnostics = [];

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public FoxitRewriter(
            string docVar, HashSet<string> pageVars,
            HashSet<string> graphicsVars, string? saveTarget)
        {
            _docVar = docVar;
            _pageVars = pageVars;
            _graphicsVars = graphicsVars;
            _saveTarget = saveTarget;
            _pageVar = pageVars.FirstOrDefault() ?? "page";
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
            // `Library.Initialize(...)` → remove
            if (IsLibraryInit(node))
            {
                _diagnostics.Add(Info("CANMIGFOXIT000",
                    "Library.Initialize(...) removed — PXA.Pdf does not require global SDK initialization."));
                return [];
            }

            // `var doc = new PDFDoc()` → `var document = new PdfDocument()`
            if (IsCreationDeclaration(node, "PDFDoc"))
            {
                _diagnostics.Add(Info("CANMIGFOXIT001", "new PDFDoc() → new PdfDocument()"));
                return [MakeGlobal("var document = new PdfDocument();", node)];
            }

            // `var page = doc.InsertPage(...)` / `CreatePage` / `AddPage` / `NewPage` → `var page = document.AddPage()`
            if (IsDeclarationWithCallOn(node, _docVar, "InsertPage") ||
                IsDeclarationWithCallOn(node, _docVar, "CreatePage") ||
                IsDeclarationWithCallOn(node, _docVar, "AddPage") ||
                IsDeclarationWithCallOn(node, _docVar, "NewPage") ||
                IsDeclarationWithCallOn(node, _docVar, "PageCreate"))
            {
                _diagnostics.Add(Info("CANMIGFOXIT002",
                    $"doc.InsertPage/CreatePage/AddPage() → document.AddPage()"));
                return [MakeGlobal($"var {_pageVar} = document.AddPage();", node)];
            }

            // `var graphics = page.GetGraphics()` → remove (draw directly on page)
            if (IsDeclarationWithGraphicsCall(node))
            {
                _diagnostics.Add(Info("CANMIGFOXIT003",
                    "GetGraphics()/StartGenerateContents() removed — PXA draw calls target the PdfPage directly."));
                return [];
            }

            // `page.StartGenerateContents(...)` / `page.GenerateContent()` as expression statements → remove
            if (IsPageMethodCall(node, "StartGenerateContents") ||
                IsPageMethodCall(node, "GenerateContent") ||
                IsPageMethodCall(node, "FinishGenerateContent"))
            {
                _diagnostics.Add(Info("CANMIGFOXIT003",
                    "Foxit content generation lifecycle call removed — not needed in PXA.Pdf."));
                return [];
            }

            // `graphics.DrawText(text, font, x, y)` → `page.DrawTextFromTop(text, x, y, 12)`
            if (TryConvertDrawText(node, out var drawText))
                return [MakeGlobal(drawText!, node)];

            // `graphics.DrawLine(pen, x1, y1, x2, y2)` → `page.DrawLineFromTop(x1, y1, x2, y2)`
            if (TryConvertDrawLine(node, out var drawLine))
                return [MakeGlobal(drawLine!, node)];

            // `graphics.DrawRect(pen, x, y, w, h)` → `page.DrawRectangleFromTop(x, y, w, h)`
            if (TryConvertDrawRect(node, out var drawRect))
                return [MakeGlobal(drawRect!, node)];

            // `graphics.FillRect(brush, x, y, w, h)` → `page.DrawRectangleFromTop(x, y, w, h, 1, true)`
            if (TryConvertFillRect(node, out var fillRect))
                return [MakeGlobal(fillRect!, node)];

            // `graphics.DrawRectangle(pen, x, y, w, h)` → `page.DrawRectangleFromTop(x, y, w, h)`
            if (TryConvertDrawRectangle(node, out var drawRectangle))
                return [MakeGlobal(drawRectangle!, node)];

            // `graphics.DrawImage(...)` → keep with warning
            if (IsGraphicsCall(node, "DrawImage") || IsGraphicsCall(node, "AddImage"))
            {
                _diagnostics.Add(Warning("CANMIGFOXIT005",
                    "DrawImage — image drawing requires manual migration outside v1."));
                return [node];
            }

            // `graphics.DrawPath(...)` → keep with warning
            if (IsGraphicsCall(node, "DrawPath") || IsGraphicsCall(node, "DrawEllipse"))
            {
                _diagnostics.Add(Warning("CANMIGFOXIT006",
                    "DrawPath/DrawEllipse — advanced shape drawing requires manual migration outside v1."));
                return [node];
            }

            // `graphics.ShowText(...)` / `TextOut(...)` / `DrawString(...)` as fallback text methods
            if (TryConvertShowText(node, out var showText))
                return [MakeGlobal(showText!, node)];

            // `doc.Save(path)` / `doc.SaveAs(path)` → `document.Save(path)`
            if (IsMethodCallOn(node, _docVar, "Save") || IsMethodCallOn(node, _docVar, "SaveAs"))
            {
                var path = GetFirstArg(node) ?? _saveTarget ?? "path";
                _diagnostics.Add(Info("CANMIGFOXIT007", $"doc.Save/SaveAs({path}) → document.Save({path})"));
                return [MakeGlobal($"document.Save({path});", node)];
            }

            // Unsupported existing-PDF editing/forms/security → keep with warning
            if (ContainsCallAnywhere(node, "Load") ||
                ContainsCallAnywhere(node, "LoadFromFile") ||
                ContainsCallAnywhere(node, "ImportFromFile") ||
                ContainsCallAnywhere(node, "DeletePage") ||
                ContainsCallAnywhere(node, "MovePage") ||
                ContainsCallAnywhere(node, "Sign") ||
                ContainsCallAnywhere(node, "SetSecurity") ||
                ContainsCallAnywhere(node, "Encrypt") ||
                ContainsCallAnywhere(node, "Decrypt") ||
                ContainsCallAnywhere(node, "GetForm") ||
                ContainsCallAnywhere(node, "GetAnnot"))
            {
                _diagnostics.Add(Warning("CANMIGFOXIT020",
                    "Existing-PDF editing, forms, annotations, signing, or security APIs require manual migration outside v1."));
                return [node];
            }

            // OCR/conversion/redaction → keep with warning
            if (ContainsCallAnywhere(node, "StartOCR") ||
                ContainsCallAnywhere(node, "RenderPageToBitmap") ||
                ContainsCallAnywhere(node, "ToPdf") ||
                ContainsCallAnywhere(node, "Redact") ||
                ContainsCallAnywhere(node, "AddAnnot"))
            {
                _diagnostics.Add(Warning("CANMIGFOXIT021",
                    "OCR, rendering, conversion, redaction, or annotation APIs require manual migration outside v1."));
                return [node];
            }

            return [node];
        }

        // --- Draw call converters ------------------------------------------------

        private bool TryConvertDrawText(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "DrawText", out var inv)) return false;
            var args = inv!.ArgumentList.Arguments;
            // (text, font, x, y) — 4 args
            // (text, x, y) — 3 args (no font)
            string text, x, y;
            if (args.Count >= 4)
            {
                text = args[0].Expression.ToString();
                x = args[2].Expression.ToString();
                y = args[3].Expression.ToString();
            }
            else if (args.Count == 3)
            {
                text = args[0].Expression.ToString();
                x = args[1].Expression.ToString();
                y = args[2].Expression.ToString();
            }
            else return false;

            _diagnostics.Add(Info("CANMIGFOXIT004",
                $"DrawText({text}) → {_pageVar}.DrawTextFromTop(...)"));
            call = $"{_pageVar}.DrawTextFromTop({text}, {x}, {y}, 12);";
            return true;
        }

        private bool TryConvertShowText(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            string? method = null;
            InvocationExpressionSyntax? inv = null;
            foreach (var m in new[] { "ShowText", "TextOut", "DrawString" })
            {
                if (TryMatchGraphicsCall(node, m, out inv)) { method = m; break; }
            }
            if (method is null) return false;

            var args = inv!.ArgumentList.Arguments;
            if (args.Count < 3) return false;
            var text = args[0].Expression.ToString();
            var x = args[args.Count - 2].Expression.ToString();
            var y = args[args.Count - 1].Expression.ToString();

            _diagnostics.Add(Info("CANMIGFOXIT004",
                $"{method}({text}) → {_pageVar}.DrawTextFromTop(...)"));
            call = $"{_pageVar}.DrawTextFromTop({text}, {x}, {y}, 12);";
            return true;
        }

        private bool TryConvertDrawLine(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "DrawLine", out var inv)) return false;
            var args = inv!.ArgumentList.Arguments;
            // (pen, x1, y1, x2, y2) — 5 args
            if (args.Count < 5) return false;
            var x1 = args[1].Expression.ToString();
            var y1 = args[2].Expression.ToString();
            var x2 = args[3].Expression.ToString();
            var y2 = args[4].Expression.ToString();
            _diagnostics.Add(Info("CANMIGFOXIT006",
                $"DrawLine → {_pageVar}.DrawLineFromTop(...)"));
            call = $"{_pageVar}.DrawLineFromTop({x1}, {y1}, {x2}, {y2});";
            return true;
        }

        private bool TryConvertDrawRect(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "DrawRect", out var inv)) return false;
            var args = inv!.ArgumentList.Arguments;
            // (pen, x, y, w, h) — 5 args
            if (args.Count < 5) return false;
            var x = args[1].Expression.ToString();
            var y = args[2].Expression.ToString();
            var w = args[3].Expression.ToString();
            var h = args[4].Expression.ToString();
            _diagnostics.Add(Info("CANMIGFOXIT006",
                $"DrawRect → {_pageVar}.DrawRectangleFromTop(...)"));
            call = $"{_pageVar}.DrawRectangleFromTop({x}, {y}, {w}, {h});";
            return true;
        }

        private bool TryConvertDrawRectangle(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "DrawRectangle", out var inv)) return false;
            var args = inv!.ArgumentList.Arguments;
            if (args.Count < 5) return false;
            var x = args[1].Expression.ToString();
            var y = args[2].Expression.ToString();
            var w = args[3].Expression.ToString();
            var h = args[4].Expression.ToString();
            _diagnostics.Add(Info("CANMIGFOXIT006",
                $"DrawRectangle → {_pageVar}.DrawRectangleFromTop(...)"));
            call = $"{_pageVar}.DrawRectangleFromTop({x}, {y}, {w}, {h});";
            return true;
        }

        private bool TryConvertFillRect(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "FillRect", out var inv)) return false;
            var args = inv!.ArgumentList.Arguments;
            // (brush, x, y, w, h) — 5 args
            if (args.Count < 5) return false;
            var x = args[1].Expression.ToString();
            var y = args[2].Expression.ToString();
            var w = args[3].Expression.ToString();
            var h = args[4].Expression.ToString();
            _diagnostics.Add(Info("CANMIGFOXIT006",
                $"FillRect → {_pageVar}.DrawRectangleFromTop(..., fill: true)"));
            call = $"{_pageVar}.DrawRectangleFromTop({x}, {y}, {w}, {h}, 1, true);";
            return true;
        }

        // --- Pattern helpers ----------------------------------------------------

        private bool TryMatchGraphicsCall(
            GlobalStatementSyntax node,
            string method,
            out InvocationExpressionSyntax? inv)
        {
            inv = null;
            var expr = ExtractExpression(node);
            if (expr is not InvocationExpressionSyntax invExpr) return false;
            if (invExpr.Expression is not MemberAccessExpressionSyntax ma) return false;
            if (ma.Name.Identifier.ValueText != method) return false;
            if (ma.Expression is not IdentifierNameSyntax targetId) return false;
            if (!_graphicsVars.Contains(targetId.Identifier.ValueText)) return false;
            inv = invExpr;
            return true;
        }

        private bool IsGraphicsCall(GlobalStatementSyntax node, string method)
            => TryMatchGraphicsCall(node, method, out _);

        private static bool IsLibraryInit(GlobalStatementSyntax node)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == "Initialize" &&
                   ma.Expression.ToString().EndsWith("Library", StringComparison.Ordinal);
        }

        private static bool IsCreationDeclaration(GlobalStatementSyntax node, string typeName)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
            return init is ObjectCreationExpressionSyntax creation &&
                   GetSimpleName(creation.Type) == typeName;
        }

        private static bool IsDeclarationWithCallOn(
            GlobalStatementSyntax node, string variable, string method)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
            return init is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method &&
                   ma.Expression.ToString() == variable;
        }

        private bool IsDeclarationWithGraphicsCall(GlobalStatementSyntax node)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
            if (init is not InvocationExpressionSyntax inv) return false;
            if (inv.Expression is not MemberAccessExpressionSyntax ma) return false;
            return ma.Name.Identifier.ValueText is "GetGraphics" or "StartGenerateContents" &&
                   _pageVars.Contains(ma.Expression.ToString());
        }

        private bool IsPageMethodCall(GlobalStatementSyntax node, string method)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method &&
                   _pageVars.Contains(ma.Expression.ToString());
        }

        private bool IsMethodCallOn(GlobalStatementSyntax node, string variable, string method)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method &&
                   ma.Expression.ToString() == variable;
        }

        private static bool IsAnyMethodCall(GlobalStatementSyntax node, string method)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method;
        }

        // Matches both expression statements AND declaration-with-call forms
        private static bool ContainsCallAnywhere(GlobalStatementSyntax node, string method)
        {
            if (IsAnyMethodCall(node, method)) return true;
            if (node.Statement is LocalDeclarationStatementSyntax local)
            {
                var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
                if (init is InvocationExpressionSyntax inv &&
                    inv.Expression is MemberAccessExpressionSyntax ma &&
                    ma.Name.Identifier.ValueText == method)
                    return true;
            }
            return false;
        }

        private static ExpressionSyntax? ExtractExpression(GlobalStatementSyntax node) =>
            node.Statement is ExpressionStatementSyntax es ? es.Expression : null;

        private static string? GetFirstArg(GlobalStatementSyntax node)
        {
            var expr = ExtractExpression(node);
            if (expr is not InvocationExpressionSyntax inv) return null;
            return inv.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString();
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
