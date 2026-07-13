using PXA.Migration.Abstractions;
using PXA.Migration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PXA.Migration.Pdf.Code.DevExpress;

public sealed class DevExpressPdfMigration : CSharpSourceMigration
{
    public override MigrationResult Migrate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code cannot be null or empty.", nameof(sourceCode));

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();

        // Hybrid semantic model: vendor types resolve to error symbols (no references), but local
        // symbols (processor/graphics/page) still resolve, letting us match receivers by symbol
        // identity rather than fragile string names. Falls back to name matching when unresolved.
        var compilation = CSharpCompilation.Create(
            "PXA.Migration.DevExpress.Semantic",
            new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });
        var semanticModel = compilation.GetSemanticModel(tree);

        var processorVar = FindProcessorVariable(root);
        var graphicsVar = FindGraphicsVariable(root, processorVar);
        var saveTarget = FindSaveTarget(root, processorVar);
        var processorSymbol = ResolveLocalSymbol(root, semanticModel, processorVar);
        var graphicsSymbol = ResolveLocalSymbol(root, semanticModel, graphicsVar);
        var fontSizes = ScanFontVariableSizes(root);
        var encryption = ScanEncryption(root);

        var rewriter = new DevExpressRewriter(
            processorVar, graphicsVar, saveTarget,
            semanticModel, processorSymbol, graphicsSymbol, fontSizes, encryption);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root)!;
        newRoot = RemoveDevExpressUsings(newRoot);
        newRoot = EnsurePxaUsing(newRoot);

        var diagnostics = rewriter.Diagnostics.ToList();
        diagnostics.AddRange(ScanForUnsupportedIdentifiers(root));

        // Encryption that we could not fold into document.Save(...) gets manual guidance instead.
        var mentionsEncryption = root.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(static id => id.Identifier.ValueText == "PdfEncryptionOptions");
        if (mentionsEncryption && !encryption.Recognized)
        {
            diagnostics.Add(Warning("CANMIGDEVEXP024",
                "DevExpress encryption maps to PXA.Pdf: set PdfSaveOptions.Encryption = new PdfEncryptionOptions " +
                "{ UserPassword = ..., OwnerPassword = ..., Permissions = ... } and pass it to document.Save(path, options). " +
                "Apply the password/permission values manually."));
        }

        return new MigrationResult
        {
            MigratedCode = NormalizeLineEndings(newRoot.NormalizeWhitespace().ToFullString()),
            Diagnostics = diagnostics
        };
    }

    private static string FindProcessorVariable(CompilationUnitSyntax root)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (GetSimpleName(creation.Type) == "PdfDocumentProcessor")
            {
                var decl = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "processor";
    }

    private static string FindGraphicsVariable(CompilationUnitSyntax root, string processorVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText == "CreateGraphics" &&
                ma.Expression.ToString() == processorVar)
            {
                var decl = inv.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                if (decl != null) return decl.Identifier.ValueText;
            }
        }
        return "graphics";
    }

    private static string? FindSaveTarget(CompilationUnitSyntax root, string processorVar)
    {
        foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.ValueText == "SaveDocument" &&
                ma.Expression.ToString() == processorVar)
            {
                var args = inv.ArgumentList.Arguments;
                if (args.Count >= 1) return args[0].Expression.ToString();
            }
        }
        return null;
    }

    private static ISymbol? ResolveLocalSymbol(CompilationUnitSyntax root, SemanticModel model, string variableName)
    {
        foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Identifier.ValueText != variableName) continue;
            var symbol = model.GetDeclaredSymbol(declarator);
            if (symbol != null) return symbol;
        }
        return null;
    }

    // Pre-scan local font variables to their DXFont(...) constructor size so DrawString can recover the
    // font size even when the font is passed as an identifier rather than an inline `new DXFont(...)`.
    private static Dictionary<string, string> ScanFontVariableSizes(CompilationUnitSyntax root)
    {
        var sizes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Initializer?.Value is not ObjectCreationExpressionSyntax creation) continue;
            if (GetSimpleName(creation.Type) != "DXFont") continue;
            var args = creation.ArgumentList?.Arguments;
            if (args?.Count >= 2)
                sizes[declarator.Identifier.ValueText] = args.Value[1].Expression.ToString();
        }
        return sizes;
    }

    // Captures a DevExpress encryption setup so it can be folded into document.Save(path, options).
    private sealed class EncryptionModel
    {
        public string? EncryptionVar { get; init; }
        public string? SaveOptionsVar { get; init; }
        public string? UserPassword { get; init; }
        public string? OwnerPassword { get; init; }
        public bool Recognized { get; init; }

        public string BuildPxaSaveOptions()
        {
            var props = new List<string>();
            if (UserPassword != null) props.Add($"UserPassword = {UserPassword}");
            if (OwnerPassword != null) props.Add($"OwnerPassword = {OwnerPassword}");
            var encryption = props.Count > 0
                ? $"new PdfEncryptionOptions {{ {string.Join(", ", props)} }}"
                : "new PdfEncryptionOptions()";
            return $"new PdfSaveOptions {{ Encryption = {encryption} }}";
        }
    }

    // Pre-scan the DevExpress encryption shape:
    //   var enc = new PdfEncryptionOptions();
    //   enc.UserPasswordString = "..."; enc.OwnerPasswordString = "...";
    //   var save = new PdfSaveOptions { EncryptionOptions = enc };
    //   processor.SaveDocument(path, save);
    // and translate the password setup so the rewriter can emit document.Save(path, PXA options).
    private static EncryptionModel ScanEncryption(CompilationUnitSyntax root)
    {
        string? encryptionVar = null;
        foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Initializer?.Value is ObjectCreationExpressionSyntax creation &&
                GetSimpleName(creation.Type) == "PdfEncryptionOptions")
            {
                encryptionVar = declarator.Identifier.ValueText;
                break;
            }
        }

        if (encryptionVar is null)
            return new EncryptionModel { Recognized = false };

        string? user = null;
        string? owner = null;

        // Property assignments: enc.UserPasswordString = "..."
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not MemberAccessExpressionSyntax ma) continue;
            if (ma.Expression.ToString() != encryptionVar) continue;
            CapturePassword(ma.Name.Identifier.ValueText, assignment.Right.ToString(), ref user, ref owner);
        }

        // Object-initializer form: new PdfEncryptionOptions { UserPasswordString = "..." }
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (GetSimpleName(creation.Type) != "PdfEncryptionOptions" || creation.Initializer is null) continue;
            foreach (var expr in creation.Initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                if (expr.Left is IdentifierNameSyntax id)
                    CapturePassword(id.Identifier.ValueText, expr.Right.ToString(), ref user, ref owner);
            }
        }

        // The PdfSaveOptions variable whose EncryptionOptions references the encryption var.
        string? saveOptionsVar = null;
        foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Initializer?.Value is not ObjectCreationExpressionSyntax creation) continue;
            if (GetSimpleName(creation.Type) != "PdfSaveOptions") continue;
            if (creation.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
                    .Any(e => e.Right.ToString() == encryptionVar) == true)
            {
                saveOptionsVar = declarator.Identifier.ValueText;
                break;
            }
        }

        // Only fold when there is a two-arg SaveDocument(path, options) to attach the options to.
        var hasTwoArgSave = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(static inv => inv.Expression is MemberAccessExpressionSyntax m &&
                               m.Name.Identifier.ValueText == "SaveDocument" &&
                               inv.ArgumentList.Arguments.Count >= 2);

        return new EncryptionModel
        {
            EncryptionVar = encryptionVar,
            SaveOptionsVar = saveOptionsVar,
            UserPassword = user,
            OwnerPassword = owner,
            Recognized = hasTwoArgSave
        };
    }

    private static void CapturePassword(string property, string value, ref string? user, ref string? owner)
    {
        switch (property)
        {
            case "UserPasswordString" or "UserPassword":
                user ??= value;
                break;
            case "OwnerPasswordString" or "OwnerPassword":
                owner ??= value;
                break;
        }
    }

    private static IEnumerable<MigrationDiagnostic> ScanForUnsupportedIdentifiers(CompilationUnitSyntax root)
    {
        var names = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(static id => id.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        // Encryption is handled separately (folded into document.Save when recognized, otherwise a
        // CANMIGDEVEXP024 guidance warning is added in Migrate).

        if (names.Overlaps(new[]
            {
                "PdfAcroForm", "PdfFormField", "PdfSignature", "PdfDocumentSigner",
                "PdfAnnotation", "PdfBookmark"
            }))
        {
            yield return Warning("CANMIGDEVEXP022",
                "Forms, signatures, annotations, or bookmarks require manual migration outside v1.");
        }

        if (names.Overlaps(new[] { "XtraReport", "PdfExportOptions", "PrintingSystemBase" }))
        {
            yield return Warning("CANMIGDEVEXP020",
                "DevExpress report export workflows require manual migration.");
        }
    }

    private static CompilationUnitSyntax RemoveDevExpressUsings(CompilationUnitSyntax root)
    {
        var filtered = root.Usings
            .Where(static u => !(u.Name?.ToString() ?? "").StartsWith("DevExpress", StringComparison.Ordinal))
            .ToArray();
        return root.WithUsings(SyntaxFactory.List(filtered));
    }

    private static CompilationUnitSyntax EnsurePxaUsing(CompilationUnitSyntax root)
    {
        if (root.Usings.Any(static u => u.Name?.ToString() == "PXA.Pdf"))
            return root;
        var canvasUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("PXA.Pdf"))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        return root.WithUsings(root.Usings.Insert(0, canvasUsing));
    }

    private static string GetSimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
        _ => type.ToString()
    };

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static MigrationDiagnostic Warning(string id, string message) => new()
    {
        Id = id,
        Message = message,
        Severity = MigrationDiagnosticSeverity.Warning
    };

    // --- Rewriter ---------------------------------------------------------------

    private sealed class DevExpressRewriter : CSharpSyntaxRewriter
    {
        private readonly string _processorVar;
        private readonly string _graphicsVar;
        private readonly string? _saveTarget;
        private readonly string _pageVar = "page";
        private readonly SemanticModel _semanticModel;
        private readonly ISymbol? _processorSymbol;
        private readonly ISymbol? _graphicsSymbol;
        private readonly IReadOnlyDictionary<string, string> _fontSizes;
        private readonly EncryptionModel _encryption;
        private readonly List<MigrationDiagnostic> _diagnostics = [];
        private readonly List<string> _deferredDrawCalls = [];
        private bool _pageDeclared;

        public IReadOnlyList<MigrationDiagnostic> Diagnostics => _diagnostics;

        public DevExpressRewriter(
            string processorVar,
            string graphicsVar,
            string? saveTarget,
            SemanticModel semanticModel,
            ISymbol? processorSymbol,
            ISymbol? graphicsSymbol,
            IReadOnlyDictionary<string, string> fontSizes,
            EncryptionModel encryption)
        {
            _processorVar = processorVar;
            _graphicsVar = graphicsVar;
            _saveTarget = saveTarget;
            _semanticModel = semanticModel;
            _processorSymbol = processorSymbol;
            _graphicsSymbol = graphicsSymbol;
            _fontSizes = fontSizes;
            _encryption = encryption;
        }

        // Override VisitCompilationUnit to allow one-to-many statement replacement.
        // RenderNewPage expands into AddPage() + all deferred draw calls.
        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            var newMembers = new List<MemberDeclarationSyntax>();
            foreach (var member in node.Members)
            {
                if (member is not GlobalStatementSyntax gs)
                {
                    newMembers.Add(member);
                    continue;
                }
                newMembers.AddRange(TransformGlobal(gs));
            }
            return node.WithMembers(SyntaxFactory.List(newMembers));
        }

        private IEnumerable<MemberDeclarationSyntax> TransformGlobal(GlobalStatementSyntax node)
        {
            // `using var processor = new PdfDocumentProcessor()` → `var document = new PdfDocument()`
            if (IsCreationDeclaration(node, "PdfDocumentProcessor"))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP001", "new PdfDocumentProcessor() → new PdfDocument()"));
                return [MakeGlobal("var document = new PdfDocument();", node)];
            }

            // `processor.CreateEmptyDocument()` → remove
            if (IsMethodCallOn(node, _processorVar, "CreateEmptyDocument"))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP002",
                    "CreateEmptyDocument() removed — document is created by new PdfDocument()."));
                return [];
            }

            // `using var graphics = processor.CreateGraphics()` → remove
            if (IsDeclarationWithCall(node, "CreateGraphics"))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP003",
                    "CreateGraphics() removed — PXA draw calls use the PdfPage surface directly."));
                return [];
            }

            // `var titleFont = new DXFont(...)` → remove (size is inlined into the draw calls).
            if (IsDxFontDeclaration(node))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP025",
                    "DXFont declaration removed — font size inlined into DrawTextFromTop."));
                return [];
            }

            // Encryption setup is folded into document.Save(path, options) below — drop the source
            // statements (PdfEncryptionOptions/PdfSaveOptions declarations and their assignments).
            if (_encryption.Recognized && IsConsumedEncryptionStatement(node))
            {
                return [];
            }

            // Draw calls on graphics → defer until after AddPage (DevExpress draws before RenderNewPage)
            if (TryConvertDrawCall(node, out var canvasCall))
            {
                _deferredDrawCalls.Add(canvasCall!);
                return [];
            }

            // `processor.RenderNewPage(...)` → `var page = document.AddPage(...);` + queued draw calls
            if (IsMethodCallOn(node, _processorVar, "RenderNewPage"))
            {
                _diagnostics.Add(Info("CANMIGDEVEXP004",
                    $"RenderNewPage() → document.AddPage() — {_deferredDrawCalls.Count} draw call(s) repositioned after AddPage."));
                var sizeArg = BuildAddPageArgs(GetArgExpressions(node), out var unmappedSize);
                if (unmappedSize != null)
                {
                    _diagnostics.Add(Warning("CANMIGDEVEXP026",
                        $"PdfPaperSize.{unmappedSize} has no PXA preset — defaulted to A4. " +
                        "Use document.AddPage(width, height) for an exact size."));
                }
                var results = new List<MemberDeclarationSyntax>();
                // First page declares `var page`; subsequent pages reassign it (avoids duplicate locals).
                var addPage = _pageDeclared
                    ? $"{_pageVar} = document.AddPage({sizeArg});"
                    : $"var {_pageVar} = document.AddPage({sizeArg});";
                _pageDeclared = true;
                results.Add(MakeGlobal(addPage, node));
                foreach (var call in _deferredDrawCalls)
                    results.Add(MakeGlobal(call, node));
                _deferredDrawCalls.Clear();
                return results;
            }

            // `processor.SaveDocument(path[, saveOptions])` → `document.Save(path[, options])`
            if (IsMethodCallOn(node, _processorVar, "SaveDocument"))
            {
                var args = GetArgExpressions(node);
                var path = args.Count >= 1 ? args[0] : (_saveTarget ?? "path");

                if (args.Count >= 2 && _encryption.Recognized)
                {
                    var options = _encryption.BuildPxaSaveOptions();
                    _diagnostics.Add(Info("CANMIGDEVEXP010",
                        "DevExpress encryption mapped to PdfSaveOptions.Encryption."));
                    _diagnostics.Add(Info("CANMIGDEVEXP008",
                        $"SaveDocument(...) → document.Save({path}, options)"));
                    return [MakeGlobal($"document.Save({path}, {options});", node)];
                }

                _diagnostics.Add(Info("CANMIGDEVEXP008", $"SaveDocument({path}) → document.Save({path})"));
                return [MakeGlobal($"document.Save({path});", node)];
            }

            // Unsupported processor APIs (existing-PDF editing) — keep with warning
            if (IsMethodCallOn(node, _processorVar, "LoadDocument") ||
                IsMethodCallOn(node, _processorVar, "AppendDocument") ||
                IsMethodCallOn(node, _processorVar, "DeletePage") ||
                IsMethodCallOn(node, _processorVar, "InsertPage"))
            {
                _diagnostics.Add(Warning("CANMIGDEVEXP021",
                    "Existing-PDF processing or page editing APIs require manual migration outside v1."));
                return [node];
            }

            // Report export APIs — keep with warning
            if (IsAnyMethodCall(node, "ExportToPdf") || IsAnyMethodCall(node, "ExportToPdfAsync"))
            {
                _diagnostics.Add(Warning("CANMIGDEVEXP020",
                    "Report export workflows require manual migration."));
                return [node];
            }

            return [node];
        }

        private bool TryConvertDrawCall(GlobalStatementSyntax node, out string? canvasCall)
        {
            canvasCall = null;
            var expr = ExtractExpression(node);
            if (expr is not InvocationExpressionSyntax inv) return false;
            if (inv.Expression is not MemberAccessExpressionSyntax ma) return false;
            if (!ReceiverMatches(ma.Expression, _graphicsSymbol, _graphicsVar)) return false;

            var args = inv.ArgumentList.Arguments;
            switch (ma.Name.Identifier.ValueText)
            {
                case "DrawString" when args.Count >= 5:
                {
                    var text = args[0].Expression.ToString();
                    var x = args[3].Expression.ToString();
                    var y = args[4].Expression.ToString();
                    var fontSize = TryExtractDxFontSize(args[1].Expression) ?? "12";
                    var color = MapDxColor(args[2].Expression, out _);
                    _diagnostics.Add(Info("CANMIGDEVEXP005",
                        $"DrawString({text}) → {_pageVar}.DrawTextFromTop(...)"));
                    if (color != null)
                    {
                        _diagnostics.Add(Info("CANMIGDEVEXP009",
                            $"DrawString brush colour mapped to {color}."));
                        canvasCall =
                            $"{_pageVar}.DrawTextFromTop({text}, {x}, {y}, new PdfDrawTextOptions {{ FontSize = {fontSize}, FillColor = {color} }});";
                    }
                    else
                    {
                        canvasCall = $"{_pageVar}.DrawTextFromTop({text}, {x}, {y}, {fontSize});";
                    }
                    return true;
                }
                case "DrawLine" when args.Count >= 5:
                {
                    // (pen, x1, y1, x2, y2)
                    var x1 = args[1].Expression.ToString();
                    var y1 = args[2].Expression.ToString();
                    var x2 = args[3].Expression.ToString();
                    var y2 = args[4].Expression.ToString();
                    var color = MapDxColor(args[0].Expression, out var lineWidth);
                    _diagnostics.Add(Info("CANMIGDEVEXP006", $"DrawLine → {_pageVar}.DrawLine(...)"));
                    if (color != null)
                    {
                        _diagnostics.Add(Info("CANMIGDEVEXP009", $"DrawLine pen colour mapped to {color}."));
                        canvasCall = $"{_pageVar}.DrawLine({x1}, {y1}, {x2}, {y2}, {lineWidth ?? "1"}, {color});";
                    }
                    else
                    {
                        canvasCall = $"{_pageVar}.DrawLine({x1}, {y1}, {x2}, {y2});";
                    }
                    return true;
                }
                case "DrawRectangle" when args.Count >= 5:
                {
                    // (pen, x, y, width, height)
                    var x = args[1].Expression.ToString();
                    var y = args[2].Expression.ToString();
                    var w = args[3].Expression.ToString();
                    var h = args[4].Expression.ToString();
                    var color = MapDxColor(args[0].Expression, out var lineWidth);
                    _diagnostics.Add(Info("CANMIGDEVEXP007",
                        $"DrawRectangle → {_pageVar}.DrawRectangle(...)"));
                    if (color != null)
                    {
                        _diagnostics.Add(Info("CANMIGDEVEXP009", $"DrawRectangle pen colour mapped to {color}."));
                        canvasCall = $"{_pageVar}.DrawRectangle({x}, {y}, {w}, {h}, {lineWidth ?? "1"}, false, {color});";
                    }
                    else
                    {
                        canvasCall = $"{_pageVar}.DrawRectangle({x}, {y}, {w}, {h});";
                    }
                    return true;
                }
                case "DrawRectangle" when args.Count == 2:
                {
                    // (pen, RectangleF) — decompose only when the rectangle is constructed inline.
                    if (TryDecomposeRectangle(args[1].Expression, out var rx, out var ry, out var rw, out var rh))
                    {
                        var color = MapDxColor(args[0].Expression, out var lineWidth);
                        _diagnostics.Add(Info("CANMIGDEVEXP007",
                            $"DrawRectangle(pen, RectangleF) → {_pageVar}.DrawRectangle(...)"));
                        if (color != null)
                        {
                            _diagnostics.Add(Info("CANMIGDEVEXP009", $"DrawRectangle pen colour mapped to {color}."));
                            canvasCall = $"{_pageVar}.DrawRectangle({rx}, {ry}, {rw}, {rh}, {lineWidth ?? "1"}, false, {color});";
                        }
                        else
                        {
                            canvasCall = $"{_pageVar}.DrawRectangle({rx}, {ry}, {rw}, {rh});";
                        }
                        return true;
                    }

                    _diagnostics.Add(Warning("CANMIGDEVEXP023",
                        "DrawRectangle(pen, RectangleF) bounds could not be decomposed — manual migration."));
                    return false;
                }
            }

            return false;
        }

        // Decompose an inline `new RectangleF(x, y, w, h)` / `new DXRectangle(...)` into its components.
        private static bool TryDecomposeRectangle(
            ExpressionSyntax expr, out string x, out string y, out string w, out string h)
        {
            x = y = w = h = "0";
            if (expr is not ObjectCreationExpressionSyntax creation) return false;
            var args = creation.ArgumentList?.Arguments;
            if (args is not { Count: >= 4 }) return false;
            x = args.Value[0].Expression.ToString();
            y = args.Value[1].Expression.ToString();
            w = args.Value[2].Expression.ToString();
            h = args.Value[3].Expression.ToString();
            return true;
        }

        // Map a DevExpress pen/brush/colour expression to a PXA.Pdf colour expression, or null when it
        // is the default black or unrecognised. Also surfaces a stroke width for `new DXPen(color, width)`.
        private string? MapDxColor(ExpressionSyntax expr, out string? lineWidth)
        {
            lineWidth = null;
            switch (expr)
            {
                case MemberAccessExpressionSyntax ma:
                {
                    // DXPens.Red / DXBrushes.Red / DXColor.Red
                    return MapNamedColor(ma.Name.Identifier.ValueText);
                }
                case ObjectCreationExpressionSyntax creation:
                {
                    var typeName = GetSimpleName(creation.Type);
                    var args = creation.ArgumentList?.Arguments;
                    if (typeName == "DXPen")
                    {
                        if (args is { Count: >= 2 })
                            lineWidth = args.Value[1].Expression.ToString();
                        return args is { Count: >= 1 } ? MapDxColor(args.Value[0].Expression, out _) : null;
                    }
                    if (typeName is "DXSolidBrush")
                        return args is { Count: >= 1 } ? MapDxColor(args.Value[0].Expression, out _) : null;
                    return null;
                }
                case InvocationExpressionSyntax inv
                    when inv.Expression is MemberAccessExpressionSyntax fma &&
                         fma.Name.Identifier.ValueText == "FromArgb":
                {
                    // DXColor.FromArgb(r,g,b) or FromArgb(a,r,g,b) — drop alpha, keep last three.
                    var a = inv.ArgumentList.Arguments;
                    if (a.Count == 3)
                        return $"PdfColor.FromRgb({a[0].Expression}, {a[1].Expression}, {a[2].Expression})";
                    if (a.Count == 4)
                        return $"PdfColor.FromRgb({a[1].Expression}, {a[2].Expression}, {a[3].Expression})";
                    return null;
                }
            }
            return null;
        }

        private static string? MapNamedColor(string name) => name switch
        {
            "Black" => null, // already the PXA default
            "White" => "PdfColor.White",
            "Gray" or "Grey" => "PdfColor.Gray",
            "Red" => "PdfColor.RedColor",
            "Green" => "PdfColor.GreenColor",
            "Blue" => "PdfColor.BlueColor",
            _ => null
        };

        private string? TryExtractDxFontSize(ExpressionSyntax fontExpr)
        {
            if (fontExpr is ObjectCreationExpressionSyntax creation)
            {
                var fontArgs = creation.ArgumentList?.Arguments;
                if (fontArgs?.Count >= 2)
                    return fontArgs.Value[1].Expression.ToString();
            }
            // Font passed as a variable: recover the size from the pre-scanned DXFont declarations.
            if (fontExpr is IdentifierNameSyntax id && _fontSizes.TryGetValue(id.Identifier.ValueText, out var size))
                return size;
            return null;
        }

        private static bool IsDxFontDeclaration(GlobalStatementSyntax node)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            return local.Declaration.Variables.Any(static v =>
                v.Initializer?.Value is ObjectCreationExpressionSyntax c && GetSimpleName(c.Type) == "DXFont");
        }

        // True for the encryption statements that BuildPxaSaveOptions has already absorbed:
        // the PdfEncryptionOptions/PdfSaveOptions declarations and any member assignments on them.
        private bool IsConsumedEncryptionStatement(GlobalStatementSyntax node)
        {
            if (IsLocalDeclNamed(node, _encryption.EncryptionVar) ||
                IsLocalDeclNamed(node, _encryption.SaveOptionsVar))
            {
                return true;
            }

            var expr = ExtractExpression(node);
            return expr is AssignmentExpressionSyntax assign &&
                   assign.Left is MemberAccessExpressionSyntax ma &&
                   (ma.Expression.ToString() == _encryption.EncryptionVar ||
                    ma.Expression.ToString() == _encryption.SaveOptionsVar);
        }

        private static bool IsLocalDeclNamed(GlobalStatementSyntax node, string? name)
        {
            if (name is null) return false;
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            return local.Declaration.Variables.Any(v => v.Identifier.ValueText == name);
        }

        private static List<string> GetArgExpressions(GlobalStatementSyntax node)
        {
            var expr = ExtractExpression(node);
            if (expr is not InvocationExpressionSyntax inv) return [];
            return inv.ArgumentList.Arguments.Select(static a => a.Expression.ToString()).ToList();
        }

        // Map a RenderNewPage(...) argument list to the arguments for document.AddPage(...).
        //   RenderNewPage(PdfPaperSize.A4, graphics)     → ""                    (A4 is the PXA default)
        //   RenderNewPage(PdfPaperSize.A3, graphics)     → "PdfPagePreset.A3"
        //   RenderNewPage(PdfPaperSize.Letter, graphics) → "PdfPagePreset.Letter"
        //   RenderNewPage(width, height, graphics)       → "width, height"
        // Unmapped paper sizes default to A4 and set <paramref name="unmappedSize"/> for a warning.
        private static string BuildAddPageArgs(IReadOnlyList<string> args, out string? unmappedSize)
        {
            unmappedSize = null;
            if (args.Count >= 1 && args[0].Contains("PdfPaperSize", StringComparison.Ordinal))
            {
                var name = args[0].Contains('.', StringComparison.Ordinal)
                    ? args[0][(args[0].LastIndexOf('.') + 1)..]
                    : args[0];
                switch (name)
                {
                    case "A4": return "";                    // PXA AddPage() defaults to A4
                    case "A3": return "PdfPagePreset.A3";
                    case "Letter": return "PdfPagePreset.Letter";
                    case "Legal": return "612, 1008";        // 8.5 × 14 in
                    case "A5": return "420, 595";            // 148 × 210 mm
                    default: unmappedSize = name; return "";
                }
            }

            // RenderNewPage(width, height, graphics) — explicit dimensions.
            if (args.Count >= 3)
                return $"{args[0]}, {args[1]}";

            return "";
        }

        private static bool IsCreationDeclaration(GlobalStatementSyntax node, string typeName)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var firstVar = local.Declaration.Variables.FirstOrDefault();
            if (firstVar?.Initializer?.Value is not ObjectCreationExpressionSyntax creation) return false;
            return GetSimpleName(creation.Type) == typeName;
        }

        private static bool IsDeclarationWithCall(GlobalStatementSyntax node, string method)
        {
            if (node.Statement is not LocalDeclarationStatementSyntax local) return false;
            var init = local.Declaration.Variables.FirstOrDefault()?.Initializer?.Value;
            return init is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method;
        }

        private bool IsMethodCallOn(GlobalStatementSyntax node, string variable, string method)
        {
            var expr = ExtractExpression(node);
            if (expr is not InvocationExpressionSyntax inv ||
                inv.Expression is not MemberAccessExpressionSyntax ma ||
                ma.Name.Identifier.ValueText != method)
            {
                return false;
            }

            // Processor calls match by symbol when resolvable; otherwise by name.
            var symbol = string.Equals(variable, _processorVar, StringComparison.Ordinal)
                ? _processorSymbol
                : null;
            return ReceiverMatches(ma.Expression, symbol, variable);
        }

        // Match a member-access receiver against the tracked local: prefer symbol identity (robust to
        // reassignment / `this.`-qualification), fall back to the syntactic variable name.
        private bool ReceiverMatches(ExpressionSyntax receiver, ISymbol? expectedSymbol, string variableName)
        {
            if (expectedSymbol != null)
            {
                var actual = _semanticModel.GetSymbolInfo(receiver).Symbol;
                if (actual != null)
                    return SymbolEqualityComparer.Default.Equals(actual, expectedSymbol);
            }
            return receiver.ToString() == variableName;
        }

        private static bool IsAnyMethodCall(GlobalStatementSyntax node, string method)
        {
            var expr = ExtractExpression(node);
            return expr is InvocationExpressionSyntax inv &&
                   inv.Expression is MemberAccessExpressionSyntax ma &&
                   ma.Name.Identifier.ValueText == method;
        }

        private static ExpressionSyntax? ExtractExpression(GlobalStatementSyntax node) =>
            node.Statement is ExpressionStatementSyntax es ? es.Expression : null;

        private static GlobalStatementSyntax MakeGlobal(string code, GlobalStatementSyntax original)
        {
            var stmt = SyntaxFactory.ParseStatement(code + "\n");
            return SyntaxFactory.GlobalStatement(stmt)
                .WithLeadingTrivia(original.GetLeadingTrivia())
                .WithTrailingTrivia(original.GetTrailingTrivia());
        }

        private static string GetSimpleName(TypeSyntax type) => type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
            _ => type.ToString()
        };

        private static MigrationDiagnostic Info(string id, string message) => new()
        {
            Id = id,
            Message = message,
            Severity = MigrationDiagnosticSeverity.Info
        };

        private static MigrationDiagnostic Warning(string id, string message) => new()
        {
            Id = id,
            Message = message,
            Severity = MigrationDiagnosticSeverity.Warning
        };
    }
}
