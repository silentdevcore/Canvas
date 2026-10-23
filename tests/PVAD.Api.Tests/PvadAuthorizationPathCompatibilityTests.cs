using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadAuthorizationPathCompatibilityTests
{
    [Theory]
    [InlineData("/api/pvad/export")]
    [InlineData("/api/pvad/document/import-pdf-engine")]
    [InlineData("/api/pvad/spreadsheet/export")]
    [InlineData("/api/pvad/migration/convert")]
    [InlineData("/api/pvad/templates/render-design")]
    [InlineData("/api/pvad/pdf-viewer/annotations/redact")]
    public async Task Canonical_branded_operations_do_not_bypass_the_anonymous_product_gate(string path)
    {
        var reachedEndpoint = false;
        var gate = new PvadProductAccessMiddleware(_ => { reachedEndpoint = true; return Task.CompletedTask; },
            Options.Create(new PvadProductAccessOptions { Enabled = true }));
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        await gate.InvokeAsync(context, null!, null!, null!);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(reachedEndpoint);
        context.Response.Body.Position = 0;
        using var payload = await System.Text.Json.JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("PXA_AUTHENTICATION_REQUIRED", payload.RootElement.GetProperty("code").GetString());
        Assert.Equal("PVAD_AUTHENTICATION_REQUIRED", payload.RootElement.GetProperty("canonicalCode").GetString());
        Assert.Equal("PXA_AUTHENTICATION_REQUIRED", payload.RootElement.GetProperty("title").GetString());
        Assert.Equal("PVAD_AUTHENTICATION_REQUIRED", payload.RootElement.GetProperty("canonicalTitle").GetString());
    }

    [Theory]
    [InlineData("/export", "POST")]
    [InlineData("/document/import-pdf-engine", "POST")]
    [InlineData("/migration/convert", "POST")]
    [InlineData("/migration/report-to-design", "POST")]
    [InlineData("/spreadsheet/export", "POST")]
    [InlineData("/spreadsheet/calculate", "POST")]
    [InlineData("/pdf-viewer/annotations/redact", "POST")]
    [InlineData("/templates", "GET")]
    [InlineData("/templates/render-design", "POST")]
    public void All_branded_paths_require_the_same_permissions_and_entitlements(string suffix, string method)
    {
        var expectedPermission = PvadDesignerPermissionResolver.Resolve("/api" + suffix, method);
        var expectedCapability = PvadProductAccessMiddleware.ResolveCapability("/api" + suffix);
        Assert.NotNull(expectedCapability);
        foreach (var brand in new[] { "pvad", "pxa", "PVAD", "PXA" })
        {
            var path = new PathString($"/api/{brand}{suffix}");
            Assert.Equal(expectedPermission, PvadDesignerPermissionResolver.Resolve(path, method));
            Assert.Equal(expectedCapability, PvadProductAccessMiddleware.ResolveCapability(path));
        }
    }

    [Theory]
    [InlineData("/api/pvad/v1/pdf/signatures/sign", "/api/pxa/v1/pdf/signatures/sign")]
    [InlineData("/api/pvad/v1/pdf/redactions/apply", "/api/pxa/v1/pdf/redactions/apply")]
    public void Versioned_aliases_preserve_sensitive_operation_requirements(string canonical, string legacy)
    {
        Assert.Equal(PvadDesignerPermissionResolver.Resolve(canonical, "POST"),
            PvadDesignerPermissionResolver.Resolve(legacy, "POST"));
        Assert.Equal(PvadProductAccessMiddleware.ResolveCapability(canonical),
            PvadProductAccessMiddleware.ResolveCapability(legacy));
    }

    [Theory]
    [InlineData("/api/pvad/exported")]
    [InlineData("/api/pxa/documentary")]
    [InlineData("/api/pvad/v10/pdf/signatures/sign")]
    public void Near_prefixes_are_not_rewritten(string path) =>
        Assert.Equal(new PathString(path), PvadAuthorizationPaths.Normalize(path));
}
