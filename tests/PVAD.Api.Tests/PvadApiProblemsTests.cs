using Microsoft.AspNetCore.Http;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadApiProblemsTests
{
    [Fact]
    public void AuditEditionDenial_PreservesGenericCodeAndLegacyTitle()
    {
        var problem = PvadApiProblems.Create(new DefaultHttpContext(), 403,
            title: "PXA_AUDIT_EXPORT_REQUIRES_ENTERPRISE");
        Assert.Equal("PXA_AUDIT_EXPORT_REQUIRES_ENTERPRISE", problem.Title);
        Assert.Equal("PVAD_AUDIT_EXPORT_REQUIRES_ENTERPRISE", problem.Extensions["canonicalTitle"]);
        Assert.Equal("PXAAPI002", problem.Extensions["code"]);
        Assert.Equal("PVADAPI002", problem.Extensions["canonicalCode"]);
        var originalType = problem.Type;
        PvadApiProblems.Complete(new DefaultHttpContext(), problem, 403);
        Assert.Equal(originalType, problem.Type);
        problem.Title = "Unmapped title";
        PvadApiProblems.Complete(new DefaultHttpContext(), problem, 403);
        Assert.False(problem.Extensions.ContainsKey("canonicalTitle"));
        Assert.Equal("PXAAPI002", problem.Extensions["code"]);
    }

    [Theory]
    [InlineData("PXAPDFPIPELINE100")]
    [InlineData("PXAPDFPIPELINE107")]
    [InlineData("PXAPDFPIPELINE200")]
    [InlineData("PXAPDFPIPELINE204")]
    [InlineData("PXALAYOUT001")]
    [InlineData("PXALAYOUT006")]
    [InlineData("PXAEINVATTACH000")]
    [InlineData("PXAEINVATTACH016")]
    public void PipelineLayoutAndAttachmentAliases_PreserveTheAuthoritativeCode(string code)
    {
        var problem = PvadApiProblems.Create(new DefaultHttpContext(), 422, code: code);
        Assert.Equal(422, problem.Status);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.Equal("PVAD" + code[3..], problem.Extensions["canonicalCode"]);
    }

    [Fact]
    public void RedactionApiAliases_PreserveAllTwelveLegacyDecisions()
    {
        for (var number = 1; number <= 12; number++)
        {
            var legacy = $"PXAPDFREDACTAPI{number:000}";
            var problem = PvadApiProblems.Create(new DefaultHttpContext(), 400, code: legacy);
            Assert.Equal(legacy, problem.Extensions["code"]);
            Assert.Equal($"PVADPDFREDACTAPI{number:000}", problem.Extensions["canonicalCode"]);
        }
    }

    [Theory]
    [InlineData("PXAPDFTRUSTAPI001", "PVADPDFTRUSTAPI001")]
    [InlineData("PXAPDFTRUSTAPI002", "PVADPDFTRUSTAPI002")]
    [InlineData("PXAPDFTRUSTAPI003", "PVADPDFTRUSTAPI003")]
    [InlineData("PXAPDFTRUSTAPI901", "PVADPDFTRUSTAPI901")]
    [InlineData("PXAPDFACONVERTAPI001", "PVADPDFACONVERTAPI001")]
    [InlineData("PXAPDFACONVERTAPI002", "PVADPDFACONVERTAPI002")]
    [InlineData("PXAPDFACONVERTAPI003", "PVADPDFACONVERTAPI003")]
    [InlineData("PXAPDFACONVERTAPI004", "PVADPDFACONVERTAPI004")]
    [InlineData("PXAPDFACONVERTAPI005", "PVADPDFACONVERTAPI005")]
    [InlineData("PXAPDFACONVERTAPI006", "PVADPDFACONVERTAPI006")]
    public void PdfTrustDiagnosticAliases_PreserveCodeAndNumericMeaning(string legacy, string canonical)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails();
        problem.Extensions["code"] = legacy;
        PvadApiProblems.Complete(new DefaultHttpContext(), problem, 400);
        Assert.Equal(legacy, problem.Extensions["code"]);
        Assert.Equal(canonical, problem.Extensions["canonicalCode"]);
    }

    [Fact]
    public void OwnedDiagnosticFamily_HasUniqueCanonicalAliasesWithoutChangingLegacyCodes()
    {
        for (var number = 1; number <= 18; number++)
        {
            var legacy = $"PXAAPI{number:000}";
            var canonical = $"PVADAPI{number:000}";
            var context = new DefaultHttpContext();
            var problem = PvadApiProblems.Create(context, 400, code: legacy);
            Assert.Equal(legacy, problem.Extensions["code"]);
            Assert.Equal(canonical, problem.Extensions["canonicalCode"]);
            problem.Extensions.Remove("canonicalCode");
            PvadApiProblems.Complete(context, problem, 400);
            Assert.Equal(canonical, problem.Extensions["canonicalCode"]);
            Assert.Equal(legacy, problem.Extensions["code"]);
        }
    }

    [Theory]
    [InlineData("PXAAPI999")]
    [InlineData("CANMIGDEVEXP001")]
    [InlineData("PXA_LICENSE_FUTURE")]
    [InlineData("PXAPDFREDACTAPI999")]
    [InlineData("PXAPDFPARSE999")]
    [InlineData("PXAPDFUA999")]
    [InlineData("PXAPDFPIPELINE999")]
    [InlineData("PXALAYOUT999")]
    [InlineData("PXAEINVATTACH006")]
    public void UnmappedDiagnosticFamilies_AreNotRenamed(string code)
    {
        var problem = PvadApiProblems.Create(new DefaultHttpContext(), 400, code: code);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.False(problem.Extensions.ContainsKey("canonicalCode"));
        problem.Extensions["canonicalCode"] = "PVADAPI005";
        PvadApiProblems.Complete(new DefaultHttpContext(), problem, 400);
        Assert.False(problem.Extensions.ContainsKey("canonicalCode"));
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
    [InlineData("/api/pvad/v1/account/profile", false)]
    [InlineData("/API/PVAD/templates/render-design", false)]
    [InlineData("/api/pxa/v1/account/profile", true)]
    [InlineData("/api/templates/render-design", true)]
    public void Problem_types_use_route_identity_without_changing_codes(string path, bool legacy)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var problem = PvadApiProblems.Create(context, 409);
        const string canonicalType = "https://docs.pvade.com/problems/pvadapi006";
        const string legacyType = "https://docs.powerdoxautomation.com/problems/pxaapi006";

        Assert.Equal(legacy ? legacyType : canonicalType, problem.Type);
        Assert.Equal(canonicalType, problem.Extensions["canonicalType"]);
        Assert.Equal(legacyType, problem.Extensions["legacyType"]);
        Assert.Equal("PXAAPI006", problem.Extensions["code"]);
        Assert.Equal("PVADAPI006", problem.Extensions["canonicalCode"]);
        PvadApiProblems.Complete(context, problem, 409);
        Assert.Equal(legacy ? legacyType : canonicalType, problem.Type);
    }

    [Theory]
    [InlineData("/api/pvad/v1/designer/templates", false)]
    [InlineData("/api/pxa/v1/designer/templates", true)]
    public void Template_conflict_type_preserves_the_original_legacy_host(string path, bool legacy)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var identity = PvadApiProblems.TemplateConflictTypeIdentity(context);

        Assert.Equal("https://docs.pvade.com/problems/designer-template-conflict", identity.CanonicalType);
        Assert.Equal("https://powerdoxautomation.com/problems/designer-template-conflict", identity.LegacyType);
        Assert.Equal(legacy ? identity.LegacyType : identity.CanonicalType, identity.Type);
    }

    [Fact]
    public void Completing_explicit_types_preserves_custom_semantics_and_clears_stale_aliases()
    {
        var context = new DefaultHttpContext();
        var problem = PvadApiProblems.Create(context, 403);
        problem.Type = "https://customer.example.test/problems/custom";

        PvadApiProblems.Complete(context, problem, 403);

        Assert.Equal("https://customer.example.test/problems/custom", problem.Type);
        Assert.False(problem.Extensions.ContainsKey("canonicalType"));
        Assert.False(problem.Extensions.ContainsKey("legacyType"));
        Assert.Equal("PXAAPI002", problem.Extensions["code"]);
    }

    [Fact]
    public void Completing_explicit_legacy_type_keeps_it_and_adds_the_canonical_reference()
    {
        var context = new DefaultHttpContext();
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "https://docs.powerdoxautomation.com/problems/pxaapi006",
            Status = 409,
        };

        PvadApiProblems.Complete(context, problem, 409);

        Assert.Equal("https://docs.powerdoxautomation.com/problems/pxaapi006", problem.Type);
        Assert.Equal("https://docs.pvade.com/problems/pvadapi006", problem.Extensions["canonicalType"]);
    }
}
