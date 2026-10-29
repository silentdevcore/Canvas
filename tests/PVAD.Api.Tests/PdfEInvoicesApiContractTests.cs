using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Infrastructure.OpenApi;
using PVAD.WebApi.Security;

namespace PVAD.Api.Tests;

/// <summary>
/// The electronic-invoice API contract: create, attach, extract, inspect, generate and validate
/// are authenticated, entitled, rate limited, bounded, and documented the same way.
/// </summary>
public sealed class PdfEInvoicesApiContractTests
{
    private const string Prefix = "/api/pvad/v1/pdf/e-invoices";

    /// <summary>
    /// The document operations, with the permission each one requires. The validation-evidence
    /// endpoints are covered by <see cref="PdfEInvoiceValidationEvidenceTests"/>.
    /// </summary>
    public static TheoryData<string, string> Operations() => new()
    {
        { $"{Prefix}/generate", PvadDesignerPermissions.PdfEInvoicesGenerate },
        { $"{Prefix}/create", PvadDesignerPermissions.PdfEInvoicesCreate },
        { $"{Prefix}/attach", PvadDesignerPermissions.PdfEInvoicesAttach },
        { $"{Prefix}/attach/preflight", PvadDesignerPermissions.PdfEInvoicesAttach },
        { $"{Prefix}/validate", PvadDesignerPermissions.PdfEInvoicesValidate },
        { $"{Prefix}/inspect", PvadDesignerPermissions.PdfEInvoicesRead },
        { $"{Prefix}/extract", PvadDesignerPermissions.PdfEInvoicesRead }
    };

    [Fact]
    public void Every_operation_declares_exactly_one_registered_permission_policy()
    {
        foreach (var action in ActionMethods())
        {
            var policies = action.GetCustomAttributes<AuthorizeAttribute>(true)
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .ToArray();
            Assert.True(
                policies.Length == 1 && PvadDesignerPermissions.All.Contains(policies[0]!),
                $"{action.Name} must declare exactly one registered Designer permission policy.");
        }
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void Route_resolves_to_the_permission_its_action_declares(string route, string permission)
    {
        Assert.Equal(permission, PvadDesignerPermissionResolver.Resolve(new PathString(route), "POST"));
        Assert.Equal(permission, DeclaredPolicy(route));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public void Every_operation_requires_the_generator_product_entitlement(string route, string permission)
    {
        Assert.Equal("generator", PvadProductAccessMiddleware.ResolveCapability(route));
        Assert.DoesNotContain("generator", PvadDesignerPermissions.All);
        Assert.Contains(permission, PvadDesignerPermissions.All);
    }

    /// <summary>
    /// Every operation that accepts a body has to bound it. A read-only GET has no body to
    /// bound, so requiring a limit there would assert something meaningless.
    /// </summary>
    [Fact]
    public void Every_operation_that_accepts_a_body_declares_a_bounded_request_size()
    {
        foreach (var action in ActionMethods())
        {
            var methods = action.GetCustomAttributes<HttpMethodAttribute>(true)
                .SelectMany(attribute => attribute.HttpMethods)
                .ToArray();
            var limit = action.GetCustomAttribute<RequestSizeLimitAttribute>();
            if (methods.Contains("POST", StringComparer.OrdinalIgnoreCase))
            {
                Assert.True(limit is not null, $"{action.Name} accepts a body and must bound it.");
            }
            else
            {
                Assert.True(
                    methods.All(method => string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)),
                    $"{action.Name} must be a POST or a GET.");
                Assert.Null(limit);
            }
        }
    }

    [Fact]
    public void The_controller_is_rate_limited()
    {
        var policy = typeof(PdfEInvoicesController).GetCustomAttribute<EnableRateLimitingAttribute>();
        Assert.Equal("designer-code", policy?.PolicyName);
    }

    [Fact]
    public void Every_operation_declares_its_problem_details_responses()
    {
        foreach (var action in ActionMethods())
        {
            var responses = action.GetCustomAttributes<ProducesResponseTypeAttribute>(true).ToArray();
            Assert.Contains(responses, response =>
                response.StatusCode == StatusCodes.Status400BadRequest &&
                response.Type == typeof(ProblemDetails));
            Assert.Contains(responses, response => response.StatusCode == StatusCodes.Status200OK);
        }
    }

    [Theory]
    [InlineData(PvadRoles.SystemAdministrator, true, true, true, true)]
    [InlineData(PvadRoles.OrganizationAdministrator, true, true, true, true)]
    [InlineData(PvadRoles.Manager, true, true, true, true)]
    [InlineData(PvadRoles.Editor, true, true, true, true)]
    // A Viewer may read and validate, but must never write a document.
    [InlineData(PvadRoles.Viewer, true, true, false, false)]
    public void Role_permissions_separate_reading_from_writing(
        string role,
        bool read,
        bool validate,
        bool create,
        bool attach)
    {
        var permissions = PvadRoles.Permissions[role];
        Assert.Equal(read, permissions.Contains(PvadDesignerPermissions.PdfEInvoicesRead));
        Assert.Equal(validate, permissions.Contains(PvadDesignerPermissions.PdfEInvoicesValidate));
        Assert.Equal(create, permissions.Contains(PvadDesignerPermissions.PdfEInvoicesCreate));
        Assert.Equal(attach, permissions.Contains(PvadDesignerPermissions.PdfEInvoicesAttach));
    }

    [Fact]
    public void Attaching_requires_its_own_permission_and_is_never_implied_by_reading()
    {
        Assert.NotEqual(PvadDesignerPermissions.PdfEInvoicesRead, PvadDesignerPermissions.PdfEInvoicesAttach);
        Assert.Contains(PvadDesignerPermissions.PdfEInvoicesAttach, PvadDesignerPermissions.All);
        Assert.DoesNotContain(
            PvadDesignerPermissions.PdfEInvoicesAttach,
            PvadRoles.Permissions[PvadRoles.Viewer]);
    }

    [Fact]
    public async Task OpenApi_documents_every_operation_and_separates_the_three_validation_states()
    {
        var document = await TransformAsync();

        foreach (var (route, _) in Operations().Select(row => ((string)row[0]!, (string)row[1]!)))
        {
            var operation = Assert.Single(document.Paths[route].Operations!.Values);
            Assert.False(string.IsNullOrWhiteSpace(operation.Summary));
            Assert.Contains("isStructurallyValid", operation.Description!, StringComparison.Ordinal);
            Assert.Contains("isConformanceValidated", operation.Description!, StringComparison.Ordinal);
            Assert.Contains("validatorArtifactSetId", operation.Description!, StringComparison.Ordinal);
            Assert.Contains(
                "never reported as conformant on the strength of structural validation alone",
                operation.Description!,
                StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("/generate")]
    [InlineData("/create")]
    public async Task OpenApi_carries_an_example_for_each_maintained_standard(string suffix)
    {
        var document = await TransformAsync();

        var media = Assert.Single(document.Paths[Prefix + suffix].Operations!.Values)
            .RequestBody!.Content!["application/json"];
        Assert.Equal(
            ["facturX", "xrechnung", "zugferd"],
            media.Examples!.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("FacturX", Value(media.Examples!["facturX"], "standard"));
        Assert.Equal("1.09.2", Value(media.Examples!["facturX"], "version"));
        Assert.Equal("ZUGFeRD", Value(media.Examples!["zugferd"], "standard"));
        Assert.Equal("2.5.2", Value(media.Examples!["zugferd"], "version"));
        Assert.Equal("XRechnung", Value(media.Examples!["xrechnung"], "standard"));
        Assert.Equal("3.0.2", Value(media.Examples!["xrechnung"], "version"));
    }

    [Fact]
    public async Task OpenApi_leaves_unrelated_paths_untouched()
    {
        var document = await TransformAsync();

        Assert.Null(document.Paths["/api/pvad/v1/pdf/compliance/convert"].Operations!.Values.Single().Summary);
    }

    private static string? Value(IOpenApiExample example, string property) =>
        (example as OpenApiExample)?.Value?[property]?.GetValue<string>();

    private static async Task<OpenApiDocument> TransformAsync()
    {
        var document = new OpenApiDocument { Paths = [] };
        foreach (var route in Operations()
                     .Select(row => (string)row[0]!)
                     .Append("/api/pvad/v1/pdf/compliance/convert"))
        {
            document.Paths[route] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Post] = new()
                    {
                        RequestBody = new OpenApiRequestBody
                        {
                            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
                            {
                                ["application/json"] = new()
                            }
                        }
                    }
                }
            };
        }

        await new PvadEInvoiceOpenApiTransformer().TransformAsync(document, null!, CancellationToken.None);
        return document;
    }

    private static IEnumerable<MethodInfo> ActionMethods() =>
        typeof(PdfEInvoicesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(true).Any());

    private static string? DeclaredPolicy(string route)
    {
        var suffix = route[(Prefix.Length + 1)..];
        var action = ActionMethods().Single(method => method
            .GetCustomAttributes<HttpMethodAttribute>(true)
            .Any(attribute => string.Equals(attribute.Template, suffix, StringComparison.Ordinal)));
        return action.GetCustomAttribute<AuthorizeAttribute>()?.Policy;
    }
}
