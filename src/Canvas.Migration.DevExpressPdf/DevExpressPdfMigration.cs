using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.DevExpressPdf;

public sealed class DevExpressPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        var processorVar = FindProcessorVariable(root);
        var graphicsVar = FindGraphicsVariable(root, processorVar);
        var saveTarget = FindSaveTarget(root, processorVar);

        var rewriter = new DevExpressRewriter(processorVar, graphicsVar, saveTarget);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        newRoot = RemoveDevExpressUsings(newRoot);
        newRoot = EnsureCanvasUsing(newRoot);

        var diagnostics = rewriter.Diagnostics.ToList();
        diagnostics.AddRange(ScanForUnsupportedIdentifiers(root));

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(newRoot.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics
        };
    }

    private static string FindProcessorVariable(CompilationUnitSyntax root)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (GetSimpleName(creation.Type) == "PdfDocumentProcessor")
            {
                var decl = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "processor";
    }

    private static string FindGraphicsVariable(CompilationUnitSyntax root, string processorVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText == "CreateGraphics" &&
                ma.Expression.ToString() == processorVar)
            {
                var decl = inv.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "graphics";
    }

    private static string? FindSaveTarget(CompilationUnitSyntax root, string processorVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText == "SaveDocument" &&
                ma.Expression.ToString() == processorVar)
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
                "PdfAcroForm", "PdfFormField", "PdfSignature", "PdfDocumentSigner",
                "PdfEncryptionOptions", "PdfAnnotation", "PdfBookmark"
            }))
        {
            yield return Warning("CANMIGDEVEXP022",
                "Forms, signatures, encryption, annotations, or bookmarks require manual migration outside v1.");
        }

        if (names.Overlaps(new[] { "XtraReport", "PdfExportOptions", "PrintingSystemBase" }))
        {
            yield return Warning("CANMIGDEVEXP020",
                "DevExpress report export workflows require manual migration.");
        }
    }

    private static CompilationUnitSyntax RemoveDevExpressUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static u => !(u.Name?.ToString() ?? "").StartsWith("DevExpress", StringComparison.Ordinal))
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

    private static MigrationDiagnostic Warning(string id, string message) => new()
    {
        Id = id,
        Message = message,
        Severity = MigrationDiagnosticSeverity.Warning
    };

    // --- Rewriter ---------------------------------------------------------------

    private sealed class DevExpressRewriter : CSharpSyntaxRewriter
    {
        private readonly string _processorVar;
        private readonly string _graphicsVar;
        private readonly string? _saveTarget;
        private readonly string _pageVar = "page";
        private readonly List<MigrationDiagnostic> _diagnostics = [];
        private readonly List<string> _deferredDrawCalls = [];

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public DevExpressRewriter(string processorVar, string graphicsVar, string? saveTarget)
        {
            _processorVar = processorVar;
            _graphicsVar = graphicsVar;
            _saveTarget = saveTarget;
        }

        // Override VisitCompilationUnit to allow one-to-many statement replacement.
        // RenderNewPage expands into AddPage() + all deferred draw calls.
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
            // `using var processor = new PdfDocumentProcessor()` → `var document = new PdfDocument()`
            if (IsCreationDeclaration(node, "PdfDocumentProcessor"))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP001", "new PdfDocumentProcessor() → new PdfDocument()"));
                return [MakeGlobal("var document = new PdfDocument();", node)];
            }

            // `processor.CreateEmptyDocument()` → remove
            if (IsMethodCallOn(node, _processorVar, "CreateEmptyDocument"))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP002",
                    "CreateEmptyDocument() removed — document is created by new PdfDocument()."));
                return [];
            }

            // `using var graphics = processor.CreateGraphics()` → remove
            if (IsDeclarationWithCall(node, "CreateGraphics"))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP003",
                    "CreateGraphics() removed — Canvas draw calls use the PdfPage surface directly."));
                return [];
            }

            // Draw calls on graphics → defer until after AddPage (DevExpress draws before RenderNewPage)
            if (TryConvertDrawCall(node, out var canvasCall))
            {
                _deferredDrawCalls.Add(canvasCall!);
                return [];
            }

            // `processor.RenderNewPage(...)` → `var page = document.AddPage();` + queued draw calls
            if (IsMethodCallOn(node, _processorVar, "RenderNewPage"))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP004",
                    $"RenderNewPage() → document.AddPage() — {_deferredDrawCalls.Count} draw call(s) repositioned after AddPage."));
                var results = new List<MemberDeclarationSyntax>();
                results.Add(MakeGlobal($"var {_pageVar} = document.AddPage();", node));
                foreach (var call in _deferredDrawCalls)
                    results.Add(MakeGlobal(call, node));
                _deferredDrawCalls.Clear();
                return results;
            }

            // `processor.SaveDocument(path)` → `document.Save(path)`
            if (IsMethodCallOn(node, _processorVar, "SaveDocument"))
            {
                var path = GetFirstArg(node) ?? _saveTarget ?? "path";
                _diagnostics.Add(Info("CANMIGDEVEXP008", $"SaveDocument({path}) → document.Save({path})"));
                return [MakeGlobal($"document.Save({path});", node)];
            }

            // Unsupported processor APIs (existing-PDF editing) — keep with warning
            if (IsMethodCallOn(node, _processorVar, "LoadDocument") ||
                IsMethodCallOn(node, _processorVar, "AppendDocument") ||
                IsMethodCallOn(node, _processorVar, "DeletePage") ||
                IsMethodCallOn(node, _processorVar, "InsertPage"))
            {
                _diagnostics.Add(Warning("CANMIGDEVEXP021",
                    "Existing-PDF processing or page editing APIs require manual migration outside v1."));
                return [node];
            }

            // Report export APIs — keep with warning
            if (IsAnyMethodCall(node, "ExportToPdf") || IsAnyMethodCall(node, "ExportToPdfAsync"))
            {
                _diagnostics.Add(Warning("CANMIGDEVEXP020",
                    "Report export workflows require manual migration."));
                return [node];
            }

            return [node];
        }

        private bool TryConvertDrawCall(GlobalStatementSyntax node, out string? canvasCall)
        {
            canvasCall = null;
            var expr = ExtractExpression(node);
            if (expr is not InvocationExpressionSyntax inv) return false;
            if (inv.Expression is not MemberAccessExpressionSyntax ma) return false;
            if (ma.Expression.ToString() != _graphicsVar) return false;

            var args = inv.ArgumentList.Arguments;
            switch (ma.Name.Identifier.ValueText)
            {
                case "DrawString" when args.Count >= 5:
                {
                    var text = args[0].Expression.ToString();
                    var x = args[3].Expression.ToString();
                    var y = args[4].Expression.ToString();
                    var fontSize = TryExtractDxFontSize(args[1].Expression) ?? "12";
                    _diagnostics.Add(Info("CANMIGDEVEXP005",
                        $"DrawString({text}) → {_pageVar}.DrawTextFromTop(...)"));
                    canvasCall = $"{_pageVar}.DrawTextFromTop({text}, {x}, {y}, {fontSize});";
                    return true;
                }
                case "DrawLine" when args.Count >= 5:
                {
                    // (pen, x1, y1, x2, y2)
                    var x1 = args[1].Expression.ToString();
                    var y1 = args[2].Expression.ToString();
                    var x2 = args[3].Expression.ToString();
                    var y2 = args[4].Expression.ToString();
                    _diagnostics.Add(Info("CANMIGDEVEXP006", $"DrawLine → {_pageVar}.DrawLine(...)"));
                    canvasCall = $"{_pageVar}.DrawLine({x1}, {y1}, {x2}, {y2});";
                    return true;
                }
                case "DrawRectangle" when args.Count >= 5:
                {
                    // (pen, x, y, width, height)
                    var x = args[1].Expression.ToString();
                    var y = args[2].Expression.ToString();
                    var w = args[3].Expression.ToString();
                    var h = args[4].Expression.ToString();
                    _diagnostics.Add(Info("CANMIGDEVEXP007",
                        $"DrawRectangle → {_pageVar}.DrawRectangle(...)"));
                    canvasCall = $"{_pageVar}.DrawRectangle({x}, {y}, {w}, {h});";
                    return true;
                }
            }

            return false;
        }

        private static string? TryExtractDxFontSize(ExpressionSyntax fontExpr)
        {
            if (fontExpr is ObjectCreationExpressionSyntax creation)
            {
                var fontArgs = creation.ArgumentList?.Arguments;
                if (fontArgs?.Count >= 2)
                    return fontArgs.Value[1].Expression.ToString();
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

        private static bool IsMethodCallOn(GlobalStatementSyntax node, string variable, string method)
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
    }
}
