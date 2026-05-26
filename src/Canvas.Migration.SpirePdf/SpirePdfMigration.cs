using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.SpirePdf;

public sealed class SpirePdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var documentVariable = FindDocumentVariable(root);
        var pageVariable = FindPageVariable(root, documentVariable);
        var saveTarget = FindSaveTarget(root, documentVariable);

        var rewriter = new SpirePdfRewriter(documentVariable, pageVariable, saveTarget);
        var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
        rewritten = RemoveSpireUsings(rewritten);
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
                && access.Name.Identifier.ValueText is "SaveToFile" or "Save"
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
                "PdfTable",
                "PdfGrid",
                "PdfFormWidget",
                "PdfSecurity",
                "PdfCertificate",
                "PdfAttachment",
                "PdfAnnotation",
                "PdfTextExtractor"
            }))
        {
            yield return Warning("CANMIGSPIRE020",
                "Spire tables, forms, annotations, extraction, attachments, signatures, or security APIs require manual migration outside v1.");
        }
    }

    private static CompilationUnitSyntax RemoveSpireUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static directive =>
            {
                var name = directive.Name?.ToString() ?? "";
                return !name.StartsWith("Spire.Pdf", StringComparison.Ordinal);
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

    private sealed class SpirePdfRewriter : CSharpSyntaxRewriter
    {
        private readonly string _documentVariable;
        private readonly string _pageVariable;
        private readonly string? _saveTarget;
        private readonly List<MigrationDiagnostic> _diagnostics = [];

        public SpirePdfRewriter(string documentVariable, string pageVariable, string? saveTarget)
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
            if (IsPdfDocumentCreation(statement))
            {
                _diagnostics.Add(Info("CANMIGSPIRE001", "new Spire PdfDocument() -> new Canvas.Pdf.PdfDocument()"));
                return [MakeGlobal("var document = new PdfDocument();", statement)];
            }

            if (TryGetPagesAddDeclaration(statement, out var variableName))
            {
                _diagnostics.Add(Info("CANMIGSPIRE002", "document.Pages.Add() -> document.AddPage()"));
                return [MakeGlobal($"var {variableName} = document.AddPage();", statement)];
            }

            if (TryConvertDrawString(statement, out var drawText))
                return [MakeGlobal(drawText!, statement)];

            if (TryConvertDrawLine(statement, out var drawLine))
                return [MakeGlobal(drawLine!, statement)];

            if (TryConvertDrawRectangle(statement, out var drawRectangle))
                return [MakeGlobal(drawRectangle!, statement)];

            if (IsCanvasCall(statement, "DrawImage"))
            {
                _diagnostics.Add(Warning("CANMIGSPIRE005",
                    "Spire DrawImage requires manual migration outside v1."));
                return [statement];
            }

            if (IsSaveCall(statement))
            {
                var target = GetFirstArgument(statement) ?? _saveTarget ?? "path";
                _diagnostics.Add(Info("CANMIGSPIRE007", $"document.SaveToFile({target}) -> document.Save({target})"));
                return [MakeGlobal($"document.Save({target});", statement)];
            }

            if (IsExistingPdfEditingOrConversionCall(statement))
            {
                _diagnostics.Add(Warning("CANMIGSPIRE021",
                    "Spire existing-PDF editing, loading, merging, splitting, or conversion requires manual migration outside v1."));
                return [statement];
            }

            return [statement];
        }

        private static bool IsPdfDocumentCreation(GlobalStatementSyntax statement)
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

        private bool TryConvertDrawString(GlobalStatementSyntax statement, out string? converted)
        {
            foreach (var invocation in statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access
                    || access.Name.Identifier.ValueText != "DrawString"
                    || !access.Expression.ToString().Contains(".Canvas", StringComparison.Ordinal))
                {
                    continue;
                }

                var arguments = invocation.ArgumentList.Arguments;
                if (arguments.Count == 0)
                    continue;

                var text = TryExtractString(arguments[0].Expression);
                var (x, y) = TryExtractPoint(arguments);
                var fontSize = TryExtractFontSize(arguments);
                if (text == null || x == null || y == null)
                {
                    _diagnostics.Add(Warning("CANMIGSPIRE003",
                        "Spire DrawString was detected but text or coordinates require manual migration."));
                    converted = null;
                    return false;
                }

                _diagnostics.Add(Info("CANMIGSPIRE003", "page.Canvas.DrawString(...) -> page.DrawTextFromTop(...)"));
                converted = $"{_pageVariable}.DrawTextFromTop({text}, {x}, {y}, {fontSize});";
                return true;
            }

            converted = null;
            return false;
        }

        private bool TryConvertDrawLine(GlobalStatementSyntax statement, out string? converted)
        {
            foreach (var invocation in FindCanvasCalls(statement, "DrawLine"))
            {
                var arguments = invocation.ArgumentList.Arguments;
                if (arguments.Count < 5)
                    continue;

                _diagnostics.Add(Info("CANMIGSPIRE006", "page.Canvas.DrawLine(...) -> page.DrawLineFromTop(...)"));
                converted = $"{_pageVariable}.DrawLineFromTop({arguments[1].Expression}, {arguments[2].Expression}, {arguments[3].Expression}, {arguments[4].Expression});";
                return true;
            }

            converted = null;
            return false;
        }

        private bool TryConvertDrawRectangle(GlobalStatementSyntax statement, out string? converted)
        {
            foreach (var invocation in FindCanvasCalls(statement, "DrawRectangle"))
            {
                var arguments = invocation.ArgumentList.Arguments;
                if (arguments.Count >= 5)
                {
                    _diagnostics.Add(Info("CANMIGSPIRE006", "page.Canvas.DrawRectangle(...) -> page.DrawRectangleFromTop(...)"));
                    converted = $"{_pageVariable}.DrawRectangleFromTop({arguments[1].Expression}, {arguments[2].Expression}, {arguments[3].Expression}, {arguments[4].Expression});";
                    return true;
                }

                var rectangle = arguments.FirstOrDefault(argument => argument.Expression is ObjectCreationExpressionSyntax creation
                    && GetSimpleName(creation.Type) is "RectangleF" or "Rectangle")?.Expression as ObjectCreationExpressionSyntax;
                if (rectangle?.ArgumentList?.Arguments.Count >= 4)
                {
                    var rectArgs = rectangle.ArgumentList.Arguments;
                    _diagnostics.Add(Info("CANMIGSPIRE006", "page.Canvas.DrawRectangle(...) -> page.DrawRectangleFromTop(...)"));
                    converted = $"{_pageVariable}.DrawRectangleFromTop({rectArgs[0].Expression}, {rectArgs[1].Expression}, {rectArgs[2].Expression}, {rectArgs[3].Expression});";
                    return true;
                }
            }

            converted = null;
            return false;
        }

        private bool IsSaveCall(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText is "SaveToFile" or "Save"
                    && access.Expression.ToString() == _documentVariable);
        }

        private bool IsCanvasCall(GlobalStatementSyntax statement, string methodName)
        {
            return FindCanvasCalls(statement, methodName).Any();
        }

        private static IEnumerable<InvocationExpressionSyntax> FindCanvasCalls(GlobalStatementSyntax statement, string methodName)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText == methodName
                    && access.Expression.ToString().Contains(".Canvas", StringComparison.Ordinal));
        }

        private bool IsExistingPdfEditingOrConversionCall(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Expression.ToString().Contains(_documentVariable, StringComparison.Ordinal)
                    && access.Name.Identifier.ValueText is "LoadFromFile" or "LoadFromStream" or "AppendPage" or "InsertPage" or "DeletePage" or "MergeFiles" or "Split" or "SaveToFile");
        }

        private static string? TryExtractString(ExpressionSyntax expression)
        {
            return expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)
                ? literal.ToString()
                : null;
        }

        private static (string? X, string? Y) TryExtractPoint(SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            foreach (var argument in arguments)
            {
                if (argument.Expression is not ObjectCreationExpressionSyntax creation
                    || GetSimpleName(creation.Type) is not ("PointF" or "Point")
                    || creation.ArgumentList == null
                    || creation.ArgumentList.Arguments.Count < 2)
                {
                    continue;
                }

                return (
                    creation.ArgumentList.Arguments[0].Expression.ToString(),
                    creation.ArgumentList.Arguments[1].Expression.ToString());
            }

            if (arguments.Count >= 5)
                return (arguments[3].Expression.ToString(), arguments[4].Expression.ToString());

            return (null, null);
        }

        private static string TryExtractFontSize(SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            foreach (var argument in arguments)
            {
                if (argument.Expression is not ObjectCreationExpressionSyntax creation
                    || GetSimpleName(creation.Type) is not ("PdfFont" or "PdfTrueTypeFont")
                    || creation.ArgumentList == null)
                {
                    continue;
                }

                var numeric = creation.ArgumentList.Arguments
                    .Select(static arg => arg.Expression.ToString())
                    .FirstOrDefault(static value => decimal.TryParse(value, out _));
                if (numeric != null)
                    return numeric;
            }

            return "12";
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
