using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

public sealed class AdminDocumentationControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public AdminDocumentationControllerTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Anonymous_users_cannot_download_handbook_or_images()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/pxa/v1/admin/documentation")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/pxa/v1/admin/documentation/images/dashboard.png")).StatusCode);
    }

    [Fact]
    public void Controller_requires_an_explicit_administrator_role_and_disables_caching()
    {
        var controllerType = typeof(AdminDocumentationController);
        var authorize = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        var responseCache = controllerType.GetCustomAttribute<ResponseCacheAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(
            $"{PxaRoles.SystemAdministrator},{PxaRoles.OrganizationAdministrator}",
            authorize.Roles);
        Assert.NotNull(responseCache);
        Assert.True(responseCache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, responseCache.Location);
    }

    [Fact]
    public void Protected_handbook_covers_system_status_and_operator_links()
    {
        var path = Path.Combine(
            factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath,
            "AdminDocumentation",
            "admin-documentation.json");
        using var handbook = JsonDocument.Parse(File.ReadAllText(path));
        var topics = handbook.RootElement.GetProperty("groups")
            .EnumerateArray()
            .SelectMany(group => group.GetProperty("topics").EnumerateArray())
            .ToArray();
        var telemetry = Assert.Single(
            topics,
            topic => topic.GetProperty("title").GetString() == "System status and telemetry");
        var links = telemetry.GetProperty("references")
            .EnumerateArray()
            .Select(reference => reference.GetProperty("href").GetString())
            .ToArray();

        Assert.Contains("https://admin.powerdoxautomation.com/system-status", links);
        Assert.Contains("https://operator.powerdoxautomation.com/operator/grafana/", links);
        Assert.Contains("http://localhost:3001/operator/grafana/", links);
        Assert.Contains("http://localhost:5087/health/live", links);
        Assert.Contains("http://localhost:5087/health/ready", links);
        Assert.Contains("http://localhost:13133/", links);
        Assert.Contains("http://localhost:8025/", links);
        Assert.Contains(
            handbook.RootElement.GetProperty("routeCoverage").EnumerateArray(),
            route => route[0].GetString() == "/system-status" &&
                     route[1].GetString() == "System status and telemetry");
    }
}
