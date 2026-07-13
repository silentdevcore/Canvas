using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.Pdf.Code.DsPdf;

public sealed class DsPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        var docVar = FindDocumentVariable(root);
        var pageVars = FindPageVariables(root, docVar);
        var saveTarget = FindSaveTarget(root, docVar);

        var rewriter = new DsPdfRewriter(docVar, pageVars, saveTarget);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        newRoot = RemoveDsUsings(newRoot);
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
            if (GetSimpleName(creation.Type) == "GcPdfDocument")
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
                (ma.Name.Identifier.ValueText is "NewPage" or "AddPage") &&
                ma.Expression.ToString() == docVar)
            {
                var decl = inv.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) pageVars.Add(decl.Identifier.ValueText);
            }
        }
        if (pageVars.Count == 0) pageVars.Add("page");
        return pageVars;
    }

    private static string? FindSaveTarget(CompilationUnitSyntax root, string docVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText == "Save" &&
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
                "AcroForm", "SignatureProperties", "Security", "PdfA",
                "TableRenderer", "LayoutHost", "TextLayout",
                "Annotation", "LinkAnnotation", "FileAttachmentAnnotation"
            }))
        {
            yield return Warning("CANMIGDSPDF023",
                "Forms, annotations, advanced layout, PDF/A, signatures, security, or redaction APIs are outside the v1 migration scope.");
        }
    }

    private static CompilationUnitSyntax RemoveDsUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static u =>
            {
                var name = u.Name?.ToString() ?? "";
                return !name.StartsWith("DS.Documents", StringComparison.Ordinal)
                    && !name.StartsWith("GrapeCity.Documents", StringComparison.Ordinal);
            })
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

    private sealed class DsPdfRewriter : CSharpSyntaxRewriter
    {
        private readonly string _docVar;
        private readonly HashSet<string> _pageVars;
        private readonly string? _saveTarget;
        private readonly string _pageVar = "page";
        private readonly List<MigrationDiagnostic> _diagnostics = [];

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public DsPdfRewriter(string docVar, HashSet<string> pageVars, string? saveTarget)
        {
            _docVar = docVar;
            _pageVars = pageVars;
            _saveTarget = saveTarget;
            // Use the first found page var as the output page variable name
            if (pageVars.Count > 0) _pageVar = pageVars.First();
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
            // `var doc = new GcPdfDocument()` → `var document = new PdfDocument()`
            if (IsCreationDeclaration(node, "GcPdfDocument"))
            {
                _diagnostics.Add(Info("CANMIGDSPDF001", "new GcPdfDocument() → new PdfDocument()"));
                return [MakeGlobal("var document = new PdfDocument();", node)];
            }

            // `var page = doc.NewPage()` or `doc.AddPage()` → `var page = document.AddPage()`
            if (IsDeclarationWithCall(node, "NewPage") || IsDeclarationWithCall(node, "AddPage"))
            {
                _diagnostics.Add(Info("CANMIGDSPDF002", $"doc.NewPage()/AddPage() → document.AddPage()"));
                return [MakeGlobal($"var {_pageVar} = document.AddPage();", node)];
            }

            // `doc.Save(path)` → `document.Save(path)`
            if (IsMethodCallOn(node, _docVar, "Save"))
            {
                var path = GetFirstArg(node) ?? _saveTarget ?? "path";
                _diagnostics.Add(Info("CANMIGDSPDF007", $"doc.Save({path}) → document.Save({path})"));
                return [MakeGlobal($"document.Save({path});", node)];
            }

            // `page.Graphics.DrawString(...)` → `page.DrawTextFromTop(...)`
            if (TryConvertDrawString(node, out var drawString))
                return [MakeGlobal(drawString!, node)];

            // `page.Graphics.DrawLine(...)` → `page.DrawLineFromTop(...)`
            if (TryConvertDrawLine(node, out var drawLine))
                return [MakeGlobal(drawLine!, node)];

            // `page.Graphics.DrawRectangle(...)` → `page.DrawRectangleFromTop(...)`
            if (TryConvertDrawRectangle(node, out var drawRect))
                return [MakeGlobal(drawRect!, node)];

            // `page.Graphics.FillRectangle(...)` → `page.DrawRectangleFromTop(..., fill: true)`
            if (TryConvertFillRectangle(node, out var fillRect))
                return [MakeGlobal(fillRect!, node)];

            // Unsupported graphics calls — keep with warning
            if (IsGraphicsCallOnPageVar(node, "DrawImage"))
            {
                _diagnostics.Add(Warning("CANMIGDSPDF005",
                    "DrawImage — image drawing requires manual migration outside v1."));
                return [node];
            }

            if (IsGraphicsCallOnPageVar(node, "DrawEllipse") ||
                IsGraphicsCallOnPageVar(node, "DrawPolygon") ||
                IsGraphicsCallOnPageVar(node, "DrawPath"))
            {
                _diagnostics.Add(Warning("CANMIGDSPDF006",
                    "DrawEllipse/DrawPolygon/DrawPath — advanced shape drawing requires manual migration outside v1."));
                return [node];
            }

            // Existing-PDF editing — keep with warning
            if (IsMethodCallOn(node, _docVar, "Load") ||
                IsMethodCallOn(node, _docVar, "LoadFromFile") ||
                IsMethodCallOn(node, _docVar, "DeletePage") ||
                IsMethodCallOn(node, _docVar, "MovePage") ||
                IsMethodCallOn(node, _docVar, "ClonePage") ||
                IsMethodCallOn(node, _docVar, "MergeWithDocument") ||
                IsMethodCallOn(node, _docVar, "ImportPage"))
            {
                _diagnostics.Add(Warning("CANMIGDSPDF021",
                    "Existing-PDF editing and page import/merge APIs require manual migration outside v1."));
                return [node];
            }

            // Security/compliance — keep with warning
            if (IsMethodCallOn(node, _docVar, "Sign") ||
                IsMethodCallOn(node, _docVar, "Encrypt") ||
                IsMethodCallOn(node, _docVar, "SetPermissions") ||
                IsMethodCallOn(node, _docVar, "SetSecurity") ||
                IsMethodCallOn(node, _docVar, "SaveAsPdfA") ||
                IsMethodCallOn(node, _docVar, "ConvertToPdfA") ||
                IsMethodCallOn(node, _docVar, "Redact") ||
                IsMethodCallOn(node, _docVar, "ApplyRedactions"))
            {
                _diagnostics.Add(Warning("CANMIGDSPDF022",
                    "Compliance, security, signature, or redaction APIs require manual migration outside v1."));
                return [node];
            }

            return [node];
        }

        private bool TryConvertDrawString(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "DrawString", out var inv, out _)) return false;

            var args = inv!.ArgumentList.Arguments;
            if (args.Count < 3) return false;

            var text = args[0].Expression.ToString();
            var fontSize = TryExtractTextFormatFontSize(args[1].Expression) ?? "12";

            if (!TryExtractPointF(args[2].Expression, out var x, out var y) &&
                !TryExtractRectangleFOrigin(args[2].Expression, out x, out y))
                return false;

            _diagnostics.Add(Info("CANMIGDSPDF003",
                $"DrawString({text}) → {_pageVar}.DrawTextFromTop(...)"));
            call = $"{_pageVar}.DrawTextFromTop({text}, {x}, {y}, {fontSize});";
            return true;
        }

        private bool TryConvertDrawLine(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "DrawLine", out var inv, out _)) return false;

            var args = inv!.ArgumentList.Arguments;
            string x1, y1, x2, y2;

            if (args.Count == 5)
            {
                // (pen, x1, y1, x2, y2)
                x1 = args[1].Expression.ToString();
                y1 = args[2].Expression.ToString();
                x2 = args[3].Expression.ToString();
                y2 = args[4].Expression.ToString();
            }
            else if (args.Count == 3 &&
                     TryExtractPointF(args[1].Expression, out x1, out y1) &&
                     TryExtractPointF(args[2].Expression, out x2, out y2))
            {
                // (pen, new PointF(x1,y1), new PointF(x2,y2))
            }
            else return false;

            _diagnostics.Add(Info("CANMIGDSPDF006", $"DrawLine → {_pageVar}.DrawLineFromTop(...)"));
            call = $"{_pageVar}.DrawLineFromTop({x1}, {y1}, {x2}, {y2});";
            return true;
        }

        private bool TryConvertDrawRectangle(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "DrawRectangle", out var inv, out _)) return false;

            var args = inv!.ArgumentList.Arguments;
            string x, y, w, h;

            if (args.Count == 2 && TryExtractRectangleF(args[1].Expression, out x, out y, out w, out h))
            {
                // (pen, new RectangleF(x, y, w, h))
            }
            else if (args.Count == 5)
            {
                // (pen, x, y, w, h)
                x = args[1].Expression.ToString();
                y = args[2].Expression.ToString();
                w = args[3].Expression.ToString();
                h = args[4].Expression.ToString();
            }
            else return false;

            _diagnostics.Add(Info("CANMIGDSPDF006", $"DrawRectangle → {_pageVar}.DrawRectangleFromTop(...)"));
            call = $"{_pageVar}.DrawRectangleFromTop({x}, {y}, {w}, {h});";
            return true;
        }

        private bool TryConvertFillRectangle(GlobalStatementSyntax node, out string? call)
        {
            call = null;
            if (!TryMatchGraphicsCall(node, "FillRectangle", out var inv, out _)) return false;

            var args = inv!.ArgumentList.Arguments;
            string x, y, w, h;

            if (args.Count == 2 && TryExtractRectangleF(args[1].Expression, out x, out y, out w, out h))
            {
                // (brush, new RectangleF(x, y, w, h))
            }
            else if (args.Count == 5)
            {
                // (brush, x, y, w, h)
                x = args[1].Expression.ToString();
                y = args[2].Expression.ToString();
                w = args[3].Expression.ToString();
                h = args[4].Expression.ToString();
            }
            else return false;

            _diagnostics.Add(Info("CANMIGDSPDF006",
                $"FillRectangle → {_pageVar}.DrawRectangleFromTop(..., fill: true)"));
            call = $"{_pageVar}.DrawRectangleFromTop({x}, {y}, {w}, {h}, 1, true);";
            return true;
        }

        private bool TryMatchGraphicsCall(
            GlobalStatementSyntax node,
            string method,
            out InvocationExpressionSyntax? inv,
            out string? pageVarName)
        {
            inv = null;
            pageVarName = null;
            var expr = ExtractExpression(node);
            if (expr is not InvocationExpressionSyntax invExpr) return false;
            if (invExpr.Expression is not MemberAccessExpressionSyntax methodAccess) return false;
            if (methodAccess.Name.Identifier.ValueText != method) return false;
            if (methodAccess.Expression is not MemberAccessExpressionSyntax graphicsAccess) return false;
            if (graphicsAccess.Name.Identifier.ValueText != "Graphics") return false;
            if (graphicsAccess.Expression is not IdentifierNameSyntax pageId) return false;
            if (!_pageVars.Contains(pageId.Identifier.ValueText)) return false;
            inv = invExpr;
            pageVarName = pageId.Identifier.ValueText;
            return true;
        }

        private bool IsGraphicsCallOnPageVar(GlobalStatementSyntax node, string method)
        {
            return TryMatchGraphicsCall(node, method, out _, out _);
        }

        private static bool TryExtractPointF(ExpressionSyntax expr, out string x, out string y)
        {
            x = y = "";
            if (expr is not ObjectCreationExpressionSyntax creation) return false;
            if (GetSimpleName(creation.Type) != "PointF") return false;
            var args = creation.ArgumentList?.Arguments;
            if (args?.Count != 2) return false;
            x = args.Value[0].Expression.ToString();
            y = args.Value[1].Expression.ToString();
            return true;
        }

        private static bool TryExtractRectangleFOrigin(ExpressionSyntax expr, out string x, out string y)
        {
            x = y = "";
            if (!TryExtractRectangleF(expr, out x, out y, out _, out _)) return false;
            return true;
        }

        private static bool TryExtractRectangleF(
            ExpressionSyntax expr,
            out string x, out string y, out string w, out string h)
        {
            x = y = w = h = "";
            if (expr is not ObjectCreationExpressionSyntax creation) return false;
            if (GetSimpleName(creation.Type) != "RectangleF") return false;
            var args = creation.ArgumentList?.Arguments;
            if (args?.Count != 4) return false;
            x = args.Value[0].Expression.ToString();
            y = args.Value[1].Expression.ToString();
            w = args.Value[2].Expression.ToString();
            h = args.Value[3].Expression.ToString();
            return true;
        }

        private static string? TryExtractTextFormatFontSize(ExpressionSyntax expr)
        {
            if (expr is not ObjectCreationExpressionSyntax creation) return null;
            if (GetSimpleName(creation.Type) != "TextFormat") return null;
            if (creation.Initializer is null) return null;
            foreach (var init in creation.Initializer.Expressions)
            {
                if (init is AssignmentExpressionSyntax assign &&
                    assign.Left is IdentifierNameSyntax nameId &&
                    nameId.Identifier.ValueText == "FontSize")
                    return assign.Right.ToString();
            }
            return null;
        }

        private static bool IsCreationDeclaration(GlobalStatementSyntax node, string typeName)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var firstVar = local.Declaration.Variables.FirstOrDefault();
            if (firstVar?.Initializer?.Value is not ObjectCreationExpressionSyntax creation) return false;
            return GetSimpleName(creation.Type) == typeName;
        }

        private static bool IsDeclarationWithCall(GlobalStatementSyntax node, string method)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
            return init is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method;
        }

        private bool IsMethodCallOn(GlobalStatementSyntax node, string variable, string method)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method &&
                   ma.Expression.ToString() == variable;
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
