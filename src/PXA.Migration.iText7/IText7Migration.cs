using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.iText7;

public sealed class IText7Migration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new IText7SyntaxRewriter(root);
        var migratedRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        migratedRoot = EnsureCanvasUsing(RemoveITextUsings(migratedRoot));

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

    private static CompilationUnitSyntax RemoveITextUsings(CompilationUnitSyntax root)
    {
        var usings = root.Usings
            .Where(static directive => directive.Name?.ToString().StartsWith("iText.", StringComparison.Ordinal) != true)
            .ToArray();

        return root.WithUsings(SyntaxFactory.List(usings));
    }

    private static CompilationUnitSyntax EnsureCanvasUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static directive => directive.Name?.ToString() == "PXA.Pdf"))
        {
            return root;
        }

        var canvasUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("PXA.Pdf"));
        return root.WithUsings(root.Usings.Insert(0, canvasUsing));
    }

    private sealed class IText7SyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly List<MigrationDiagnostic> _diagnostics = new();
        private readonly Dictionary<string, ExpressionSyntax> _writerTargetByVariable;
        private readonly Dictionary<string, string> _pdfWriterByVariable;
        private readonly Dictionary<string, ITextDocumentInfo> _documentInfoByVariable;
        private readonly HashSet<string> _pdfCanvasVariables;
        private readonly HashSet<string> _removablePdfCanvasVariables;

        public IText7SyntaxRewriter(CompilationUnitSyntax root)
        {
            _writerTargetByVariable = FindWriterTargets(root);
            _pdfWriterByVariable = FindPdfDocuments(root, _writerTargetByVariable);
            _documentInfoByVariable = FindLayoutDocuments(root, _pdfWriterByVariable, _writerTargetByVariable);
            _pdfCanvasVariables = FindPdfCanvasVariables(root);
            _removablePdfCanvasVariables = FindRemovablePdfCanvasVariables(root, _pdfCanvasVariables);
            AddUnsupportedFeatureDiagnostics(root);
        }

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var visited = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
            var members = new List<MemberDeclarationSyntax>();
            var insertedPages = new HashSet<string>(StringComparer.Ordinal);
            var rewrittenMembers = RewriteSeparatedPdfCanvasTextState(visited.Members);

            foreach (var member in rewrittenMembers)
            {
                members.Add(member);

                if (member is GlobalStatementSyntax
                    {
                        Statement: LocalDeclarationStatementSyntax localDeclaration
                    }
                    && TryGetOnlyVariableName(localDeclaration, out var variableName)
                    && _documentInfoByVariable.TryGetValue(variableName, out var documentInfo)
                    && insertedPages.Add(variableName))
                {
                    members.Add(SyntaxFactory.GlobalStatement(CreatePageDeclaration(variableName, documentInfo)));
                }
            }

            foreach (var (documentVariableName, documentInfo) in _documentInfoByVariable)
            {
                members.Add(SyntaxFactory.GlobalStatement(CreateSaveStatement(documentVariableName, documentInfo.SaveTarget)));
                _diagnostics.Add(Info("CANMIGITEXT007", "iText7 PdfWriter target was migrated to Canvas document.Save(...)."));
            }

            return visited.WithMembers(SyntaxFactory.List(members));
        }

        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            var visited = (BlockSyntax)base.VisitBlock(node)!;
            return visited.WithStatements(RewriteSeparatedPdfCanvasTextState(visited.Statements));
        }

        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            if (node.Statement is LocalDeclarationStatementSyntax localDeclaration)
            {
                if (ShouldRemovePdfCanvasDeclaration(localDeclaration))
                {
                    return null;
                }

                if (ShouldRemoveWriterOrPdfDeclaration(localDeclaration))
                {
                    return null;
                }

                if (TryMigrateDocumentDeclaration(localDeclaration, out var migratedDeclaration, out var pageVariableName))
                {
                    return SyntaxFactory.GlobalStatement(migratedDeclaration);
                }
            }

            if (node.Statement is ExpressionStatementSyntax exprStmt &&
                exprStmt.Expression is InvocationExpressionSyntax exprInv &&
                exprInv.Expression is MemberAccessExpressionSyntax exprMa &&
                exprMa.Expression is IdentifierNameSyntax docIdent &&
                _documentInfoByVariable.ContainsKey(docIdent.Identifier.ValueText))
            {
                var method = exprMa.Name.Identifier.ValueText;
                if (method == "Close")
                {
                    _diagnostics.Add(Info("CANMIGITEXT016", "document.Close() removed — PXA.Pdf document does not require explicit closing."));
                    return null;
                }
                if (method == "SetMargins")
                {
                    _diagnostics.Add(Info("CANMIGITEXT017", "document.SetMargins() removed — configure page margins via PXA.Pdf page layout options."));
                    return null;
                }
            }

            return base.VisitGlobalStatement(node);
        }

        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            if (node.Parent is GlobalStatementSyntax)
            {
                return base.VisitLocalDeclarationStatement(node);
            }

            if (ShouldRemovePdfCanvasDeclaration(node))
            {
                return null;
            }

            if (ShouldRemoveWriterOrPdfDeclaration(node))
            {
                return null;
            }

            if (TryMigrateDocumentDeclaration(node, out var migratedDeclaration, out _))
            {
                return migratedDeclaration;
            }

            return base.VisitLocalDeclarationStatement(node);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

            if (TryMigrateShowTextAligned(visited, out var alignedTextMigration))
            {
                _diagnostics.Add(Info("CANMIGITEXT009", "Simple iText7 ShowTextAligned call was migrated to Canvas DrawText."));
                return alignedTextMigration;
            }

            if (TryMigratePdfCanvasInvocation(visited, out var canvasMigration, out var canvasDiagnosticId, out var canvasDiagnosticMessage))
            {
                _diagnostics.Add(Info(canvasDiagnosticId, canvasDiagnosticMessage));
                return canvasMigration;
            }

            if (TryMigrateParagraphAdd(visited, out var migrated))
            {
                _diagnostics.Add(Info("CANMIGITEXT003", "Simple iText7 Paragraph addition was migrated to Canvas DrawTextFromTop."));
                return migrated;
            }

            if (TryCreateUnsupportedInvocationDiagnostic(visited, out var diagnostic))
            {
                _diagnostics.Add(diagnostic);
            }

            return visited;
        }

        private void AddUnsupportedFeatureDiagnostics(CompilationUnitSyntax root)
        {
            var identifierNames = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(static identifier => identifier.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);

            if (identifierNames.Contains("Table"))
            {
                _diagnostics.Add(Warning("CANMIGITEXT005", "iText7 Table usage requires manual Canvas table migration."));
            }

            if (identifierNames.Overlaps(new[]
                {
                    "PdfSigner",
                    "PdfSignatureAppearance",
                    "WriterProperties",
                    "EncryptionConstants",
                    "PdfAcroForm",
                    "PdfFormField",
                    "PdfMerger",
                    "PdfDocumentInfo"
                }))
            {
                _diagnostics.Add(Warning("CANMIGITEXT006", "iText7 signatures, encryption, forms, metadata, or existing-PDF processing are outside the v1 migration scope."));
            }
        }

        private bool ShouldRemoveWriterOrPdfDeclaration(LocalDeclarationStatementSyntax declaration)
        {
            if (!TryGetOnlyVariableName(declaration, out var variableName))
            {
                return false;
            }

            if (_writerTargetByVariable.ContainsKey(variableName))
            {
                _diagnostics.Add(Info("CANMIGITEXT001", "iText7 PdfWriter construction was folded into Canvas document.Save(...)."));
                return true;
            }

            if (_pdfWriterByVariable.ContainsKey(variableName))
            {
                _diagnostics.Add(Info("CANMIGITEXT002", "iText7 kernel PdfDocument construction was folded into Canvas PdfDocument."));
                return true;
            }

            return false;
        }

        private bool ShouldRemovePdfCanvasDeclaration(LocalDeclarationStatementSyntax declaration)
        {
            if (!TryGetOnlyVariableName(declaration, out var variableName)
                || !_removablePdfCanvasVariables.Contains(variableName))
            {
                return false;
            }

            _diagnostics.Add(Info("CANMIGITEXT013", "Supported iText7 PdfCanvas variable was removed after its drawing calls were migrated."));
            return true;
        }

        private bool TryMigrateDocumentDeclaration(
            LocalDeclarationStatementSyntax declaration,
            out LocalDeclarationStatementSyntax migrated,
            out string pageVariableName)
        {
            migrated = declaration;
            pageVariableName = "page";

            if (!TryGetOnlyVariableName(declaration, out var variableName)
                || !_documentInfoByVariable.TryGetValue(variableName, out var documentInfo))
            {
                return false;
            }

            pageVariableName = documentInfo.PageVariableName;
            _diagnostics.Add(Info("CANMIGITEXT004", "iText7 layout Document construction was migrated to Canvas PdfDocument."));
            if (documentInfo.PageSize is not null)
            {
                _diagnostics.Add(Info("CANMIGITEXT008", "iText7 PageSize was migrated to Canvas PdfPagePreset."));
            }

            var variable = declaration.Declaration.Variables[0]
                .WithInitializer(SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName("PdfDocument"))
                        .WithArgumentList(SyntaxFactory.ArgumentList())));

            return true.WithMigratedDeclaration(declaration, variable, out migrated);
        }

        private bool TryMigrateParagraphAdd(
            InvocationExpressionSyntax invocation,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name.Identifier.ValueText != "Add"
                || memberAccess.Expression is not IdentifierNameSyntax documentIdentifier
                || !_documentInfoByVariable.TryGetValue(documentIdentifier.Identifier.ValueText, out var documentInfo)
                || invocation.ArgumentList.Arguments.Count != 1
                || !TryExtractParagraphInfo(invocation.ArgumentList.Arguments[0].Expression,
                    out var textExpression, out var fontSizeExpression, out var hasFontSize))
            {
                return false;
            }

            if (hasFontSize)
                _diagnostics.Add(Info("CANMIGITEXT018", "Paragraph.SetFontSize(N) mapped to DrawTextFromTop fontSize argument."));

            migrated = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(documentInfo.PageVariableName),
                    SyntaxFactory.IdentifierName("DrawTextFromTop")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(textExpression),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(40))),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(40))),
                    SyntaxFactory.Argument(fontSizeExpression)
                })));
            return true;
        }

        private bool TryMigrateShowTextAligned(
            InvocationExpressionSyntax invocation,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name.Identifier.ValueText != "ShowTextAligned"
                || memberAccess.Expression is not IdentifierNameSyntax documentIdentifier
                || !_documentInfoByVariable.TryGetValue(documentIdentifier.Identifier.ValueText, out var documentInfo)
                || invocation.ArgumentList.Arguments.Count < 4
                || !TryExtractParagraphInfo(invocation.ArgumentList.Arguments[0].Expression,
                    out var textExpression, out var fontSizeExpression, out var hasFontSize)
                || !IsLeftTextAlignment(invocation.ArgumentList.Arguments[3].Expression))
            {
                return false;
            }

            if (hasFontSize)
                _diagnostics.Add(Info("CANMIGITEXT018", "Paragraph.SetFontSize(N) mapped to DrawText fontSize argument."));

            migrated = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(documentInfo.PageVariableName),
                    SyntaxFactory.IdentifierName("DrawText")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(textExpression),
                    invocation.ArgumentList.Arguments[1],
                    invocation.ArgumentList.Arguments[2],
                    SyntaxFactory.Argument(fontSizeExpression)
                })));
            return true;
        }

        private bool TryMigratePdfCanvasInvocation(
            InvocationExpressionSyntax invocation,
            out InvocationExpressionSyntax migrated,
            out string diagnosticId,
            out string diagnosticMessage)
        {
            diagnosticId = string.Empty;
            diagnosticMessage = string.Empty;

            if (TryMigratePdfCanvasLine(invocation, _pdfCanvasVariables, out migrated))
            {
                diagnosticId = "CANMIGITEXT011";
                diagnosticMessage = "Simple iText7 PdfCanvas line was migrated to Canvas DrawLine.";
                return true;
            }

            if (TryMigratePdfCanvasRectangle(invocation, _pdfCanvasVariables, out migrated, out var fill))
            {
                diagnosticId = "CANMIGITEXT012";
                diagnosticMessage = fill
                    ? "Simple iText7 PdfCanvas filled rectangle was migrated to Canvas DrawRectangle."
                    : "Simple iText7 PdfCanvas stroked rectangle was migrated to Canvas DrawRectangle.";
                return true;
            }

            if (TryMigratePdfCanvasText(invocation, _pdfCanvasVariables, out migrated))
            {
                diagnosticId = "CANMIGITEXT014";
                diagnosticMessage = "Simple iText7 PdfCanvas text chain was migrated to Canvas DrawText.";
                return true;
            }

            migrated = invocation;
            return false;
        }

        private static bool TryMigratePdfCanvasLine(
            InvocationExpressionSyntax invocation,
            IReadOnlySet<string> pdfCanvasVariables,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax strokeAccess
                || strokeAccess.Name.Identifier.ValueText != "Stroke"
                || strokeAccess.Expression is not InvocationExpressionSyntax lineToInvocation
                || lineToInvocation.Expression is not MemberAccessExpressionSyntax lineToAccess
                || lineToAccess.Name.Identifier.ValueText != "LineTo"
                || lineToInvocation.ArgumentList.Arguments.Count != 2
                || lineToAccess.Expression is not InvocationExpressionSyntax moveToInvocation
                || moveToInvocation.Expression is not MemberAccessExpressionSyntax moveToAccess
                || moveToAccess.Name.Identifier.ValueText != "MoveTo"
                || moveToInvocation.ArgumentList.Arguments.Count != 2
                || moveToAccess.Expression is not IdentifierNameSyntax canvasIdentifier
                || !pdfCanvasVariables.Contains(canvasIdentifier.Identifier.ValueText))
            {
                return false;
            }

            migrated = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("page"),
                    SyntaxFactory.IdentifierName("DrawLine")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    moveToInvocation.ArgumentList.Arguments[0],
                    moveToInvocation.ArgumentList.Arguments[1],
                    lineToInvocation.ArgumentList.Arguments[0],
                    lineToInvocation.ArgumentList.Arguments[1],
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)))
                })));
            return true;
        }

        private static bool TryMigratePdfCanvasRectangle(
            InvocationExpressionSyntax invocation,
            IReadOnlySet<string> pdfCanvasVariables,
            out InvocationExpressionSyntax migrated,
            out bool fill)
        {
            migrated = invocation;
            fill = false;

            if (invocation.Expression is not MemberAccessExpressionSyntax terminalAccess
                || terminalAccess.Name.Identifier.ValueText is not "Stroke" and not "Fill"
                || terminalAccess.Expression is not InvocationExpressionSyntax rectangleInvocation
                || rectangleInvocation.Expression is not MemberAccessExpressionSyntax rectangleAccess
                || rectangleAccess.Name.Identifier.ValueText != "Rectangle"
                || rectangleInvocation.ArgumentList.Arguments.Count != 4
                || rectangleAccess.Expression is not IdentifierNameSyntax canvasIdentifier
                || !pdfCanvasVariables.Contains(canvasIdentifier.Identifier.ValueText))
            {
                return false;
            }

            fill = terminalAccess.Name.Identifier.ValueText == "Fill";
            migrated = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("page"),
                    SyntaxFactory.IdentifierName("DrawRectangle")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    rectangleInvocation.ArgumentList.Arguments[0],
                    rectangleInvocation.ArgumentList.Arguments[1],
                    rectangleInvocation.ArgumentList.Arguments[2],
                    rectangleInvocation.ArgumentList.Arguments[3],
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1))),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        fill
                            ? SyntaxKind.TrueLiteralExpression
                            : SyntaxKind.FalseLiteralExpression))
                })));
            return true;
        }

        private static bool TryMigratePdfCanvasText(
            InvocationExpressionSyntax invocation,
            IReadOnlySet<string> pdfCanvasVariables,
            out InvocationExpressionSyntax migrated)
        {
            migrated = invocation;

            if (invocation.Expression is not MemberAccessExpressionSyntax endTextAccess
                || endTextAccess.Name.Identifier.ValueText != "EndText"
                || endTextAccess.Expression is not InvocationExpressionSyntax showTextInvocation
                || showTextInvocation.Expression is not MemberAccessExpressionSyntax showTextAccess
                || showTextAccess.Name.Identifier.ValueText != "ShowText"
                || showTextInvocation.ArgumentList.Arguments.Count != 1
                || showTextAccess.Expression is not InvocationExpressionSyntax moveTextInvocation
                || moveTextInvocation.Expression is not MemberAccessExpressionSyntax moveTextAccess
                || moveTextAccess.Name.Identifier.ValueText != "MoveText"
                || moveTextInvocation.ArgumentList.Arguments.Count != 2
                || moveTextAccess.Expression is not InvocationExpressionSyntax beginTextInvocation
                || beginTextInvocation.Expression is not MemberAccessExpressionSyntax beginTextAccess
                || beginTextAccess.Name.Identifier.ValueText != "BeginText"
                || beginTextInvocation.ArgumentList.Arguments.Count != 0
                || beginTextAccess.Expression is not IdentifierNameSyntax canvasIdentifier
                || !pdfCanvasVariables.Contains(canvasIdentifier.Identifier.ValueText))
            {
                return false;
            }

            migrated = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("page"),
                    SyntaxFactory.IdentifierName("DrawText")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    showTextInvocation.ArgumentList.Arguments[0],
                    moveTextInvocation.ArgumentList.Arguments[0],
                    moveTextInvocation.ArgumentList.Arguments[1],
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(12)))
            })));
            return true;
        }

        private SyntaxList<MemberDeclarationSyntax> RewriteSeparatedPdfCanvasTextState(SyntaxList<MemberDeclarationSyntax> members)
        {
            var rewritten = new List<MemberDeclarationSyntax>();

            for (var index = 0; index < members.Count; index++)
            {
                if (TryMatchSeparatedPdfCanvasTextState(members, index, _pdfCanvasVariables, out var migrated, out var consumed))
                {
                    rewritten.Add(SyntaxFactory.GlobalStatement(migrated));
                    _diagnostics.Add(Info("CANMIGITEXT015", "Separated iText7 PdfCanvas text state statements were migrated to Canvas DrawText."));
                    index += consumed - 1;
                    continue;
                }

                rewritten.Add(members[index]);
            }

            return SyntaxFactory.List(rewritten);
        }

        private SyntaxList<StatementSyntax> RewriteSeparatedPdfCanvasTextState(SyntaxList<StatementSyntax> statements)
        {
            var rewritten = new List<StatementSyntax>();

            for (var index = 0; index < statements.Count; index++)
            {
                if (TryMatchSeparatedPdfCanvasTextState(statements, index, _pdfCanvasVariables, out var migrated, out var consumed))
                {
                    rewritten.Add(migrated);
                    _diagnostics.Add(Info("CANMIGITEXT015", "Separated iText7 PdfCanvas text state statements were migrated to Canvas DrawText."));
                    index += consumed - 1;
                    continue;
                }

                rewritten.Add(statements[index]);
            }

            return SyntaxFactory.List(rewritten);
        }

        private static bool TryMatchSeparatedPdfCanvasTextState(
            SyntaxList<MemberDeclarationSyntax> members,
            int startIndex,
            IReadOnlySet<string> pdfCanvasVariables,
            out ExpressionStatementSyntax migrated,
            out int consumed)
        {
            migrated = SyntaxFactory.ExpressionStatement(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
            consumed = 0;

            if (startIndex + 3 >= members.Count
                || members[startIndex] is not GlobalStatementSyntax beginStatement
                || members[startIndex + 1] is not GlobalStatementSyntax moveStatement
                || members[startIndex + 2] is not GlobalStatementSyntax showStatement
                || members[startIndex + 3] is not GlobalStatementSyntax endStatement)
            {
                return false;
            }

            return TryMatchSeparatedPdfCanvasTextState(
                beginStatement.Statement,
                moveStatement.Statement,
                showStatement.Statement,
                endStatement.Statement,
                pdfCanvasVariables,
                out migrated,
                out consumed);
        }

        private static bool TryMatchSeparatedPdfCanvasTextState(
            SyntaxList<StatementSyntax> statements,
            int startIndex,
            IReadOnlySet<string> pdfCanvasVariables,
            out ExpressionStatementSyntax migrated,
            out int consumed)
        {
            migrated = SyntaxFactory.ExpressionStatement(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
            consumed = 0;

            if (startIndex + 3 >= statements.Count)
            {
                return false;
            }

            return TryMatchSeparatedPdfCanvasTextState(
                statements[startIndex],
                statements[startIndex + 1],
                statements[startIndex + 2],
                statements[startIndex + 3],
                pdfCanvasVariables,
                out migrated,
                out consumed);
        }

        private static bool TryMatchSeparatedPdfCanvasTextState(
            StatementSyntax beginStatement,
            StatementSyntax moveStatement,
            StatementSyntax showStatement,
            StatementSyntax endStatement,
            IReadOnlySet<string> pdfCanvasVariables,
            out ExpressionStatementSyntax migrated,
            out int consumed)
        {
            migrated = SyntaxFactory.ExpressionStatement(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
            consumed = 0;

            if (!TryGetCanvasInvocation(beginStatement, "BeginText", out var canvasName, out var beginInvocation)
                || beginInvocation.ArgumentList.Arguments.Count != 0
                || !TryGetCanvasInvocation(moveStatement, "MoveText", out var moveCanvasName, out var moveInvocation)
                || moveCanvasName != canvasName
                || moveInvocation.ArgumentList.Arguments.Count != 2
                || !TryGetCanvasInvocation(showStatement, "ShowText", out var showCanvasName, out var showInvocation)
                || showCanvasName != canvasName
                || showInvocation.ArgumentList.Arguments.Count != 1
                || !TryGetCanvasInvocation(endStatement, "EndText", out var endCanvasName, out var endInvocation)
                || endCanvasName != canvasName
                || endInvocation.ArgumentList.Arguments.Count != 0
                || !pdfCanvasVariables.Contains(canvasName))
            {
                return false;
            }

            migrated = SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("page"),
                    SyntaxFactory.IdentifierName("DrawText")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    showInvocation.ArgumentList.Arguments[0],
                    moveInvocation.ArgumentList.Arguments[0],
                    moveInvocation.ArgumentList.Arguments[1],
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(12)))
                }))));
            consumed = 4;
            return true;
        }

        private static bool TryGetCanvasInvocation(
            StatementSyntax statement,
            string methodName,
            out string canvasName,
            out InvocationExpressionSyntax invocation)
        {
            canvasName = string.Empty;
            invocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName("missing"));

            if (statement is not ExpressionStatementSyntax expressionStatement
                || expressionStatement.Expression is not InvocationExpressionSyntax candidate
                || candidate.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Name.Identifier.ValueText != methodName
                || memberAccess.Expression is not IdentifierNameSyntax canvasIdentifier)
            {
                return false;
            }

            canvasName = canvasIdentifier.Identifier.ValueText;
            invocation = candidate;
            return true;
        }

        private static bool TryCreateUnsupportedInvocationDiagnostic(
            InvocationExpressionSyntax invocation,
            out MigrationDiagnostic diagnostic)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name.Identifier.ValueText == "ShowTextAligned"
                && invocation.ArgumentList.Arguments.Count >= 4
                && IsCenterOrRightTextAlignment(invocation.ArgumentList.Arguments[3].Expression))
            {
                diagnostic = Warning("CANMIGITEXT010", "iText7 ShowTextAligned center/right anchor alignment needs manual Canvas positioning review.");
                return true;
            }

            diagnostic = Info("CANMIGITEXT000", "No migration was applied.");
            return false;
        }

        private static bool TryExtractParagraphInfo(
            ExpressionSyntax expression,
            out ExpressionSyntax textExpression,
            out ExpressionSyntax fontSizeExpression,
            out bool hasFontSize)
        {
            textExpression = expression;
            fontSizeExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(12));
            hasFontSize = false;

            // Unwrap fluent chain: new Paragraph("text").SetFontSize(N).SetBold()...
            var current = expression;
            ExpressionSyntax? extractedFontSize = null;

            while (current is InvocationExpressionSyntax chainInv &&
                   chainInv.Expression is MemberAccessExpressionSyntax chainAccess)
            {
                if (chainAccess.Name.Identifier.ValueText == "SetFontSize" &&
                    chainInv.ArgumentList.Arguments.Count == 1)
                {
                    extractedFontSize = chainInv.ArgumentList.Arguments[0].Expression;
                }
                current = chainAccess.Expression;
            }

            if (current is not ObjectCreationExpressionSyntax paragraphCreation
                || paragraphCreation.Type.ToString() != "Paragraph"
                || paragraphCreation.ArgumentList?.Arguments.Count != 1)
            {
                return false;
            }

            textExpression = paragraphCreation.ArgumentList.Arguments[0].Expression;
            if (extractedFontSize is not null)
            {
                fontSizeExpression = extractedFontSize;
                hasFontSize = true;
            }
            return true;
        }

        private static bool IsLeftTextAlignment(ExpressionSyntax expression)
        {
            return expression.ToString().Split('.').Last() == "LEFT";
        }

        private static bool IsCenterOrRightTextAlignment(ExpressionSyntax expression)
        {
            return expression.ToString().Split('.').Last() is "CENTER" or "RIGHT";
        }

        private static Dictionary<string, ExpressionSyntax> FindWriterTargets(CompilationUnitSyntax root)
        {
            var result = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);

            foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (!TryGetOnlyVariableName(declaration, out var variableName)
                    || declaration.Declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation
                    || creation.Type.ToString() != "PdfWriter"
                    || creation.ArgumentList?.Arguments.Count != 1)
                {
                    continue;
                }

                result[variableName] = creation.ArgumentList.Arguments[0].Expression;
            }

            return result;
        }

        private static HashSet<string> FindPdfCanvasVariables(CompilationUnitSyntax root)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (!TryGetOnlyVariableName(declaration, out var variableName)
                    || declaration.Declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation
                    || creation.Type.ToString() != "PdfCanvas")
                {
                    continue;
                }

                result.Add(variableName);
            }

            return result;
        }

        private static HashSet<string> FindRemovablePdfCanvasVariables(
            CompilationUnitSyntax root,
            IReadOnlySet<string> pdfCanvasVariables)
        {
            var removable = new HashSet<string>(StringComparer.Ordinal);

            foreach (var variableName in pdfCanvasVariables)
            {
                var statements = root
                    .DescendantNodes()
                    .OfType<ExpressionStatementSyntax>()
                    .Where(statement => ContainsIdentifier(statement, variableName))
                    .ToArray();

                if (statements.Length > 0
                    && ArePdfCanvasStatementsFullyMigratable(statements, pdfCanvasVariables))
                {
                    removable.Add(variableName);
                }
            }

            return removable;
        }

        private static bool ArePdfCanvasStatementsFullyMigratable(
            IReadOnlyList<ExpressionStatementSyntax> statements,
            IReadOnlySet<string> pdfCanvasVariables)
        {
            for (var index = 0; index < statements.Count; index++)
            {
                if (statements[index].Expression is InvocationExpressionSyntax invocation
                    && (TryMigratePdfCanvasLine(invocation, pdfCanvasVariables, out _)
                        || TryMigratePdfCanvasRectangle(invocation, pdfCanvasVariables, out _, out _)
                        || TryMigratePdfCanvasText(invocation, pdfCanvasVariables, out _)))
                {
                    continue;
                }

                if (TryMatchSeparatedPdfCanvasTextState(
                    SyntaxFactory.List(statements.Select(static statement => (StatementSyntax)statement)),
                    index,
                    pdfCanvasVariables,
                    out _,
                    out var consumed))
                {
                    index += consumed - 1;
                    continue;
                }

                return false;
            }

            return true;
        }

        private static bool ContainsIdentifier(SyntaxNode node, string identifierName)
        {
            return node.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.ValueText == identifierName);
        }

        private static Dictionary<string, string> FindPdfDocuments(
            CompilationUnitSyntax root,
            IReadOnlyDictionary<string, ExpressionSyntax> writerTargetByVariable)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (!TryGetOnlyVariableName(declaration, out var variableName)
                    || declaration.Declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation
                    || creation.Type.ToString() != "PdfDocument"
                    || creation.ArgumentList?.Arguments.Count != 1
                    || creation.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax writerIdentifier
                    || !writerTargetByVariable.ContainsKey(writerIdentifier.Identifier.ValueText))
                {
                    continue;
                }

                result[variableName] = writerIdentifier.Identifier.ValueText;
            }

            return result;
        }

        private static Dictionary<string, ITextDocumentInfo> FindLayoutDocuments(
            CompilationUnitSyntax root,
            IReadOnlyDictionary<string, string> pdfWriterByVariable,
            IReadOnlyDictionary<string, ExpressionSyntax> writerTargetByVariable)
        {
            var result = new Dictionary<string, ITextDocumentInfo>(StringComparer.Ordinal);

            foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (!TryGetOnlyVariableName(declaration, out var variableName)
                    || declaration.Declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation
                || creation.Type.ToString() != "Document"
                || creation.ArgumentList?.Arguments.Count is not (1 or 2)
                || creation.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax pdfIdentifier
                || !pdfWriterByVariable.TryGetValue(pdfIdentifier.Identifier.ValueText, out var writerVariable)
                || !writerTargetByVariable.TryGetValue(writerVariable, out var saveTarget))
            {
                continue;
            }

                ITextPageSizeInfo? pageSize = null;

                if (creation.ArgumentList.Arguments.Count == 2
                    && !TryMapPageSize(creation.ArgumentList.Arguments[1].Expression, out pageSize))
                {
                    continue;
                }

                result[variableName] = new ITextDocumentInfo("page", saveTarget, pageSize);
            }

            return result;
        }

        private static bool TryMapPageSize(ExpressionSyntax expression, out ITextPageSizeInfo? pageSize)
        {
            pageSize = null;
            var pageSizeExpression = expression;
            var landscape = false;

            if (expression is InvocationExpressionSyntax invocation
                && invocation.Expression is MemberAccessExpressionSyntax rotateAccess
                && rotateAccess.Name.Identifier.ValueText == "Rotate")
            {
                pageSizeExpression = rotateAccess.Expression;
                landscape = true;
            }

            if (pageSizeExpression is not MemberAccessExpressionSyntax pageSizeAccess
                || pageSizeAccess.Expression.ToString() != "PageSize")
            {
                return false;
            }

            var presetName = pageSizeAccess.Name.Identifier.ValueText switch
            {
                "A4" => "A4",
                "A3" => "A3",
                "LETTER" or "Letter" => "Letter",
                _ => null
            };

            if (presetName is null)
            {
                return false;
            }

            pageSize = new ITextPageSizeInfo(presetName, landscape);
            return true;
        }

        private static bool TryGetOnlyVariableName(LocalDeclarationStatementSyntax declaration, out string variableName)
        {
            variableName = string.Empty;

            if (declaration.Declaration.Variables.Count != 1)
            {
                return false;
            }

            variableName = declaration.Declaration.Variables[0].Identifier.ValueText;
            return true;
        }

        private static LocalDeclarationStatementSyntax CreatePageDeclaration(string documentVariableName, ITextDocumentInfo documentInfo)
        {
            var addPageArguments = documentInfo.PageSize is null
                ? SyntaxFactory.ArgumentList()
                : SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("PdfPagePreset"),
                        SyntaxFactory.IdentifierName(documentInfo.PageSize.PresetName))),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        documentInfo.PageSize.Landscape
                            ? SyntaxKind.TrueLiteralExpression
                            : SyntaxKind.FalseLiteralExpression))
                }));

            return SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(documentInfo.PageVariableName))
                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(documentVariableName),
                                        SyntaxFactory.IdentifierName("AddPage")),
                                    addPageArguments))))));
        }

        private static ExpressionStatementSyntax CreateSaveStatement(string documentVariableName, ExpressionSyntax saveTarget)
        {
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(documentVariableName),
                        SyntaxFactory.IdentifierName("Save")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(saveTarget)))));
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

        private sealed record ITextDocumentInfo(
            string PageVariableName,
            ExpressionSyntax SaveTarget,
            ITextPageSizeInfo? PageSize);

        private sealed record ITextPageSizeInfo(
            string PresetName,
            bool Landscape);
    }
}

file static class IText7SyntaxExtensions
{
    public static bool WithMigratedDeclaration(
        this bool value,
        LocalDeclarationStatementSyntax original,
        VariableDeclaratorSyntax variable,
        out LocalDeclarationStatementSyntax migrated)
    {
        migrated = original
            .WithUsingKeyword(default)
            .WithAwaitKeyword(default)
            .WithDeclaration(original.Declaration
                .WithType(SyntaxFactory.IdentifierName("var"))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(variable)));

        return value;
    }
}
