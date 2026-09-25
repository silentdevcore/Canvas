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

        var semanticContext = CSharpMigrationSemanticContext.Create(
            sourceCode,
            "PXA.Migration.Nutrient.Semantic");
        var root = semanticContext.Root;
        var trustReport = NutrientTrustEvidence.Scan(root);
        var documentSymbols = new HashSet<ISymbol>(
            semanticContext.FindDeclaredSymbolsByType("GdPicturePDF"),
            SymbolEqualityComparer.Default);
        var documentVariables = FindDocumentVariables(root, semanticContext);
        bool IsDocumentReceiver(ExpressionSyntax expression)
        {
            var symbol = semanticContext.GetReferencedSymbol(expression);
            return symbol is not null
                ? documentSymbols.Contains(symbol)
                : documentVariables.Contains(expression.ToString());
        }
        foreach (var declaration in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declaration.Initializer?.Value is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Name.Identifier.ValueText == "MergeDocuments" &&
                IsDocumentReceiver(member.Expression))
            {
                documentVariables.Add(declaration.Identifier.ValueText);
                if (semanticContext.SemanticModel.GetDeclaredSymbol(declaration) is { } symbol)
                    documentSymbols.Add(symbol);
            }
        }

        var editableInvocations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax member
                && IsDocumentReceiver(member.Expression)
                && member.Name.Identifier.ValueText is "LoadFromFile" or "LoadFromStream"
                    or "MovePage" or "DeletePage" or "ClonePage" or "ClonePages" or "InsertPage"
                    or "SetPageBox" or "RemovePageBox"
                    or "MergeDocuments" or "Merge2Documents")
            .ToArray();
        var editablePdfVariables = editableInvocations
            .Select(static invocation => ((MemberAccessExpressionSyntax)invocation.Expression).Expression.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var editablePdfSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var invocation in editableInvocations)
        {
            var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
            if (semanticContext.GetReferencedSymbol(receiver) is { } symbol)
                editablePdfSymbols.Add(symbol);
        }
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member ||
                member.Name.Identifier.ValueText is not ("MergeDocuments" or "ClonePage" or "ClonePages") ||
                invocation.ArgumentList.Arguments.Count == 0)
                continue;

            editablePdfVariables.Add(member.Expression.ToString());
            if (semanticContext.GetReferencedSymbol(member.Expression) is { } targetSymbol)
                editablePdfSymbols.Add(targetSymbol);
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                foreach (var identifier in argument.Expression
                             .DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                             .Select(static identifier => identifier.Identifier.ValueText)
                             .Where(documentVariables.Contains))
                {
                    editablePdfVariables.Add(identifier);
                    if (semanticContext.GetReferencedSymbol(argument.Expression) is { } sourceSymbol)
                        editablePdfSymbols.Add(sourceSymbol);
                }
            }
        }
        foreach (var variable in documentVariables.Where(variable => root.DescendantNodes()
                     .OfType<VariableDeclaratorSyntax>()
                     .Any(declaration => declaration.Identifier.ValueText == variable &&
                         declaration.Initializer?.Value is InvocationExpressionSyntax invocation &&
                         invocation.Expression is MemberAccessExpressionSyntax member &&
                         member.Name.Identifier.ValueText == "MergeDocuments")))
            editablePdfVariables.Add(variable);

        foreach (var declaration in root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                     .Where(declaration => editablePdfVariables.Contains(declaration.Identifier.ValueText)))
        {
            if (semanticContext.SemanticModel.GetDeclaredSymbol(declaration) is { } symbol)
                editablePdfSymbols.Add(symbol);
        }

        var rewriter = new NutrientRewriter(
            documentVariables,
            editablePdfVariables,
            editablePdfSymbols,
            semanticContext,
            documentSymbols);
        var migrated = (CompilationUnitSyntax)rewriter.Visit(root)!;
        migrated = RemoveNutrientUsings(migrated);
        migrated = EnsurePxaUsing(migrated);
        if (editablePdfVariables.Count > 0)
            migrated = EnsureUsing(migrated, "PXA.Importer.Editing");

        var migratedCode = migrated.NormalizeWhitespace().ToFullString()
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        if (rewriter.ReviewMarkers.Count > 0)
        {
            migratedCode += "\n\n// PXA migration review\n" + string.Join(
                "\n",
                rewriter.ReviewMarkers
                    .Distinct(StringComparer.Ordinal)
                    .Select(static marker => $"// {marker}"));
        }

        var diagnostics = rewriter.Diagnostics.ToList();
        diagnostics.AddRange(ScanUnsupportedCapabilities(root));

        return new MigrationResult
        {
            MigratedCode = migratedCode,
            Diagnostics = diagnostics
                .GroupBy(static diagnostic => (diagnostic.Id, diagnostic.Message))
                .Select(static group => group.First())
                .ToArray(),
            PdfTrust = trustReport
        };
    }

    private static HashSet<string> FindDocumentVariables(
        CompilationUnitSyntax root,
        CSharpMigrationSemanticContext semanticContext)
    {
        var variables = root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => semanticContext.TypeMatches(creation.Type, "GdPicturePDF"))
            .Select(static creation => creation.Parent is EqualsValueClauseSyntax
                { Parent: VariableDeclaratorSyntax declaration }
                    ? declaration
                    : null)
            .Where(static declaration => declaration is not null)
            .Select(static declaration => declaration!.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var declaration in root.DescendantNodes().OfType<VariableDeclarationSyntax>()
                     .Where(declaration => semanticContext.TypeMatches(declaration.Type, "GdPicturePDF")))
        {
            foreach (var variable in declaration.Variables)
                variables.Add(variable.Identifier.ValueText);
        }
        foreach (var parameter in root.DescendantNodes().OfType<ParameterSyntax>()
                     .Where(parameter => parameter.Type is not null &&
                         semanticContext.TypeMatches(parameter.Type, "GdPicturePDF")))
            variables.Add(parameter.Identifier.ValueText);
        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
                     .Where(property => semanticContext.TypeMatches(property.Type, "GdPicturePDF")))
            variables.Add(property.Identifier.ValueText);
        return variables;
    }

    private static IEnumerable<MigrationDiagnostic> ScanUnsupportedCapabilities(CompilationUnitSyntax root)
    {
        if (root.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(static invocation =>
                invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Name.Identifier.ValueText == "MergeDocuments" &&
                invocation.ArgumentList.Arguments.Count > 1 &&
                invocation.ArgumentList.Arguments[1].ToString().Contains("PdfDocumentMergingOptions", StringComparison.Ordinal)))
        {
            yield return Warning("CANMIGNUTNET069",
                "Nutrient merge options control attachment handling. PXA preserves the call as a fail-closed review operation until catalog-level attachment migration is available.");
        }

        var methodNames = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(static invocation => invocation.Expression is MemberAccessExpressionSyntax member
                ? member.Name.Identifier.ValueText
                : string.Empty)
            .Where(static name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        if (methodNames.Any(static name =>
                name.Contains("Sign", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Certificate", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase)))
        {
            yield return Warning("CANMIGNUTNET021",
                "Digital signatures, certificates, timestamps, PAdES, and validation require a runtime-managed PXA signing key. Generated code contains an explicit Trust workflow action and never copies certificate secrets.");
        }

        if (methodNames.Any(static name =>
                name.Contains("Redact", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Redaction", StringComparison.OrdinalIgnoreCase)))
        {
            yield return Warning("CANMIGNUTNET022",
                "Nutrient redaction requires source regions and existing-PDF bytes. Generated code contains an explicit PXA Trust pipeline action and never substitutes a visual overlay.");
        }

        if (methodNames.Any(static name =>
                !string.Equals(name, "ConvertToPDFA", StringComparison.Ordinal) &&
                (name.Contains("PdfA", StringComparison.OrdinalIgnoreCase)
                || name.Contains("PdfUA", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Conformance", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Linear", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Security", StringComparison.OrdinalIgnoreCase))))
        {
            yield return Warning("CANMIGNUTNET023",
                "PDF/A, PDF/UA, encryption, security, conformance, and linearization require explicit adapter review; available PXA Trust capabilities are not substituted without equivalent source semantics.");
        }

        if (methodNames.Any(static name =>
                name.Contains("OCR", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Twain", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Wia", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Print", StringComparison.OrdinalIgnoreCase)
                || name.Contains("HyperCompress", StringComparison.OrdinalIgnoreCase)))
        {
            yield return Warning("CANMIGNUTNET024",
                "OCR, scanning, native printing, and hyper-compression cross application-service or hardware boundaries and are emitted as explicit review actions.");
        }

        if (methodNames.Any(static name =>
                name.StartsWith("Add", StringComparison.Ordinal) && name.Contains("Annotation", StringComparison.Ordinal)
                    && name is not "AddStickyNoteAnnotation" and not "AddLinkToWebAnnotation"
                || name.StartsWith("Add", StringComparison.Ordinal) && name.Contains("FormField", StringComparison.Ordinal)
                    && name is not "AddTextFormField" and not "AddCheckBoxFormField"
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
                && !name.StartsWith("Nutrient", StringComparison.Ordinal)
                && !(directive.Alias is not null && name == "PdfEditableDocument");
        });
        return root.WithUsings(SyntaxFactory.List(usings));
    }

    private static CompilationUnitSyntax EnsurePxaUsing(CompilationUnitSyntax root) =>
        EnsureUsing(root, "PXA.Pdf");

    private static CompilationUnitSyntax EnsureUsing(CompilationUnitSyntax root, string namespaceName)
    {
        if (root.Usings.Any(directive => directive.Name?.ToString() == namespaceName))
            return root;

        return root.WithUsings(root.Usings.Insert(0,
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName))));
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
        private readonly CSharpMigrationSemanticContext _semanticContext;
        private readonly IReadOnlySet<ISymbol> _documentSymbols;
        private readonly IReadOnlySet<ISymbol> _editablePdfSymbols;
        private readonly HashSet<string> _imageResourceVariables = [];
        private readonly List<MigrationDiagnostic> _diagnostics = [];
        private readonly List<string> _reviewMarkers = [];
        private readonly Dictionary<int, string> _pageVariables = [];
        private string _currentPageVariable = "page";
        private int _pageCount;
        private double _unitScale = 1;
        private bool _topLeftOrigin;
        private bool _rightOrigin;
        private string _fontSize = "12";
        private string _lineWidth = "1";
        private string _lineColor = "PdfColor.Black";
        private string _fillColor = "PdfColor.Black";
        private bool _pdfA2b;
        private string? _ownerPassword;
        private string? _userPassword;
        private readonly HashSet<string> _editablePdfVariables;

        public NutrientRewriter(
            HashSet<string> documentVariables,
            HashSet<string> editablePdfVariables,
            IReadOnlySet<ISymbol> editablePdfSymbols,
            CSharpMigrationSemanticContext semanticContext,
            IReadOnlySet<ISymbol> documentSymbols)
        {
            _documentVariables = documentVariables;
            _editablePdfVariables = editablePdfVariables;
            _editablePdfSymbols = editablePdfSymbols;
            _semanticContext = semanticContext;
            _documentSymbols = documentSymbols;
        }

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;
        public IReadOnlyList<string> ReviewMarkers => _reviewMarkers;

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (IsTypeReference(node) && _semanticContext.TypeMatches(node, "GdPicturePDF"))
                return SyntaxFactory.IdentifierName("PdfEditableDocument").WithTriviaFrom(node);

            return node.Identifier.ValueText switch
            {
                "GdPicturePDF" => SyntaxFactory.IdentifierName("PdfEditableDocument").WithTriviaFrom(node),
                "GdPictureStatus" => SyntaxFactory.IdentifierName("PdfPageOperationStatus").WithTriviaFrom(node),
                "PdfDocumentMergingOptions" => SyntaxFactory.IdentifierName("PdfEditableMergeOptions").WithTriviaFrom(node),
                "PdfPageBox" => SyntaxFactory.IdentifierName("PdfEditablePageBox").WithTriviaFrom(node),
                _ => base.VisitIdentifierName(node)
            };
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
        {
            if (_semanticContext.TypeMatches(node, "GdPicturePDF"))
                return SyntaxFactory.IdentifierName("PdfEditableDocument").WithTriviaFrom(node);
            return base.VisitQualifiedName(node);
        }

        public override SyntaxNode? VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
        {
            if (_semanticContext.TypeMatches(node, "GdPicturePDF"))
                return SyntaxFactory.IdentifierName("PdfEditableDocument").WithTriviaFrom(node);
            return base.VisitAliasQualifiedName(node);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Expression.ToString() == "GdPictureStatus")
            {
                var status = node.Name.Identifier.ValueText == "OK" ? "Success" : "Failed";
                return SyntaxFactory.ParseExpression($"PdfPageOperationStatus.{status}").WithTriviaFrom(node);
            }

            if (node.Expression.ToString() == "PdfPageSizes")
            {
                var size = node.Name.Identifier.ValueText switch
                {
                    "PdfPageSizeA4" => "A4",
                    "PdfPageSizeA3" => "A3",
                    "PdfPageSizeLetter" => "Letter",
                    _ => null
                };
                if (size is not null)
                    return SyntaxFactory.ParseExpression($"PdfEditablePageSize.{size}").WithTriviaFrom(node);
            }

            if (node.Expression.ToString() == "PdfDocumentMergingOptions")
            {
                return SyntaxFactory.ParseExpression($"PdfEditableMergeOptions.{node.Name.Identifier.ValueText}")
                    .WithTriviaFrom(node);
            }

            if (node.Expression.ToString() == "PdfPageBox" &&
                node.Name.Identifier.ValueText == "PdfPageBoxCropBox")
            {
                return SyntaxFactory.ParseExpression("PdfEditablePageBox.CropBox").WithTriviaFrom(node);
            }

            if (node.Expression.ToString() == "PdfMeasurementUnit")
            {
                var unit = node.Name.Identifier.ValueText switch
                {
                    "PdfMeasurementUnitMillimeter" => "Millimeter",
                    "PdfMeasurementUnitCentimeter" => "Centimeter",
                    "PdfMeasurementUnitInch" => "Inch",
                    _ => "Point"
                };
                return SyntaxFactory.ParseExpression($"PdfEditableMeasurementUnit.{unit}").WithTriviaFrom(node);
            }

            if (_editablePdfVariables.Contains(node.Expression.ToString()))
            {
                var targetName = node.Name.Identifier.ValueText switch
                {
                    "GetStat" => "GetStatus",
                    "NewPDF" => "NewDocument",
                    _ => node.Name.Identifier.ValueText
                };
                if (targetName != node.Name.Identifier.ValueText)
                    return node.WithName(SyntaxFactory.IdentifierName(targetName).WithTriviaFrom(node.Name));
            }

            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            var statement = Visit(node.Statement) as StatementSyntax;
            return statement is null ? null : node.WithStatement(statement);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax member && IsDocumentReceiver(member.Expression))
            {
                var method = member.Name.Identifier.ValueText;
                if (method == "ClonePages" && IsEditablePdf(member.Expression))
                {
                    var arguments = node.ArgumentList.Arguments;
                    if (!IsSupportedClonePages(arguments))
                    {
                        _diagnostics.Add(Warning("CANMIGNUTNET071",
                            "Only ClonePages(FromPDF, PageRange) is mapped; this overload requires explicit review."));
                        _reviewMarkers.Add(
                            "[CANMIGNUTNET071] ClonePages: use PdfEditableDocument.ClonePages with a loaded source and a validated page-range string.");
                        return FailedStatus(node);
                    }

                    _diagnostics.Add(Info("CANMIGNUTNET071",
                        "Nutrient ClonePages(FromPDF, PageRange) converted to lossless existing-PDF page appending."));
                }

                if (TrustReviewMethods.Contains(method))
                {
                    var diagnosticId = method is "VerifySignature" or "ValidateSignature"
                        ? "CANMIGNUTNET072"
                        : method.Contains("Redact", StringComparison.OrdinalIgnoreCase)
                            ? "CANMIGNUTNET058"
                            : "CANMIGNUTNET059";
                    _diagnostics.Add(Warning(diagnosticId,
                        $"Nutrient {method} requires an explicit PXA Trust workflow and was not executed during migration."));
                    _reviewMarkers.Add(
                        $"[{diagnosticId}] {method}: configure and execute the server-owned PXA Trust workflow.");
                    return FailedStatus(node);
                }
            }

            return base.VisitInvocationExpression(node);
        }

        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            if (TryRewriteDocumentDeclaration(node, out var documentDeclaration))
                return documentDeclaration;

            if (TryRewriteFontDeclaration(node, out var fontDeclaration))
                return fontDeclaration;

            if (TryRewriteImageResourceDeclaration(node, out var imageDeclaration))
                return imageDeclaration;

            if (TryRewriteFormFieldDeclaration(node, out var formFieldStatement))
                return formFieldStatement;

            return base.VisitLocalDeclarationStatement(node);
        }

        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (node.Expression is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || !IsDocumentReceiver(member.Expression))
            {
                return base.VisitExpressionStatement(node);
            }

            var method = member.Name.Identifier.ValueText;
            var arguments = invocation.ArgumentList.Arguments;

            switch (method)
            {
                case "LoadFromFile":
                    if (!IsEditablePdf(member.Expression) || arguments.Count is < 1 or > 2)
                        return ReviewOperation("CANMIGNUTNET062", "LoadFromFile",
                            "Load the PDF bytes through the PXA existing-document workflow.",
                            "LoadFromFile requires a source path and an optional load-in-memory flag for automatic migration.");
                    _diagnostics.Add(Info("CANMIGNUTNET062",
                        "Nutrient LoadFromFile converted to PdfEditableDocument.LoadFromFile; its optional load-in-memory flag is normalized to bounded PXA processing."));
                    return base.VisitExpressionStatement(node);

                case "LoadFromStream":
                    if (!IsEditablePdf(member.Expression) || arguments.Count is < 1 or > 2)
                        return ReviewOperation("CANMIGNUTNET065", "LoadFromStream",
                            "Load the PDF through PdfEditableDocument.LoadFromStream with explicit stream ownership.",
                            "LoadFromStream requires a source stream and an optional ownership flag.");
                    _diagnostics.Add(Info("CANMIGNUTNET065",
                        "Nutrient LoadFromStream converted to PdfEditableDocument.LoadFromStream with matching ownership semantics."));
                    return base.VisitExpressionStatement(node);

                case "DeletePage":
                    if (!IsEditablePdf(member.Expression) || arguments.Count != 1)
                        return ReviewOperation("CANMIGNUTNET063", "DeletePage",
                            "Use PdfDeletePageOperation with an explicit one-based page number.",
                            "DeletePage requires an existing-PDF source and one explicit page number.");
                    _diagnostics.Add(Info("CANMIGNUTNET063",
                        "Nutrient DeletePage converted to PdfEditableDocument.DeletePage."));
                    return base.VisitExpressionStatement(node);

                case "MovePage":
                    if (!IsEditablePdf(member.Expression) || arguments.Count != 2)
                        return ReviewOperation("CANMIGNUTNET064", "MovePage",
                            "Use PdfMovePageOperation with explicit one-based source and final target positions.",
                            "MovePage requires an existing-PDF source, source page, and final target page.");
                    _diagnostics.Add(Info("CANMIGNUTNET064",
                        "Nutrient MovePage converted to PdfEditableDocument.MovePage."));
                    return base.VisitExpressionStatement(node);

                case "ClonePage":
                    if (!IsEditablePdf(member.Expression) || arguments.Count is < 1 or > 2)
                        return ReviewOperation("CANMIGNUTNET066", "ClonePage",
                            "Use PdfEditableDocument.ClonePage with a loaded source document and one-based page number.",
                            "ClonePage requires an explicit page number and an optional PXA editable source document.");
                    _diagnostics.Add(Info("CANMIGNUTNET066",
                        "Nutrient ClonePage converted to lossless cross-document page cloning."));
                    return base.VisitExpressionStatement(node);

                case "ClonePages":
                    if (!IsEditablePdf(member.Expression) || !IsSupportedClonePages(arguments))
                    {
                        return ReviewOperation("CANMIGNUTNET071", "ClonePages",
                            "Use PdfEditableDocument.ClonePages with a loaded source document and a validated one-based page range.",
                            "Only ClonePages(loadedSource, pageRange) is mapped; other overloads remain explicit review cases.");
                    }
                    _diagnostics.Add(Info("CANMIGNUTNET071",
                        "Nutrient ClonePages(FromPDF, PageRange) converted to lossless existing-PDF page appending."));
                    return base.VisitExpressionStatement(node);

                case "SetPageBox":
                    if (!IsEditablePdf(member.Expression) || arguments.Count != 5 ||
                        !arguments[0].ToString().EndsWith("PdfPageBoxCropBox", StringComparison.Ordinal))
                    {
                        return ReviewOperation("CANMIGNUTNET070", "SetPageBox",
                            "Use PdfEditableDocument.SetPageBox for the CropBox with bottom-left PDF-point coordinates.",
                            "Only SetPageBox(PdfPageBoxCropBox, left, top, right, bottom) is mapped automatically.");
                    }
                    _diagnostics.Add(Info("CANMIGNUTNET070",
                        "Nutrient SetPageBox(CropBox, left, top, right, bottom) converted to the typed PXA CropBox operation."));
                    return ParseStatement(
                        $"{member.Expression}.SetPageBox(PdfEditablePageBox.CropBox, {arguments[1]}, {arguments[2]}, {arguments[3]}, {arguments[4]});",
                        node);

                case "RemovePageBox":
                    if (!IsEditablePdf(member.Expression) || arguments.Count != 1 ||
                        !arguments[0].ToString().EndsWith("PdfPageBoxCropBox", StringComparison.Ordinal))
                    {
                        return ReviewOperation("CANMIGNUTNET070", "RemovePageBox",
                            "Use PdfEditableDocument.RemovePageBox for the CropBox.",
                            "Only RemovePageBox(PdfPageBoxCropBox) is mapped automatically.");
                    }
                    _diagnostics.Add(Info("CANMIGNUTNET070",
                        "Nutrient RemovePageBox(CropBox) converted to the typed PXA CropBox reset operation."));
                    return ParseStatement($"{member.Expression}.RemovePageBox(PdfEditablePageBox.CropBox);", node);

                case "Merge2Documents":
                    if (!IsEditablePdf(member.Expression) || arguments.Count is < 2 or > 3)
                        return ReviewOperation("CANMIGNUTNET067", "Merge2Documents",
                            "Use PdfEditableDocument.Merge2Documents with two source paths and a distinct destination path.",
                            "Merge2Documents requires either two loaded source documents or two source paths and one destination path.");
                    _diagnostics.Add(Info("CANMIGNUTNET067",
                        "Nutrient Merge2Documents converted to collision-safe PXA page composition."));
                    return base.VisitExpressionStatement(node);

                case "InsertPage":
                    if (!IsEditablePdf(member.Expression) || arguments.Count is < 2 or > 3)
                        return ReviewOperation("CANMIGNUTNET068", "InsertPage",
                            "Use PdfEditableDocument.InsertPage with a supported preset or explicit point dimensions and one-based position.",
                            "InsertPage overload could not be mapped to a supported blank-page insertion.");
                    _diagnostics.Add(Info("CANMIGNUTNET068",
                        "Nutrient InsertPage converted to PdfEditableDocument.InsertPage."));
                    return base.VisitExpressionStatement(node);

                case "NewPDF":
                    if (IsEditablePdf(member.Expression))
                    {
                        _diagnostics.Add(Info("CANMIGNUTNET002",
                            "NewPDF converted to PdfEditableDocument.NewDocument."));
                        return ParseStatement($"{member.Expression}.NewDocument();", node);
                    }
                    if (arguments.Count > 0 && arguments[0].ToString().Contains("PDF_A_2b", StringComparison.OrdinalIgnoreCase))
                    {
                        _pdfA2b = true;
                        _diagnostics.Add(Warning("CANMIGNUTNET032",
                            "Nutrient PDF/A-2b was mapped to PdfComplianceProfile.PdfA2B. Configure PdfFontLoader and review unsupported annotations before export."));
                    }
                    else if (arguments.Count > 0 && !arguments[0].ToString().EndsWith(".PDF", StringComparison.Ordinal))
                    {
                        _diagnostics.Add(Warning("CANMIGNUTNET006",
                            "Only PDF/A-2b is currently mapped automatically; the requested NewPDF conformance requires explicit PXA Trust review."));
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

                case "SelectPage":
                    if (IsEditablePdf(member.Expression))
                    {
                        _diagnostics.Add(Info("CANMIGNUTNET012",
                            "Nutrient SelectPage converted to the stateful PdfEditableDocument page selection."));
                        return base.VisitExpressionStatement(node);
                    }
                    return RewriteSelectPage(node, arguments);

                case "RotatePage":
                    return RewriteRotatePage(node, arguments);

                case "SaveToFile":
                    if (arguments.Count == 0)
                        return base.VisitExpressionStatement(node);
                    if (IsEditablePdf(member.Expression))
                    {
                        _diagnostics.Add(Info("CANMIGNUTNET005",
                            "SaveToFile(path) converted to PdfEditableDocument.SaveToFile(path)."));
                        return base.VisitExpressionStatement(node);
                    }
                    _diagnostics.Add(Info("CANMIGNUTNET005", "SaveToFile(path) converted to PdfDocument.Save(path)."));
                    return ParseStatement(BuildSaveStatement(member.Expression.ToString(), arguments[0].ToString()), node);

                case "Encrypt":
                    if (IsEditablePdf(member.Expression))
                    {
                        if (arguments.Count < 2)
                            return ReviewOperation("CANMIGNUTNET023", "Encrypt",
                                "Configure AES-256 with explicit owner and user passwords after reviewing the source overload.",
                                "The Nutrient Encrypt overload does not provide enough information for an equivalent PXA policy.");
                        var ownerPassword = SecurePasswordExpression(arguments[0].Expression, "PXA_PDF_OWNER_PASSWORD");
                        var userPassword = SecurePasswordExpression(arguments[1].Expression, "PXA_PDF_USER_PASSWORD");
                        _diagnostics.Add(Info("CANMIGNUTNET055",
                            "Nutrient existing-PDF encryption was mapped to PdfEditableDocument.ConfigureAes256."));
                        return ParseStatement(
                            $"{member.Expression}.ConfigureAes256({ownerPassword}, {userPassword});", node);
                    }
                    return RewriteEncryption(arguments);

                case "OcrPage":
                    return ReviewOperation(
                        "CANMIGNUTNET056",
                        "OcrPage",
                        "Route the selected PDF page through a PDF OCR service that preserves the original page and adds an invisible text layer. PXA image OCR is not semantically equivalent.",
                        "Nutrient OcrPage requires a PDF OCR application-service handoff; it was not replaced by image-to-design OCR.");

                case "TwainAcquire":
                    return ReviewOperation(
                        "CANMIGNUTNET057",
                        "TwainAcquire",
                        "Acquire the scan through a customer-side TWAIN/WIA adapter, then submit the resulting bytes to PXA import or OCR.",
                        "TWAIN acquisition requires scanner hardware and remains an explicit external adapter boundary.");

                case "ApplyRedaction":
                    return ReviewOperation(
                        "CANMIGNUTNET058",
                        "ApplyRedaction",
                        "Map every source redaction region to PdfRedactionRegion and execute IPdfTrustPostProcessingPipeline with PdfTrustRedactionOptions before signing.",
                        "ApplyRedaction requires source regions and existing-PDF bytes; PXA will not substitute a visual overlay.");

                case "AddRedactionRegion":
                case "SearchAndAddRedactionRegions":
                case "SmartRedaction":
                case "ClearRedactionRegions":
                    return ReviewOperation(
                        "CANMIGNUTNET058",
                        method,
                        "Create validated PdfRedactionRegion values in PDF user-space coordinates and pass them to PdfTrustRedactionOptions.Regions before executing the Trust pipeline.",
                        "The Nutrient redaction definition was captured for Trust review but was not copied as a visual overlay or executed during migration.");

                case "SignPDF":
                case "SignDocument":
                case "ApplySignature":
                case "SetSignatureCertificateFromP12":
                case "SetSignatureCertificateFromStore":
                case "SetSignatureInfo":
                case "SetSignaturePos":
                case "SetSignaturePosFromPlaceHolder":
                case "SetSignatureStampImage":
                case "SetSignatureHash":
                case "SetSignatureTimestampInfo":
                case "SetSignatureCertificationLevel":
                    return ReviewOperation(
                        "CANMIGNUTNET059",
                        method,
                        "Resolve an IPdfSigningKey in the trusted host and execute IPdfTrustPostProcessingPipeline with PdfTrustSigningOptions. Never embed the PFX password in generated code.",
                        "PDF signing requires a runtime-managed signing key; certificate paths and passwords were intentionally not copied.");

                case "VerifySignature":
                case "ValidateSignature":
                    return ReviewOperation(
                        "CANMIGNUTNET072",
                        method,
                        "Inspect the final PDF bytes with IPdfTrustInspector and apply the server-owned trust policy to the returned signature report.",
                        "Nutrient signature validation was mapped to an explicit PXA Trust inspection action; source trust decisions were not copied blindly.");

                case "SetOrigin":
                    var origin = arguments.Count > 0 ? arguments[0].ToString() : string.Empty;
                    _topLeftOrigin = origin.Contains("Top", StringComparison.Ordinal);
                    _rightOrigin = origin.Contains("Right", StringComparison.Ordinal);
                    _diagnostics.Add(_rightOrigin
                        ? Warning("CANMIGNUTNET049",
                            "Right-side coordinate origins require manual geometry review and subsequent drawing operations are not rewritten.")
                        : Info("CANMIGNUTNET007",
                            $"Nutrient coordinate origin recorded as {(_topLeftOrigin ? "top-left" : "bottom-left")}."));
                    return null;

                case "SetMeasurementUnit":
                    if (IsEditablePdf(member.Expression))
                    {
                        _diagnostics.Add(Info("CANMIGNUTNET008",
                            "Nutrient existing-PDF measurement units are preserved for symmetric page-box reads and writes."));
                        return base.VisitExpressionStatement(node);
                    }
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

                case "SetLineWidth":
                    if (arguments.Count > 0)
                        _lineWidth = Scale(arguments[0].ToString());
                    _diagnostics.Add(Info("CANMIGNUTNET050", "Nutrient line width captured for subsequent graphics operations."));
                    return null;

                case "SetLineColor":
                    if (arguments.Count == 3)
                    {
                        _lineColor = Rgb(arguments);
                        _diagnostics.Add(Info("CANMIGNUTNET053", "Nutrient line color captured for subsequent graphics operations."));
                    }
                    else
                        _diagnostics.Add(Warning("CANMIGNUTNET051", "SetLineColor overload requires manual color review."));
                    return null;

                case "SetFillColor":
                    if (arguments.Count == 3)
                    {
                        _fillColor = Rgb(arguments);
                        _diagnostics.Add(Info("CANMIGNUTNET054", "Nutrient fill color captured for subsequent text and graphics operations."));
                    }
                    else
                        _diagnostics.Add(Warning("CANMIGNUTNET052", "SetFillColor overload requires manual color review."));
                    return null;

                case "DrawText":
                    return RewriteDrawText(node, arguments);

                case "DrawTextBox":
                    return RewriteDrawTextBox(node, arguments);

                case "DrawLine":
                    return RewriteDrawLine(node, arguments);

                case "DrawRectangle":
                    return RewriteDrawRectangle(node, arguments);

                case "DrawImage":
                    return RewriteDrawImage(node, arguments);

                case "AddLinkToWeb":
                    return RewriteWebLink(node, arguments);

                case "AddLinkToWebAnnotation":
                    return RewriteOfficialWebLink(node, arguments);

                case "AddTextFormField":
                    return RewriteTextFormField(node, arguments);

                case "AddCheckBoxFormField":
                    return RewriteCheckBoxFormField(node, arguments);

                case "AddStickyNoteAnnotation":
                    return RewriteStickyNote(node, arguments);

                case "Dispose":
                    _diagnostics.Add(Info("CANMIGNUTNET010", "GdPicturePDF.Dispose() removed; PdfDocument does not own unmanaged SDK state."));
                    return null;

                default:
                    return ReviewOperation(
                        "CANMIGNUTNET031",
                        method,
                        $"Select an equivalent PXA API for Nutrient {method}(...) and verify its semantics before enabling the migrated workflow.",
                        $"Nutrient operation '{method}' is not mapped by the converter and requires explicit review.");
            }
        }

        private SyntaxNode? RewriteEncryption(SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (arguments.Count < 2)
            {
                return ReviewOperation(
                    "CANMIGNUTNET023",
                    "Encrypt",
                    "Map the Nutrient encryption overload to PdfSaveOptions.Encryption after reviewing passwords, algorithm, permissions, and metadata encryption.",
                    "The Nutrient Encrypt overload does not provide enough information for an equivalent PXA policy.");
            }

            _ownerPassword = SecurePasswordExpression(arguments[0].Expression, "PXA_PDF_OWNER_PASSWORD");
            _userPassword = SecurePasswordExpression(arguments[1].Expression, "PXA_PDF_USER_PASSWORD");
            _diagnostics.Add(Info("CANMIGNUTNET055",
                "Nutrient password encryption was mapped to PdfSaveOptions.Encryption with AES-256. Literal passwords were replaced by environment lookups."));
            return null;
        }

        private string BuildSaveStatement(string document, string path)
        {
            if (!_pdfA2b && _ownerPassword is null)
                return $"{document}.Save({path});";

            if (_pdfA2b && _ownerPassword is not null)
            {
                _diagnostics.Add(Warning("CANMIGNUTNET060",
                    "PDF/A forbids encryption. The generated options retain both requested policies so PXA fails closed; choose archival compliance or encryption before production use."));
            }

            var properties = new List<string>();
            if (_pdfA2b)
                properties.Add("ComplianceProfile = PdfComplianceProfile.PdfA2B");
            if (_ownerPassword is not null)
            {
                properties.Add(
                    "Encryption = new PdfEncryptionOptions { " +
                    $"OwnerPassword = {_ownerPassword}, UserPassword = {_userPassword}, " +
                    "Algorithm = PdfEncryptionAlgorithm.Aes256 }");
            }

            return $"{document}.Save({path}, new PdfSaveOptions {{ {string.Join(", ", properties)} }});";
        }

        private string SecurePasswordExpression(ExpressionSyntax expression, string environmentVariable)
        {
            if (!expression.IsKind(SyntaxKind.StringLiteralExpression))
                return expression.ToString();

            _diagnostics.Add(Warning("CANMIGNUTNET061",
                $"A hard-coded PDF password was removed. Supply it at runtime through {environmentVariable} or replace the generated lookup with a secret-provider expression."));
            return $"global::System.Environment.GetEnvironmentVariable(\"{environmentVariable}\") ?? throw new global::System.InvalidOperationException(\"Missing {environmentVariable}.\")";
        }

        private SyntaxNode? ReviewOperation(
            string diagnosticId,
            string operation,
            string action,
            string message)
        {
            _diagnostics.Add(Warning(diagnosticId, message));
            _reviewMarkers.Add($"[{diagnosticId}] {operation}: {action}");
            return null;
        }

        private bool TryRewriteDocumentDeclaration(
            LocalDeclarationStatementSyntax node,
            out LocalDeclarationStatementSyntax rewritten)
        {
            rewritten = node;
            var variable = node.Declaration.Variables.Count == 1
                ? node.Declaration.Variables[0]
                : null;
            if (variable?.Initializer?.Value is not ObjectCreationExpressionSyntax creation
                || SimpleName(creation.Type) != "GdPicturePDF")
            {
                return false;
            }

            var newCreation = creation.WithType(SyntaxFactory.IdentifierName("PdfDocument"));
            var editablePdf = _editablePdfVariables.Contains(variable.Identifier.ValueText);
            ExpressionSyntax replacement = editablePdf
                ? SyntaxFactory.ParseExpression("new PdfEditableDocument()")
                : newCreation;
            var newVariable = variable.WithInitializer(variable.Initializer.WithValue(replacement));
            var declaration = node.Declaration
                .WithType(SyntaxFactory.IdentifierName("var"))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(newVariable));

            rewritten = node
                .WithUsingKeyword(default)
                .WithAwaitKeyword(default)
                .WithDeclaration(declaration);
            _diagnostics.Add(Info("CANMIGNUTNET001", editablePdf
                ? "GdPicturePDF construction converted to a stateful PdfEditableDocument workflow."
                : "GdPicturePDF construction converted to new PdfDocument()."));
            return true;
        }

        private bool IsEditablePdf(ExpressionSyntax expression)
        {
            var symbol = _semanticContext.GetReferencedSymbol(expression);
            return symbol is not null
                ? _editablePdfSymbols.Contains(symbol)
                : _editablePdfVariables.Contains(expression.ToString());
        }

        private bool IsDocumentVariableArgument(ExpressionSyntax expression) =>
            IsDocumentReceiver(expression);

        private static readonly HashSet<string> TrustReviewMethods = new(StringComparer.Ordinal)
        {
            "AddRedactionRegion",
            "SearchAndAddRedactionRegions",
            "SmartRedaction",
            "ClearRedactionRegions",
            "ApplyRedaction",
            "SignPDF",
            "SignDocument",
            "ApplySignature",
            "SetSignatureCertificateFromP12",
            "SetSignatureCertificateFromStore",
            "SetSignatureInfo",
            "SetSignaturePos",
            "SetSignaturePosFromPlaceHolder",
            "SetSignatureStampImage",
            "SetSignatureHash",
            "SetSignatureTimestampInfo",
            "SetSignatureCertificationLevel",
            "VerifySignature",
            "ValidateSignature"
        };

        private static ExpressionSyntax FailedStatus(SyntaxNode source) =>
            SyntaxFactory.ParseExpression("global::PXA.Importer.Editing.PdfPageOperationStatus.Failed")
                .WithTriviaFrom(source);

        private static bool IsTypeReference(IdentifierNameSyntax node) => node.Parent switch
        {
            VariableDeclarationSyntax declaration => declaration.Type == node,
            ObjectCreationExpressionSyntax creation => creation.Type == node,
            ParameterSyntax parameter => parameter.Type == node,
            PropertyDeclarationSyntax property => property.Type == node,
            FieldDeclarationSyntax field => field.Declaration.Type == node,
            _ => false
        };

        private bool IsDocumentReceiver(ExpressionSyntax expression)
        {
            var symbol = _semanticContext.GetReferencedSymbol(expression);
            if (symbol is not null)
                return _documentSymbols.Contains(symbol);
            return _documentVariables.Contains(expression.ToString()) ||
                   expression is IdentifierNameSyntax identifier &&
                   _documentVariables.Contains(identifier.Identifier.ValueText);
        }

        private bool IsSupportedClonePages(SeparatedSyntaxList<ArgumentSyntax> arguments) =>
            arguments.Count == 2 &&
            IsDocumentVariableArgument(arguments[0].Expression) &&
            !arguments[1].Expression.IsKind(SyntaxKind.NullLiteralExpression) &&
            (arguments[1].Expression is not LiteralExpressionSyntax literal ||
             literal.IsKind(SyntaxKind.StringLiteralExpression));

        private bool TryRewriteFontDeclaration(
            LocalDeclarationStatementSyntax node,
            out LocalDeclarationStatementSyntax rewritten)
        {
            rewritten = node;
            var variable = node.Declaration.Variables.Count == 1
                ? node.Declaration.Variables[0]
                : null;
            if (variable?.Initializer?.Value is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || member.Name.Identifier.ValueText != "AddStandardFont"
                || !IsDocumentReceiver(member.Expression)
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

        private bool TryRewriteImageResourceDeclaration(
            LocalDeclarationStatementSyntax node,
            out LocalDeclarationStatementSyntax rewritten)
        {
            rewritten = node;
            var variable = node.Declaration.Variables.Count == 1
                ? node.Declaration.Variables[0]
                : null;
            if (variable?.Initializer?.Value is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || !IsDocumentReceiver(member.Expression)
                || member.Name.Identifier.ValueText is not ("AddJpegImageFromFile" or "AddPngImageFromFile" or "AddImageFromFile")
                || invocation.ArgumentList.Arguments.Count != 1)
            {
                return false;
            }

            _imageResourceVariables.Add(variable.Identifier.ValueText);
            var declaration = node.Declaration
                .WithType(SyntaxFactory.IdentifierName("var"))
                .WithVariables(SyntaxFactory.SingletonSeparatedList(
                    variable.WithInitializer(variable.Initializer.WithValue(
                        invocation.ArgumentList.Arguments[0].Expression))));
            rewritten = node.WithDeclaration(declaration);
            _diagnostics.Add(Info("CANMIGNUTNET029",
                "Nutrient file image resource converted to a PXA image path reference."));
            return true;
        }

        private bool TryRewriteFormFieldDeclaration(
            LocalDeclarationStatementSyntax node,
            out SyntaxNode rewritten)
        {
            rewritten = node;
            var variable = node.Declaration.Variables.Count == 1
                ? node.Declaration.Variables[0]
                : null;
            if (variable?.Initializer?.Value is not InvocationExpressionSyntax invocation
                || invocation.Expression is not MemberAccessExpressionSyntax member
                || !IsDocumentReceiver(member.Expression))
            {
                return false;
            }

            var method = member.Name.Identifier.ValueText;
            var arguments = invocation.ArgumentList.Arguments;
            rewritten = method switch
            {
                "AddTextFormField" => RewriteOfficialTextFormField(node, arguments),
                "AddCheckBoxFormField" => RewriteOfficialCheckBoxFormField(node, arguments),
                "AddLinkToWebAnnotation" => RewriteOfficialWebLink(node, arguments),
                "AddStickyNoteAnnotation" => RewriteStickyNote(node, arguments),
                _ => node,
            };
            return !ReferenceEquals(rewritten, node);
        }

        private SyntaxNode RewriteNewPage(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            _pageCount++;
            _currentPageVariable = _pageCount == 1 ? "page" : $"page{_pageCount}";
            _pageVariables[_pageCount] = _currentPageVariable;

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

        private SyntaxNode? RewriteSelectPage(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (arguments.Count == 1
                && int.TryParse(arguments[0].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageNumber)
                && _pageVariables.TryGetValue(pageNumber, out var pageVariable))
            {
                _currentPageVariable = pageVariable;
                _diagnostics.Add(Info("CANMIGNUTNET012",
                    $"Nutrient SelectPage({pageNumber}) mapped to the generated page variable '{pageVariable}'."));
                return null;
            }

            return Unsupported(node, "SelectPage",
                "CANMIGNUTNET033", "Dynamic or forward page selection requires manual review.");
        }

        private SyntaxNode RewriteRotatePage(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (arguments.Count != 1)
                return Unsupported(node, "RotatePage", "CANMIGNUTNET034", "RotatePage overload could not be mapped safely.");

            _diagnostics.Add(Info("CANMIGNUTNET013", "Nutrient page rotation converted to PdfPage.RotationDegrees."));
            return ParseStatement($"{_currentPageVariable}.RotationDegrees = (int)({arguments[0]});", node);
        }

        private SyntaxNode RewriteDrawText(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "DrawText", "CANMIGNUTNET049", "DrawText with a right-side origin requires manual geometry review.");

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
            var call = $"{_currentPageVariable}.{method}({text}, {x}, {y}, new PdfDrawTextOptions {{ FontSize = {_fontSize}, Font = {font}, FillColor = {_fillColor} }});";
            _diagnostics.Add(Info("CANMIGNUTNET011", $"Nutrient DrawText converted to PdfPage.{method}."));
            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteDrawTextBox(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "DrawTextBox", "CANMIGNUTNET049", "DrawTextBox with a right-side origin requires manual geometry review.");

            if (arguments.Count < 8)
                return Unsupported(node, "DrawTextBox", "CANMIGNUTNET035", "DrawTextBox overload requires font, bounds, alignment, and text arguments.");

            var method = _topLeftOrigin ? "DrawTextBoxFromTop" : "DrawTextBox";
            if (!_topLeftOrigin)
            {
                return Unsupported(node, "DrawTextBox", "CANMIGNUTNET035",
                    "Bottom-left DrawTextBox vertical alignment requires manual baseline review.");
            }

            var call = $"{_currentPageVariable}.{method}({arguments[7]}, {Scale(arguments[1].ToString())}, " +
                $"{Scale(arguments[2].ToString())}, {Scale(arguments[3].ToString())}, {Scale(arguments[4].ToString())}, " +
                $"new PdfTextBoxOptions {{ FontSize = {_fontSize}, Font = {arguments[0]}, FillColor = {_fillColor}, " +
                $"Alignment = PdfTextAlignment.{MapHorizontalAlignment(arguments[5].ToString())}, " +
                $"VerticalAlignment = PdfVerticalAlignment.{MapVerticalAlignment(arguments[6].ToString())} }});";
            _diagnostics.Add(Info("CANMIGNUTNET014", "Nutrient DrawTextBox converted to PdfPage.DrawTextBoxFromTop."));
            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteDrawLine(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "DrawLine", "CANMIGNUTNET049", "DrawLine with a right-side origin requires manual geometry review.");

            if (arguments.Count < 4)
                return Unsupported(node, "DrawLine", "CANMIGNUTNET036", "DrawLine overload could not be mapped safely.");

            var method = _topLeftOrigin ? "DrawLineFromTop" : "DrawLine";
            var lineWidth = arguments.Count >= 5 ? Scale(arguments[4].ToString()) : _lineWidth;
            var call = $"{_currentPageVariable}.{method}({Scale(arguments[0].ToString())}, {Scale(arguments[1].ToString())}, " +
                $"{Scale(arguments[2].ToString())}, {Scale(arguments[3].ToString())}, {lineWidth}, {_lineColor});";
            _diagnostics.Add(Info("CANMIGNUTNET015", $"Nutrient DrawLine converted to PdfPage.{method}."));
            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteDrawRectangle(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "DrawRectangle", "CANMIGNUTNET049", "DrawRectangle with a right-side origin requires manual geometry review.");

            if (arguments.Count < 4)
                return Unsupported(node, "DrawRectangle", "CANMIGNUTNET037", "DrawRectangle overload could not be mapped safely.");

            var method = _topLeftOrigin ? "DrawRectangleFromTop" : "DrawRectangle";
            var fill = arguments.Count >= 5 ? arguments[4].ToString() : "false";
            var call = $"{_currentPageVariable}.{method}({Scale(arguments[0].ToString())}, {Scale(arguments[1].ToString())}, " +
                $"{Scale(arguments[2].ToString())}, {Scale(arguments[3].ToString())}, lineWidth: {_lineWidth}, fill: {fill}, " +
                $"strokeColor: {_lineColor}, fillColor: {_fillColor});";
            _diagnostics.Add(Info("CANMIGNUTNET016", $"Nutrient DrawRectangle converted to PdfPage.{method}."));

            if (arguments.Count >= 6 && arguments[5].ToString() == "false")
            {
                _diagnostics.Add(Warning("CANMIGNUTNET038",
                    "The source rectangle disabled stroking; PXA currently renders a one-point border and requires visual review."));
            }

            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteDrawImage(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "DrawImage", "CANMIGNUTNET049", "DrawImage with a right-side origin requires manual geometry review.");

            if (arguments.Count is not (3 or 5) || !IsSupportedImageExpression(arguments[0].Expression))
            {
                return Unsupported(node, "DrawImage", "CANMIGNUTNET039",
                    "Only file-path, byte-array, or stream image overloads can be mapped; GdPicture image resource IDs require manual export.");
            }

            var method = _topLeftOrigin ? "DrawImageFromTop" : "DrawImage";
            var scaled = arguments.Skip(1).Select(argument => Scale(argument.ToString()));
            var call = $"{_currentPageVariable}.{method}({arguments[0]}, {string.Join(", ", scaled)});";
            _diagnostics.Add(Info("CANMIGNUTNET017", $"Nutrient file or in-memory image converted to PdfPage.{method}."));
            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteWebLink(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "AddLinkToWeb", "CANMIGNUTNET049", "Web links with a right-side origin require manual geometry review.");

            if (arguments.Count != 5)
                return Unsupported(node, "AddLinkToWeb", "CANMIGNUTNET040", "Web-link overload could not be mapped safely.");

            var y = BoxY(arguments[2].ToString(), arguments[4].ToString());
            var call = $"{_currentPageVariable}.AddWebLink({Scale(arguments[1].ToString())}, {y}, " +
                $"{Scale(arguments[3].ToString())}, {Scale(arguments[4].ToString())}, {arguments[0]});";
            _diagnostics.Add(Info("CANMIGNUTNET018", "Nutrient web link converted to PdfPage.AddWebLink."));
            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteOfficialWebLink(
            StatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "AddLinkToWebAnnotation", "CANMIGNUTNET049", "Web links with a right-side origin require manual geometry review.");

            if (arguments.Count != 9)
                return Unsupported(node, "AddLinkToWebAnnotation", "CANMIGNUTNET040", "Web-link annotation overload could not be mapped safely.");

            var call = $"{_currentPageVariable}.AddWebLink({Scale(arguments[0].ToString())}, " +
                $"{BoxY(arguments[1].ToString(), arguments[3].ToString())}, {Scale(arguments[2].ToString())}, " +
                $"{Scale(arguments[3].ToString())}, {arguments[4]});";
            _diagnostics.Add(Info("CANMIGNUTNET018", "Nutrient web-link annotation converted to PdfPage.AddWebLink."));
            if (arguments[5].ToString() == "true")
            {
                _diagnostics.Add(Warning("CANMIGNUTNET045",
                    "The source web-link border appearance is not retained by the current PXA link contract."));
            }
            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteTextFormField(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "AddTextFormField", "CANMIGNUTNET049", "Text fields with a right-side origin require manual geometry review.");

            if (arguments.Count != 12)
                return Unsupported(node, "AddTextFormField", "CANMIGNUTNET041", "Text form-field overload could not be mapped safely.");
            return RewriteOfficialTextFormField(node, arguments);
        }

        private SyntaxNode RewriteCheckBoxFormField(
            ExpressionStatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "AddCheckBoxFormField", "CANMIGNUTNET049", "Checkboxes with a right-side origin require manual geometry review.");

            if (arguments.Count != 10)
                return Unsupported(node, "AddCheckBoxFormField", "CANMIGNUTNET042", "Checkbox form-field overload could not be mapped safely.");
            return RewriteOfficialCheckBoxFormField(node, arguments);
        }

        private SyntaxNode RewriteOfficialTextFormField(
            StatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "AddTextFormField", "CANMIGNUTNET049", "Text fields with a right-side origin require manual geometry review.");

            if (arguments.Count != 12)
                return Unsupported(node, "AddTextFormField", "CANMIGNUTNET041", "Text form-field overload could not be mapped safely.");

            var commonArguments = $"{arguments[4]}, {Scale(arguments[0].ToString())}, " +
                $"{BoxY(arguments[1].ToString(), arguments[3].ToString())}, {Scale(arguments[2].ToString())}, " +
                $"{Scale(arguments[3].ToString())}, {arguments[5]}, {Scale(arguments[8].ToString())}";
            var multiline = arguments[6].ToString();
            var call = multiline switch
            {
                "true" => $"{_currentPageVariable}.AddMultilineTextField({commonArguments});",
                "false" => $"{_currentPageVariable}.AddTextField({commonArguments});",
                _ => $"if ({multiline}) {_currentPageVariable}.AddMultilineTextField({commonArguments}); " +
                    $"else {_currentPageVariable}.AddTextField({commonArguments});",
            };
            _diagnostics.Add(Info("CANMIGNUTNET019", "Nutrient text form field converted to the matching PXA text-field contract."));
            _diagnostics.Add(Warning("CANMIGNUTNET046",
                "Text-field font resource and RGB appearance require visual review because PXA currently preserves the font size only."));
            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteOfficialCheckBoxFormField(
            StatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "AddCheckBoxFormField", "CANMIGNUTNET049", "Checkboxes with a right-side origin require manual geometry review.");

            if (arguments.Count != 10)
                return Unsupported(node, "AddCheckBoxFormField", "CANMIGNUTNET042", "Checkbox form-field overload could not be mapped safely.");

            if (arguments[2].ToString() != arguments[3].ToString())
            {
                _diagnostics.Add(Warning("CANMIGNUTNET043",
                    "A non-square checkbox was normalized to its width because PXA checkboxes are square."));
            }
            _diagnostics.Add(Warning("CANMIGNUTNET047",
                "Checkbox mark style and RGB appearance require visual review in PXA."));
            var call = $"{_currentPageVariable}.AddCheckBox({arguments[4]}, {Scale(arguments[0].ToString())}, " +
                $"{BoxY(arguments[1].ToString(), arguments[3].ToString())}, {Scale(arguments[2].ToString())}, {arguments[6]});";
            _diagnostics.Add(Info("CANMIGNUTNET027", "Nutrient checkbox form field converted to PdfPage.AddCheckBox."));
            return ParseStatement(call, node);
        }

        private SyntaxNode RewriteStickyNote(
            StatementSyntax node,
            SeparatedSyntaxList<ArgumentSyntax> arguments)
        {
            if (_rightOrigin)
                return Unsupported(node, "AddStickyNoteAnnotation", "CANMIGNUTNET049", "Sticky notes with a right-side origin require manual geometry review.");

            if (arguments.Count != 15)
                return Unsupported(node, "AddStickyNoteAnnotation", "CANMIGNUTNET044", "Sticky-note overload could not be mapped safely.");

            const string iconSize = "18";
            var call = $"{_currentPageVariable}.AddStickyNoteAnnotation({Scale(arguments[1].ToString())}, " +
                $"{BoxY(arguments[2].ToString(), iconSize)}, {iconSize}, {iconSize}, {arguments[5]}, " +
                $"PdfColor.FromRgb({arguments[8]}, {arguments[9]}, {arguments[10]}), {arguments[6]});";
            _diagnostics.Add(Info("CANMIGNUTNET028", "Nutrient sticky note converted to PdfPage.AddStickyNoteAnnotation."));
            _diagnostics.Add(Warning("CANMIGNUTNET048",
                "Sticky-note author, subject, icon, popup state, and popup geometry are not represented by the current PXA annotation contract."));
            return ParseStatement(call, node);
        }

        private SyntaxNode Unsupported(StatementSyntax node, string operation, string diagnosticId, string message)
        {
            _diagnostics.Add(Warning(diagnosticId, message));
            _reviewMarkers.Add($"[{diagnosticId}] {operation}: {message}");
            return SyntaxFactory.EmptyStatement()
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }

        private string BoxY(string y, string height) => _topLeftOrigin
            ? $"{_currentPageVariable}.Height - ({Scale(y)}) - ({Scale(height)})"
            : Scale(y);

        private bool IsSupportedImageExpression(ExpressionSyntax expression) => expression switch
        {
            LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
            IdentifierNameSyntax identifier => _imageResourceVariables.Contains(identifier.Identifier.ValueText),
            ArrayCreationExpressionSyntax => true,
            ImplicitArrayCreationExpressionSyntax => true,
            ObjectCreationExpressionSyntax creation => SimpleName(creation.Type) is "MemoryStream" or "FileStream",
            _ => false,
        };

        private static string MapHorizontalAlignment(string expression)
        {
            if (expression.Contains("Center", StringComparison.OrdinalIgnoreCase)) return "Center";
            if (expression.Contains("Right", StringComparison.OrdinalIgnoreCase)
                || expression.Contains("Far", StringComparison.OrdinalIgnoreCase)) return "Right";
            if (expression.Contains("Just", StringComparison.OrdinalIgnoreCase)) return "Justify";
            return "Left";
        }

        private static string MapVerticalAlignment(string expression)
        {
            if (expression.Contains("Center", StringComparison.OrdinalIgnoreCase)
                || expression.Contains("Middle", StringComparison.OrdinalIgnoreCase)) return "Middle";
            if (expression.Contains("Bottom", StringComparison.OrdinalIgnoreCase)
                || expression.Contains("Far", StringComparison.OrdinalIgnoreCase)) return "Bottom";
            return "Top";
        }

        private static string Rgb(SeparatedSyntaxList<ArgumentSyntax> arguments) =>
            $"PdfColor.FromRgb({arguments[0]}, {arguments[1]}, {arguments[2]})";

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
