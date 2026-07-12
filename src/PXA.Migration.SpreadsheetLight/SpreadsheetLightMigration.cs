using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.SpreadsheetLight;

/// <summary>
/// Migrates SpreadsheetLight (<c>SLDocument</c>) authoring code to the Canvas spreadsheet API
/// (<c>CanvasWorkbook</c>). In SpreadsheetLight one <c>SLDocument</c> is both the workbook and the active
/// worksheet; Canvas separates them, so this maps the document to a <c>CanvasWorkbook</c>, <b>injects</b>
/// <c>var sheet = &lt;doc&gt;.AddSheet("Sheet1");</c>, and retargets the cell calls to that worksheet.
/// </summary>
public sealed class SpreadsheetLightMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();

        // The SLDocument variable name (= new SLDocument()).
        string? docVar = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(v => v.Initializer?.Value is ObjectCreationExpressionSyntax oce && SimpleTypeName(oce.Type) == "SLDocument")
            ?.Identifier.ValueText;

        var rewriter = new Rewriter(docVar, "sheet");
        var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
        rewritten = SwapUsings(rewritten);

        var diagnostics = new List<MigrationDiagnostic>(rewriter.Diagnostics);
        if (docVar is not null && !rewriter.SheetInjected)
            diagnostics.Add(Warn("CANMIGSLXL011",
                "SpreadsheetLight's SLDocument doubles as the active worksheet. Add `var sheet = " + docVar + ".AddSheet(\"Sheet1\");` and target the cell calls at `sheet`."));

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(rewritten.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics,
        };
    }

    private static CompilationUnitSyntax SwapUsings(CompilationUnitSyntax root)
    {
        var kept = root.Usings.Where(u => !(u.Name?.ToString() ?? "").StartsWith("SpreadsheetLight", StringComparison.Ordinal)).ToList();
        const string canvasNs = "PXA.Infrastructure.Spreadsheet";
        if (!kept.Any(u => u.Name?.ToString() == canvasNs))
            kept.Insert(0, SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(canvasNs))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
        return root.WithUsings(SyntaxFactory.List(kept));
    }

    private static MigrationDiagnostic Warn(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Warning };
    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

    private static string SimpleTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        QualifiedNameSyntax q => q.Right.Identifier.ValueText,
        _ => type.ToString(),
    };

    private sealed class Rewriter(string? docVar, string sheetVar) : CSharpSyntaxRewriter
    {
        public List<MigrationDiagnostic> Diagnostics { get; } = [];
        public bool SheetInjected { get; private set; }

        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var visited = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            if (SimpleTypeName(visited.Type) == "SLDocument")
                return visited.WithType(SyntaxFactory.IdentifierName("CanvasWorkbook").WithTriviaFrom(visited.Type))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
            return visited;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma || docVar is null) return visited;
            if (ma.Expression is not IdentifierNameSyntax recv || recv.Identifier.ValueText != docVar) return visited;
            var name = ma.Name.Identifier.ValueText;
            var args = visited.ArgumentList.Arguments;

            // doc.SetCellValue("A1", v) / (row, col, v) → sheet.Cell(..).Value(v)/Formula(v)
            if (name == "SetCellValue" && (args.Count == 2 || args.Count == 3))
            {
                ExpressionSyntax cellAccess;
                if (args.Count == 2)
                    cellAccess = Cell(SyntaxFactory.SingletonSeparatedList(args[0]));
                else
                    cellAccess = Cell(SyntaxFactory.SeparatedList(new[] {
                        args[0].WithExpression(MinusOne(args[0].Expression)),
                        args[1].WithExpression(MinusOne(args[1].Expression)) }));
                var value = args[^1].Expression;
                var method = value is LiteralExpressionSyntax { Token.ValueText: var t } && t.StartsWith('=') ? "Formula" : "Value";
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, cellAccess, SyntaxFactory.IdentifierName(method)),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(value))));
            }

            // doc.RenameWorksheet(old, "New") → sheet.Name = "New" (handled as expression → invocation form not ideal; emit assignment)
            if (name == "RenameWorksheet" && args.Count == 2)
                return SyntaxFactory.ParseExpression($"{sheetVar}.Name = {args[1].Expression}");

            // doc.SaveAs(path) / doc.Save(path) → doc.Save(path)  (doc is the workbook)
            if (name is "SaveAs" or "Save")
                return visited.WithExpression(ma.WithName(SyntaxFactory.IdentifierName("Save")))
                    .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(args.Take(1))));

            return visited;
        }

        // Inject `var sheet = <doc>.AddSheet("Sheet1");` right after the workbook declaration (top-level program).
        public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var visited = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
            if (docVar is null || SheetInjected) return visited;
            var members = visited.Members.ToList();
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i] is GlobalStatementSyntax { Statement: LocalDeclarationStatementSyntax ld }
                    && ld.Declaration.Variables.Any(v => v.Identifier.ValueText == docVar))
                {
                    var inject = SyntaxFactory.GlobalStatement(
                        SyntaxFactory.ParseStatement($"var {sheetVar} = {docVar}.AddSheet(\"Sheet1\");\n"));
                    members.Insert(i + 1, inject);
                    SheetInjected = true;
                    return visited.WithMembers(SyntaxFactory.List(members));
                }
            }
            return visited;
        }

        private InvocationExpressionSyntax Cell(SeparatedSyntaxList<ArgumentSyntax> args) =>
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(sheetVar), SyntaxFactory.IdentifierName("Cell")),
                SyntaxFactory.ArgumentList(args));

        private static ExpressionSyntax MinusOne(ExpressionSyntax e) =>
            e is LiteralExpressionSyntax lit && lit.Token.Value is int iv
                ? SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(iv - 1))
                : SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.SubtractExpression, e,
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1))));
    }
}
