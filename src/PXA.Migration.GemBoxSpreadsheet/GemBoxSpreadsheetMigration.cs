using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.GemBoxSpreadsheet;

/// <summary>
/// Migrates GemBox.Spreadsheet (<c>ExcelFile</c>) authoring code to the Canvas spreadsheet API
/// (<c>CanvasWorkbook</c>). Roslyn-based: drops the <c>SpreadsheetInfo.SetLicense</c> call, rewrites
/// workbook/worksheet/cell-indexer/value/formula/font-weight style/save calls. GemBox cell indexes are
/// already 0-based (no shift). Charts, pivots, and range merges are flagged for manual review.
/// </summary>
public sealed class GemBoxSpreadsheetMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();

        var rewriter = new Rewriter();
        var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
        rewritten = SwapUsings(rewritten);

        var diagnostics = new List<MigrationDiagnostic>();
        diagnostics.AddRange(rewriter.Diagnostics);
        diagnostics.AddRange(ScanUnsupported(root));

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(rewritten.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics,
        };
    }

    private static CompilationUnitSyntax SwapUsings(CompilationUnitSyntax root)
    {
        var kept = root.Usings.Where(u =>
        {
            var n = u.Name?.ToString() ?? "";
            return !n.StartsWith("GemBox", StringComparison.Ordinal);
        }).ToList();

        const string canvasNs = "PXA.Infrastructure.Spreadsheet";
        if (!kept.Any(u => u.Name?.ToString() == canvasNs))
            kept.Insert(0, SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(canvasNs))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));

        return root.WithUsings(SyntaxFactory.List(kept));
    }

    private static IEnumerable<MigrationDiagnostic> ScanUnsupported(CompilationUnitSyntax root)
    {
        var names = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(i => i.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);

        if (names.Overlaps(new[] { "ExcelChart", "PivotTable", "PivotTables" }))
            yield return Warn("CANMIGGBSS030", "Charts / pivot tables are not supported by the Canvas spreadsheet engine; migrate manually.");
        if (names.Contains("Merged") || names.Contains("GetSubrange"))
            yield return Warn("CANMIGGBSS020", "GemBox merge (Cells.GetSubrange(\"A1:B1\").Merged = true) → ws.Range(\"A1:B1\").Merge() — migrate manually.");
    }

    private static MigrationDiagnostic Warn(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Warning };
    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        public List<MigrationDiagnostic> Diagnostics { get; } = [];

        // Drop SpreadsheetInfo.SetLicense("...") — top-level (GlobalStatement) and in-method (block) forms.
        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            if (node.Statement is ExpressionStatementSyntax es && IsSetLicense(es)) return null;
            return base.VisitGlobalStatement(node);
        }

        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (node.Parent is not GlobalStatementSyntax && IsSetLicense(node)) return null;
            return base.VisitExpressionStatement(node);
        }

        private static bool IsSetLicense(ExpressionStatementSyntax node) =>
            node.Expression is InvocationExpressionSyntax inv
            && inv.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.ValueText == "SetLicense"
            && m.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "SpreadsheetInfo";

        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var visited = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            if (SimpleTypeName(visited.Type) == "ExcelFile")
                return visited.WithType(SyntaxFactory.IdentifierName("CanvasWorkbook").WithTriviaFrom(visited.Type))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
            return visited;
        }

        // ws.Cells["A1"] → ws.Cell("A1");  ws.Cells[0,1] → ws.Cell(0,1)  (GemBox is already 0-based)
        public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            var visited = (ElementAccessExpressionSyntax)base.VisitElementAccessExpression(node)!;
            if (visited.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.ValueText == "Cells")
                return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName("Cell")),
                    SyntaxFactory.ArgumentList(visited.ArgumentList.Arguments));
            return visited;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var name = ma.Name.Identifier.ValueText;

            // wb.Worksheets.Add("X") → wb.AddSheet("X")
            if (name == "Add" && ma.Expression is MemberAccessExpressionSyntax inner && inner.Name.Identifier.ValueText == "Worksheets")
                return visited.WithExpression(ma.WithExpression(inner.Expression).WithName(SyntaxFactory.IdentifierName("AddSheet")));

            // wb.Save("x.xlsx") stays Save (Canvas has Save(string)). GemBox Save(stream/options) → diagnostic.
            if (name == "Save" && !(visited.ArgumentList.Arguments.Count == 1 && visited.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax))
                Diagnostics.Add(Warn("CANMIGGBSS024", "GemBox Save(stream/SaveOptions) → Canvas Save(string path): pass a file path."));

            return visited;
        }

        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var visited = (AssignmentExpressionSyntax)base.VisitAssignmentExpression(node)!;
            if (!visited.IsKind(SyntaxKind.SimpleAssignmentExpression)) return visited;
            if (visited.Left is not MemberAccessExpressionSyntax lhs) return visited;

            var member = lhs.Name.Identifier.ValueText;
            var rhs = visited.Right;

            if (member is "Value" or "Formula" or "Width" or "Height")
                return Call(lhs.Expression, member, rhs);

            // X.Style.Font.Weight = ExcelFont.BoldWeight → X.Style(s => s.Bold())
            // X.Style.Font.Italic = true → s.Italic(true);  .Size = n → s.FontSize(n)
            if (TryStyleChain(lhs, out var cellExpr, out var styleMethod, out var dropArg))
                return StyleLambda(cellExpr!, styleMethod!, dropArg ? null : rhs);

            // X.Style.HorizontalAlignment = HorizontalAlignmentStyle.Center → X.Style(s => s.Align("center"))
            if (member == "HorizontalAlignment" && lhs.Expression is MemberAccessExpressionSyntax st
                && st.Name.Identifier.ValueText == "Style" && MapAlign(rhs) is { } align)
                return StyleLambda(st.Expression, "Align", StringLit(align));

            if (IsStyleChain(lhs))
                Diagnostics.Add(Warn("CANMIGGBSS021", $"Style assignment '{lhs}' needs manual migration to .Style(s => …)."));

            return visited;
        }

        private static InvocationExpressionSyntax Call(ExpressionSyntax receiver, string method, ExpressionSyntax arg) =>
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, SyntaxFactory.IdentifierName(method)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(arg))));

        private static InvocationExpressionSyntax StyleLambda(ExpressionSyntax cellExpr, string method, ExpressionSyntax? arg)
        {
            var argList = arg is null
                ? SyntaxFactory.ArgumentList()
                : SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(arg)));
            var body = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("s"), SyntaxFactory.IdentifierName(method)),
                argList);
            var lambda = SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier("s")), body);
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, cellExpr, SyntaxFactory.IdentifierName("Style")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(lambda))));
        }

        // GemBox: Bold is Font.Weight = BoldWeight (drop the arg → s.Bold()); Italic/Size keep the arg.
        private static bool TryStyleChain(MemberAccessExpressionSyntax lhs, out ExpressionSyntax? cellExpr, out string? styleMethod, out bool dropArg)
        {
            cellExpr = null; dropArg = false;
            var leaf = lhs.Name.Identifier.ValueText;
            (styleMethod, dropArg) = leaf switch
            {
                "Weight" => ("Bold", true),
                "Italic" => ("Italic", false),
                "Size" => ("FontSize", false),
                _ => (null, false),
            };
            if (styleMethod is null) return false;
            if (lhs.Expression is MemberAccessExpressionSyntax font && font.Name.Identifier.ValueText == "Font"
                && font.Expression is MemberAccessExpressionSyntax style && style.Name.Identifier.ValueText == "Style")
            {
                cellExpr = style.Expression;
                return true;
            }
            styleMethod = null;
            return false;
        }

        private static bool IsStyleChain(MemberAccessExpressionSyntax lhs) =>
            lhs.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>().Any(m => m.Name.Identifier.ValueText == "Style");

        private static string? MapAlign(ExpressionSyntax rhs) =>
            (rhs as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText switch
            {
                "Left" => "left",
                "Center" or "CenterContinuous" => "center",
                "Right" => "right",
                _ => null,
            };

        private static LiteralExpressionSyntax StringLit(string s) =>
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(s));

        private static string SimpleTypeName(TypeSyntax type) => type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => type.ToString(),
        };
    }
}
