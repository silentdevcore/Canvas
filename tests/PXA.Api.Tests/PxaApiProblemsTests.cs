using Microsoft.AspNetCore.Http;
using PXA.WebApi.Infrastructure;

namespace PXA.Api.Tests;

public sealed class PxaApiProblemsTests
{
    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized, PxaApiProblems.AuthenticationRequired)]
    [InlineData(StatusCodes.Status403Forbidden, PxaApiProblems.PermissionDenied)]
    [InlineData(StatusCodes.Status404NotFound, PxaApiProblems.ResourceNotFound)]
    [InlineData(StatusCodes.Status409Conflict, PxaApiProblems.Conflict)]
    [InlineData(StatusCodes.Status423Locked, PxaApiProblems.AccountLocked)]
    [InlineData(StatusCodes.Status429TooManyRequests, PxaApiProblems.RateLimited)]
    [InlineData(StatusCodes.Status400BadRequest, PxaApiProblems.InvalidRequest)]
    public void ResolveCode_maps_status_codes_to_stable_codes(int status, string expectedCode)
    {
        Assert.Equal(expectedCode, PxaApiProblems.ResolveCode(status));
    }

    [Fact]
    public void Create_stamps_type_instance_and_trace_id()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/pxa/v1/account/profile";
        context.TraceIdentifier = "trace-123";

        var problem = PxaApiProblems.Create(context, StatusCodes.Status423Locked);

        Assert.Equal(PxaApiProblems.AccountLocked, problem.Extensions["code"]);
        Assert.Equal("trace-123", problem.Extensions["traceId"]);
        Assert.Equal("/api/pxa/v1/account/profile", problem.Instance);
        Assert.Equal(
            $"https://docs.powerdoxautomation.com/problems/{PxaApiProblems.AccountLocked.ToLowerInvariant()}",
            problem.Type);
    }

    [Fact]
    public void Create_preserves_an_explicit_code_override()
    {
        var context = new DefaultHttpContext();

        var problem = PxaApiProblems.Create(
            context,
            StatusCodes.Status409Conflict,
            code: PxaApiProblems.LastOwnerProtected);

        Assert.Equal(PxaApiProblems.LastOwnerProtected, problem.Extensions["code"]);
    }

    [Fact]
    public void All_new_account_codes_are_distinct()
    {
        string[] codes =
        [
            PxaApiProblems.AccountLocked,
            PxaApiProblems.VerificationRequired,
            PxaApiProblems.TrialAlreadyClaimed,
            PxaApiProblems.OrganizationSlugUnavailable,
            PxaApiProblems.LastOwnerProtected,
            PxaApiProblems.ClosureConflict,
        ];

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }
}
