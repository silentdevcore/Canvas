using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.ActivePdf;

public sealed class ActivePdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var documentVariable = FindDocumentVariable(root);
        var pageVariable = FindPageVariable(root, documentVariable);
        var saveTarget = FindSaveTarget(root, documentVariable);

        var rewriter = new ActivePdfRewriter(documentVariable, pageVariable, saveTarget);
        var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
        rewritten = RemoveActivePdfUsings(rewritten);
        rewritten = EnsureCanvasUsing(rewritten);

        var diagnostics = new List<MigrationDiagnostic>
        {
            Warning("CANMIGACTIVE000",
                "ActivePDF has multiple products and COM/server workflows; validate the source product before applying the migrated code.")
        };
        diagnostics.AddRange(rewriter.Diagnostics);
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
            if (GetSimpleName(creation.Type) is not ("Toolkit" or "DocConverter" or "Document" or "APDoc" or "Server"))
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
            if (!IsPageCreationCall(invocation, documentVariable))
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
                && access.Name.Identifier.ValueText is "Save" or "SaveAs" or "SaveToFile" or "CloseDocument" or "EndDoc" or "EndDocument"
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
                "WebGrabber",
                "DocConverter",
                "Merger",
                "Printer",
                "Server",
                "ComObject",
                "Type",
                "Form",
                "Signature",
                "Encryption",
                "Security",
                "Annotation",
                "Stamp"
            }))
        {
            yield return Warning("CANMIGACTIVE020",
                "ActivePDF WebGrabber, DocConverter, merge/stamp, COM/server, printer, forms, signatures, security, or annotation workflows require manual migration outside v1.");
        }
    }

    private static CompilationUnitSyntax RemoveActivePdfUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static directive =>
            {
                var name = directive.Name?.ToString() ?? "";
                return !name.StartsWith("activePDF", StringComparison.OrdinalIgnoreCase)
                    && !name.StartsWith("ActivePDF", StringComparison.Ordinal);
            })
            .ToArray();

        return root.WithUsings(SyntaxFactory.List(filtered));
    }

    private static CompilationUnitSyntax EnsureCanvasUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static directive => directive.Name?.ToString() == "PXA.Pdf"))
            return root;

        var canvasUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("PXA.Pdf"))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        return root.WithUsings(root.Usings.Insert(0, canvasUsing));
    }

    private static bool IsPageCreationCall(InvocationExpressionSyntax invocation, string documentVariable)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
            return false;

        return access.Name.Identifier.ValueText is "AddPage" or "BeginPage" or "NewPage"
            && access.Expression.ToString() == documentVariable;
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

    private sealed class ActivePdfRewriter : CSharpSyntaxRewriter
    {
        private readonly string _documentVariable;
        private readonly string _pageVariable;
        private readonly string? _saveTarget;
        private readonly List<MigrationDiagnostic> _diagnostics = [];

        public ActivePdfRewriter(string documentVariable, string pageVariable, string? saveTarget)
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
            if (IsActivePdfDocumentCreation(statement))
            {
                _diagnostics.Add(Info("CANMIGACTIVE001", "Likely ActivePDF document creation -> new PXA.Pdf.PdfDocument()"));
                return [MakeGlobal("var document = new PdfDocument();", statement)];
            }

            if (TryGetPageCreationDeclaration(statement, out var variableName))
            {
                _diagnostics.Add(Info("CANMIGACTIVE002", "Likely ActivePDF page creation -> document.AddPage()"));
                return [MakeGlobal($"var {variableName} = document.AddPage();", statement)];
            }

            if (TryConvertText(statement, out var drawText))
                return [MakeGlobal(drawText!, statement)];

            if (TryConvertLine(statement, out var drawLine))
                return [MakeGlobal(drawLine!, statement)];

            if (TryConvertRectangle(statement, out var drawRectangle))
                return [MakeGlobal(drawRectangle!, statement)];

            if (IsDrawingCall(statement, "AddImage", "DrawImage", "StampImage"))
            {
                _diagnostics.Add(Warning("CANMIGACTIVE005",
                    "ActivePDF image/stamp drawing requires manual migration outside v1."));
                return [statement];
            }

            if (IsSaveCall(statement))
            {
                var target = GetFirstArgument(statement) ?? _saveTarget ?? "path";
                _diagnostics.Add(Info("CANMIGACTIVE007", $"Likely ActivePDF save/close -> document.Save({target})"));
                return [MakeGlobal($"document.Save({target});", statement)];
            }

            if (IsManualWorkflowCall(statement))
            {
                _diagnostics.Add(Warning("CANMIGACTIVE021",
                    "ActivePDF HTML conversion, merge, stamp, printer, COM, server automation, or existing-PDF editing requires manual migration outside v1."));
                return [statement];
            }

            return [statement];
        }

        private static bool IsActivePdfDocumentCreation(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Any(static creation => GetSimpleName(creation.Type) is "Toolkit" or "DocConverter" or "Document" or "APDoc" or "Server");
        }

        private bool TryGetPageCreationDeclaration(GlobalStatementSyntax statement, out string variableName)
        {
            foreach (var invocation in statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsPageCreationCall(invocation, _documentVariable))
                    continue;

                var declaration = invocation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                variableName = declaration?.Identifier.ValueText ?? _pageVariable;
                return true;
            }

            variableName = _pageVariable;
            return false;
        }

        private bool TryConvertText(GlobalStatementSyntax statement, out string? converted)
        {
            foreach (var invocation in FindDrawingCalls(statement, "PrintText", "DrawText", "AddText", "TextOut"))
            {
                var arguments = invocation.ArgumentList.Arguments;
                if (arguments.Count < 3)
                    continue;

                var text = TryExtractString(arguments[0].Expression);
                if (text == null)
                {
                    _diagnostics.Add(Warning("CANMIGACTIVE003",
                        "ActivePDF text drawing was detected but text or coordinates require manual migration."));
                    converted = null;
                    return false;
                }

                _diagnostics.Add(Info("CANMIGACTIVE003", "Likely ActivePDF text drawing -> page.DrawTextFromTop(...)"));
                converted = $"{_pageVariable}.DrawTextFromTop({text}, {arguments[1].Expression}, {arguments[2].Expression}, 12);";
                return true;
            }

            converted = null;
            return false;
        }

        private bool TryConvertLine(GlobalStatementSyntax statement, out string? converted)
        {
            foreach (var invocation in FindDrawingCalls(statement, "DrawLine", "AddLine"))
            {
                var arguments = invocation.ArgumentList.Arguments;
                if (arguments.Count < 4)
                    continue;

                _diagnostics.Add(Info("CANMIGACTIVE006", "Likely ActivePDF line drawing -> page.DrawLineFromTop(...)"));
                converted = $"{_pageVariable}.DrawLineFromTop({arguments[0].Expression}, {arguments[1].Expression}, {arguments[2].Expression}, {arguments[3].Expression});";
                return true;
            }

            converted = null;
            return false;
        }

        private bool TryConvertRectangle(GlobalStatementSyntax statement, out string? converted)
        {
            foreach (var invocation in FindDrawingCalls(statement, "DrawRectangle", "AddRectangle"))
            {
                var arguments = invocation.ArgumentList.Arguments;
                if (arguments.Count < 4)
                    continue;

                _diagnostics.Add(Info("CANMIGACTIVE006", "Likely ActivePDF rectangle drawing -> page.DrawRectangleFromTop(...)"));
                converted = $"{_pageVariable}.DrawRectangleFromTop({arguments[0].Expression}, {arguments[1].Expression}, {arguments[2].Expression}, {arguments[3].Expression});";
                return true;
            }

            converted = null;
            return false;
        }

        private bool IsSaveCall(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText is "Save" or "SaveAs" or "SaveToFile" or "CloseDocument" or "EndDoc" or "EndDocument"
                    && access.Expression.ToString() == _documentVariable);
        }

        private bool IsDrawingCall(GlobalStatementSyntax statement, params string[] methodNames)
        {
            return FindDrawingCalls(statement, methodNames).Any();
        }

        private IEnumerable<InvocationExpressionSyntax> FindDrawingCalls(GlobalStatementSyntax statement, params string[] methodNames)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && methodNames.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal)
                    && (access.Expression.ToString() == _documentVariable
                        || access.Expression.ToString() == _pageVariable));
        }

        private bool IsManualWorkflowCall(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Expression.ToString().Contains(_documentVariable, StringComparison.Ordinal)
                    && access.Name.Identifier.ValueText is "Convert" or "ConvertToPDF" or "Submit" or "Merge" or "Stamp" or "Open" or "OpenFile" or "Load" or "Print" or "StartPrintJob");
        }

        private static string? TryExtractString(ExpressionSyntax expression)
        {
            return expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)
                ? literal.ToString()
                : null;
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
