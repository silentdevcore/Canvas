using Canvas.Migration.Abstractions;
using Canvas.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Canvas.Migration.ClosedXmlSpreadsheet;

/// <summary>
/// Migrates ClosedXML (<c>XLWorkbook</c>) authoring code to the Canvas spreadsheet API
/// (<c>CanvasWorkbook</c>, see <c>Canvas.Infrastructure.Spreadsheet.CanvasWorkbookBuilder</c>).
/// Roslyn-based: rewrites workbook/worksheet/cell/value/formula/style/save calls and shifts ClosedXML's
/// 1-based indexes to Canvas's 0-based. Charts, pivots, conditional formatting, data validation, and
/// auto-filter are flagged for manual review.
/// </summary>
public sealed class ClosedXmlSpreadsheetMigration : CSharpSourceMigration
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
            diagnostics.Add(Info("CANMIGCLXL010",
                "ClosedXML cell/column indexes are 1-based; converted numeric Cell(row,col)/Column(i) to Canvas's 0-based equivalent. Verify any computed indexes."));
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
            return !n.StartsWith("ClosedXML", StringComparison.Ordinal);
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

        if (names.Overlaps(new[] { "PivotTable", "PivotTables", "AddPivotTable" }))
            yield return Warning("CANMIGCLXL030", "Pivot tables are not supported by the Canvas spreadsheet engine; migrate manually.");

        if (names.Overlaps(new[] { "AddConditionalFormat", "SetAutoFilter", "SetDataValidation", "CreateDataValidation" }))
            yield return Warning("CANMIGCLXL031",
                "Conditional formatting / auto-filter / data validation are set on the SpreadsheetDto model (ws.ToWorkbook().Sheets[..]), not the fluent builder yet — migrate manually.");
    }

    private static MigrationDiagnostic Info(string id, string message) => new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Info };
    private static MigrationDiagnostic Warn(string id, string message) => new() { Id = id, Message = message, Severity = MigrationDiagnosticSeverity.Warning };
    private static MigrationDiagnostic Warning(string id, string message) => Warn(id, message);

    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

    // ── the rewriter ─────────────────────────────────────────────────────────────────────────────────
    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        public List<MigrationDiagnostic> Diagnostics { get; } = [];
        public bool AppliedIndexShift { get; private set; }

        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var visited = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!;
            if (SimpleTypeName(visited.Type) == "XLWorkbook")
                return visited.WithType(SyntaxFactory.IdentifierName("CanvasWorkbook").WithTriviaFrom(visited.Type));
            return visited;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var name = ma.Name.Identifier.ValueText;
            var argc = visited.ArgumentList.Arguments.Count;

            // wb.Worksheets.Add("X") / wb.AddWorksheet("X") → wb.AddSheet("X")
            if (name == "Add" && ma.Expression is MemberAccessExpressionSyntax inner && inner.Name.Identifier.ValueText == "Worksheets")
                return visited.WithExpression(ma.WithExpression(inner.Expression).WithName(SyntaxFactory.IdentifierName("AddSheet")));
            if (name == "AddWorksheet")
                return visited.WithExpression(ma.WithName(SyntaxFactory.IdentifierName("AddSheet")));

            // wb.SaveAs(path) → wb.Save(path)
            if (name == "SaveAs")
                return visited.WithExpression(ma.WithName(SyntaxFactory.IdentifierName("Save")));

            // numeric Cell(row, col) → 0-based (subtract 1 from each)
            if (name == "Cell" && argc == 2)
            {
                AppliedIndexShift = true;
                return visited.WithArgumentList(ShiftArgs(visited.ArgumentList));
            }

            // numeric Column(i) / Row(i) → 0-based
            if ((name == "Column" || name == "Row") && argc == 1 && IsNumeric(visited.ArgumentList.Arguments[0].Expression))
            {
                AppliedIndexShift = true;
                if (name == "Row")
                    Diagnostics.Add(Warn("CANMIGCLXL021", "ws.Row(i) has no direct Canvas builder method; review row height/grouping manually."));
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

            // X.Value = v → X.Value(v);  X.Width/Height = v → X.Width(v)/Height(v)
            if (member is "Value" or "Width" or "Height")
                return Call(lhs.Expression, member, rhs);

            // X.FormulaA1 = "f" → X.Formula("f")  (R1C1 flagged)
            if (member is "FormulaA1" or "FormulaR1C1")
            {
                if (member == "FormulaR1C1")
                    Diagnostics.Add(Warn("CANMIGCLXL022", "R1C1 formula converted as-is; Canvas expects A1 syntax — review."));
                return Call(lhs.Expression, "Formula", rhs);
            }

            // X.Style.Font.Bold / .Italic / .FontSize = v → X.Style(s => s.Bold(v) / ...)
            if (TryStyleChain(lhs, out var cellExpr, out var styleMethod))
                return StyleLambda(cellExpr!, styleMethod!, rhs);

            if (IsStyleChain(lhs))
                Diagnostics.Add(Warn("CANMIGCLXL020", $"Style assignment '{lhs}' needs manual migration to .Style(s => …) (e.g. Fill/Border/Alignment)."));

            return visited;
        }

        // ── helpers ──
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

        // X.Style(s => s.<method>(<arg>))
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

        // Detects X.Style.Font.<Bold|Italic|FontSize> and returns the cell expression + Canvas style method.
        private static bool TryStyleChain(MemberAccessExpressionSyntax lhs, out ExpressionSyntax? cellExpr, out string? styleMethod)
        {
            cellExpr = null; styleMethod = null;
            var leaf = lhs.Name.Identifier.ValueText;
            styleMethod = leaf switch { "Bold" => "Bold", "Italic" => "Italic", "FontSize" => "FontSize", _ => null };
            if (styleMethod is null) return false;
            // lhs.Expression must be  <cell>.Style.Font
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
