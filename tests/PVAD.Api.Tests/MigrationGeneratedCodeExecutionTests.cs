using System.IO.Compression;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PVAD.Infrastructure.Spreadsheet;
using PVAD.Migration.Abstractions;
using PVAD.Migration.Pdf;
using PVAD.Migration.Spreadsheet;
using PVAD.Pdf;

namespace PVAD.Api.Tests;

public sealed class MigrationGeneratedCodeExecutionTests
{
    [Fact]
    public async Task WorkbookBuilderDocumentationExample_CompilesAndExports()
    {
        var fixture = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(fixture, "src/Infrastructure/PVAD.Infrastructure.Spreadsheet/PvadWorkbookBuilder.cs"));
        var syntax = CSharpSyntaxTree.ParseText(source).GetRoot();
        var example = syntax.DescendantTrivia().Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>().SelectMany(comment => comment.DescendantNodes().OfType<XmlElementSyntax>())
            .First(element => element.StartTag.Name.ToString() == "example");
        var code = string.Concat(example.Content.OfType<XmlTextSyntax>().SelectMany(text => text.TextTokens).Select(token => token.ValueText));
        var directory = Directory.CreateTempSubdirectory("pvad-builder-example-").FullName;
        try
        {
            var output = Path.Combine(directory, "example.xlsx");
            var bound = (CompilationUnitSyntax)new FixtureOutputPath(output).Visit(
                CSharpSyntaxTree.ParseText("using PVAD.Infrastructure.Spreadsheet;\n" + code).GetCompilationUnitRoot())!;
            await CompileAndRun(bound);
            using var stream = File.OpenRead(output);
            var workbook = new ExcelWorkbookImporter().Import(stream);
            Assert.Equal("Sales", Assert.Single(workbook.Sheets).Name);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    public static IEnumerable<object[]> Providers() =>
        PdfMigrationProviders.Keys.Select(key => new object[] { "pdf", key })
            .Concat(SpreadsheetMigrationProviders.Keys.Select(key => new object[] { "spreadsheet", key }));

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task BasicProviderFixture_CompilesAndExecutesAgainstPvad(string domain, string key)
    {
        ISourceMigration provider = domain == "pdf"
            ? PdfMigrationProviders.Create(key) : SpreadsheetMigrationProviders.Create(key);
        var fixture = FindFixture(provider, domain);
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(fixture)).GetCompilationUnitRoot();
        var source = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.AttributeLists.ToString().Contains("Fact", StringComparison.Ordinal))
            .SelectMany(method => method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            .Where(variable => variable.Identifier.ValueText is "source" or "src")
            .Select(variable => variable.Initializer?.Value as LiteralExpressionSyntax)
            .First(literal => literal?.Token.ValueText.Contains("using ", StringComparison.Ordinal) == true)!.Token.ValueText;
        var directory = Directory.CreateTempSubdirectory("pvad-migration-execution-").FullName;
        var output = Path.Combine(directory, domain == "pdf" ? "result.pdf" : "result.xlsx");
        try
        {
            // Bind only synthetic fixture output filenames; production source is never rewritten here.
            var bound = new FixtureOutputPath(output).Visit(CSharpSyntaxTree.ParseText(source).GetRoot())!.ToFullString();
            var result = provider.Migrate(bound);
            if (domain == "pdf" && key == PdfMigrationProviderKeys.PdfTools)
            {
                Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CANMIGPDFTOOLS020");
                Assert.Contains("using PdfTools;", result.MigratedCode);
                Assert.DoesNotContain("using PVAD.Pdf;", result.MigratedCode);
                return; // This provider deliberately preserves unsupported vendor code for manual work.
            }

            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == MigrationDiagnosticSeverity.Error);
            var migrated = CSharpSyntaxTree.ParseText(result.MigratedCode).GetCompilationUnitRoot();
            Assert.DoesNotContain(migrated.Usings, directive =>
                directive.Name?.ToString().StartsWith("PXA.", StringComparison.Ordinal) == true);
            var prelude = CSharpSyntaxTree.ParseText($"var path = {JsonSerializer.Serialize(output)}; var outputPath = path; var outPath = path;")
                .GetCompilationUnitRoot();
            migrated = migrated.WithMembers(migrated.Members.InsertRange(0, prelude.Members));
            if (domain == "spreadsheet" && !migrated.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(call => call.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "Save"))
            {
                var workbook = migrated.DescendantNodes().OfType<VariableDeclaratorSyntax>().First(variable =>
                    variable.Initializer?.Value is ObjectCreationExpressionSyntax creation && creation.Type.ToString() == "PvadWorkbook");
                migrated = migrated.AddMembers(SyntaxFactory.GlobalStatement(
                    SyntaxFactory.ParseStatement($"{workbook.Identifier.ValueText}.Save(path);")));
            }

            await CompileAndRun(migrated);
            var bytes = await File.ReadAllBytesAsync(output);
            if (domain == "pdf")
            {
                Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes.AsSpan(0, Math.Min(8, bytes.Length))));
                Assert.Contains("%%EOF", Encoding.Latin1.GetString(bytes));
            }
            else
            {
                using var stream = new MemoryStream(bytes);
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
                    Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
                stream.Position = 0;
                var workbook = new ExcelWorkbookImporter().Import(stream);
                Assert.NotEmpty(workbook.Sheets);
                Assert.Contains(workbook.Sheets, sheet => sheet.Cells.Count > 0);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string FindFixture(ISourceMigration provider, string domain)
    {
        var root = FindRepositoryRoot();
        var directories = domain == "pdf"
            ? [Path.Combine(root, "tests", provider.GetType().Assembly.GetName().Name + ".Tests")]
            : Directory.GetDirectories(Path.Combine(root, "tests"), "PVAD.Migration.Spreadsheet.Code.*.Tests");
        return Assert.Single(directories.SelectMany(directory => Directory.GetFiles(directory, provider.GetType().Name + "Tests.cs")));
    }

    private static string FindRepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PVAD.sln"))) root = root.Parent;
        return root?.FullName ?? throw new DirectoryNotFoundException("PVAD repository root was not found.");
    }

    private static async Task CompileAndRun(CompilationUnitSyntax root)
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => Path.GetDirectoryName(path) == runtimeDirectory)
            .Append(typeof(PdfDocument).Assembly.Location).Append(typeof(PvadWorkbook).Assembly.Location)
            .Append(typeof(PVAD.Core.Contracts.SpreadsheetDto).Assembly.Location)
            .Append(typeof(PVAD.Generator.Pdf).Assembly.Location)
            .Append(typeof(PVAD.Importer.Editing.PdfPageEditor).Assembly.Location).Distinct(StringComparer.Ordinal);
        var compilation = CSharpCompilation.Create("PvadMigrationExecution_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.Create(root)], paths.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        using var binary = new MemoryStream();
        var emit = compilation.Emit(binary);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        binary.Position = 0;
        var context = new AssemblyLoadContext("pvad-migration-test", isCollectible: true);
        try
        {
            var entry = context.LoadFromStream(binary).EntryPoint!;
            var result = entry.Invoke(null, entry.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
            if (result is Task task) await task;
        }
        finally { context.Unload(); }
    }

    private sealed class FixtureOutputPath(string output) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node) =>
            node.IsKind(SyntaxKind.StringLiteralExpression) && node.Token.ValueText.EndsWith(".xlsx", StringComparison.Ordinal)
                ? SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(output)).WithTriviaFrom(node)
                : base.VisitLiteralExpression(node);
    }
}
