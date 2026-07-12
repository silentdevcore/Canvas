using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.SyncfusionXlsIo;

/// <summary>
/// Migrates Syncfusion XlsIO (<c>ExcelEngine</c> / <c>IWorkbook</c>) authoring code to the Canvas spreadsheet
/// API (<c>CanvasWorkbook</c>). Roslyn-based: drops the ExcelEngine/IApplication scaffolding, maps
/// <c>Workbooks.Create</c> → <c>new CanvasWorkbook()</c>, <c>Range["A1"]</c> indexer, Text/Value/Number/
/// Formula, CellStyle.Font.Bold, merge, SetColumnWidth, SaveAs.
/// </summary>
public sealed class SyncfusionXlsIoMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();

        // Pre-scan: ExcelEngine variables + the IApplication variables assigned from <engine>.Excel.
        var engineVars = new HashSet<string>(StringComparer.Ordinal);
        var appVars = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            var init = v.Initializer?.Value;
            if (init is ObjectCreationExpressionSyntax oce && SimpleTypeName(oce.Type) == "ExcelEngine")
                engineVars.Add(v.Identifier.ValueText);
            else if (init is MemberAccessExpressionSyntax ma && ma.Name.Identifier.ValueText == "Excel"
                     && ma.Expression is IdentifierNameSyntax eid && engineVars.Contains(eid.Identifier.ValueText))
                appVars.Add(v.Identifier.ValueText);
        }

        var rewriter = new Rewriter(engineVars, appVars);
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
        var kept = root.Usings.Where(u => !(u.Name?.ToString() ?? "").StartsWith("Syncfusion", StringComparison.Ordinal)).ToList();
        const string canvasNs = "PXA.Infrastructure.Spreadsheet";
        if (!kept.Any(u => u.Name?.ToString() == canvasNs))
            kept.Insert(0, SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(canvasNs))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
        return root.WithUsings(SyntaxFactory.List(kept));
    }

    private static IEnumerable<MigrationDiagnostic> ScanUnsupported(CompilationUnitSyntax root)
    {
        var names = root.DescendantNodes().OfType<IdentifierNameSyntax>().Select(i => i.Identifier.ValueText).ToHashSet(StringComparer.Ordinal);
        if (names.Overlaps(new[] { "IChart", "Chart", "Charts", "IPivotTable", "PivotTables" }))
            yield return Warn("CANMIGSFXL030", "Charts / pivot tables are not supported by the Canvas spreadsheet engine; migrate manually.");
    }

    private static MigrationDiagnostic Info(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Info };
    private static MigrationDiagnostic Warn(string id, string m) => new() { Id = id, Message = m, Severity = MigrationDiagnosticSeverity.Warning };
    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

    private static string SimpleTypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        QualifiedNameSyntax q => q.Right.Identifier.ValueText,
        _ => type.ToString(),
    };

    private sealed class Rewriter(HashSet<string> engineVars, HashSet<string> appVars) : CSharpSyntaxRewriter
    {
        public List<MigrationDiagnostic> Diagnostics { get; } = [];
        private bool _notedDefaultSheet;

        // Drop the ExcelEngine / IApplication declarations and engine.Dispose() — top-level + in-method.
        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
            => IsScaffolding(node.Statement) ? null : base.VisitGlobalStatement(node);

        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
            => DeclaresScaffoldVar(node) ? null : base.VisitLocalDeclarationStatement(node);

        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
            => node.Parent is not GlobalStatementSyntax && IsEngineDispose(node) ? null : base.VisitExpressionStatement(node);

        private bool IsScaffolding(StatementSyntax s) =>
            (s is LocalDeclarationStatementSyntax l && DeclaresScaffoldVar(l)) || (s is ExpressionStatementSyntax e && IsEngineDispose(e));

        private bool DeclaresScaffoldVar(LocalDeclarationStatementSyntax node) =>
            node.Declaration.Variables.Any(v => engineVars.Contains(v.Identifier.ValueText) || appVars.Contains(v.Identifier.ValueText));

        private bool IsEngineDispose(ExpressionStatementSyntax node) =>
            node.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Dispose", Expression: IdentifierNameSyntax id } }
            && engineVars.Contains(id.Identifier.ValueText);

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var name = ma.Name.Identifier.ValueText;

            // <app>.Workbooks.Create(n) → new CanvasWorkbook()
            if (name == "Create" && ma.Expression is MemberAccessExpressionSyntax wbs && wbs.Name.Identifier.ValueText == "Workbooks")
            {
                if (!_notedDefaultSheet)
                {
                    Diagnostics.Add(Info("CANMIGSFXL011", "Workbooks.Create(n) maps to new CanvasWorkbook(); Worksheets[0] → AddSheet(\"Sheet1\")."));
                    _notedDefaultSheet = true;
                }
                return SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName("CanvasWorkbook"))
                    .WithArgumentList(SyntaxFactory.ArgumentList());
            }

            if (name == "SaveAs")
                return visited.WithExpression(ma.WithName(SyntaxFactory.IdentifierName("Save")))
                    .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(visited.ArgumentList.Arguments.Take(1))));

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

        // worksheet.Range["A1"] → Cell("A1");  Range["A1:B1"] → Range("A1:B1");  Worksheets[0] → AddSheet
        public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            var visited = (ElementAccessExpressionSyntax)base.VisitElementAccessExpression(node)!;
            if (visited.Expression is not MemberAccessExpressionSyntax ma) return visited;
            var member = ma.Name.Identifier.ValueText;
            var args = visited.ArgumentList.Arguments;

            if (member == "Range" && args.Count == 1)
            {
                var isRange = args[0].Expression is LiteralExpressionSyntax { Token.ValueText: var t } && t.Contains(':');
                return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName(isRange ? "Range" : "Cell")),
                    SyntaxFactory.ArgumentList(args));
            }
            if (member == "Worksheets" && args.Count == 1)
            {
                if (args[0].Expression is LiteralExpressionSyntax { Token.Value: 0 })
                    return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName("AddSheet")),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Sheet1"))))));
                return SyntaxFactory.InvocationExpression(ma.WithName(SyntaxFactory.IdentifierName("Sheet")), SyntaxFactory.ArgumentList(args));
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

            if (member is "Text" or "Value" or "Number")
                return Call(lhs.Expression, "Value", rhs);
            if (member == "Formula")
                return Call(lhs.Expression, "Formula", rhs);
            if (TryStyleChain(lhs, out var cell, out var styleMethod))
                return StyleLambda(cell!, styleMethod!, rhs);
            if (IsStyleChain(lhs))
                Diagnostics.Add(Warn("CANMIGSFXL020", $"Style assignment '{lhs}' needs manual migration to .Style(s => …)."));
            return visited;
        }

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

        // <cell>.CellStyle.Font.<Bold|Italic> / .Size   (also accepts Style.Font.*)
        private static bool TryStyleChain(MemberAccessExpressionSyntax lhs, out ExpressionSyntax? cellExpr, out string? styleMethod)
        {
            cellExpr = null;
            styleMethod = lhs.Name.Identifier.ValueText switch { "Bold" => "Bold", "Italic" => "Italic", "Size" => "FontSize", _ => null };
            if (styleMethod is null) return false;
            if (lhs.Expression is MemberAccessExpressionSyntax font && font.Name.Identifier.ValueText == "Font"
                && font.Expression is MemberAccessExpressionSyntax style && style.Name.Identifier.ValueText is "CellStyle" or "Style")
            {
                cellExpr = style.Expression;
                return true;
            }
            styleMethod = null;
            return false;
        }

        private static bool IsStyleChain(MemberAccessExpressionSyntax lhs) =>
            lhs.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>().Any(m => m.Name.Identifier.ValueText is "CellStyle" or "Style");
    }
}
