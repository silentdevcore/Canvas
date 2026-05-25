using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.SyncfusionPdf;

public sealed class SyncfusionPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new SyncfusionPdfSyntaxRewriter(root);
        var migratedRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        migratedRoot = EnsureCanvasUsing(RemoveSyncfusionUsings(migratedRoot));

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(migratedRoot.NormalizeWhitespace().ToFullString()),
            Diagnostics = rewriter.Diagnostics
        };
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static CompilationUnitSyntax RemoveSyncfusionUsings(CompilationUnitSyntax root)
    {
        var usings = root.Usings
            .Where(static directive => directive.Name?.ToString() is not ("Syncfusion.Pdf" or "Syncfusion.Pdf.Graphics"))
            .ToArray();

        return root.WithUsings(SyntaxFactory.List(usings));
    }

    private static CompilationUnitSyntax EnsureCanvasUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static directive => directive.Name?.ToString() == "Canvas.Pdf"))
        {
            return root;
        }

        var canvasUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Canvas.Pdf"));
        return root.WithUsings(root.Usings.Insert(0, canvasUsing));
    }

    private sealed class SyncfusionPdfSyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly List<MigrationDiagnostic> _diagnostics = new();
        private readonly Dictionary<string, ExpressionSyntax> _graphicsPageByVariable;
        private readonly HashSet<string> _removableGraphicsVariables;
        private readonly Dictionary<string, PdfStringFormatMigrationInfo> _stringFormatsByVariable;
        private readonly HashSet<string> _removableStringFormatVariables;
        private readonly HashSet<string> _savedDocumentVariables;

        public SyncfusionPdfSyntaxRewriter(CompilationUnitSyntax root)
        {
            _graphicsPageByVariable = FindGraphicsVariables(root);
            _stringFormatsByVariable = FindStringFormatVariables(root);
            _removableGraphicsVariables = FindRemovableGraphicsVariables(root, _graphicsPageByVariable, _stringFormatsByVariable);
            _removableStringFormatVariables = FindRemovableStringFormatVariables(root, _graphicsPageByVariable, _stringFormatsByVariable);
            _savedDocumentVariables = FindSavedDocumentVariables(root, FindDocumentVariables(root));
            AddUnsupportedFeatureDiagnostics(root);
        }

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            if (node.Statement is ExpressionStatementSyntax expressionStatement
                && TryRemoveDocumentClose(expressionStatement, out var closeDiagnostic))
            {
                _diagnostics.Add(closeDiagnostic);
                return null;
            }

            if (node.Statement is LocalDeclarationStatementSyntax localDeclaration
                && localDeclaration.Declaration.Variables.Count == 1
                && TryGetRemovalDiagnostic(localDeclaration.Declaration.Variables[0].Identifier.ValueText, out var diagnostic))
            {
                _diagnostics.Add(diagnostic);
                return null;
            }

            return base.VisitGlobalStatement(node);
        }

        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var visited = (ExpressionStatementSyntax)base.VisitExpressionStatement(node)!;

            if (visited.Parent is not GlobalStatementSyntax
                && TryRemoveDocumentClose(visited, out var diagnostic))
            {
                _diagnostics.Add(diagnostic);
                return null;
            }

            return visited;
        }

        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            var visited = (LocalDeclarationStatementSyntax)base.VisitLocalDeclarationStatement(node)!;

            if (visited.Declaration.Variables.Count == 1
                && visited.Parent is not GlobalStatementSyntax
                && TryGetRemovalDiagnostic(visited.Declaration.Variables[0].Identifier.ValueText, out var diagnostic))
            {
                _diagnostics.Add(diagnostic);
                return null;
            }

            if (visited.UsingKeyword.IsKind(SyntaxKind.None)
                || visited.Declaration.Variables.Count != 1
                || visited.Declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation
                || creation.Type.ToString() != "PdfDocument")
            {
                return visited;
            }

            _diagnostics.Add(Info("CANMIGSYNC001", "Syncfusion PdfDocument construction was migrated to Canvas.Pdf.PdfDocument."));

            return visited
                .WithUsingKeyword(default)
                .WithAwaitKeyword(default);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

            if (visited.Name.Identifier.ValueText == "Add"
                && visited.Expression is MemberAccessExpressionSyntax pagesAccess
                && pagesAccess.Name.Identifier.ValueText == "Pages")
            {
                _diagnostics.Add(Info("CANMIGSYNC002", "Syncfusion document.Pages.Add() was migrated to Canvas document.AddPage()."));

                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    pagesAccess.Expression,
                    SyntaxFactory.IdentifierName("AddPage"));
            }

            return visited;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

            if (!TryMigrateGraphicsInvocation(visited, _graphicsPageByVariable, _stringFormatsByVariable, out var migrated, out var diagnosticId, out var diagnosticMessage))
            {
                if (TryCreateUnsupportedGraphicsDiagnostic(visited, _graphicsPageByVariable, out var diagnostic))
                {
                    _diagnostics.Add(diagnostic);
                }

                return visited;
            }

            _diagnostics.Add(Info(diagnosticId, diagnosticMessage));
            return migrated;
        }

        private void AddUnsupportedFeatureDiagnostics(CompilationUnitSyntax root)
        {
            var identifierNames = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(static identifier => identifier.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);

            if (identifierNames.Contains("PdfGrid"))
            {
                _diagnostics.Add(Warning("CANMIGSYNC005", "Syncfusion PdfGrid/table usage requires manual Canvas table migration."));
            }

            if (identifierNames.Overlaps(new[]
                {
                    "PdfLoadedDocument",
                    "PdfForm",
                    "PdfField",
                    "PdfSignature",
                    "PdfCertificate",
                    "PdfSecurity",
                    "PdfDocumentSecurity",
                    "PdfPageTemplateElement",
                    "PdfDocumentTemplate",
                    "PdfConformanceLevel",
                    "PdfPortfolio"
                }))
            {
                _diagnostics.Add(Warning("CANMIGSYNC006", "Syncfusion forms, security, PDF/A, template, or existing-PDF processing is outside the v1 migration scope."));
            }
        }

        private static bool TryMigrateGraphicsInvocation(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            IReadOnlyDictionary<string, PdfStringFormatMigrationInfo> stringFormatsByVariable,
            out InvocationExpressionSyntax migrated,
            out string diagnosticId,
            out string diagnosticMessage)
        {
            diagnosticId = string.Empty;
            diagnosticMessage = string.Empty;

            if (TryMigrateDrawTextBox(invocation, graphicsPageByVariable, stringFormatsByVariable, out migrated))
            {
                diagnosticId = "CANMIGSYNC012";
                diagnosticMessage = "Syncfusion RectangleF DrawString call was migrated to Canvas DrawTextBoxFromTop.";
                return true;
            }

            if (TryMigrateDrawString(invocation, graphicsPageByVariable, out migrated))
            {
                diagnosticId = "CANMIGSYNC003";
                diagnosticMessage = "Simple Syncfusion DrawString call was migrated to Canvas DrawTextFromTop.";
                return true;
            }

            if (TryMigrateDrawLine(invocation, graphicsPageByVariable, out migrated))
            {
                diagnosticId = "CANMIGSYNC010";
                diagnosticMessage = "Simple Syncfusion DrawLine call was migrated to Canvas DrawLineFromTop.";
                return true;
            }

            if (TryMigrateDrawRectangle(invocation, graphicsPageByVariable, out migrated))
            {
                diagnosticId = "CANMIGSYNC011";
                diagnosticMessage = "Simple Syncfusion DrawRectangle call was migrated to Canvas DrawRectangleFromTop.";
                return true;
            }

            if (TryMigrateDrawImage(invocation, graphicsPageByVariable, out migrated))
            {
                diagnosticId = "CANMIGSYNC014";
                diagnosticMessage = "Simple Syncfusion DrawImage call was migrated to Canvas DrawImageFromTop.";
                return true;
            }

            migrated = invocation;
            return false;
        }

        private bool TryGetRemovalDiagnostic(string variableName, out MigrationDiagnostic diagnostic)
        {
            if (_removableGraphicsVariables.Contains(variableName))
            {
                diagnostic = Info("CANMIGSYNC009", "Syncfusion PdfGraphics variable was removed after all usages were migrated.");
                return true;
            }

            if (_removableStringFormatVariables.Contains(variableName))
            {
                diagnostic = Info("CANMIGSYNC013", "Syncfusion PdfStringFormat variable was removed after all usages were migrated.");
                return true;
            }

            diagnostic = Info("CANMIGSYNC000", "No migration was applied.");
            return false;
        }

        private bool TryRemoveDocumentClose(ExpressionStatementSyntax statement, out MigrationDiagnostic diagnostic)
        {
            diagnostic = Info("CANMIGSYNC000", "No migration was applied.");

            if (statement.Expression is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name.Identifier.ValueText != "Close"
                || memberAccess.Expression is not IdentifierNameSyntax documentIdentifier
                || !_savedDocumentVariables.Contains(documentIdentifier.Identifier.ValueText)
                || invocation.ArgumentList.Arguments.Count != 1
                || !invocation.ArgumentList.Arguments[0].Expression.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                return false;
            }

            diagnostic = Info("CANMIGSYNC015", "Syncfusion document.Close(true) was removed after the saved document was migrated.");
            return true;
        }

        private static bool TryCreateUnsupportedGraphicsDiagnostic(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            out MigrationDiagnostic diagnostic)
        {
            if (IsRectangleOrFormattedDrawString(invocation, graphicsPageByVariable))
            {
                diagnostic = Warning("CANMIGSYNC004", "Syncfusion DrawString uses rectangle layout or string format options that need manual layout review.");
                return true;
            }

            if (IsUnresolvedSimpleDrawString(invocation, graphicsPageByVariable))
            {
                diagnostic = Warning("CANMIGSYNC007", "Syncfusion DrawString could not be migrated because the font size or coordinate conversion inputs were not resolved.");
                return true;
            }

            if (IsUnresolvedDrawImage(invocation, graphicsPageByVariable))
            {
                diagnostic = Warning("CANMIGSYNC008", "Syncfusion DrawImage could not be migrated because the source image path, stream, or bytes were not resolved.");
                return true;
            }

            diagnostic = Info("CANMIGSYNC000", "No migration was applied.");
            return false;
        }

        private static bool IsRectangleOrFormattedDrawString(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable)
        {
            if (!TryGetGraphicsMethodInvocation(invocation, graphicsPageByVariable, "DrawString", out var arguments))
            {
                return false;
            }

            return arguments.Count >= 4 && IsRectangleCreation(arguments[3].Expression)
                || arguments.Count >= 5 && IsPdfStringFormatExpression(arguments[4].Expression);
        }

        private static bool IsUnresolvedSimpleDrawString(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable)
        {
            return TryGetGraphicsMethodInvocation(invocation, graphicsPageByVariable, "DrawString", out var arguments)
                && arguments.Count == 5
                && !IsRectangleCreation(arguments[3].Expression)
                && !TryMapStandardFont(arguments[1].Expression, out _, out _);
        }

        private static bool IsUnresolvedDrawImage(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable)
        {
            return TryGetGraphicsMethodInvocation(invocation, graphicsPageByVariable, "DrawImage", out var arguments)
                && arguments.Count is 3 or 5
                && !TryMapImageSource(arguments[0].Expression, out _);
        }

        private static bool TryGetGraphicsMethodInvocation(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            string methodName,
            out SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            arguments = default;

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name.Identifier.ValueText != methodName
                || !TryResolveGraphicsPageExpression(memberAccess.Expression, graphicsPageByVariable, out _))
            {
                return false;
            }

            arguments = invocation.ArgumentList.Arguments;
            return true;
        }

        private static bool TryMigrateDrawTextBox(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            IReadOnlyDictionary<string, PdfStringFormatMigrationInfo> stringFormatsByVariable,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax drawStringAccess
                || drawStringAccess.Name.Identifier.ValueText != "DrawString"
                || !TryResolveGraphicsPageExpression(drawStringAccess.Expression, graphicsPageByVariable, out var pageExpression))
            {
                return false;
            }

            var arguments = invocation.ArgumentList.Arguments;

            if (arguments.Count is not (4 or 5)
                || !TryMapStandardFont(arguments[1].Expression, out var fontSizeExpression, out var fontFamilyExpression)
                || !TryMapPdfBrushColor(arguments[2].Expression, out var fillColorExpression)
                || !TryGetRectangleArguments(arguments[3].Expression, out var rectangleArguments))
            {
                return false;
            }

            PdfStringFormatMigrationInfo? stringFormat = null;

            if (arguments.Count == 5
                && !TryResolveStringFormat(arguments[4].Expression, stringFormatsByVariable, out stringFormat))
            {
                return false;
            }

            var textBoxOptions = CreateTextBoxOptions(fontFamilyExpression, fontSizeExpression, fillColorExpression, stringFormat);
            var newArguments = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                arguments[0],
                SyntaxFactory.Argument(rectangleArguments.X),
                SyntaxFactory.Argument(rectangleArguments.Y),
                SyntaxFactory.Argument(rectangleArguments.Width),
                SyntaxFactory.Argument(rectangleArguments.Height),
                SyntaxFactory.Argument(textBoxOptions)
            }));

            migrated = invocation
                .WithExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    pageExpression,
                    SyntaxFactory.IdentifierName("DrawTextBoxFromTop")))
                .WithArgumentList(newArguments);

            return true;
        }

        private static bool TryMigrateDrawString(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax drawStringAccess
                || drawStringAccess.Name.Identifier.ValueText != "DrawString"
                || !TryResolveGraphicsPageExpression(drawStringAccess.Expression, graphicsPageByVariable, out var pageExpression))
            {
                return false;
            }

            var arguments = invocation.ArgumentList.Arguments;

            if (arguments.Count != 5)
            {
                return false;
            }

            if (IsRectangleCreation(arguments[3].Expression)
                || IsPdfStringFormatExpression(arguments[4].Expression))
            {
                return false;
            }

            if (!TryMapStandardFont(arguments[1].Expression, out var fontSizeExpression, out var fontFamilyExpression))
            {
                return false;
            }

            var newArguments = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                arguments[0],
                arguments[3],
                arguments[4],
                SyntaxFactory.Argument(fontSizeExpression),
                SyntaxFactory.Argument(fontFamilyExpression)
            }));

            migrated = invocation
                .WithExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    pageExpression,
                    SyntaxFactory.IdentifierName("DrawTextFromTop")))
                .WithArgumentList(newArguments);

            return true;
        }

        private static bool TryMigrateDrawLine(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax drawLineAccess
                || drawLineAccess.Name.Identifier.ValueText != "DrawLine"
                || !TryResolveGraphicsPageExpression(drawLineAccess.Expression, graphicsPageByVariable, out var pageExpression))
            {
                return false;
            }

            var arguments = invocation.ArgumentList.Arguments;

            if (arguments.Count != 5)
            {
                return false;
            }

            if (!TryMapPdfPen(arguments[0].Expression, out var pen))
            {
                return false;
            }

            var newArguments = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
            {
                arguments[1],
                arguments[2],
                arguments[3],
                arguments[4],
                SyntaxFactory.Argument(pen.LineWidth),
                SyntaxFactory.Argument(pen.Color)
            }));

            migrated = invocation
                .WithExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    pageExpression,
                    SyntaxFactory.IdentifierName("DrawLineFromTop")))
                .WithArgumentList(newArguments);

            return true;
        }

        private static bool TryMigrateDrawRectangle(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax drawRectangleAccess
                || drawRectangleAccess.Name.Identifier.ValueText != "DrawRectangle"
                || !TryResolveGraphicsPageExpression(drawRectangleAccess.Expression, graphicsPageByVariable, out var pageExpression))
            {
                return false;
            }

            var arguments = invocation.ArgumentList.Arguments;

            if (arguments.Count != 5)
            {
                return false;
            }

            var firstArgumentExpression = arguments[0].Expression;

            if (TryMapPdfPen(firstArgumentExpression, out var pen))
            {
                migrated = invocation
                    .WithExpression(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        pageExpression,
                        SyntaxFactory.IdentifierName("DrawRectangleFromTop")))
                    .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                    {
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        arguments[4],
                        SyntaxFactory.Argument(pen.LineWidth),
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)),
                        SyntaxFactory.Argument(pen.Color)
                    })));

                return true;
            }

            if (TryMapPdfBrushColor(firstArgumentExpression, out var fillColorExpression))
            {
                migrated = invocation
                    .WithExpression(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        pageExpression,
                        SyntaxFactory.IdentifierName("DrawRectangleFromTop")))
                    .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                    {
                        arguments[1],
                        arguments[2],
                        arguments[3],
                        arguments[4],
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1))),
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)),
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                        SyntaxFactory.Argument(fillColorExpression)
                    })));

                return true;
            }

            return false;
        }

        private static bool TryMigrateDrawImage(
            InvocationExpressionSyntax invocation,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax drawImageAccess
                || drawImageAccess.Name.Identifier.ValueText != "DrawImage"
                || !TryResolveGraphicsPageExpression(drawImageAccess.Expression, graphicsPageByVariable, out var pageExpression))
            {
                return false;
            }

            var arguments = invocation.ArgumentList.Arguments;

            if (arguments.Count is not (3 or 5)
                || !TryMapImageSource(arguments[0].Expression, out var imageSourceExpression))
            {
                return false;
            }

            var mappedArguments = arguments.Count == 3
                ? new[]
                {
                    SyntaxFactory.Argument(imageSourceExpression),
                    arguments[1],
                    arguments[2]
                }
                : new[]
                {
                    SyntaxFactory.Argument(imageSourceExpression),
                    arguments[1],
                    arguments[2],
                    arguments[3],
                    arguments[4]
                };

            migrated = invocation
                .WithExpression(SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    pageExpression,
                    SyntaxFactory.IdentifierName("DrawImageFromTop")))
                .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(mappedArguments)));

            return true;
        }

        private static bool TryResolveGraphicsPageExpression(
            ExpressionSyntax graphicsExpression,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            out ExpressionSyntax pageExpression)
        {
            if (graphicsExpression is MemberAccessExpressionSyntax graphicsAccess
                && graphicsAccess.Name.Identifier.ValueText == "Graphics")
            {
                pageExpression = graphicsAccess.Expression;
                return true;
            }

            if (graphicsExpression is IdentifierNameSyntax graphicsVariable
                && graphicsPageByVariable.TryGetValue(graphicsVariable.Identifier.ValueText, out var mappedPageExpression))
            {
                pageExpression = mappedPageExpression;
                return true;
            }

            pageExpression = SyntaxFactory.IdentifierName("page");
            return false;
        }

        private static Dictionary<string, ExpressionSyntax> FindGraphicsVariables(CompilationUnitSyntax root)
        {
            var result = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);

            foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (declaration.Declaration.Type.ToString() != "PdfGraphics"
                    || declaration.Declaration.Variables.Count != 1)
                {
                    continue;
                }

                var variable = declaration.Declaration.Variables[0];

                if (variable.Initializer?.Value is MemberAccessExpressionSyntax graphicsAccess
                    && graphicsAccess.Name.Identifier.ValueText == "Graphics")
                {
                    result[variable.Identifier.ValueText] = graphicsAccess.Expression;
                }
            }

            return result;
        }

        private static HashSet<string> FindDocumentVariables(CompilationUnitSyntax root)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (declaration.Declaration.Variables.Count != 1)
                {
                    continue;
                }

                var variable = declaration.Declaration.Variables[0];

                if (variable.Initializer?.Value is ObjectCreationExpressionSyntax creation
                    && creation.Type.ToString() == "PdfDocument")
                {
                    result.Add(variable.Identifier.ValueText);
                }
            }

            return result;
        }

        private static HashSet<string> FindSavedDocumentVariables(
            CompilationUnitSyntax root,
            IReadOnlySet<string> documentVariables)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
                    && memberAccess.Name.Identifier.ValueText == "Save"
                    && memberAccess.Expression is IdentifierNameSyntax documentIdentifier
                    && documentVariables.Contains(documentIdentifier.Identifier.ValueText))
                {
                    result.Add(documentIdentifier.Identifier.ValueText);
                }
            }

            return result;
        }

        private static HashSet<string> FindRemovableGraphicsVariables(
            CompilationUnitSyntax root,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            IReadOnlyDictionary<string, PdfStringFormatMigrationInfo> stringFormatsByVariable)
        {
            var removable = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (variableName, _) in graphicsPageByVariable)
            {
                var invocations = root
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess
                        && memberAccess.Expression is IdentifierNameSyntax identifier
                        && identifier.Identifier.ValueText == variableName)
                    .ToArray();

                if (invocations.Length > 0
                    && invocations.All(invocation => TryMigrateGraphicsInvocation(invocation, graphicsPageByVariable, stringFormatsByVariable, out _, out _, out _)))
                {
                    removable.Add(variableName);
                }
            }

            return removable;
        }

        private static Dictionary<string, PdfStringFormatMigrationInfo> FindStringFormatVariables(CompilationUnitSyntax root)
        {
            var result = new Dictionary<string, PdfStringFormatMigrationInfo>(StringComparer.Ordinal);

            foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (declaration.Declaration.Variables.Count != 1)
                {
                    continue;
                }

                var variable = declaration.Declaration.Variables[0];

                if (variable.Initializer?.Value is not ObjectCreationExpressionSyntax creation
                    || creation.Type.ToString() != "PdfStringFormat"
                    || !TryResolveInlineStringFormat(creation, out var info))
                {
                    continue;
                }

                if (info is null)
                {
                    continue;
                }

                result[variable.Identifier.ValueText] = info;
            }

            return result;
        }

        private static HashSet<string> FindRemovableStringFormatVariables(
            CompilationUnitSyntax root,
            IReadOnlyDictionary<string, ExpressionSyntax> graphicsPageByVariable,
            IReadOnlyDictionary<string, PdfStringFormatMigrationInfo> stringFormatsByVariable)
        {
            var removable = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (variableName, _) in stringFormatsByVariable)
            {
                var invocations = root
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => invocation.ArgumentList.Arguments.Any(argument =>
                        argument.Expression is IdentifierNameSyntax identifier
                        && identifier.Identifier.ValueText == variableName))
                    .ToArray();

                if (invocations.Length > 0
                    && invocations.All(invocation => TryMigrateDrawTextBox(invocation, graphicsPageByVariable, stringFormatsByVariable, out _)))
                {
                    removable.Add(variableName);
                }
            }

            return removable;
        }

        private static bool TryMapStandardFont(ExpressionSyntax expression, out ExpressionSyntax fontSizeExpression, out ExpressionSyntax fontFamilyExpression)
        {
            fontSizeExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(12));
            fontFamilyExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfFontFamily"),
                SyntaxFactory.IdentifierName("Helvetica"));

            if (expression is not ObjectCreationExpressionSyntax creation
                || creation.Type.ToString() != "PdfStandardFont"
                || creation.ArgumentList?.Arguments.Count != 2)
            {
                return false;
            }

            var familyName = creation.ArgumentList.Arguments[0].Expression.ToString().Split('.').Last();
            var mappedFamilyName = familyName switch
            {
                "Helvetica" => "Helvetica",
                "Courier" => "Courier",
                "TimesRoman" or "Times" => "Times",
                _ => null
            };

            if (mappedFamilyName is null)
            {
                return false;
            }

            fontSizeExpression = creation.ArgumentList.Arguments[1].Expression;
            fontFamilyExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfFontFamily"),
                SyntaxFactory.IdentifierName(mappedFamilyName));
            return true;
        }

        private static bool TryMapPdfPen(ExpressionSyntax expression, out PdfPenMigrationInfo pen)
        {
            pen = new PdfPenMigrationInfo(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("PdfColor"),
                    SyntaxFactory.IdentifierName("Black")),
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)));

            if (TryMapStaticColor(expression, "PdfPens", out var staticPenColor))
            {
                pen = pen with { Color = staticPenColor };
                return true;
            }

            if (expression is ObjectCreationExpressionSyntax creation
                && creation.Type.ToString() == "PdfPen"
                && creation.ArgumentList?.Arguments.Count == 2
                && TryMapColorFromArgb(creation.ArgumentList.Arguments[0].Expression, out var colorExpression))
            {
                pen = new PdfPenMigrationInfo(colorExpression, creation.ArgumentList.Arguments[1].Expression);
                return true;
            }

            return false;
        }

        private static bool TryMapPdfBrushColor(ExpressionSyntax expression, out ExpressionSyntax colorExpression)
        {
            if (TryMapStaticColor(expression, "PdfBrushes", out colorExpression))
            {
                return true;
            }

            if (expression is ObjectCreationExpressionSyntax creation
                && creation.Type.ToString() == "PdfSolidBrush"
                && creation.ArgumentList?.Arguments.Count == 1
                && TryMapColorFromArgb(creation.ArgumentList.Arguments[0].Expression, out colorExpression))
            {
                return true;
            }

            return false;
        }

        private static bool TryMapStaticColor(ExpressionSyntax expression, string ownerTypeName, out ExpressionSyntax colorExpression)
        {
            colorExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfColor"),
                SyntaxFactory.IdentifierName("Black"));

            if (expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Expression.ToString() != ownerTypeName)
            {
                return false;
            }

            var colorName = memberAccess.Name.Identifier.ValueText switch
            {
                "Black" => "Black",
                "White" => "White",
                "Gray" => "Gray",
                "Red" => "RedColor",
                "Green" => "GreenColor",
                "Blue" => "BlueColor",
                _ => null
            };

            if (colorName is null)
            {
                return false;
            }

            colorExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfColor"),
                SyntaxFactory.IdentifierName(colorName));
            return true;
        }

        private static bool TryMapColorFromArgb(ExpressionSyntax expression, out ExpressionSyntax colorExpression)
        {
            colorExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfColor"),
                SyntaxFactory.IdentifierName("Black"));

            if (expression is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Expression.ToString() != "Color"
                || memberAccess.Name.Identifier.ValueText != "FromArgb"
                || invocation.ArgumentList.Arguments.Count != 3)
            {
                return false;
            }

            colorExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("PdfColor"),
                    SyntaxFactory.IdentifierName("FromRgb")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(invocation.ArgumentList.Arguments)));
            return true;
        }

        private static bool TryMapImageSource(ExpressionSyntax expression, out ExpressionSyntax imageSourceExpression)
        {
            imageSourceExpression = expression;

            if (expression is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression.ToString() == "PdfImage"
                && memberAccess.Name.Identifier.ValueText == "FromFile"
                && invocation.ArgumentList.Arguments.Count == 1)
            {
                imageSourceExpression = invocation.ArgumentList.Arguments[0].Expression;
                return true;
            }

            if (expression is ObjectCreationExpressionSyntax creation
                && creation.Type.ToString() == "PdfBitmap"
                && creation.ArgumentList?.Arguments.Count == 1)
            {
                imageSourceExpression = creation.ArgumentList.Arguments[0].Expression;
                return true;
            }

            return false;
        }

        private static bool TryGetRectangleArguments(ExpressionSyntax expression, out RectangleArguments rectangleArguments)
        {
            rectangleArguments = new RectangleArguments(
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)),
                SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)));

            if (expression is not ObjectCreationExpressionSyntax creation
                || creation.Type.ToString() != "RectangleF"
                || creation.ArgumentList?.Arguments.Count != 4)
            {
                return false;
            }

            rectangleArguments = new RectangleArguments(
                creation.ArgumentList.Arguments[0].Expression,
                creation.ArgumentList.Arguments[1].Expression,
                creation.ArgumentList.Arguments[2].Expression,
                creation.ArgumentList.Arguments[3].Expression);
            return true;
        }

        private static bool IsRectangleCreation(ExpressionSyntax expression)
        {
            return expression is ObjectCreationExpressionSyntax creation
                && creation.Type.ToString() == "RectangleF"
                && creation.ArgumentList?.Arguments.Count == 4;
        }

        private static bool IsPdfStringFormatExpression(ExpressionSyntax expression)
        {
            return expression is ObjectCreationExpressionSyntax { Type: { } type }
                && type.ToString() == "PdfStringFormat";
        }

        private static bool TryResolveStringFormat(
            ExpressionSyntax expression,
            IReadOnlyDictionary<string, PdfStringFormatMigrationInfo> stringFormatsByVariable,
            out PdfStringFormatMigrationInfo? info)
        {
            if (expression is ObjectCreationExpressionSyntax creation)
            {
                return TryResolveInlineStringFormat(creation, out info);
            }

            if (expression is IdentifierNameSyntax identifier
                && stringFormatsByVariable.TryGetValue(identifier.Identifier.ValueText, out var mappedInfo))
            {
                info = mappedInfo;
                return true;
            }

            info = null;
            return false;
        }

        private static bool TryResolveInlineStringFormat(ObjectCreationExpressionSyntax creation, out PdfStringFormatMigrationInfo? info)
        {
            info = null;

            if (creation.Type.ToString() != "PdfStringFormat")
            {
                return false;
            }

            ExpressionSyntax? alignment = null;
            ExpressionSyntax? verticalAlignment = null;

            foreach (var expression in creation.Initializer?.Expressions ?? default)
            {
                if (expression is not AssignmentExpressionSyntax assignment
                    || assignment.Left is not IdentifierNameSyntax property)
                {
                    return false;
                }

                switch (property.Identifier.ValueText)
                {
                    case "Alignment":
                        if (!TryMapTextAlignment(assignment.Right, out alignment))
                        {
                            return false;
                        }

                        break;
                    case "LineAlignment":
                        if (!TryMapVerticalAlignment(assignment.Right, out verticalAlignment))
                        {
                            return false;
                        }

                        break;
                    default:
                        return false;
                }
            }

            info = new PdfStringFormatMigrationInfo(alignment, verticalAlignment);
            return true;
        }

        private static ObjectCreationExpressionSyntax CreateTextBoxOptions(
            ExpressionSyntax fontFamilyExpression,
            ExpressionSyntax fontSizeExpression,
            ExpressionSyntax fillColorExpression,
            PdfStringFormatMigrationInfo? stringFormat)
        {
            var assignments = new List<ExpressionSyntax>
            {
                CreateOptionAssignment("FontFamily", fontFamilyExpression),
                CreateOptionAssignment("FontSize", fontSizeExpression),
                CreateOptionAssignment("FillColor", fillColorExpression)
            };

            if (stringFormat?.Alignment is { } alignment)
            {
                assignments.Add(CreateOptionAssignment("Alignment", alignment));
            }

            if (stringFormat?.VerticalAlignment is { } verticalAlignment)
            {
                assignments.Add(CreateOptionAssignment("VerticalAlignment", verticalAlignment));
            }

            return SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName("PdfTextBoxOptions"))
                .WithInitializer(SyntaxFactory.InitializerExpression(
                    SyntaxKind.ObjectInitializerExpression,
                    SyntaxFactory.SeparatedList(assignments)));
        }

        private static AssignmentExpressionSyntax CreateOptionAssignment(string propertyName, ExpressionSyntax value)
        {
            return SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(propertyName),
                value);
        }

        private static bool TryMapTextAlignment(ExpressionSyntax expression, out ExpressionSyntax alignmentExpression)
        {
            alignmentExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfTextAlignment"),
                SyntaxFactory.IdentifierName("Left"));

            var alignmentName = expression.ToString().Split('.').Last() switch
            {
                "Left" => "Left",
                "Center" => "Center",
                "Right" => "Right",
                "Justify" => "Justify",
                _ => null
            };

            if (alignmentName is null)
            {
                return false;
            }

            alignmentExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfTextAlignment"),
                SyntaxFactory.IdentifierName(alignmentName));
            return true;
        }

        private static bool TryMapVerticalAlignment(ExpressionSyntax expression, out ExpressionSyntax verticalAlignmentExpression)
        {
            verticalAlignmentExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfVerticalAlignment"),
                SyntaxFactory.IdentifierName("Top"));

            var alignmentName = expression.ToString().Split('.').Last() switch
            {
                "Top" => "Top",
                "Middle" => "Middle",
                "Bottom" => "Bottom",
                _ => null
            };

            if (alignmentName is null)
            {
                return false;
            }

            verticalAlignmentExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PdfVerticalAlignment"),
                SyntaxFactory.IdentifierName(alignmentName));
            return true;
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

        private sealed record RectangleArguments(
            ExpressionSyntax X,
            ExpressionSyntax Y,
            ExpressionSyntax Width,
            ExpressionSyntax Height);

        private sealed record PdfPenMigrationInfo(
            ExpressionSyntax Color,
            ExpressionSyntax LineWidth);

        private sealed record PdfStringFormatMigrationInfo(
            ExpressionSyntax? Alignment,
            ExpressionSyntax? VerticalAlignment);
    }
}
