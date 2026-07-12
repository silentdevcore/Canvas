using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.SpireXls;

/// <summary>
/// Migrates Spire.XLS (e-iceblue <c>Workbook</c>) authoring code to the PXA spreadsheet API
/// (<c>PxaWorkbook</c>). Roslyn-based: workbook/worksheet, <c>Range["A1"]</c> indexer, Text/Value/Number/
/// Formula, IsBold style, merge, SaveToFile. Charts and complex styles are flagged for manual review.
/// </summary>
public sealed class SpireXlsMigration : CSharpSourceMigration
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
            return !n.StartsWith("Spire.Xls", StringComparison.Ordinal) && n != "Spire";
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
        if (names.Overlaps(new[] { "Chart", "Charts", "PivotTable", "PivotTables" }))
            yield return Warn("CANMIGSPXL030", "Charts / pivot tables are not supported by the PXA spreadsheet engine; migrate manually.");
    }

    private static MigrationDiagnostic Info(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Info };
    private static MigrationDiagnostic Warn(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Warning };
    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        public List<MigrationDiagnostic> Diagnostics { get; } = [];
        private bool _notedDefaultSheet;

        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var visited = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            if (SimpleTypeName(visited.Type) == "Workbook")
            {
                if (!_notedDefaultSheet)
                {
                    Diagnostics.Add(Info("CANMIGSPXL011", "Spire Workbook auto-creates default worksheets; Worksheets[0] is mapped to AddSheet(\"Sheet1\")."));
                    _notedDefaultSheet = true;
                }
                return visited.WithType(SyntaxFactory.IdentifierName("PxaWorkbook").WithTriviaFrom(visited.Type))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
            }
            return visited;
        }

        // X.Range["A1"] → X.Cell("A1");  X.Range["A1:B1"] → X.Range("A1:B1");  X.Worksheets[0] → X.AddSheet("Sheet1")
        public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            var visited = (ElementAccessExpressionSyntax)base.VisitElementAccessExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var member = ma.Name.Identifier.ValueText;
            var args = visited.ArgumentList.Arguments;

            if (member == "Range" && args.Count == 1)
            {
                var isRange = args[0].Expression is LiteralExpressionSyntax { Token.ValueText: var t } && t.Contains(':');
                var method = isRange ? "Range" : "Cell";
                return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName(method)),
                    SyntaxFactory.ArgumentList(args));
            }
            if (member == "Worksheets" && args.Count == 1)
            {
                if (args[0].Expression is LiteralExpressionSyntax { Token.Value: 0 })
                    return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName("AddSheet")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Sheet1"))))));
                return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName("Sheet")),
                    SyntaxFactory.ArgumentList(args));
            }
            return visited;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var name = ma.Name.Identifier.ValueText;

            // wb.SaveToFile("out.xlsx", ExcelVersion.X) → wb.Save("out.xlsx")
            if (name is "SaveToFile" or "SaveAs")
            {
                var first = visited.ArgumentList.Arguments.Take(1);
                return visited.WithExpression(ma.WithName(SyntaxFactory.IdentifierName("Save")))
                    .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(first)));
            }
            // sheet.SetColumnWidth(col, w) (1-based) → sheet.Column(col-1).Width(w)
            if (name == "SetColumnWidth" && visited.ArgumentList.Arguments.Count == 2)
            {
                var colRecv = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ma.Expression, SyntaxFactory.IdentifierName("Column")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(visited.ArgumentList.Arguments[0].WithExpression(MinusOne(visited.ArgumentList.Arguments[0].Expression)))));
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, colRecv, SyntaxFactory.IdentifierName("Width")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(visited.ArgumentList.Arguments[1])));
            }
            return visited;
        }

        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var visited = (AssignmentExpressionSyntax)base.VisitAssignmentExpression(node)!;
            if (!visited.IsKind(SyntaxKind.SimpleAssignmentExpression)) return visited;
            if (visited.Left is not MemberAccessExpressionSyntax lhs) return visited;
            var member = lhs.Name.Identifier.ValueText;
            var rhs = visited.Right;

            // X.Text/.Value/.NumberValue/.Number = v → X.Value(v)
            if (member is "Text" or "Value" or "NumberValue" or "Number")
                return Call(lhs.Expression, "Value", rhs);
            if (member == "Formula")
                return Call(lhs.Expression, "Formula", rhs);
            // X.Style.Font.IsBold/.IsItalic/.Size = v → X.Style(s => s.Bold(v)/Italic(v)/FontSize(v))
            if (TryStyleChain(lhs, out var cell, out var styleMethod))
                return StyleLambda(cell!, styleMethod!, rhs);
            if (IsStyleChain(lhs))
                Diagnostics.Add(Warn("CANMIGSPXL020", $"Style assignment '{lhs}' needs manual migration to .Style(s => …)."));
            return visited;
        }

        // helpers
        private static ExpressionSyntax MinusOne(ExpressionSyntax e) =>
            e is LiteralExpressionSyntax lit && lit.Token.Value is int iv
                ? SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(iv - 1))
                : SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.SubtractExpression, e,
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1))));

        private static InvocationExpressionSyntax Call(ExpressionSyntax receiver, string method, ExpressionSyntax arg) =>
            SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, SyntaxFactory.IdentifierName(method)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(arg))));

        private static InvocationExpressionSyntax StyleLambda(ExpressionSyntax cellExpr, string method, ExpressionSyntax arg)
        {
            var body = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("s"), SyntaxFactory.IdentifierName(method)),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(arg))));
            var lambda = SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier("s")), body);
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, cellExpr, SyntaxFactory.IdentifierName("Style")),
                SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(lambda))));
        }

        private static bool TryStyleChain(MemberAccessExpressionSyntax lhs, out ExpressionSyntax? cellExpr, out string? styleMethod)
        {
            cellExpr = null;
            styleMethod = lhs.Name.Identifier.ValueText switch { "IsBold" => "Bold", "IsItalic" => "Italic", "Size" => "FontSize", _ => null };
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

        private static string SimpleTypeName(TypeSyntax type) => type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => type.ToString(),
        };
    }
}
