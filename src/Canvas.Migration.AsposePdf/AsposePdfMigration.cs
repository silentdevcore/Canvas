using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.AsposePdf;

public sealed class AsposePdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new AsposePdfSyntaxRewriter(root);
        var migratedRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        migratedRoot = EnsureCanvasUsing(RemoveAsposeUsings(migratedRoot));

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

    private static CompilationUnitSyntax RemoveAsposeUsings(CompilationUnitSyntax root)
    {
        var usings = root.Usings
            .Where(static directive => directive.Name?.ToString().StartsWith("Aspose.Pdf", StringComparison.Ordinal) != true)
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

    private sealed class AsposePdfSyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly List<MigrationDiagnostic> _diagnostics = new();
        private readonly Dictionary<string, AsposeTextFragmentInfo> _textFragmentsByVariable;
        private readonly HashSet<string> _removableTextFragments;
        private readonly Dictionary<string, string> _textBuilderPagesByVariable;
        private readonly HashSet<string> _removableTextBuilders;
        private readonly HashSet<string> _savedDocumentVariables;

        public AsposePdfSyntaxRewriter(CompilationUnitSyntax root)
        {
            _textFragmentsByVariable = FindTextFragments(root);
            _removableTextFragments = FindRemovableTextFragments(root, _textFragmentsByVariable);
            _textBuilderPagesByVariable = FindTextBuilderPages(root);
            _removableTextBuilders = FindRemovableTextBuilders(root, _textBuilderPagesByVariable, _textFragmentsByVariable);
            _savedDocumentVariables = FindSavedDocumentVariables(root, FindDocumentVariables(root));
            AddUnsupportedFeatureDiagnostics(root);
        }

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            if (node.Statement is ExpressionStatementSyntax expressionStatement
                && (TryRemoveSupportedTextFragmentPositionAssignment(expressionStatement)
                    || TryRemoveSupportedTextFragmentStateAssignment(expressionStatement)))
            {
                return null;
            }

            if (node.Statement is LocalDeclarationStatementSyntax localDeclaration)
            {
                if (ShouldRemoveLocalDeclaration(localDeclaration))
                {
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

            if (ShouldRemoveLocalDeclaration(node))
            {
                return null;
            }

            return (LocalDeclarationStatementSyntax)base.VisitLocalDeclarationStatement(node)!;
        }

        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (TryRemoveSupportedTextFragmentPositionAssignment(node)
                || TryRemoveSupportedTextFragmentStateAssignment(node))
            {
                return null;
            }

            var visited = (ExpressionStatementSyntax)base.VisitExpressionStatement(node)!;
            return visited;
        }

        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var visited = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;

            if (IsType(visited.Type, "Document") && visited.ArgumentList?.Arguments.Count == 0)
            {
                _diagnostics.Add(Info("CANMIGASPOSE001", "Aspose.PDF Document construction was migrated to Canvas.Pdf.PdfDocument."));
                return visited.WithType(SyntaxFactory.IdentifierName("PdfDocument"));
            }

            return visited;
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

            if (visited.Name.Identifier.ValueText == "Add"
                && visited.Expression is MemberAccessExpressionSyntax pagesAccess
                && pagesAccess.Name.Identifier.ValueText == "Pages")
            {
                _diagnostics.Add(Info("CANMIGASPOSE002", "Aspose document.Pages.Add() was migrated to Canvas document.AddPage()."));

                return SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    pagesAccess.Expression,
                    SyntaxFactory.IdentifierName("AddPage"));
            }

            return visited;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (TryMigrateParagraphsAdd(node, out var paragraphMigration))
            {
                _diagnostics.Add(Info(
                    paragraphMigration.UsedPosition ? "CANMIGASPOSE005" : "CANMIGASPOSE003",
                    paragraphMigration.UsedPosition
                        ? "Positioned Aspose.PDF TextFragment was migrated to Canvas DrawText."
                        : "Simple Aspose.PDF TextFragment paragraph was migrated to Canvas DrawTextFromTop."));
                return paragraphMigration.Invocation;
            }

            if (TryMigrateTextBuilderAppendText(node, out var builderMigration))
            {
                _diagnostics.Add(Info(
                    builderMigration.UsedPosition ? "CANMIGASPOSE006" : "CANMIGASPOSE004",
                    builderMigration.UsedPosition
                        ? "Positioned Aspose.PDF TextBuilder.AppendText call was migrated to Canvas DrawText."
                        : "Simple Aspose.PDF TextBuilder.AppendText call was migrated to Canvas DrawTextFromTop."));
                return builderMigration.Invocation;
            }

            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

            if (IsSaveInvocation(visited, out var documentVariableName)
                && _savedDocumentVariables.Contains(documentVariableName))
            {
                _diagnostics.Add(Info("CANMIGASPOSE007", "Aspose.PDF document.Save(...) now targets Canvas.Pdf.PdfDocument.Save(...)."));
            }

            return visited;
        }

        private bool ShouldRemoveLocalDeclaration(LocalDeclarationStatementSyntax localDeclaration)
        {
            if (localDeclaration.Declaration.Variables.Count != 1)
            {
                return false;
            }

            var variableName = localDeclaration.Declaration.Variables[0].Identifier.ValueText;

            if (_removableTextFragments.Contains(variableName))
            {
                _diagnostics.Add(Info("CANMIGASPOSE008", "Supported Aspose.PDF TextFragment variable was folded into Canvas drawing calls."));
                return true;
            }

            if (_removableTextBuilders.Contains(variableName))
            {
                _diagnostics.Add(Info("CANMIGASPOSE009", "Supported Aspose.PDF TextBuilder variable was removed after AppendText calls were migrated."));
                return true;
            }

            return false;
        }

        private bool TryMigrateParagraphsAdd(InvocationExpressionSyntax invocation, out AsposeTextMigration migration)
        {
            migration = default;

            if (invocation.Expression is not MemberAccessExpressionSyntax addAccess
                || addAccess.Name.Identifier.ValueText != "Add"
                || addAccess.Expression is not MemberAccessExpressionSyntax paragraphsAccess
                || paragraphsAccess.Name.Identifier.ValueText != "Paragraphs"
                || invocation.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            var pageExpression = paragraphsAccess.Expression;
            var textExpression = invocation.ArgumentList.Arguments[0].Expression;

            if (!TryGetTextFragmentInfo(textExpression, out var fragmentInfo))
            {
                return false;
            }

            migration = CreateDrawTextMigration(pageExpression, fragmentInfo);
            return true;
        }

        private bool TryMigrateTextBuilderAppendText(InvocationExpressionSyntax invocation, out AsposeTextMigration migration)
        {
            migration = default;

            if (invocation.Expression is not MemberAccessExpressionSyntax appendAccess
                || appendAccess.Name.Identifier.ValueText != "AppendText"
                || appendAccess.Expression is not IdentifierNameSyntax builderIdentifier
                || !_textBuilderPagesByVariable.TryGetValue(builderIdentifier.Identifier.ValueText, out var pageVariableName)
                || invocation.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            if (!TryGetTextFragmentInfo(invocation.ArgumentList.Arguments[0].Expression, out var fragmentInfo))
            {
                return false;
            }

            migration = CreateDrawTextMigration(SyntaxFactory.IdentifierName(pageVariableName), fragmentInfo);
            return true;
        }

        private bool TryGetTextFragmentInfo(ExpressionSyntax expression, out AsposeTextFragmentInfo fragmentInfo)
        {
            fragmentInfo = default;

            if (expression is ObjectCreationExpressionSyntax creation
                && IsType(creation.Type, "TextFragment")
                && creation.ArgumentList?.Arguments.Count == 1)
            {
                fragmentInfo = new AsposeTextFragmentInfo(creation.ArgumentList.Arguments[0].Expression, null, null);
                return true;
            }

            if (expression is IdentifierNameSyntax identifier
                && _textFragmentsByVariable.TryGetValue(identifier.Identifier.ValueText, out var knownFragment))
            {
                fragmentInfo = knownFragment;
                return true;
            }

            return false;
        }

        private static AsposeTextMigration CreateDrawTextMigration(ExpressionSyntax pageExpression, AsposeTextFragmentInfo fragmentInfo)
        {
            var methodName = fragmentInfo.PositionX is null || fragmentInfo.PositionY is null
                ? "DrawTextFromTop"
                : "DrawText";
            var x = fragmentInfo.PositionX ?? SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(40));
            var y = fragmentInfo.PositionY ?? SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(40));
            var invocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    pageExpression,
                    SyntaxFactory.IdentifierName(methodName)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(fragmentInfo.TextExpression),
                    SyntaxFactory.Argument(x),
                    SyntaxFactory.Argument(y),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(12)))
                })));

            return new AsposeTextMigration(invocation, methodName == "DrawText");
        }

        private bool TryRemoveSupportedTextFragmentPositionAssignment(ExpressionStatementSyntax expressionStatement)
        {
            if (expressionStatement.Expression is not AssignmentExpressionSyntax assignment
                || assignment.Left is not MemberAccessExpressionSyntax positionAccess
                || positionAccess.Name.Identifier.ValueText != "Position"
                || positionAccess.Expression is not IdentifierNameSyntax fragmentIdentifier
                || !_removableTextFragments.Contains(fragmentIdentifier.Identifier.ValueText))
            {
                return false;
            }

            _diagnostics.Add(Info("CANMIGASPOSE010", "Aspose.PDF TextFragment.Position assignment was folded into Canvas DrawText coordinates."));
            return true;
        }

        private bool TryRemoveSupportedTextFragmentStateAssignment(ExpressionStatementSyntax expressionStatement)
        {
            if (expressionStatement.Expression is not AssignmentExpressionSyntax assignment
                || assignment.Left is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Expression is not MemberAccessExpressionSyntax stateAccess
                || stateAccess.Name.Identifier.ValueText != "TextState"
                || stateAccess.Expression is not IdentifierNameSyntax fragmentIdentifier
                || !_removableTextFragments.Contains(fragmentIdentifier.Identifier.ValueText))
            {
                return false;
            }

            _diagnostics.Add(Warning("CANMIGASPOSE011", "Aspose.PDF TextFragment.TextState styling requires manual Canvas font/color review."));
            return true;
        }

        private static Dictionary<string, AsposeTextFragmentInfo> FindTextFragments(CompilationUnitSyntax root)
        {
            var positionByVariable = FindTextFragmentPositions(root);
            var fragments = new Dictionary<string, AsposeTextFragmentInfo>(StringComparer.Ordinal);

            foreach (var localDeclaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (localDeclaration.Declaration.Variables.Count != 1)
                {
                    continue;
                }

                var variable = localDeclaration.Declaration.Variables[0];
                if (variable.Initializer?.Value is not ObjectCreationExpressionSyntax creation
                    || !IsType(creation.Type, "TextFragment")
                    || creation.ArgumentList?.Arguments.Count != 1)
                {
                    continue;
                }

                var hasPosition = positionByVariable.TryGetValue(variable.Identifier.ValueText, out var position);
                fragments[variable.Identifier.ValueText] = new AsposeTextFragmentInfo(
                    creation.ArgumentList.Arguments[0].Expression,
                    hasPosition ? position.X : null,
                    hasPosition ? position.Y : null);
            }

            return fragments;
        }

        private static Dictionary<string, AsposePositionInfo> FindTextFragmentPositions(CompilationUnitSyntax root)
        {
            var positions = new Dictionary<string, AsposePositionInfo>(StringComparer.Ordinal);

            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is not MemberAccessExpressionSyntax positionAccess
                    || positionAccess.Name.Identifier.ValueText != "Position"
                    || positionAccess.Expression is not IdentifierNameSyntax fragmentIdentifier
                    || assignment.Right is not ObjectCreationExpressionSyntax positionCreation
                    || !IsType(positionCreation.Type, "Position")
                    || positionCreation.ArgumentList?.Arguments.Count != 2)
                {
                    continue;
                }

                positions[fragmentIdentifier.Identifier.ValueText] = new AsposePositionInfo(
                    positionCreation.ArgumentList.Arguments[0].Expression,
                    positionCreation.ArgumentList.Arguments[1].Expression);
            }

            return positions;
        }

        private static HashSet<string> FindRemovableTextFragments(
            CompilationUnitSyntax root,
            IReadOnlyDictionary<string, AsposeTextFragmentInfo> textFragmentsByVariable)
        {
            var removable = new HashSet<string>(textFragmentsByVariable.Keys, StringComparer.Ordinal);

            foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var name = identifier.Identifier.ValueText;
                if (!removable.Contains(name) || IsSupportedTextFragmentUse(identifier))
                {
                    continue;
                }

                removable.Remove(name);
            }

            return removable;
        }

        private static bool IsSupportedTextFragmentUse(IdentifierNameSyntax identifier)
        {
            if (identifier.Parent is VariableDeclaratorSyntax declarator && declarator.Identifier.ValueText == identifier.Identifier.ValueText)
            {
                return true;
            }

            if (identifier.Parent is ArgumentSyntax
                && identifier.Parent.Parent is ArgumentListSyntax
                && identifier.Parent.Parent.Parent is InvocationExpressionSyntax invocation
                && IsSupportedTextFragmentInvocation(invocation))
            {
                return true;
            }

            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Expression == identifier
                && memberAccess.Name.Identifier.ValueText == "Position"
                && memberAccess.Parent is AssignmentExpressionSyntax assignment
                && assignment.Left == memberAccess)
            {
                return true;
            }

            if (identifier.Parent is MemberAccessExpressionSyntax stateFragmentAccess
                && stateFragmentAccess.Expression == identifier
                && stateFragmentAccess.Name.Identifier.ValueText == "TextState"
                && stateFragmentAccess.Parent is MemberAccessExpressionSyntax stateMemberAccess
                && stateMemberAccess.Parent is AssignmentExpressionSyntax stateAssignment
                && stateAssignment.Left == stateMemberAccess)
            {
                return true;
            }

            return false;
        }

        private static bool IsSupportedTextFragmentInvocation(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax access)
            {
                return false;
            }

            if (access.Name.Identifier.ValueText == "Add"
                && access.Expression is MemberAccessExpressionSyntax paragraphsAccess
                && paragraphsAccess.Name.Identifier.ValueText == "Paragraphs")
            {
                return true;
            }

            return access.Name.Identifier.ValueText == "AppendText"
                && access.Expression is IdentifierNameSyntax;
        }

        private static Dictionary<string, string> FindTextBuilderPages(CompilationUnitSyntax root)
        {
            var builders = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var localDeclaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                if (localDeclaration.Declaration.Variables.Count != 1)
                {
                    continue;
                }

                var variable = localDeclaration.Declaration.Variables[0];
                if (variable.Initializer?.Value is not ObjectCreationExpressionSyntax creation
                    || !IsType(creation.Type, "TextBuilder")
                    || creation.ArgumentList?.Arguments.Count != 1
                    || creation.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax pageIdentifier)
                {
                    continue;
                }

                builders[variable.Identifier.ValueText] = pageIdentifier.Identifier.ValueText;
            }

            return builders;
        }

        private static HashSet<string> FindRemovableTextBuilders(
            CompilationUnitSyntax root,
            IReadOnlyDictionary<string, string> textBuilderPagesByVariable,
            IReadOnlyDictionary<string, AsposeTextFragmentInfo> textFragmentsByVariable)
        {
            var removable = new HashSet<string>(textBuilderPagesByVariable.Keys, StringComparer.Ordinal);

            foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var name = identifier.Identifier.ValueText;
                if (!removable.Contains(name) || IsSupportedTextBuilderUse(identifier, textFragmentsByVariable))
                {
                    continue;
                }

                removable.Remove(name);
            }

            return removable;
        }

        private static bool IsSupportedTextBuilderUse(
            IdentifierNameSyntax identifier,
            IReadOnlyDictionary<string, AsposeTextFragmentInfo> textFragmentsByVariable)
        {
            if (identifier.Parent is VariableDeclaratorSyntax declarator && declarator.Identifier.ValueText == identifier.Identifier.ValueText)
            {
                return true;
            }

            if (identifier.Parent is MemberAccessExpressionSyntax access
                && access.Expression == identifier
                && access.Name.Identifier.ValueText == "AppendText"
                && access.Parent is InvocationExpressionSyntax invocation
                && invocation.ArgumentList.Arguments.Count == 1)
            {
                var argumentExpression = invocation.ArgumentList.Arguments[0].Expression;
                return argumentExpression is ObjectCreationExpressionSyntax creation && IsType(creation.Type, "TextFragment")
                    || argumentExpression is IdentifierNameSyntax fragmentIdentifier
                    && textFragmentsByVariable.ContainsKey(fragmentIdentifier.Identifier.ValueText);
            }

            return false;
        }

        private static HashSet<string> FindDocumentVariables(CompilationUnitSyntax root)
        {
            return root.DescendantNodes()
                .OfType<LocalDeclarationStatementSyntax>()
                .Where(static localDeclaration => localDeclaration.Declaration.Variables.Count == 1)
                .Where(static localDeclaration =>
                    localDeclaration.Declaration.Variables[0].Initializer?.Value is ObjectCreationExpressionSyntax creation
                    && IsType(creation.Type, "Document"))
                .Select(static localDeclaration => localDeclaration.Declaration.Variables[0].Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static HashSet<string> FindSavedDocumentVariables(
            CompilationUnitSyntax root,
            IReadOnlySet<string> documentVariables)
        {
            return root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Select(static invocation => IsSaveInvocation(invocation, out var documentVariableName) ? documentVariableName : null)
                .Where(static documentVariableName => documentVariableName is not null)
                .Where(documentVariables.Contains!)
                .ToHashSet(StringComparer.Ordinal)!;
        }

        private static bool IsSaveInvocation(InvocationExpressionSyntax invocation, out string documentVariableName)
        {
            documentVariableName = string.Empty;

            if (invocation.Expression is not MemberAccessExpressionSyntax saveAccess
                || saveAccess.Name.Identifier.ValueText != "Save"
                || saveAccess.Expression is not IdentifierNameSyntax documentIdentifier)
            {
                return false;
            }

            documentVariableName = documentIdentifier.Identifier.ValueText;
            return true;
        }

        private void AddUnsupportedFeatureDiagnostics(CompilationUnitSyntax root)
        {
            var names = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(static identifier => identifier.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);

            if (names.Contains("Table"))
            {
                _diagnostics.Add(Warning("CANMIGASPOSE020", "Aspose.PDF Table usage requires manual Canvas table migration."));
            }

            if (names.Overlaps(new[]
                {
                    "Form",
                    "Field",
                    "TextBoxField",
                    "SignatureField",
                    "Facades",
                    "PdfFileSecurity",
                    "DocumentPrivilege",
                    "Stamp",
                    "TextStamp",
                    "RedactionAnnotation",
                    "OptimizationOptions",
                    "Annotation"
                }))
            {
                _diagnostics.Add(Warning("CANMIGASPOSE021", "Aspose.PDF forms, stamps, annotations, redaction, optimization, or security APIs are outside the v1 migration scope."));
            }
        }

        private static bool IsType(TypeSyntax type, string typeName)
        {
            return type switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText == typeName,
                QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText == typeName,
                AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText == typeName,
                _ => type.ToString().EndsWith("." + typeName, StringComparison.Ordinal)
            };
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
    }

    private readonly record struct AsposeTextFragmentInfo(
        ExpressionSyntax TextExpression,
        ExpressionSyntax? PositionX,
        ExpressionSyntax? PositionY);

    private readonly record struct AsposePositionInfo(ExpressionSyntax X, ExpressionSyntax Y);

    private readonly record struct AsposeTextMigration(InvocationExpressionSyntax Invocation, bool UsedPosition);
}
