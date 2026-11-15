using Microsoft.AspNetCore.Http;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadApiProblemsTests
{
    [Fact]
    public void Problems_expose_only_the_canonical_code_and_type()
    {
        var problem = PvadApiProblems.Create(new DefaultHttpContext(), 403,
            title: "PVAD_AUDIT_EXPORT_REQUIRES_ENTERPRISE");
        Assert.Equal("PVAD_AUDIT_EXPORT_REQUIRES_ENTERPRISE", problem.Title);
        Assert.Equal("PVADAPI002", problem.Extensions["code"]);
        Assert.Equal("https://docs.pvade.com/problems/pvadapi002", problem.Type);
        foreach (var removed in new[] { "canonicalCode", "canonicalTitle", "canonicalType", "legacyType" })
            Assert.False(problem.Extensions.ContainsKey(removed));
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized, PvadApiProblems.AuthenticationRequired)]
    [InlineData(StatusCodes.Status403Forbidden, PvadApiProblems.PermissionDenied)]
    [InlineData(StatusCodes.Status404NotFound, PvadApiProblems.ResourceNotFound)]
    [InlineData(StatusCodes.Status409Conflict, PvadApiProblems.Conflict)]
    [InlineData(StatusCodes.Status423Locked, PvadApiProblems.AccountLocked)]
    [InlineData(StatusCodes.Status429TooManyRequests, PvadApiProblems.RateLimited)]
    [InlineData(StatusCodes.Status400BadRequest, PvadApiProblems.InvalidRequest)]
    [InlineData(StatusCodes.Status500InternalServerError, PvadApiProblems.UnexpectedError)]
    public void ResolveCode_maps_status_codes_to_stable_codes(int status, string expectedCode)
    {
        Assert.Equal(expectedCode, PvadApiProblems.ResolveCode(status));
    }

    [Fact]
    public void ResolveCode_recognizes_the_email_verification_title_on_a_403()
    {
        Assert.Equal(
            PvadApiProblems.VerificationRequired,
            PvadApiProblems.ResolveCode(StatusCodes.Status403Forbidden, "Email verification required"));
    }

    [Fact]
    public void Create_stamps_type_instance_and_trace_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/pvad/v1/account/profile";
        context.TraceIdentifier = "trace-123";

        var problem = PvadApiProblems.Create(context, StatusCodes.Status423Locked);

        Assert.Equal(PvadApiProblems.AccountLocked, problem.Extensions["code"]);
        Assert.Equal("trace-123", problem.Extensions["traceId"]);
        Assert.Equal("/api/pvad/v1/account/profile", problem.Instance);
        Assert.Equal(
            "https://docs.pvade.com/problems/pvadapi009",
            problem.Type);
    }

    [Fact]
    public void Create_preserves_an_explicit_code_override()
    {
        var context = new DefaultHttpContext();

        var problem = PvadApiProblems.Create(
            context,
            StatusCodes.Status409Conflict,
            code: PvadApiProblems.LastOwnerProtected);

        Assert.Equal(PvadApiProblems.LastOwnerProtected, problem.Extensions["code"]);
    }

    [Fact]
    public void All_new_account_codes_are_distinct()
    {
        string[] codes =
        [
            PvadApiProblems.AccountLocked,
            PvadApiProblems.VerificationRequired,
            PvadApiProblems.TrialAlreadyClaimed,
            PvadApiProblems.OrganizationSlugUnavailable,
            PvadApiProblems.LastOwnerProtected,
            PvadApiProblems.ClosureConflict,
        ];

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("/api/pvad/v1/account/profile")]
    [InlineData("/API/PVAD/templates/render-design")]
    [InlineData("/api/templates/render-design")]
    public void Problem_types_are_canonical_for_every_route(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var problem = PvadApiProblems.Create(context, 409);
        Assert.Equal("https://docs.pvade.com/problems/pvadapi006", problem.Type);
        Assert.Equal("PVADAPI006", problem.Extensions["code"]);
        PvadApiProblems.Complete(context, problem, 409);
        Assert.Equal("https://docs.pvade.com/problems/pvadapi006", problem.Type);
    }

    [Fact]
    public void Template_conflict_type_uses_the_documentation_domain()
    {
        Assert.Equal("https://docs.pvade.com/problems/designer-template-conflict", PvadApiProblems.TemplateConflictType);
    }

    [Fact]
    public void Completing_explicit_types_preserves_custom_semantics()
    {
        var context = new DefaultHttpContext();
        var problem = PvadApiProblems.Create(context, 403);
        problem.Type = "https://customer.example.test/problems/custom";

        PvadApiProblems.Complete(context, problem, 403);

        Assert.Equal("https://customer.example.test/problems/custom", problem.Type);
        Assert.Equal("PVADAPI002", problem.Extensions["code"]);
    }
}
