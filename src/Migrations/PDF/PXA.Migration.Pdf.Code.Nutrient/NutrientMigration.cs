using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;

namespace PXA.Migration.Pdf.Code.Nutrient;

/// <summary>
/// Migrates supported Nutrient .NET SDK and legacy GdPicture.NET PDF-generation
/// patterns to PXA.Pdf while diagnosing operations that need manual review.
/// </summary>
public sealed class NutrientMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
        var documentVariables = FindDocumentVariables(root);
        var rewriter = new NutrientRewriter(documentVariables);
        var migrated = (CompilationUnitSyntax)rewriter.Visit(root)!;
        migrated = RemoveNutrientUsings(migrated);
        migrated = EnsurePxaUsing(migrated);

        var diagnostics = rewriter.Diagnostics.ToList();
        diagnostics.AddRange(ScanUnsupportedCapabilities(root));

        return new MigrationResult
        {
            MigratedCode = migrated.NormalizeWhitespace().ToFullString()
                .Replace("\r\n", "\n", StringComparison.Ordinal),
            Diagnostics = diagnostics
                .GroupBy(static diagnostic => (diagnostic.Id, diagnostic.Message))
                .Select(static group => group.First())
                .ToArray(),
        };
    }

    private static HashSet<string> FindDocumentVariables(CompilationUnitSyntax root) =>
        root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(static creation => SimpleName(creation.Type) == "GdPicturePDF")
            .Select(static creation => creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault())
            .Where(static declaration => declaration is not null)
            .Select(static declaration => declaration!.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<MigrationDiagnostic> ScanUnsupportedCapabilities(CompilationUnitSyntax root)
    {
        var methodNames = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(static invocation => invocation.Expression is MemberAccessExpressionSyntax member
                ? member.Name.Identifier.ValueText
                : string.Empty)
            .Where(static name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        if (methodNames.Overlaps(new[]
            {
                "LoadFromFile", "LoadFromStream", "ClonePage", "MovePage", "DeletePage",
                "MergeDocuments", "Merge2Documents", "InsertPage", "SelectPage"
            }))
        {
            yield return Warning("CANMIGNUTNET020",
                "Existing-PDF editing, page selection, insertion, merge, or reordering requires review; the source operation was preserved.");
        }

        if (methodNames.Any(static name =>
                name.Contains("Sign", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Certificate", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase)))
        {
            yield return Warning("CANMIGNUTNET021",
                "Digital signatures, certificates, timestamps, PAdES, and validation are deferred to the PXA PDF Trust Foundation; source calls were preserved.");
        }

        if (methodNames.Any(static name =>
                name.Contains("Redact", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Redaction", StringComparison.OrdinalIgnoreCase)))
        {
            yield return Warning("CANMIGNUTNET022",
                "Nutrient redaction cannot yet be migrated as irreversible content removal; source calls were preserved and must not be replaced by visual overlays.");
        }

        if (methodNames.Any(static name =>
                !string.Equals(name, "ConvertToPDFA", StringComparison.Ordinal) &&
                (name.Contains("PdfA", StringComparison.OrdinalIgnoreCase)
                || name.Contains("PdfUA", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Conformance", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Linear", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Encrypt", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Security", StringComparison.OrdinalIgnoreCase))))
        {
            yield return Warning("CANMIGNUTNET023",
                "PDF/A, PDF/UA, encryption, security, conformance, and linearization require review until equivalent PXA trust capabilities are available.");
        }

        if (methodNames.Any(static name =>
                name.Contains("OCR", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Twain", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Wia", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Print", StringComparison.OrdinalIgnoreCase)
                || name.Contains("HyperCompress", StringComparison.OrdinalIgnoreCase)))
        {
            yield return Warning("CANMIGNUTNET024",
                "OCR, scanning, native printing, and hyper-compression require migration to separate PXA application services or manual replacement.");
        }

        if (methodNames.Any(static name =>
                name.StartsWith("Add", StringComparison.Ordinal) && name.Contains("Annotation", StringComparison.Ordinal)
                || name.StartsWith("Add", StringComparison.Ordinal) && name.Contains("FormField", StringComparison.Ordinal)
                || name.StartsWith("SetFormField", StringComparison.Ordinal)
                || name.StartsWith("ImportXFDF", StringComparison.Ordinal)))
        {
            yield return Warning("CANMIGNUTNET025",
                "Nutrient annotations, forms, and XFDF were detected but are not part of the initial C# generation mapping; source calls were preserved.");
        }
    }

    private static CompilationUnitSyntax RemoveNutrientUsings(CompilationUnitSyntax root)
    {
        var usings = root.Usings.Where(static directive =>
        {
            var name = directive.Name?.ToString() ?? string.Empty;
            return !name.StartsWith("GdPicture", StringComparison.Ordinal)
                && !name.StartsWith("Nutrient", StringComparison.Ordinal);
        });
        return root.WithUsings(SyntaxFactory.List(usings));
    }

    private static CompilationUnitSyntax EnsurePxaUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static directive => directive.Name?.ToString() == "PXA.Pdf"))
            return root;

        return root.WithUsings(root.Usings.Insert(0,
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("PXA.Pdf"))));
    }

    private static string SimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => type.ToString().Split('.').Last(),
    };

    private static MigrationDiagnostic Info(string id, string message) => new()
    {
        Id = id,
        Message = message,
        Severity = MigrationDiagnosticSeverity.Info,
    };

    private static MigrationDiagnostic Warning(string id, string message) => new()
    {
        Id = id,
        Message = message,
        Severity = MigrationDiagnosticSeverity.Warning,
    };

    private sealed class NutrientRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _documentVariables;
        private readonly List<MigrationDiagnostic> _diagnostics = [];
        private string _currentPageVariable = "page";
        private int _pageCount;
        private double _unitScale = 1;
        private bool _topLeftOrigin;
        private string _fontSize = "12";
        private bool _pdfA2b;

        public NutrientRewriter(HashSet<string> documentVariables)
        {
            _documentVariables = documentVariables;
        }

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            var statement = Visit(node.Statement) as StatementSyntax;
            return statement is null ? null : node.WithStatement(statement);
        }

        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            if (TryRewriteDocumentDeclaration(node, out var documentDeclaration))
                return documentDeclaration;

            if (TryRewriteFontDeclaration(node, out var fontDeclaration))
                return fontDeclaration;

            return base.VisitLocalDeclarationStatement(node);
        }

        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (node.Expression is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || !_documentVariables.Contains(member.Expression.ToString()))
            {
                return base.VisitExpressionStatement(node);
            }

            var method = member.Name.Identifier.ValueText;
            var arguments = invocation.ArgumentList.Arguments;

            switch (method)
            {
                case "NewPDF":
                    if (arguments.Count > 0 && arguments[0].ToString().Contains("PDF_A_2b", StringComparison.OrdinalIgnoreCase))
                    {
                        _pdfA2b = true;
                        _diagnostics.Add(Warning("CANMIGNUTNET032",
                            "Nutrient PDF/A-2b was mapped to PdfComplianceProfile.PdfA2B. Configure PdfFontLoader and review unsupported annotations before export."));
                    }
                    else if (arguments.Count > 0 && !arguments[0].ToString().EndsWith(".PDF", StringComparison.Ordinal))
                    {
                        _diagnostics.Add(Warning("CANMIGNUTNET006",
                            "NewPDF conformance was removed from generated code because PXA does not yet provide equivalent PDF/A or PDF/UA guarantees."));
                    }
                    else
                    {
                        _diagnostics.Add(Info("CANMIGNUTNET002",
                            "NewPDF() removed because new PdfDocument() already creates an empty document."));
                    }
                    return null;

                case "ConvertToPDFA":
                    if (arguments.Count > 0 && arguments[0].ToString().Contains("PDF_A_2b", StringComparison.OrdinalIgnoreCase))
                    {
                        _pdfA2b = true;
                        _diagnostics.Add(Warning("CANMIGNUTNET032",
                            "Nutrient PDF/A-2b conversion was mapped to PDF/A-2b generation. This does not preserve an existing source PDF; verify the regenerated document."));
                        return null;
                    }

                    _diagnostics.Add(Warning("CANMIGNUTNET006",
                        "Only Nutrient PDF/A-2b is mapped automatically; this conformance conversion requires review."));
                    return null;

                case "SetLicenseNumber":
                case "SetLicenseKey":
                case "RegisterLicense":
                    _diagnostics.Add(Warning("CANMIGNUTNET026",
                        "A Nutrient or GdPicture license configuration call was removed. Configure PXA licensing separately and rotate any committed credential."));
                    return null;

                case "NewPage":
                    return RewriteNewPage(node, arguments);

                case "SaveToFile":
                    if (arguments.Count == 0)
                        return base.VisitExpressionStatement(node);
                    _diagnostics.Add(Info("CANMIGNUTNET005", "SaveToFile(path) converted to PdfDocument.Save(path)."));
                    return ParseStatement(_pdfA2b
                        ? $"{member.Expression}.Save({arguments[0]}, new PdfSaveOptions {{ ComplianceProfile = PdfComplianceProfile.PdfA2B }});"
                        : $"{member.Expression}.Save({arguments[0]});", node);

                case "SetOrigin":
                    _topLeftOrigin = arguments.Count > 0
                        && arguments[0].ToString().Contains("TopLeft", StringComparison.Ordinal);
                    _diagnostics.Add(Info("CANMIGNUTNET007",
                        $"Nutrient coordinate origin recorded as {(_topLeftOrigin ? "top-left" : "bottom-left")}."));
                    return null;

                case "SetMeasurementUnit":
                    if (arguments.Count > 0)
                        _unitScale = UnitScale(arguments[0].ToString());
                    _diagnostics.Add(Info("CANMIGNUTNET008",
                        "Nutrient measurement units were normalized to PDF points for supported draw calls."));
                    return null;

                case "SetTextSize":
                    if (arguments.Count > 0)
                        _fontSize = Scale(arguments[0].ToString());
                    _diagnostics.Add(Info("CANMIGNUTNET009", "Nutrient text size was captured for subsequent text drawing."));
                    return null;

                case "DrawText":
                    return RewriteDrawText(node, arguments);

                case "Dispose":
                    _diagnostics.Add(Info("CANMIGNUTNET010", "GdPicturePDF.Dispose() removed; PdfDocument does not own unmanaged SDK state."));
                    return null;

                default:
                    _diagnostics.Add(Warning("CANMIGNUTNET031",
                        $"Nutrient operation '{method}' is not mapped by the Beta converter and was replaced with a review marker."));
                    return SyntaxFactory.EmptyStatement()
                        .WithLeadingTrivia(node.GetLeadingTrivia().Add(
                            SyntaxFactory.Comment($"// TODO: Migrate Nutrient {method}(...) manually. ")))
                        .WithTrailingTrivia(node.GetTrailingTrivia());
            }
        }

        private bool TryRewriteDocumentDeclaration(
            LocalDeclarationStatementSyntax node,
            out LocalDeclarationStatementSyntax rewritten)
        {
            rewritten = node;
            var variable = node.Declaration.Variables.SingleOrDefault();
            if (variable?.Initializer?.Value is not ObjectCreationExpressionSyntax creation
                || SimpleName(creation.Type) != "GdPicturePDF")
            {
                return false;
            }

            var newCreation = creation.WithType(SyntaxFactory.IdentifierName("PdfDocument"));
            var newVariable = variable.WithInitializer(variable.Initializer.WithValue(newCreation));
            var declaration = node.Declaration
                .WithType(SyntaxFactory.IdentifierName("var"))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(newVariable));

            rewritten = node
                .WithUsingKeyword(default)
                .WithAwaitKeyword(default)
                .WithDeclaration(declaration);
            _diagnostics.Add(Info("CANMIGNUTNET001", "GdPicturePDF construction converted to new PdfDocument()."));
            return true;
        }

        private bool TryRewriteFontDeclaration(
            LocalDeclarationStatementSyntax node,
            out LocalDeclarationStatementSyntax rewritten)
        {
            rewritten = node;
            var variable = node.Declaration.Variables.SingleOrDefault();
            if (variable?.Initializer?.Value is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.ValueText != "AddStandardFont"
                || !_documentVariables.Contains(member.Expression.ToString())
                || invocation.ArgumentList.Arguments.Count == 0)
            {
                return false;
            }

            var font = MapStandardFont(invocation.ArgumentList.Arguments[0].ToString());
            var declaration = node.Declaration
                .WithType(SyntaxFactory.IdentifierName("var"))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(
                    variable.WithInitializer(variable.Initializer.WithValue(
                        SyntaxFactory.ParseExpression($"PdfStandardFont.{font}")))));
            rewritten = node.WithDeclaration(declaration);
            _diagnostics.Add(Info("CANMIGNUTNET004", $"Nutrient standard font converted to PdfStandardFont.{font}."));
            return true;
        }

        private SyntaxNode RewriteNewPage(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            _pageCount++;
            _currentPageVariable = _pageCount == 1 ? "page" : $"page{_pageCount}";

            string addPage;
            if (arguments.Count == 1 && TryMapPagePreset(arguments[0].ToString(), out var preset))
                addPage = $"document.AddPage(PdfPagePreset.{preset})";
            else if (arguments.Count >= 2)
                addPage = $"document.AddPage({Scale(arguments[0].ToString())}, {Scale(arguments[1].ToString())})";
            else
                addPage = "document.AddPage()";

            var documentVariable = ((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)node.Expression).Expression)
                .Expression.ToString();
            addPage = addPage.Replace("document", documentVariable, StringComparison.Ordinal);

            _diagnostics.Add(Info("CANMIGNUTNET003", "Nutrient NewPage converted to PdfDocument.AddPage()."));
            return ParseStatement($"var {_currentPageVariable} = {addPage};", node);
        }

        private SyntaxNode RewriteDrawText(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (arguments.Count < 4)
            {
                _diagnostics.Add(Warning("CANMIGNUTNET030",
                    "DrawText overload could not be mapped safely and was preserved."));
                return base.VisitExpressionStatement(node)!;
            }

            var font = arguments[0].ToString();
            var x = Scale(arguments[1].ToString());
            var y = Scale(arguments[2].ToString());
            var text = arguments[3].ToString();
            var method = _topLeftOrigin ? "DrawTextFromTop" : "DrawText";
            var call = $"{_currentPageVariable}.{method}({text}, {x}, {y}, new PdfDrawTextOptions {{ FontSize = {_fontSize}, Font = {font} }});";
            _diagnostics.Add(Info("CANMIGNUTNET011", $"Nutrient DrawText converted to PdfPage.{method}."));
            return ParseStatement(call, node);
        }

        private static StatementSyntax ParseStatement(string statement, StatementSyntax source) =>
            SyntaxFactory.ParseStatement(statement)
                .WithLeadingTrivia(source.GetLeadingTrivia())
                .WithTrailingTrivia(source.GetTrailingTrivia());

        private string Scale(string expression)
        {
            if (_unitScale == 1)
                return expression;

            if (double.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return (value * _unitScale).ToString("0.####", CultureInfo.InvariantCulture);

            return $"({expression}) * {_unitScale.ToString("0.########", CultureInfo.InvariantCulture)}";
        }

        private static double UnitScale(string expression)
        {
            if (expression.Contains("Millimeter", StringComparison.Ordinal)) return 72d / 25.4d;
            if (expression.Contains("Centimeter", StringComparison.Ordinal)) return 72d / 2.54d;
            if (expression.Contains("Inch", StringComparison.Ordinal)) return 72d;
            return 1;
        }

        private static bool TryMapPagePreset(string expression, out string preset)
        {
            if (expression.EndsWith("PdfPageSizeA4", StringComparison.Ordinal))
            {
                preset = "A4";
                return true;
            }
            if (expression.EndsWith("PdfPageSizeA3", StringComparison.Ordinal))
            {
                preset = "A3";
                return true;
            }
            if (expression.EndsWith("PdfPageSizeLetter", StringComparison.Ordinal))
            {
                preset = "Letter";
                return true;
            }

            preset = string.Empty;
            return false;
        }

        private static string MapStandardFont(string expression)
        {
            if (expression.Contains("CourierBoldOblique", StringComparison.Ordinal)) return "CourierBoldOblique";
            if (expression.Contains("CourierOblique", StringComparison.Ordinal)) return "CourierOblique";
            if (expression.Contains("CourierBold", StringComparison.Ordinal)) return "CourierBold";
            if (expression.Contains("Courier", StringComparison.Ordinal)) return "Courier";
            if (expression.Contains("TimesBoldItalic", StringComparison.Ordinal)) return "TimesBoldItalic";
            if (expression.Contains("TimesItalic", StringComparison.Ordinal)) return "TimesItalic";
            if (expression.Contains("TimesBold", StringComparison.Ordinal)) return "TimesBold";
            if (expression.Contains("Times", StringComparison.Ordinal)) return "TimesRoman";
            if (expression.Contains("HelveticaBoldOblique", StringComparison.Ordinal)) return "HelveticaBoldOblique";
            if (expression.Contains("HelveticaOblique", StringComparison.Ordinal)) return "HelveticaOblique";
            if (expression.Contains("HelveticaBold", StringComparison.Ordinal)) return "HelveticaBold";
            return "Helvetica";
        }
    }
}
