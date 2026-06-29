using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.EpplusSpreadsheet;

/// <summary>
/// Migrates EPPlus (<c>ExcelPackage</c>) authoring code to the Canvas spreadsheet API (<c>CanvasWorkbook</c>).
/// Roslyn-based: rewrites the package/worksheet/cell-indexer/value/formula/merge/style/save calls and shifts
/// EPPlus's 1-based indexes to Canvas's 0-based. Charts, pivots, conditional formatting, and data validation
/// are flagged for manual review.
/// </summary>
public sealed class EpplusSpreadsheetMigration : CSharpSourceMigration
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
        if (rewriter.AppliedIndexShift)
            diagnostics.Add(Info("CANMIGEPPL010",
                "EPPlus cell/column indexes are 1-based; converted numeric Cells[row,col]/Column(i) to Canvas's 0-based equivalent. Verify any computed indexes."));
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
            return !n.StartsWith("OfficeOpenXml", StringComparison.Ordinal);
        }).ToList();

        const string canvasNs = "Canvas.Infrastructure.Spreadsheet";
        if (!kept.Any(u => u.Name?.ToString() == canvasNs))
            kept.Insert(0, SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(canvasNs))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));

        return root.WithUsings(SyntaxFactory.List(kept));
    }

    private static IEnumerable<MigrationDiagnostic> ScanUnsupported(CompilationUnitSyntax root)
    {
        var names = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Select(i => i.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);

        if (names.Overlaps(new[] { "PivotTables", "PivotTable", "Drawings", "Chart" }))
            yield return Warn("CANMIGEPPL030", "Pivot tables / charts / drawings are not supported by the Canvas spreadsheet engine; migrate manually.");

        if (names.Overlaps(new[] { "ConditionalFormatting", "AutoFilter", "DataValidations", "DataValidation" }))
            yield return Warn("CANMIGEPPL031",
                "Conditional formatting / auto-filter / data validation are set on the SpreadsheetDto model, not the fluent builder yet — migrate manually.");
    }

    private static MigrationDiagnostic Info(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Info };
    private static MigrationDiagnostic Warn(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Warning };
    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        public List<MigrationDiagnostic> Diagnostics { get; } = [];
        public bool AppliedIndexShift { get; private set; }

        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var visited = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            if (SimpleTypeName(visited.Type) == "ExcelPackage")
            {
                if (visited.ArgumentList?.Arguments.Count > 0)
                    Diagnostics.Add(Warn("CANMIGEPPL023", "new ExcelPackage(file/stream) loads an existing file; Canvas import is via ExcelWorkbookImporter — review."));
                return visited.WithType(SyntaxFactory.IdentifierName("CanvasWorkbook").WithTriviaFrom(visited.Type))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
            }
            return visited;
        }

        // ws.Cells["A1"] → ws.Cell("A1");  ws.Cells[1,2] → ws.Cell(0,1)
        public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            var visited = (ElementAccessExpressionSyntax)base.VisitElementAccessExpression(node)!;
            if (visited.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.ValueText == "Cells")
            {
                var args = visited.ArgumentList.Arguments;
                var argList = args.Count == 2
                    ? ShiftArgs(SyntaxFactory.ArgumentList(args))
                    : SyntaxFactory.ArgumentList(args);
                if (args.Count == 2) AppliedIndexShift = true;
                return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName("Cell")), argList);
            }
            return visited;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var name = ma.Name.Identifier.ValueText;

            // pkg.Workbook.Worksheets.Add("X") → pkg.AddSheet("X")
            if (name == "Add" && ma.Expression is MemberAccessExpressionSyntax wsAcc && wsAcc.Name.Identifier.ValueText == "Worksheets")
            {
                var receiver = wsAcc.Expression is MemberAccessExpressionSyntax wbAcc && wbAcc.Name.Identifier.ValueText == "Workbook"
                    ? wbAcc.Expression          // pkg.Workbook.Worksheets → pkg
                    : wsAcc.Expression;          // wb.Worksheets → wb
                return visited.WithExpression(ma.WithExpression(receiver).WithName(SyntaxFactory.IdentifierName("AddSheet")));
            }

            // pkg.SaveAs(fileInfo) → pkg.Save(...)  (string path expected)
            if (name == "SaveAs")
            {
                if (!(visited.ArgumentList.Arguments.Count == 1 && visited.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax))
                    Diagnostics.Add(Warn("CANMIGEPPL024", "EPPlus SaveAs(FileInfo/Stream) → Canvas Save(string path): pass a file path."));
                return visited.WithExpression(ma.WithName(SyntaxFactory.IdentifierName("Save")));
            }

            // pkg.Save() (no path) → can't map directly
            if (name == "Save" && visited.ArgumentList.Arguments.Count == 0)
                Diagnostics.Add(Warn("CANMIGEPPL025", "EPPlus Save() writes to the package's own file; Canvas Save(path) needs a target path."));

            // numeric Column(i)/Row(i) → 0-based
            if ((name == "Column" || name == "Row") && visited.ArgumentList.Arguments.Count == 1
                && IsNumeric(visited.ArgumentList.Arguments[0].Expression))
            {
                AppliedIndexShift = true;
                if (name == "Row") Diagnostics.Add(Warn("CANMIGEPPL021", "Row(i) has no direct Canvas builder method; review row height/grouping manually."));
                return visited.WithArgumentList(ShiftArgs(visited.ArgumentList));
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

            // X.Cell("A1:B1").Merge = true → X.Range("A1:B1").Merge()
            if (member == "Merge" && lhs.Expression is InvocationExpressionSyntax cellInv
                && cellInv.Expression is MemberAccessExpressionSyntax cellMa && cellMa.Name.Identifier.ValueText == "Cell"
                && cellInv.ArgumentList.Arguments.Count == 1)
            {
                var rangeRecv = SyntaxFactory.InvocationExpression(
                    cellMa.WithName(SyntaxFactory.IdentifierName("Range")), cellInv.ArgumentList);
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, rangeRecv, SyntaxFactory.IdentifierName("Merge")),
                    SyntaxFactory.ArgumentList());
            }

            // X.Value/Formula/Width/Height = v → X.<m>(v)
            if (member is "Value" or "Formula" or "Width" or "Height")
                return Call(lhs.Expression, member, rhs);

            if (TryStyleChain(lhs, out var cellExpr, out var styleMethod))
                return StyleLambda(cellExpr!, styleMethod!, rhs);

            if (IsStyleChain(lhs))
                Diagnostics.Add(Warn("CANMIGEPPL020", $"Style assignment '{lhs}' needs manual migration to .Style(s => …) (e.g. Fill/Border/Alignment)."));

            return visited;
        }

        // ── helpers (shared shape with the ClosedXML reference) ──
        private static ArgumentListSyntax ShiftArgs(ArgumentListSyntax args) =>
            args.WithArguments(SyntaxFactory.SeparatedList(args.Arguments.Select(a => a.WithExpression(MinusOne(a.Expression)))));

        private static ExpressionSyntax MinusOne(ExpressionSyntax e)
        {
            if (e is LiteralExpressionSyntax lit && lit.Token.Value is int iv)
                return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(iv - 1));
            return SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.BinaryExpression(SyntaxKind.SubtractExpression, e,
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1))));
        }

        private static bool IsNumeric(ExpressionSyntax e) =>
            e is LiteralExpressionSyntax { Token.Value: int } or IdentifierNameSyntax or BinaryExpressionSyntax;

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
            styleMethod = lhs.Name.Identifier.ValueText switch { "Bold" => "Bold", "Italic" => "Italic", "FontSize" or "Size" => "FontSize", _ => null };
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
