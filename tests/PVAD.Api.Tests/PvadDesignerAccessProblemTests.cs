using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using PVAD.WebApi.Infrastructure;
using PVAD.WebApi.Application.Designer;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

public sealed class PvadDesignerAccessProblemTests
{
    [Theory]
    [InlineData(false, "PVADAPI001")]
    [InlineData(true, "PVAD_DESIGNER_SESSION_EXPIRED")]
    public async Task Access_failures_use_the_problem_details_media_type(bool authenticated, string code)
    {
        using var server = new TestServer(new WebHostBuilder().Configure(app => app.Run(async context =>
        {
            if (authenticated) context.User = new ClaimsPrincipal(new ClaimsIdentity([], "synthetic-problem-test"));
            var middleware = new PvadDesignerAccessMiddleware(_ => throw new InvalidOperationException("Denied requests must not proceed."));
            // Both early exits must occur before entitlement, persistence or CSRF work.
            await middleware.InvokeAsync(context, null!, null!, null!);
        })));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/export");
        request.Headers.Add("X-PVAD-Application", "designer");
        using var client = server.CreateClient();
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/api/pvad/v1/migrations/pdf/viewer/nutrient-viewer/convert")]
    [InlineData("/api/pvad/v1/migrations/pdf/designer/nutrient-viewer/convert")]
    public async Task Both_Nutrient_routes_evaluate_the_organization_feature_before_conversion(string route)
    {
        var gate = new DeniedFeature();
        using var server = new TestServer(new WebHostBuilder().Configure(app => app.Run(async context =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([], "synthetic-feature-test"));
            var middleware = new PvadDesignerFeatureGateMiddleware(_ => throw new InvalidOperationException("Disabled features must not execute."));
            await middleware.InvokeAsync(context, new Tenant(), gate);
        })));
        using var client = server.CreateClient();
        using var response = await client.PostAsync(route, new StringContent("{}"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("designer.nutrient-viewer-migration", gate.Feature);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PVAD_DESIGNER_FEATURE_DISABLED", body.RootElement.GetProperty("code").GetString());
    }

    private sealed class Tenant : IPvadTenantContext
    {
        public Guid? UserId => Guid.Parse("11111111-1111-4111-8111-111111111111");
        public Guid? OrganizationId => Guid.Parse("22222222-2222-4222-8222-222222222222");
    }

    private sealed class DeniedFeature : IPvadDesignerFeatureGate
    {
        public string? Feature { get; private set; }
        public Task<DesignerFeatureDecision> EvaluateAsync(Guid organizationId, Guid userId, string featureId, CancellationToken cancellationToken = default)
        {
            Feature = featureId;
            return Task.FromResult(new DesignerFeatureDecision(false, "PVAD_DESIGNER_FEATURE_DISABLED", "Synthetic organization disabled this feature.", null, null));
        }
    }
}
