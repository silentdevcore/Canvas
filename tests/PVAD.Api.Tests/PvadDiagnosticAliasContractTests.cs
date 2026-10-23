using System.Text.Json;
using PVAD.Core.Contracts;
using PVAD.WebApi.Application.Identity;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

public sealed class PvadDiagnosticAliasContractTests
{
    [Fact]
    public void ActualMvcFactory_AllowsCallerDiagnosticExtensionsBeforeCompletion()
    {
        using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        var controller = new ProblemProbe
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = factory.Services },
            },
            ProblemDetailsFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory>(factory.Services),
        };
        var result = controller.Problem(statusCode: 403, extensions:
            new Dictionary<string, object?> { ["code"] = "PXA_DESIGNER_ACCOUNT_LOCKED" });
        var problem = Assert.IsType<Microsoft.AspNetCore.Mvc.ProblemDetails>(result.Value);
        PVAD.WebApi.Infrastructure.PvadApiProblems.Complete(controller.HttpContext, problem, 403);
        Assert.Equal("PXA_DESIGNER_ACCOUNT_LOCKED", problem.Extensions["code"]);
        Assert.Equal("PVAD_DESIGNER_ACCOUNT_LOCKED", problem.Extensions["canonicalCode"]);
    }

    private sealed class ProblemProbe : Microsoft.AspNetCore.Mvc.ControllerBase { }

    [Theory]
    [InlineData("PXACODE001", "PVADCODE001")]
    [InlineData("PXACODE042", "PVADCODE042")]
    [InlineData("PXACODE122", "PVADCODE122")]
    [InlineData("PXAPROJECT014", "PVADPROJECT014")]
    [InlineData("PXAPROJECT_EXTERNAL_LINK", "PVADPROJECT_EXTERNAL_LINK")]
    [InlineData("PXADESIGNER001", "PVADDESIGNER001")]
    [InlineData("PXA_LICENSE_EXPIRED", "PVAD_LICENSE_EXPIRED")]
    [InlineData("PXA_LICENSE_VALID", "PVAD_LICENSE_VALID")]
    [InlineData("PXA_DESIGNER_HANDOFF_INVALID", "PVAD_DESIGNER_HANDOFF_INVALID")]
    [InlineData("PXA_TRIAL_EXPIRED", "PVAD_TRIAL_EXPIRED")]
    [InlineData("PXA_DESIGNER_ALPHA_OPT_IN_REQUIRED", "PVAD_DESIGNER_ALPHA_OPT_IN_REQUIRED")]
    public void KnownIds_HaveCanonicalAliases(string legacy, string canonical) =>
        Assert.Equal(canonical, PvadDiagnosticAliases.CanonicalCode(legacy));

    [Theory]
    [InlineData("PXACODE999")]
    [InlineData("PXA_LICENSE_FUTURE")]
    [InlineData("PXA_DESIGNER_FUTURE")]
    [InlineData("PXAPROJECT999")]
    [InlineData("PXAPROJECT_FUTURE")]
    [InlineData("CANMIGDEVEXP001")]
    [InlineData("CS1002")]
    [InlineData(null)]
    public void UnknownIds_AreNotRenamed(string? code) =>
        Assert.Null(PvadDiagnosticAliases.CanonicalCode(code));

    [Fact]
    public void LegacyWorkerPayload_GainsDerivedAliasWithoutChangingCode()
    {
        const string legacy = """{"code":"PXACODE003","severity":"error","message":"Unsafe code is blocked","line":2}""";
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var diagnostic = JsonSerializer.Deserialize<PvadCodeDiagnosticDto>(legacy, options)!;
        Assert.Equal("PXACODE003", diagnostic.Code);
        Assert.Equal("PVADCODE003", diagnostic.CanonicalCode);
        diagnostic.Code = "PXACODE042";
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(diagnostic, options));
        Assert.Equal("PXACODE042", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("PVADCODE042", document.RootElement.GetProperty("canonicalCode").GetString());
        Assert.Equal(2, diagnostic.Line);
    }

    [Fact]
    public void IncomingAlias_CannotOverrideAuthoritativeWorkerCode()
    {
        var diagnostic = JsonSerializer.Deserialize<PvadCodeDiagnosticDto>(
            """{"code":"PXACODE003","canonicalCode":"PVADCODE001"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("PVADCODE003", diagnostic.CanonicalCode);
    }

    [Fact]
    public void PublicLicenseResponses_ExposeAliasWithoutChangingDecisionOrDates()
    {
        var now = DateTimeOffset.UtcNow;
        var account = new AccountLicenseValidationResponse(false, "Expired", now, now, "PXA_LICENSE_EXPIRED");
        var admin = new AdminLicenseValidationResponse(false, true, "Expired", now, now, "PXA_LICENSE_EXPIRED");
        Assert.Equal("PVAD_LICENSE_EXPIRED", account.CanonicalCode);
        Assert.Equal(account.CanonicalCode, admin.CanonicalCode);
        Assert.False(admin.Valid);
        Assert.True(admin.SignatureValid);
        Assert.Equal("PXA_LICENSE_EXPIRED", account.Code);
        Assert.Equal(now, admin.ValidUntil);
    }

    [Fact]
    public void HandoffFailures_KeepDecisionsAndLegacyCodes()
    {
        var invalid = DesignerHandoffResult.Invalid();
        var denied = DesignerExchangeResult.Denied("PXA_TRIAL_EXPIRED", "Trial expired");
        Assert.False(invalid.Success);
        Assert.Equal("PVAD_DESIGNER_HANDOFF_INVALID", invalid.CanonicalCode);
        Assert.True(denied.Forbidden);
        Assert.Equal("PXA_TRIAL_EXPIRED", denied.Code);
        Assert.Equal("PVAD_TRIAL_EXPIRED", denied.CanonicalCode);
    }
}
