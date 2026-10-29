using System.Text.Json;
using System.Xml.Linq;

namespace PVAD.Api.Tests;

public sealed class RoslynDependencyAlignmentTests
{
    private const string CompilerProject = "src/Migrations/Common/PVAD.Migration.Roslyn/PVAD.Migration.Roslyn.csproj";

    [Theory]
    [InlineData(CompilerProject, "Microsoft.CodeAnalysis.CSharp")]
    [InlineData("src/Migrations/Report/PVAD.Migration.Report.Designer.DevExpress/PVAD.Migration.Report.Designer.DevExpress.csproj", "Microsoft.CodeAnalysis.CSharp")]
    [InlineData("PVAD.WebApi/PVAD.WebApi.csproj", "Microsoft.CodeAnalysis.CSharp.Scripting")]
    [InlineData("PVAD.WebApi/PVAD.WebApi.csproj", "Microsoft.CodeAnalysis.CSharp.Workspaces")]
    [InlineData("PVAD.WebApi/PVAD.WebApi.csproj", "Microsoft.CodeAnalysis.Workspaces.MSBuild")]
    public void Roslyn_references_match_the_migration_compiler(string project, string package)
    {
        Assert.Equal(ExpectedVersion(), ReferenceVersion(project, package));
    }

    [Theory]
    [InlineData("Microsoft.CodeAnalysis.Common")]
    [InlineData("Microsoft.CodeAnalysis.CSharp")]
    [InlineData("Microsoft.CodeAnalysis.Scripting.Common")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.Scripting")]
    [InlineData("Microsoft.CodeAnalysis.Workspaces.Common")]
    [InlineData("Microsoft.CodeAnalysis.CSharp.Workspaces")]
    [InlineData("Microsoft.CodeAnalysis.Workspaces.MSBuild")]
    public void Resolved_Roslyn_dependencies_match_the_migration_compiler(string package)
    {
        using var dependencies = JsonDocument.Parse(File.ReadAllText(
            Path.ChangeExtension(typeof(RoslynDependencyAlignmentTests).Assembly.Location, ".deps.json")));
        var expected = $"{package}/{ExpectedVersion()}";

        Assert.True(dependencies.RootElement.GetProperty("libraries").TryGetProperty(expected, out _),
            $"The resolved test dependency graph does not contain {expected}.");
    }

    private static string ExpectedVersion()
    {
        var version = ReferenceVersion(CompilerProject, "Microsoft.CodeAnalysis.CSharp");
        Assert.True(Version.TryParse(version, out _), "The migration compiler must specify a stable version.");
        return version;
    }

    private static string ReferenceVersion(string project, string package)
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), project));
        var reference = Assert.Single(document.Descendants("PackageReference"),
            element => (string?)element.Attribute("Include") == package);
        return Assert.IsType<string>((string?)reference.Attribute("Version"));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "PVAD.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
