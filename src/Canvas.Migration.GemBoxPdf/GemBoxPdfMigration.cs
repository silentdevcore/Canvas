using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.GemBoxPdf;

public sealed class GemBoxPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        var documentVariable = FindDocumentVariable(root);
        var pageVariable = FindPageVariable(root, documentVariable);
        var saveTarget = FindSaveTarget(root, documentVariable);

        var rewriter = new GemBoxPdfRewriter(documentVariable, pageVariable, saveTarget);
        var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
        rewritten = RemoveGemBoxUsings(rewritten);
        rewritten = EnsureCanvasUsing(rewritten);

        var diagnostics = rewriter.Diagnostics.ToList();
        diagnostics.AddRange(ScanForUnsupportedIdentifiers(root));

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(rewritten.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics
        };
    }

    private static string FindDocumentVariable(CompilationUnitSyntax root)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (GetSimpleName(creation.Type) != "PdfDocument")
                continue;

            if (!creation.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>()
                .Any(static directive => (directive.Name?.ToString() ?? "").StartsWith("GemBox.Pdf", StringComparison.Ordinal)))
            {
                continue;
            }

            var declaration = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
            if (declaration != null)
                return declaration.Identifier.ValueText;
        }

        return "document";
    }

    private static string FindPageVariable(CompilationUnitSyntax root, string documentVariable)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsPagesAddCall(invocation, documentVariable))
                continue;

            var declaration = invocation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
            if (declaration != null)
                return declaration.Identifier.ValueText;
        }

        return "page";
    }

    private static string? FindSaveTarget(CompilationUnitSyntax root, string documentVariable)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText == "Save"
                && access.Expression.ToString() == documentVariable
                && invocation.ArgumentList.Arguments.Count > 0)
            {
                return invocation.ArgumentList.Arguments[0].Expression.ToString();
            }
        }

        return null;
    }

    private static IEnumerable<MigrationDiagnostic> ScanForUnsupportedIdentifiers(CompilationUnitSyntax root)
    {
        var names = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(static identifier => identifier.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        if (names.Overlaps(new[]
            {
                "PdfInteractiveForm",
                "PdfSignature",
                "PdfSignatureField",
                "PdfEncryption",
                "PdfPermission",
                "PdfPortfolio",
                "PdfAttachment",
                "PdfTaggedContent",
                "PdfStructureElement",
                "PdfAnnotation",
                "PdfLinkAnnotation"
            }))
        {
            yield return Warning("CANMIGGEMBOX020",
                "GemBox forms, annotations, tagged PDF, attachments, encryption, or signatures require manual migration outside v1.");
        }
    }

    private static CompilationUnitSyntax RemoveGemBoxUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static directive =>
            {
                var name = directive.Name?.ToString() ?? "";
                return !name.StartsWith("GemBox.Pdf", StringComparison.Ordinal)
                    && name != "GemBox";
            })
            .ToArray();

        return root.WithUsings(SyntaxFactory.List(filtered));
    }

    private static CompilationUnitSyntax EnsureCanvasUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static directive => directive.Name?.ToString() == "Canvas.Pdf"))
            return root;

        var canvasUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Canvas.Pdf"))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        return root.WithUsings(root.Usings.Insert(0, canvasUsing));
    }

    private static bool IsPagesAddCall(InvocationExpressionSyntax invocation, string documentVariable)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax addAccess
            || addAccess.Name.Identifier.ValueText != "Add"
            || addAccess.Expression is not MemberAccessExpressionSyntax pagesAccess)
        {
            return false;
        }

        return pagesAccess.Name.Identifier.ValueText == "Pages"
            && pagesAccess.Expression.ToString() == documentVariable;
    }

    private static string GetSimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
        _ => type.ToString()
    };

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

    private sealed class GemBoxPdfRewriter : CSharpSyntaxRewriter
    {
        private readonly string _documentVariable;
        private readonly string _pageVariable;
        private readonly string? _saveTarget;
        private readonly List<MigrationDiagnostic> _diagnostics = [];

        public GemBoxPdfRewriter(string documentVariable, string pageVariable, string? saveTarget)
        {
            _documentVariable = documentVariable;
            _pageVariable = pageVariable;
            _saveTarget = saveTarget;
        }

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var members = new List<MemberDeclarationSyntax>();
            foreach (var member in node.Members)
            {
                if (member is not GlobalStatementSyntax statement)
                {
                    members.Add(member);
                    continue;
                }

                members.AddRange(TransformGlobalStatement(statement));
            }

            return node.WithMembers(SyntaxFactory.List(members));
        }

        private IEnumerable<MemberDeclarationSyntax> TransformGlobalStatement(GlobalStatementSyntax statement)
        {
            if (IsComponentInfoLicenseCall(statement))
            {
                _diagnostics.Add(Info("CANMIGGEMBOX000",
                    "GemBox ComponentInfo.SetLicense(...) does not have a Canvas.Pdf equivalent and was removed."));
                return [];
            }

            if (IsPdfDocumentCreation(statement))
            {
                _diagnostics.Add(Info("CANMIGGEMBOX001", "new GemBox PdfDocument() -> new Canvas.Pdf.PdfDocument()"));
                return [MakeGlobal("var document = new PdfDocument();", statement)];
            }

            if (TryGetPagesAddDeclaration(statement, out var variableName))
            {
                _diagnostics.Add(Info("CANMIGGEMBOX002", "document.Pages.Add() -> document.AddPage()"));
                return [MakeGlobal($"var {variableName} = document.AddPage();", statement)];
            }

            if (IsSaveCall(statement))
            {
                var target = GetFirstArgument(statement) ?? _saveTarget ?? "path";
                _diagnostics.Add(Info("CANMIGGEMBOX007", $"document.Save({target}) -> document.Save({target})"));
                return [MakeGlobal($"document.Save({target});", statement)];
            }

            if (TryConvertDrawText(statement, out var drawText))
                return [MakeGlobal(drawText!, statement)];

            if (IsContentCall(statement, "DrawImage"))
            {
                _diagnostics.Add(Warning("CANMIGGEMBOX005",
                    "GemBox DrawImage content operations require manual migration outside v1."));
                return [statement];
            }

            if (IsContentCall(statement, "DrawLine")
                || IsContentCall(statement, "DrawRectangle")
                || IsContentCall(statement, "DrawPath"))
            {
                _diagnostics.Add(Warning("CANMIGGEMBOX006",
                    "GemBox shape/path content operations require manual migration outside v1."));
                return [statement];
            }

            if (IsExistingPdfEditingCall(statement))
            {
                _diagnostics.Add(Warning("CANMIGGEMBOX021",
                    "GemBox existing-PDF loading, page import, or content editing requires manual migration outside v1."));
                return [statement];
            }

            return [statement];
        }

        private bool IsComponentInfoLicenseCall(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(static invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText == "SetLicense"
                    && access.Expression.ToString().EndsWith("ComponentInfo", StringComparison.Ordinal));
        }

        private bool IsPdfDocumentCreation(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Any(static creation => GetSimpleName(creation.Type) == "PdfDocument");
        }

        private bool TryGetPagesAddDeclaration(GlobalStatementSyntax statement, out string variableName)
        {
            foreach (var invocation in statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsPagesAddCall(invocation, _documentVariable))
                    continue;

                var declaration = invocation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                variableName = declaration?.Identifier.ValueText ?? _pageVariable;
                return true;
            }

            variableName = _pageVariable;
            return false;
        }

        private bool IsSaveCall(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText == "Save"
                    && access.Expression.ToString() == _documentVariable);
        }

        private bool TryConvertDrawText(GlobalStatementSyntax statement, out string? converted)
        {
            foreach (var invocation in statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access
                    || access.Name.Identifier.ValueText != "DrawText"
                    || !access.Expression.ToString().Contains(".Content", StringComparison.Ordinal))
                {
                    continue;
                }

                var arguments = invocation.ArgumentList.Arguments;
                if (arguments.Count == 0)
                    continue;

                var text = TryExtractText(arguments[0].Expression);
                var (x, y) = TryExtractPoint(arguments);
                if (text == null || x == null || y == null)
                {
                    _diagnostics.Add(Warning("CANMIGGEMBOX003",
                        "GemBox DrawText was detected but text or coordinates require manual migration."));
                    converted = null;
                    return false;
                }

                _diagnostics.Add(Info("CANMIGGEMBOX003", "GemBox DrawText(...) -> page.DrawTextFromTop(...)"));
                converted = $"{_pageVariable}.DrawTextFromTop({text}, {x}, {y}, 12);";
                return true;
            }

            converted = null;
            return false;
        }

        private static string? TryExtractText(ExpressionSyntax expression)
        {
            if (expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.ToString();
            }

            return null;
        }

        private static (string? X, string? Y) TryExtractPoint(SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            foreach (var argument in arguments)
            {
                if (argument.Expression is not ObjectCreationExpressionSyntax creation
                    || GetSimpleName(creation.Type) is not ("PdfPoint" or "Point" or "PointF")
                    || creation.ArgumentList == null
                    || creation.ArgumentList.Arguments.Count < 2)
                {
                    continue;
                }

                return (
                    creation.ArgumentList.Arguments[0].Expression.ToString(),
                    creation.ArgumentList.Arguments[1].Expression.ToString());
            }

            if (arguments.Count >= 3)
                return (arguments[1].Expression.ToString(), arguments[2].Expression.ToString());

            return (null, null);
        }

        private bool IsContentCall(GlobalStatementSyntax statement, string methodName)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText == methodName
                    && access.Expression.ToString().Contains(".Content", StringComparison.Ordinal));
        }

        private bool IsExistingPdfEditingCall(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText is "Load" or "LoadFromFile" or "Clone" or "ImportPages" or "Clear"
                    && access.Expression.ToString().Contains(_documentVariable, StringComparison.Ordinal));
        }

        private static string? GetFirstArgument(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(static invocation => invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString())
                .FirstOrDefault(static value => value != null);
        }

        private static GlobalStatementSyntax MakeGlobal(string source, GlobalStatementSyntax original)
        {
            return SyntaxFactory.GlobalStatement(SyntaxFactory.ParseStatement(source))
                .WithLeadingTrivia(original.GetLeadingTrivia())
                .WithTrailingTrivia(original.GetTrailingTrivia());
        }
    }
}
