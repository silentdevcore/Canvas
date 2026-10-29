using System.Text.Json;
using System.Xml.Linq;

namespace PVAD.Api.Tests;

public sealed class DotnetDependencyAlignmentTests
{
    [Theory]
    [InlineData("PVAD.WebApi/PVAD.WebApi.csproj", "Microsoft.AspNetCore.OpenApi")]
    [InlineData("PVAD.WebApi/PVAD.WebApi.csproj", "Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore")]
    [InlineData("PVAD.WebApi/PVAD.WebApi.csproj", "Microsoft.EntityFrameworkCore.Design")]
    [InlineData("src/Infrastructure/PVAD.Infrastructure.Persistence/PVAD.Infrastructure.Persistence.csproj", "Microsoft.AspNetCore.Identity.EntityFrameworkCore")]
    [InlineData("src/Infrastructure/PVAD.Infrastructure.Persistence/PVAD.Infrastructure.Persistence.csproj", "Microsoft.EntityFrameworkCore.Design")]
    [InlineData("tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj", "Microsoft.AspNetCore.Mvc.Testing")]
    [InlineData("tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj", "Microsoft.EntityFrameworkCore.InMemory")]
    [InlineData("tests/PVAD.Observability.WebhookRelay.Tests/PVAD.Observability.WebhookRelay.Tests.csproj", "Microsoft.AspNetCore.Mvc.Testing")]
    public void Platform_references_match_the_local_EF_tool(string project, string package)
    {
        Assert.Equal(ExpectedVersion(), ReferenceVersion(project, package));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.EntityFrameworkCore.Abstractions")]
    [InlineData("Microsoft.EntityFrameworkCore.Relational")]
    [InlineData("Microsoft.EntityFrameworkCore.InMemory")]
    [InlineData("Microsoft.AspNetCore.Identity.EntityFrameworkCore")]
    [InlineData("Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore.Mvc.Testing")]
    [InlineData("Microsoft.AspNetCore.OpenApi")]
    public void Resolved_platform_dependencies_match_the_local_EF_tool(string package)
    {
        AssertResolvedVersion(package, ExpectedVersion());
    }

    [Fact]
    public void Resolved_OpenApi_library_matches_the_explicit_project_reference()
    {
        const string package = "Microsoft.OpenApi";
        AssertResolvedVersion(package, ReferenceVersion("PVAD.WebApi/PVAD.WebApi.csproj", package));
    }

    [Theory]
    [InlineData("PVAD.WebApi/PVAD.WebApi.csproj")]
    [InlineData("src/Infrastructure/PVAD.Infrastructure.Word/PVAD.Infrastructure.Word.csproj")]
    public void Qr_consumers_resolve_the_same_reviewed_version(string project)
    {
        Assert.Equal("1.8.1", ReferenceVersion(project, "QRCoder"));
        AssertResolvedVersion("QRCoder", "1.8.1");
    }

    [Theory]
    [InlineData("OpenTelemetry")]
    [InlineData("OpenTelemetry.Api")]
    [InlineData("OpenTelemetry.Api.ProviderBuilderExtensions")]
    [InlineData("OpenTelemetry.Exporter.OpenTelemetryProtocol")]
    [InlineData("OpenTelemetry.Extensions.Hosting")]
    [InlineData("OpenTelemetry.Instrumentation.AspNetCore")]
    [InlineData("OpenTelemetry.Instrumentation.Http")]
    [InlineData("OpenTelemetry.Instrumentation.Runtime")]
    public void Telemetry_components_resolve_as_one_reviewed_version(string package)
    {
        const string version = "1.20.1";
        if (package is not ("OpenTelemetry" or "OpenTelemetry.Api" or "OpenTelemetry.Api.ProviderBuilderExtensions"))
            Assert.Equal(version, ReferenceVersion("PVAD.WebApi/PVAD.WebApi.csproj", package));
        AssertResolvedVersion(package, version);
    }

    [Theory]
    [InlineData("PVAD.WebApi/PVAD.WebApi.csproj", "Npgsql.OpenTelemetry")]
    [InlineData("src/Infrastructure/PVAD.Infrastructure.Persistence/PVAD.Infrastructure.Persistence.csproj", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void PostgreSql_provider_references_remain_on_the_validated_version(string project, string package)
    {
        Assert.Equal("10.0.3", ReferenceVersion(project, package));
        AssertResolvedVersion(package, "10.0.3");
    }

    [Fact]
    public void Git_build_task_is_resolved_as_a_private_build_only_dependency()
    {
        const string project = "src/Infrastructure/PVAD.Infrastructure.Spreadsheet";
        const string package = "Microsoft.Build.Tasks.Git";
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), project, "PVAD.Infrastructure.Spreadsheet.csproj"));
        var reference = Assert.Single(document.Descendants("PackageReference"),
            element => (string?)element.Attribute("Include") == package);
        Assert.Equal("10.0.403", (string?)reference.Attribute("Version"));
        Assert.Equal("all", (string?)reference.Attribute("PrivateAssets"));
        using var assets = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), project, "obj/project.assets.json")));
        Assert.True(assets.RootElement.GetProperty("libraries").TryGetProperty($"{package}/10.0.403", out _));
        var dependency = assets.RootElement.GetProperty("project").GetProperty("frameworks")
            .GetProperty("net10.0").GetProperty("dependencies").GetProperty(package);
        Assert.Equal("All", dependency.GetProperty("suppressParent").GetString());
        using var runtime = JsonDocument.Parse(File.ReadAllText(
            Path.ChangeExtension(typeof(DotnetDependencyAlignmentTests).Assembly.Location, ".deps.json")));
        Assert.DoesNotContain(runtime.RootElement.GetProperty("libraries").EnumerateObject(),
            property => property.Name.StartsWith($"{package}/", StringComparison.Ordinal));
    }

    private static string ReferenceVersion(string project, string package)
    {
        var document = XDocument.Load(Path.Combine(FindRepositoryRoot(), project));
        var reference = Assert.Single(document.Descendants("PackageReference"),
            element => (string?)element.Attribute("Include") == package);
        return Assert.IsType<string>((string?)reference.Attribute("Version"));
    }

    private static void AssertResolvedVersion(string package, string version)
    {
        using var dependencies = JsonDocument.Parse(File.ReadAllText(
            Path.ChangeExtension(typeof(DotnetDependencyAlignmentTests).Assembly.Location, ".deps.json")));
        var expected = $"{package}/{version}";

        Assert.True(dependencies.RootElement.GetProperty("libraries").TryGetProperty(expected, out _),
            $"The resolved test dependency graph does not contain {expected}.");
    }

    private static string ExpectedVersion()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), ".config", "dotnet-tools.json")));
        var version = manifest.RootElement.GetProperty("tools").GetProperty("dotnet-ef")
            .GetProperty("version").GetString();
        Assert.True(Version.TryParse(version, out _), "The local EF tool must specify a stable version.");
        return version!;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "PVAD.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
