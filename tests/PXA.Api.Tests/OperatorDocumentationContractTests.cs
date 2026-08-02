using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

public sealed class OperatorDocumentationContractTests
{
    [Fact]
    public void Operator_documentation_has_a_separate_protected_gateway_and_safe_client()
    {
        var root = FindRepositoryRoot();
        var gateway = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "operator-gateway",
            "templates",
            "default.conf.template"));
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));
        var client = File.ReadAllText(Path.Combine(
            root,
            "deploy",
            "observability",
            "operator-gateway",
            "site",
            "app.js"));
        var publicDocumentation = File.ReadAllText(Path.Combine(
            root,
            "websites",
            "PXA.Documentation",
            "src",
            "main.js"));

        Assert.Contains("location /documentation/", gateway, StringComparison.Ordinal);
        Assert.Contains("auth_request /_pxa_operator_auth;", gateway, StringComparison.Ordinal);
        Assert.Contains("Content-Security-Policy", gateway, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", gateway, StringComparison.Ordinal);
        Assert.Contains(
            "operator-gateway/site:/usr/share/nginx/html/documentation:ro",
            compose,
            StringComparison.Ordinal);
        Assert.Contains("textContent", client, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", client, StringComparison.Ordinal);
        Assert.DoesNotContain("eval(", client, StringComparison.Ordinal);
        Assert.DoesNotContain("PXA_RESTORE_CONFIRM", publicDocumentation, StringComparison.Ordinal);
        Assert.DoesNotContain("restore-postgres.sh", publicDocumentation, StringComparison.Ordinal);
    }

    [Fact]
    public void Operator_documentation_controller_requires_system_administrator_and_no_store()
    {
        var controller = typeof(OperatorDocumentationController);
        var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();
        var cache = controller.GetCustomAttribute<ResponseCacheAttribute>();

        Assert.Equal(PxaRoles.SystemAdministrator, authorize?.Roles);
        Assert.True(cache?.NoStore == true);
        Assert.Equal(ResponseCacheLocation.None, cache?.Location);
    }

    [Fact]
    public void Only_explicitly_registered_runbooks_are_packaged_for_operator_delivery()
    {
        var root = FindRepositoryRoot();
        var controller = File.ReadAllText(Path.Combine(
            root,
            "PXA.WebApi",
            "Controllers",
            "OperatorDocumentationController.cs"));

        Assert.Contains("PXA.Admin-Operations.md", controller, StringComparison.Ordinal);
        Assert.Contains("PXA.Legal-Backup-Restore-And-Recovery.md", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.GetFiles", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.Combine(documentationRoot, slug", controller, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "PXA.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
