using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
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
}
