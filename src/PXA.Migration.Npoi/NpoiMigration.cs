using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.Npoi;

/// <summary>
/// Migrates NPOI (<c>XSSFWorkbook</c>/<c>HSSFWorkbook</c>) authoring code to the Canvas spreadsheet API
/// (<c>CanvasWorkbook</c>). NPOI uses a row/cell object model (CreateRow/CreateCell), so this pre-scans the
/// code to map row/cell variables to their (sheet, row, col) and inlines the writes as
/// <c>sheet.Cell(r, c).Value(..)/Formula(..)</c>. Stream-based <c>Write</c> and column-width units are flagged.
/// </summary>
public sealed class NpoiMigration : CSharpSourceMigration
{
    private readonly record struct CellRef(ExpressionSyntax Sheet, ExpressionSyntax Row, ExpressionSyntax Col);

    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();

        // Pre-scan: row vars (= <sheet>.CreateRow(R)) and cell vars (= <row>.CreateCell(C)).
        var rowMap = new Dictionary<string, (ExpressionSyntax sheet, ExpressionSyntax row)>(StringComparer.Ordinal);
        var cellMap = new Dictionary<string, CellRef>(StringComparer.Ordinal);
        foreach (var v in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (v.Initializer?.Value is not InvocationExpressionSyntax inv || inv.Expression is not MemberAccessExpressionSyntax m
                || inv.ArgumentList.Arguments.Count != 1) continue;
            var arg = inv.ArgumentList.Arguments[0].Expression;
            if (m.Name.Identifier.ValueText == "CreateRow")
                rowMap[v.Identifier.ValueText] = (m.Expression, arg);
            else if (m.Name.Identifier.ValueText == "CreateCell" && ResolveCreateCell(m.Expression, arg, rowMap) is { } cr)
                cellMap[v.Identifier.ValueText] = cr;
        }

        var rewriter = new Rewriter(rowMap, cellMap);
        var rewritten = (CompilationUnitSyntax)rewriter.Visit(root)!;
        rewritten = SwapUsings(rewritten);

        var diagnostics = new List<MigrationDiagnostic>(rewriter.Diagnostics);

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(rewritten.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics,
        };
    }

    // <row>.CreateCell(C) → (sheet, row, C). <row> may be a row variable or an inline sheet.CreateRow(R).
    private static CellRef? ResolveCreateCell(ExpressionSyntax rowExpr, ExpressionSyntax col, IReadOnlyDictionary<string, (ExpressionSyntax sheet, ExpressionSyntax row)> rowMap)
    {
        if (rowExpr is IdentifierNameSyntax id && rowMap.TryGetValue(id.Identifier.ValueText, out var r))
            return new CellRef(r.sheet, r.row, col);
        if (rowExpr is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax cm } ci
            && cm.Name.Identifier.ValueText == "CreateRow" && ci.ArgumentList.Arguments.Count == 1)
            return new CellRef(cm.Expression, ci.ArgumentList.Arguments[0].Expression, col);
        return null;
    }

    private static CompilationUnitSyntax SwapUsings(CompilationUnitSyntax root)
    {
        var kept = root.Usings.Where(u => !(u.Name?.ToString() ?? "").StartsWith("NPOI", StringComparison.Ordinal)).ToList();
        const string canvasNs = "PXA.Infrastructure.Spreadsheet";
        if (!kept.Any(u => u.Name?.ToString() == canvasNs))
            kept.Insert(0, SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(canvasNs))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
        return root.WithUsings(SyntaxFactory.List(kept));
    }

    private static MigrationDiagnostic Warn(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Warning };
    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

    private sealed class Rewriter(
        IReadOnlyDictionary<string, (ExpressionSyntax sheet, ExpressionSyntax row)> rowMap,
        IReadOnlyDictionary<string, CellRef> cellMap) : CSharpSyntaxRewriter
    {
        public List<MigrationDiagnostic> Diagnostics { get; } = [];

        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var visited = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            var name = SimpleTypeName(visited.Type);
            if (name is "XSSFWorkbook" or "HSSFWorkbook")
                return visited.WithType(SyntaxFactory.IdentifierName("CanvasWorkbook").WithTriviaFrom(visited.Type))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
            return visited;
        }

        // NPOI interface-typed locals (IWorkbook/ISheet) → var, so the migrated code compiles.
        public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            var visited = (VariableDeclarationSyntax)base.VisitVariableDeclaration(node)!;
            if (SimpleTypeName(visited.Type) is "IWorkbook" or "ISheet" or "IRow" or "ICell")
                return visited.WithType(SyntaxFactory.IdentifierName("var").WithTriviaFrom(visited.Type));
            return visited;
        }

        // Drop the addressing-only `var row = sheet.CreateRow(..)` / `var cell = row.CreateCell(..)` declarations.
        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
            => node.Statement is LocalDeclarationStatementSyntax l && IsAddressingDecl(l) ? null : base.VisitGlobalStatement(node);

        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
            => node.Parent is not GlobalStatementSyntax && IsAddressingDecl(node) ? null : base.VisitLocalDeclarationStatement(node);

        private static bool IsAddressingDecl(LocalDeclarationStatementSyntax node) =>
            node.Declaration.Variables.Any(v => v.Initializer?.Value is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax m }
                && m.Name.Identifier.ValueText is "CreateRow" or "CreateCell");

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var name = ma.Name.Identifier.ValueText;

            // wb.CreateSheet("X") → wb.AddSheet("X")
            if (name == "CreateSheet")
                return visited.WithExpression(ma.WithName(SyntaxFactory.IdentifierName("AddSheet")));

            // <cell>.SetCellValue(v) / <cell>.SetCellFormula(f) → <sheet>.Cell(r, c).Value(v)/Formula(f)
            if (name is "SetCellValue" or "SetCellFormula" && Resolve(ma.Expression) is { } cell && visited.ArgumentList.Arguments.Count == 1)
            {
                var cellAccess = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, cell.Sheet, SyntaxFactory.IdentifierName("Cell")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Argument(cell.Row), SyntaxFactory.Argument(cell.Col) })));
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, cellAccess,
                        SyntaxFactory.IdentifierName(name == "SetCellFormula" ? "Formula" : "Value")),
                    visited.ArgumentList);
            }

            // sheet.SetColumnWidth(col, w) → sheet.Column(col).Width(w)  (NPOI col is 0-based; width is 1/256 char)
            if (name == "SetColumnWidth" && visited.ArgumentList.Arguments.Count == 2)
            {
                Diagnostics.Add(Warn("CANMIGNPOI012", "NPOI SetColumnWidth uses 1/256-character units; Canvas Width() takes character units — adjust the value."));
                var colRecv = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ma.Expression, SyntaxFactory.IdentifierName("Column")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(visited.ArgumentList.Arguments[0])));
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, colRecv, SyntaxFactory.IdentifierName("Width")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(visited.ArgumentList.Arguments[1])));
            }

            // wb.Write(stream) → wb.Save("output.xlsx")
            if (name == "Write")
            {
                Diagnostics.Add(Warn("CANMIGNPOI013", "NPOI Write(stream) → Canvas Save(path): replace the stream with a target file path."));
                return visited.WithExpression(ma.WithName(SyntaxFactory.IdentifierName("Save")))
                    .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("output.xlsx"))))));
            }

            return visited;
        }

        private CellRef? Resolve(ExpressionSyntax expr)
        {
            if (expr is IdentifierNameSyntax id && cellMap.TryGetValue(id.Identifier.ValueText, out var cr)) return cr;
            if (expr is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax m } ci && m.Name.Identifier.ValueText == "CreateCell"
                && ci.ArgumentList.Arguments.Count == 1)
                return ResolveCreateCell(m.Expression, ci.ArgumentList.Arguments[0].Expression, rowMap);
            return null;
        }

        private static string SimpleTypeName(TypeSyntax type) => type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => type.ToString(),
        };
    }
}
