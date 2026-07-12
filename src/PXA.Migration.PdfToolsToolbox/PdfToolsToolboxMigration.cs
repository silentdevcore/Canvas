using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.PdfToolsToolbox;

public sealed class PdfToolsToolboxMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var context = ToolboxContext.From(root);
        var rewriter = new PdfToolsToolboxRewriter(context);
        var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;

        var diagnostics = new List<MigrationDiagnostic>
        {
            Warning("CANMIGPDFTOOLBOX000",
                "PDF Toolbox SDK migration is a cautious pilot; validate package version, coordinates, fonts, and output stream handling before applying broadly.")
        };
        diagnostics.AddRange(rewriter.Diagnostics);
        diagnostics.AddRange(ScanForManualWork(root));

        var introducedPxaCode = rewriter.IntroducedPxaCode;
        var hasToolboxRemainders = HasToolboxRemainders(rewritten);

        if (hasToolboxRemainders)
        {
            diagnostics.Add(Warning("CANMIGPDFTOOLBOX010",
                "PDF Toolbox code remains after partial migration; Toolbox usings were preserved for manual follow-up."));
        }
        else
        {
            rewritten = RemoveToolboxUsings(rewritten);
        }

        if (introducedPxaCode)
            rewritten = EnsurePxaUsing(rewritten);

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(rewritten.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics
        };
    }

    private static IEnumerable<MigrationDiagnostic> ScanForManualWork(CompilationUnitSyntax root)
    {
        var names = root.Members
            .SelectMany(static member => member.DescendantNodes())
            .OfType<IdentifierNameSyntax>()
            .Select(static identifier => identifier.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        if (names.Overlaps(new[]
            {
                "Document.Open",
                "Page.Copy",
                "PageList",
                "PageCopyOptions",
                "CopyDocumentData",
                "CopyContent"
            }))
        {
            yield return Warning("CANMIGPDFTOOLBOX020",
                "PDF Toolbox existing-PDF copy/edit/tag workflows require manual migration outside v1.");
        }

        if (names.Overlaps(new[]
            {
                "Annotation",
                "Annotations",
                "Form",
                "Forms",
                "Metadata",
                "Outline",
                "OutlineItem",
                "Structure",
                "Tag",
                "Tagged",
                "ViewerSettings"
            }))
        {
            yield return Warning("CANMIGPDFTOOLBOX006",
                "PDF Toolbox forms, annotations, metadata, outlines, viewer settings, or tagging require manual migration outside v1.");
        }

        if (names.Overlaps(new[]
            {
                "ColorSpace",
                "Paint",
                "Fill",
                "Transparency",
                "Stroke",
                "Image",
                "Barcode",
                "Watermark"
            }))
        {
            yield return Warning("CANMIGPDFTOOLBOX004",
                "PDF Toolbox font, color, paint, transparency, image, barcode, or watermark details require manual review outside v1.");
        }
    }

    private static CompilationUnitSyntax RemoveToolboxUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static directive =>
            {
                var name = directive.Name?.ToString() ?? "";
                return !name.Equals("PdfTools.Toolbox", StringComparison.Ordinal)
                    && !name.StartsWith("PdfTools.Toolbox.", StringComparison.Ordinal);
            })
            .ToArray();

        return root.WithUsings(SyntaxFactory.List(filtered));
    }

    private static bool HasToolboxRemainders(CompilationUnitSyntax root)
    {
        var memberText = string.Join("\n", root.Members.Select(static member => member.ToFullString()));
        return new[]
        {
            "Document.Open",
            "Page.Copy",
            "PageList",
            "PageCopyOptions",
            "ContentGenerator",
            "TextGenerator",
            "Text.Create",
            "Font.CreateFromSystem",
            "new Annotation",
            "new Form",
            "ColorSpace",
            "Paint.",
            "new Fill",
            "Image.",
            "Barcode",
            "Watermark",
            "Metadata",
            "Outline",
            "ViewerSettings"
        }.Any(marker => memberText.Contains(marker, StringComparison.Ordinal));
    }

    private static CompilationUnitSyntax EnsurePxaUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static directive => directive.Name?.ToString() == "PXA.Pdf"))
            return root;

        var canvasUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("PXA.Pdf"))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        return root.WithUsings(root.Usings.Insert(0, canvasUsing));
    }

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

    private sealed class PdfToolsToolboxRewriter : CSharpSyntaxRewriter
    {
        private readonly ToolboxContext _context;
        private readonly List<MigrationDiagnostic> _diagnostics = [];

        public PdfToolsToolboxRewriter(ToolboxContext context)
        {
            _context = context;
        }

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;
        public bool IntroducedPxaCode { get; private set; }

        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var members = new List<MemberDeclarationSyntax>();
            var insertedSave = false;
            foreach (var member in node.Members)
            {
                if (member is not GlobalStatementSyntax statement)
                {
                    members.Add(member);
                    continue;
                }

                var transformed = TransformGlobalStatement(statement).ToArray();
                if (transformed.Length > 0 && IsPagesAddStatement(statement))
                {
                    var saveStatement = TryCreateSaveStatement(statement);
                    if (saveStatement != null)
                    {
                        transformed = [.. transformed, saveStatement];
                        insertedSave = true;
                    }
                }

                members.AddRange(transformed);
            }

            if (!insertedSave)
            {
                var saveStatement = TryCreateSaveStatement();
                if (saveStatement != null)
                {
                    members.Add(saveStatement);
                    insertedSave = true;
                }
            }

            return node.WithMembers(SyntaxFactory.List(members));
        }

        private IEnumerable<MemberDeclarationSyntax> TransformGlobalStatement(GlobalStatementSyntax statement)
        {
            if (TryGetOutputPathDeclaration(statement, out var outputPath))
            {
                _context.OutputPath ??= outputPath;
                return [];
            }

            if (IsDocumentCreateStatement(statement))
            {
                _diagnostics.Add(Info("CANMIGPDFTOOLBOX001", "PDF Toolbox Document.Create(...) -> new PXA.Pdf.PdfDocument()"));
                IntroducedPxaCode = true;
                return [MakeGlobal("var document = new PdfDocument();", statement)];
            }

            if (IsPageCreateStatement(statement, out var pageVariable, out var addPageExpression))
            {
                _diagnostics.Add(Info("CANMIGPDFTOOLBOX002", $"PDF Toolbox Page.Create(...) -> document.{addPageExpression}"));
                IntroducedPxaCode = true;
                return [MakeGlobal($"var {pageVariable} = document.{addPageExpression};", statement)];
            }

            if (TryConvertText(statement, out var drawText))
                return [MakeGlobal(drawText!, statement)];

            if (IsPagesAddStatement(statement))
            {
                _diagnostics.Add(Info("CANMIGPDFTOOLBOX002", "PDF Toolbox Pages.Add(...) removed because document.AddPage() already attaches the page."));
                var saveStatement = TryCreateSaveStatement(statement);
                return saveStatement != null ? [saveStatement] : [];
            }

            if (IsToolboxPlumbingStatement(statement))
                return [];

            return [statement];
        }

        private GlobalStatementSyntax? TryCreateSaveStatement(GlobalStatementSyntax? original = null)
        {
            if (_context.OutputPath == null)
            {
                if (_context.SaveWarningInserted)
                    return null;

                _context.SaveWarningInserted = true;
                _diagnostics.Add(Warning("CANMIGPDFTOOLBOX008",
                    "PDF Toolbox output path was not detected; add document.Save(...) manually."));
                return null;
            }

            if (_context.SaveInserted)
                return null;

            _context.SaveInserted = true;
            IntroducedPxaCode = true;
            _diagnostics.Add(Info("CANMIGPDFTOOLBOX007", $"PDF Toolbox output target -> document.Save({_context.OutputPath})."));
            return original != null
                ? MakeGlobal($"document.Save({_context.OutputPath});", original)
                : SyntaxFactory.GlobalStatement(SyntaxFactory.ParseStatement($"document.Save({_context.OutputPath});"));
        }

        private bool TryGetOutputPathDeclaration(GlobalStatementSyntax statement, out string? outputPath)
        {
            outputPath = null;
            if (statement.Statement is not LocalDeclarationStatementSyntax declaration)
                return false;

            foreach (var variable in declaration.Declaration.Variables)
            {
                if (variable.Identifier.ValueText != _context.OutputStreamVariable)
                    continue;

                outputPath = FindFileStreamPath(variable.Initializer?.Value);
                if (outputPath != null)
                {
                    _diagnostics.Add(Info("CANMIGPDFTOOLBOX007", $"PDF Toolbox output stream target detected as {outputPath}."));
                    return true;
                }
            }

            return false;
        }

        private bool IsDocumentCreateStatement(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Expression.ToString() == "Document"
                    && access.Name.Identifier.ValueText == "Create");
        }

        private bool IsPageCreateStatement(GlobalStatementSyntax statement, out string pageVariable, out string addPageExpression)
        {
            foreach (var invocation in statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access
                    || access.Expression.ToString() != "Page"
                    || access.Name.Identifier.ValueText != "Create")
                {
                    continue;
                }

                pageVariable = invocation.Ancestors().OfType<VariableDeclaratorSyntax>()
                    .FirstOrDefault()?.Identifier.ValueText ?? "page";
                addPageExpression = GetAddPageExpression(invocation);
                return true;
            }

            pageVariable = "page";
            addPageExpression = "AddPage()";
            return false;
        }

        private string GetAddPageExpression(InvocationExpressionSyntax invocation)
        {
            if (invocation.ArgumentList.Arguments.Count < 2)
                return "AddPage()";

            var pageSize = invocation.ArgumentList.Arguments[1].Expression.ToString();
            if (TryMapPagePreset(pageSize, out var preset, out var landscape))
                return $"AddPage(PdfPagePreset.{preset}, {landscape.ToString().ToLowerInvariant()})";

            _diagnostics.Add(Warning("CANMIGPDFTOOLBOX009",
                $"PDF Toolbox page size '{pageSize}' requires manual review; default PXA.Pdf A4 page was used."));
            return "AddPage()";
        }

        private static bool TryMapPagePreset(string pageSize, out string preset, out bool landscape)
        {
            var normalized = pageSize.Replace(" ", "", StringComparison.Ordinal);
            landscape = normalized.Contains(".Rotate()", StringComparison.Ordinal)
                || normalized.Contains(".Rotate(", StringComparison.Ordinal)
                || normalized.EndsWith(".Landscape", StringComparison.Ordinal)
                || normalized.Contains("Landscape", StringComparison.Ordinal);

            if (normalized.Contains("A4", StringComparison.OrdinalIgnoreCase))
            {
                preset = "A4";
                return true;
            }

            if (normalized.Contains("Letter", StringComparison.OrdinalIgnoreCase))
            {
                preset = "Letter";
                return true;
            }

            preset = "";
            landscape = false;
            return false;
        }

        private bool TryConvertText(GlobalStatementSyntax statement, out string? converted)
        {
            converted = null;
            foreach (var invocation in statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access
                    || access.Name.Identifier.ValueText != "ShowLine"
                    || access.Expression.ToString() != _context.TextGeneratorVariable
                    || invocation.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                var text = invocation.ArgumentList.Arguments[0].Expression;
                var position = _context.Position ?? ("72", "72");
                var fontSize = _context.FontSize ?? "12";
                converted = $"{_context.PageVariable}.DrawTextFromTop({text}, {position.X}, {position.TopY}, {fontSize});";
                _diagnostics.Add(Info("CANMIGPDFTOOLBOX003", "PDF Toolbox TextGenerator.ShowLine(...) -> page.DrawTextFromTop(...)"));
                IntroducedPxaCode = true;
                return true;
            }

            return false;
        }

        private bool IsPagesAddStatement(GlobalStatementSyntax statement)
        {
            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText == "Add"
                    && access.Expression.ToString() == $"{_context.DocumentVariable}.Pages");
        }

        private bool IsToolboxPlumbingStatement(GlobalStatementSyntax statement)
        {
            if (statement.Statement.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                .Any(static creation => GetSimpleName(creation.Type) is "ContentGenerator" or "TextGenerator"))
            {
                return true;
            }

            return statement.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
                    && ((access.Name.Identifier.ValueText is "CreateFromSystem" && access.Expression.ToString() == "Font")
                        || (access.Name.Identifier.ValueText == "Create" && access.Expression.ToString() == "Text")
                        || access.Name.Identifier.ValueText is "MoveTo" or "PaintText"));
        }

        private static GlobalStatementSyntax MakeGlobal(string source, GlobalStatementSyntax original)
        {
            return SyntaxFactory.GlobalStatement(SyntaxFactory.ParseStatement(source))
                .WithLeadingTrivia(original.GetLeadingTrivia())
                .WithTrailingTrivia(original.GetTrailingTrivia());
        }
    }

    private sealed class ToolboxContext
    {
        public string DocumentVariable { get; private init; } = "outDoc";
        public string PageVariable { get; private init; } = "outPage";
        public string? OutputStreamVariable { get; private init; }
        public string? OutputPath { get; set; }
        public bool SaveInserted { get; set; }
        public bool SaveWarningInserted { get; set; }
        public string? TextGeneratorVariable { get; private init; }
        public string? FontSize { get; private init; }
        public (string X, string TopY)? Position { get; private init; }

        public static ToolboxContext From(CompilationUnitSyntax root)
        {
            var documentVariable = FindAssignedVariable(root, "Document", "Create") ?? "outDoc";
            var pageVariable = FindAssignedVariable(root, "Page", "Create") ?? "outPage";
            var textGeneratorVariable = FindObjectVariable(root, "TextGenerator");
            var outputStreamVariable = FindLikelyOutputStreamVariable(root);
            var outputPath = FindOutputPath(root, outputStreamVariable);
            var fontSize = FindTextGeneratorFontSize(root, textGeneratorVariable);
            var position = FindMoveToPosition(root, textGeneratorVariable);

            return new ToolboxContext
            {
                DocumentVariable = documentVariable,
                PageVariable = pageVariable,
                OutputStreamVariable = outputStreamVariable,
                OutputPath = outputPath,
                TextGeneratorVariable = textGeneratorVariable,
                FontSize = fontSize,
                Position = position
            };
        }

        private static string? FindAssignedVariable(CompilationUnitSyntax root, string expressionName, string methodName)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access
                    || access.Expression.ToString() != expressionName
                    || access.Name.Identifier.ValueText != methodName)
                {
                    continue;
                }

                var declaration = invocation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (declaration != null)
                    return declaration.Identifier.ValueText;
            }

            return null;
        }

        private static string? FindObjectVariable(CompilationUnitSyntax root, string typeName)
        {
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (GetSimpleName(creation.Type) != typeName)
                    continue;

                var declaration = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (declaration != null)
                    return declaration.Identifier.ValueText;
            }

            return null;
        }

        private static string? FindLikelyOutputStreamVariable(CompilationUnitSyntax root)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access
                    || access.Expression.ToString() != "Document"
                    || access.Name.Identifier.ValueText != "Create"
                    || invocation.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                return invocation.ArgumentList.Arguments[0].Expression.ToString();
            }

            return null;
        }

        private static string? FindOutputPath(CompilationUnitSyntax root, string? outputStreamVariable)
        {
            if (outputStreamVariable == null)
                return null;

            foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (variable.Identifier.ValueText != outputStreamVariable)
                    continue;

                return FindFileStreamPath(variable.Initializer?.Value);
            }

            return null;
        }

        private static string? FindTextGeneratorFontSize(CompilationUnitSyntax root, string? textGeneratorVariable)
        {
            if (textGeneratorVariable == null)
                return null;

            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (GetSimpleName(creation.Type) != "TextGenerator"
                    || creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.ValueText != textGeneratorVariable
                    || creation.ArgumentList?.Arguments.Count < 3)
                {
                    continue;
                }

                return creation.ArgumentList?.Arguments[2].Expression.ToString();
            }

            return null;
        }

        private static (string X, string TopY)? FindMoveToPosition(CompilationUnitSyntax root, string? textGeneratorVariable)
        {
            if (textGeneratorVariable == null)
                return null;

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access
                    || access.Expression.ToString() != textGeneratorVariable
                    || access.Name.Identifier.ValueText != "MoveTo"
                    || invocation.ArgumentList.Arguments.Count == 0)
                {
                    continue;
                }

                var expression = invocation.ArgumentList.Arguments[0].Expression;
                if (expression is ObjectCreationExpressionSyntax creation)
                    return ExtractPoint(creation.Initializer);

                if (expression is IdentifierNameSyntax identifier)
                    return FindPointDeclaration(root, identifier.Identifier.ValueText);
            }

            return null;
        }

        private static (string X, string TopY)? FindPointDeclaration(CompilationUnitSyntax root, string variableName)
        {
            foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (variable.Identifier.ValueText != variableName
                    || variable.Initializer?.Value is not ObjectCreationExpressionSyntax creation)
                {
                    continue;
                }

                return ExtractPoint(creation.Initializer);
            }

            return null;
        }

        private static (string X, string TopY)? ExtractPoint(InitializerExpressionSyntax? initializer)
        {
            if (initializer == null)
                return null;

            string? x = null;
            string? y = null;
            foreach (var expression in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                if (expression.Left.ToString() == "X")
                    x = expression.Right.ToString();
                if (expression.Left.ToString() == "Y")
                    y = NormalizeTopY(expression.Right.ToString());
            }

            return x != null && y != null ? (x, y) : null;
        }

        private static string NormalizeTopY(string y)
        {
            const string heightPrefix = "outPage.Size.Height - ";
            return y.StartsWith(heightPrefix, StringComparison.Ordinal)
                ? y[heightPrefix.Length..]
                : y;
        }
    }

    private static string GetSimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
        _ => type.ToString()
    };

    private static string? FindFileStreamPath(ExpressionSyntax? expression)
    {
        return expression switch
        {
            ObjectCreationExpressionSyntax creation
                when GetSimpleName(creation.Type) is "FileStream"
                    && creation.ArgumentList?.Arguments.Count > 0
                => creation.ArgumentList.Arguments[0].Expression.ToString(),
            InvocationExpressionSyntax invocation
                when invocation.Expression is MemberAccessExpressionSyntax access
                    && access.Expression.ToString() == "File"
                    && access.Name.Identifier.ValueText is "Create" or "OpenWrite"
                    && invocation.ArgumentList.Arguments.Count > 0
                => invocation.ArgumentList.Arguments[0].Expression.ToString(),
            _ => null
        };
    }
}
