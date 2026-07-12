using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.AsposeCells;

/// <summary>
/// Migrates Aspose.Cells (<c>Workbook</c>) authoring code to the PXA spreadsheet API (<c>PxaWorkbook</c>).
/// Roslyn-based: rewrites workbook/worksheet/cell-indexer/PutValue/formula/SetColumnWidth/save calls.
/// Aspose cell indexes are already 0-based (no shift). The GetStyle/SetStyle pattern, charts, and pivots are
/// flagged for manual review. ClosedXML's formula engine covers fewer functions than Aspose — exotic
/// functions may compute differently.
/// </summary>
public sealed class AsposeCellsMigration : CSharpSourceMigration
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
            return !n.StartsWith("Aspose.Cells", StringComparison.Ordinal) && n != "Aspose";
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

        if (names.Contains("GetStyle") || names.Contains("SetStyle"))
            yield return Warn("CANMIGASPC020", "Aspose GetStyle/SetStyle styling → migrate manually to .Style(s => …) on the cell.");
        if (names.Overlaps(new[] { "Chart", "Charts", "PivotTable", "PivotTables" }))
            yield return Warn("CANMIGASPC030", "Charts / pivot tables are not supported by the PXA spreadsheet engine; migrate manually.");
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
                    Diagnostics.Add(Info("CANMIGASPC011", "Aspose Workbook auto-creates a default worksheet; in PXA call wb.AddSheet(...) explicitly (Worksheets[0] is mapped to AddSheet(\"Sheet1\"))."));
                    _notedDefaultSheet = true;
                }
                if (visited.ArgumentList?.Arguments.Count > 0)
                    Diagnostics.Add(Warn("CANMIGASPC023", "new Workbook(path) loads an existing file; PXA import is via ExcelWorkbookImporter — review."));
                return visited.WithType(SyntaxFactory.IdentifierName("PxaWorkbook").WithTriviaFrom(visited.Type))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
            }
            return visited;
        }

        public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            var visited = (ElementAccessExpressionSyntax)base.VisitElementAccessExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var member = ma.Name.Identifier.ValueText;

            // X.Cells["A1"] / X.Cells[0,1] → X.Cell(...)   (Aspose is 0-based — no shift)
            if (member == "Cells")
                return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName("Cell")),
                    SyntaxFactory.ArgumentList(visited.ArgumentList.Arguments));

            // wb.Worksheets[0] → wb.AddSheet("Sheet1");  wb.Worksheets[i] → wb.Sheet(i)
            if (member == "Worksheets" && visited.ArgumentList.Arguments.Count == 1)
            {
                var idx = visited.ArgumentList.Arguments[0].Expression;
                if (idx is LiteralExpressionSyntax { Token.Value: 0 })
                    return SyntaxFactory.InvocationExpression(
                        ma.WithName(SyntaxFactory.IdentifierName("AddSheet")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Sheet1"))))));
                return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName("Sheet")),
                    SyntaxFactory.ArgumentList(visited.ArgumentList.Arguments));
            }

            return visited;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var name = ma.Name.Identifier.ValueText;

            // cell.PutValue(v[, ...]) → cell.Value(v)
            if (name == "PutValue")
            {
                var first = visited.ArgumentList.Arguments.Take(1);
                if (visited.ArgumentList.Arguments.Count > 1)
                    Diagnostics.Add(Warn("CANMIGASPC022", "PutValue(value, isConverted/…) extra args dropped; PXA Value(object) infers the type."));
                return visited
                    .WithExpression(ma.WithName(SyntaxFactory.IdentifierName("Value")))
                    .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(first)));
            }

            // wb.Worksheets.Add("X") → wb.AddSheet("X")
            if (name == "Add" && ma.Expression is MemberAccessExpressionSyntax inner && inner.Name.Identifier.ValueText == "Worksheets")
                return visited.WithExpression(ma.WithExpression(inner.Expression).WithName(SyntaxFactory.IdentifierName("AddSheet")));

            // X.Cells.SetColumnWidth(col, w) → X.Column(col).Width(w)
            if (name == "SetColumnWidth" && visited.ArgumentList.Arguments.Count == 2
                && ma.Expression is MemberAccessExpressionSyntax cellsAcc && cellsAcc.Name.Identifier.ValueText == "Cells")
            {
                var colRecv = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, cellsAcc.Expression, SyntaxFactory.IdentifierName("Column")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(visited.ArgumentList.Arguments[0])));
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, colRecv, SyntaxFactory.IdentifierName("Width")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(visited.ArgumentList.Arguments[1])));
            }

            // wb.Save(stream/SaveFormat) → PXA Save(string path)
            if (name == "Save" && !(visited.ArgumentList.Arguments.Count == 1 && visited.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax))
                Diagnostics.Add(Warn("CANMIGASPC024", "Aspose Save(stream/SaveFormat) → PXA Save(string path): pass a file path."));

            return visited;
        }

        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            var visited = (AssignmentExpressionSyntax)base.VisitAssignmentExpression(node)!;
            if (!visited.IsKind(SyntaxKind.SimpleAssignmentExpression)) return visited;
            if (visited.Left is not MemberAccessExpressionSyntax lhs) return visited;

            // cell.Formula = "=f" → cell.Formula("=f");  Value/Width/Height likewise
            if (lhs.Name.Identifier.ValueText is "Formula" or "Value" or "Width" or "Height")
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, lhs.Expression, lhs.Name),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(visited.Right))));

            return visited;
        }

        private static string SimpleTypeName(TypeSyntax type) => type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => type.ToString(),
        };
    }
}
