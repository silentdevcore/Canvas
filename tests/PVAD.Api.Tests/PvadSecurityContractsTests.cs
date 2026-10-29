using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using PVAD.Migration.Abstractions;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services;

namespace PVAD.Api.Tests;

public sealed class PvadSecurityContractsTests
{
    [Fact]
    public void Nutrient_viewer_migration_requires_domain_provider_and_rate_limit_policies()
    {
        var controllerType = typeof(NutrientViewerMigrationController);
        var policies = controllerType.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();
        var rateLimit = controllerType.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.Contains(PvadDesignerPermissions.MigrationPdfExecute, policies);
        Assert.Contains("designer.migration.pdf.provider.nutrient-viewer.execute", policies);
        Assert.NotNull(rateLimit);
        Assert.Equal("designer-code", rateLimit.PolicyName);
    }

    [Fact]
    public void Nutrient_code_example_requires_domain_provider_and_rate_limit_policies()
    {
        var controllerType = typeof(NutrientCodeMigrationController);
        var policies = controllerType.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();

        Assert.Contains(PvadDesignerPermissions.MigrationPdfExecute, policies);
        Assert.Contains("designer.migration.pdf.provider.nutrient.execute", policies);
        Assert.Equal("designer-code", controllerType.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public void Tenant_context_resolves_only_valid_authenticated_identifiers()
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(PvadClaimTypes.ActiveOrganization, organizationId.ToString()),
                ], "test")),
            },
        };

        var context = new PvadTenantContext(accessor);

        Assert.Equal(userId, context.UserId);
        Assert.Equal(organizationId, context.OrganizationId);

        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "not-a-user-id"),
            new Claim(PvadClaimTypes.ActiveOrganization, "not-an-organization-id"),
        ], "test"));
        Assert.Null(context.UserId);
        Assert.Null(context.OrganizationId);
    }

    [Fact]
    public void Built_in_roles_use_only_registered_permissions_and_keep_commercial_access_separate()
    {
        var registeredPermissions = PvadPermissions.All
            .Concat(PvadAccountPermissions.All)
            .Concat(PvadDesignerPermissions.All)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var permission in PvadRoles.Permissions.SelectMany(value => value.Value))
            Assert.Contains(permission, registeredPermissions);

        Assert.Equal(
            PvadPermissions.All.Concat(PvadDesignerPermissions.All)
                .ToHashSet(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
            PvadRoles.Permissions[PvadRoles.SystemAdministrator].Order(StringComparer.Ordinal));
        Assert.Contains(PvadPermissions.SubscriptionsRead,
            PvadRoles.Permissions[PvadRoles.OrganizationAdministrator]);
        Assert.DoesNotContain(PvadPermissions.SubscriptionsManage,
            PvadRoles.Permissions[PvadRoles.OrganizationAdministrator]);

        // Editor/Viewer are customer-facing member roles: no Admin-app permissions,
        // but they do get the self-scoped Account permissions every member needs.
        Assert.DoesNotContain(PvadRoles.Permissions[PvadRoles.Editor], PvadPermissions.All.Contains);
        Assert.DoesNotContain(PvadRoles.Permissions[PvadRoles.Viewer], PvadPermissions.All.Contains);
        Assert.Contains(PvadAccountPermissions.ProfileManage, PvadRoles.Permissions[PvadRoles.Editor]);
        Assert.Contains(PvadAccountPermissions.ProfileManage, PvadRoles.Permissions[PvadRoles.Viewer]);
        Assert.DoesNotContain(PvadAccountPermissions.MembersInvite, PvadRoles.Permissions[PvadRoles.Editor]);
        Assert.DoesNotContain(PvadAccountPermissions.MembersInvite, PvadRoles.Permissions[PvadRoles.Viewer]);
    }

    [Fact]
    public void Designer_permissions_are_unique_registered_and_separated_by_role()
    {
        Assert.Equal(
            PvadDesignerPermissions.All.Count,
            PvadDesignerPermissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PvadDesignerPermissions.All,
            permission => Assert.StartsWith("designer.", permission, StringComparison.Ordinal));

        Assert.All(PvadDesignerPermissions.All,
            permission => Assert.Contains(permission, PvadRoles.Permissions[PvadRoles.SystemAdministrator]));
        Assert.All(PvadDesignerPermissions.All,
            permission => Assert.Contains(permission, PvadRoles.Permissions[PvadRoles.OrganizationAdministrator]));

        var manager = PvadRoles.Permissions[PvadRoles.Manager];
        Assert.Contains(PvadDesignerPermissions.TemplatesPublish, manager);
        Assert.Contains(PvadDesignerPermissions.PdfViewerRedact, manager);
        Assert.Contains(PvadDesignerPermissions.PdfCreate, manager);
        Assert.Contains(PvadDesignerPermissions.MigrationPdfExecute, manager);
        Assert.All(PvadDesignerMigrationPermissions.All, permission => Assert.Contains(permission, manager));
        Assert.DoesNotContain(PvadDesignerPermissions.FeaturesManage, manager);

        var editor = PvadRoles.Permissions[PvadRoles.Editor];
        Assert.Contains(PvadDesignerPermissions.TemplatesEdit, editor);
        Assert.Contains(PvadDesignerPermissions.PdfEdit, editor);
        Assert.Contains(PvadDesignerPermissions.SpreadsheetCreate, editor);
        Assert.Contains(PvadDesignerPermissions.CodeExecute, editor);
        Assert.Contains(PvadDesignerPermissions.MigrationExecute, editor);
        Assert.All(PvadDesignerMigrationPermissions.All, permission => Assert.Contains(permission, editor));
        Assert.Contains(PvadDesignerPermissions.JobsCancel, editor);
        Assert.DoesNotContain(PvadDesignerPermissions.TemplatesPublish, editor);
        Assert.DoesNotContain(PvadDesignerPermissions.TemplatesArchive, editor);
        Assert.DoesNotContain(PvadDesignerPermissions.PdfViewerRedact, editor);

        var viewer = PvadRoles.Permissions[PvadRoles.Viewer];
        Assert.Contains(PvadDesignerPermissions.TemplatesRead, viewer);
        Assert.Contains(PvadDesignerPermissions.PdfAccess, viewer);
        Assert.Contains(PvadDesignerPermissions.PdfTemplatesRead, viewer);
        Assert.Contains(PvadDesignerPermissions.Export, viewer);
        Assert.Contains(PvadDesignerPermissions.PdfViewerRead, viewer);
        Assert.Contains(PvadDesignerPermissions.JobsRead, viewer);
        Assert.DoesNotContain(PvadDesignerPermissions.TemplatesEdit, viewer);
        Assert.DoesNotContain(PvadDesignerPermissions.PdfCreate, viewer);
        Assert.DoesNotContain(PvadDesignerPermissions.PdfEdit, viewer);
        Assert.DoesNotContain(PvadDesignerPermissions.Import, viewer);
        Assert.DoesNotContain(PvadDesignerPermissions.MigrationExecute, viewer);
        Assert.All(PvadDesignerMigrationPermissions.All, permission => Assert.DoesNotContain(permission, viewer));
        Assert.DoesNotContain(PvadDesignerPermissions.PdfViewerAnnotate, viewer);
        Assert.DoesNotContain(PvadDesignerPermissions.JobsCancel, viewer);
    }

    [Fact]
    public void Migration_permissions_cover_every_supported_provider_by_domain()
    {
        Assert.Equal(16, PvadDesignerMigrationPermissions.PdfProviders.Values.Distinct().Count());
        Assert.Equal(8, PvadDesignerMigrationPermissions.SpreadsheetProviders.Values.Distinct().Count());
        Assert.Equal(8, PvadDesignerMigrationPermissions.ReportProviders.Values.Distinct().Count());
        Assert.Single(PvadDesignerMigrationPermissions.ViewerProviders.Values.Distinct());
        Assert.Equal(33, PvadDesignerMigrationPermissions.LegacyAll.Count);
        var expectedCanonical = MigrationProviderCatalog.All
            .Select(static descriptor => descriptor.PermissionKey)
            .Concat(MigrationProviderCatalog.All.SelectMany(static descriptor => new[]
            {
                PvadDesignerMigrationPermissions.KindPermission(descriptor.Domain, descriptor.MigrationKind, "read"),
                PvadDesignerMigrationPermissions.KindPermission(descriptor.Domain, descriptor.MigrationKind, "execute")
            }))
            .Distinct(StringComparer.Ordinal)
            .Count();
        Assert.Equal(expectedCanonical, PvadDesignerMigrationPermissions.CanonicalAll.Count);
        Assert.Equal(PvadDesignerMigrationPermissions.LegacyAll
            .Concat(PvadDesignerMigrationPermissions.CanonicalAll)
            .Distinct(StringComparer.Ordinal)
            .Count(), PvadDesignerMigrationPermissions.All.Count);
        Assert.Contains("designer.migration.report.designer.provider.auto.execute",
            PvadDesignerMigrationPermissions.CanonicalAll);
        Assert.Contains("designer.migration.pdf.designer.provider.nutrient.execute",
            PvadDesignerMigrationPermissions.CanonicalAll);
        Assert.All(PvadDesignerMigrationPermissions.PdfProviders.Values,
            permission => Assert.StartsWith("designer.migration.pdf.provider.", permission, StringComparison.Ordinal));
        Assert.All(PvadDesignerMigrationPermissions.SpreadsheetProviders.Values,
            permission => Assert.StartsWith("designer.migration.spreadsheet.provider.", permission, StringComparison.Ordinal));
        Assert.All(PvadDesignerMigrationPermissions.ReportProviders.Values,
            permission => Assert.StartsWith("designer.migration.report.provider.", permission, StringComparison.Ordinal));
        Assert.All(PvadDesignerMigrationPermissions.ViewerProviders.Values,
            permission => Assert.StartsWith("designer.migration.pdf.provider.nutrient-viewer", permission, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/api/export/pdf", "POST", PvadDesignerPermissions.PdfExport)]
    [InlineData("/api/document/import-pdf", "POST", PvadDesignerPermissions.PdfImport)]
    [InlineData("/api/document/convert-image-to-pdf", "POST", PvadDesignerPermissions.PdfConvert)]
    [InlineData("/api/document/validate", "POST", PvadDesignerPermissions.PdfEdit)]
    [InlineData("/api/migration/frameworks", "GET", PvadDesignerPermissions.Access)]
    [InlineData("/api/migration/convert", "POST", PvadDesignerPermissions.Access)]
    [InlineData("/api/spreadsheet/import", "POST", PvadDesignerPermissions.SpreadsheetImport)]
    [InlineData("/api/spreadsheet/export", "POST", PvadDesignerPermissions.SpreadsheetExport)]
    [InlineData("/api/spreadsheet/validate", "POST", PvadDesignerPermissions.SpreadsheetRead)]
    [InlineData("/api/pdf-viewer/document", "GET", PvadDesignerPermissions.PdfViewerRead)]
    [InlineData("/api/pdf-viewer/forms/fill", "POST", PvadDesignerPermissions.PdfViewerForms)]
    [InlineData("/api/pdf-viewer/redact", "POST", PvadDesignerPermissions.PdfViewerRedact)]
    [InlineData("/api/pdf-viewer/annotations", "POST", PvadDesignerPermissions.PdfViewerAnnotate)]
    [InlineData("/api/templates", "GET", PvadDesignerPermissions.PdfTemplatesRead)]
    [InlineData("/api/templates", "POST", PvadDesignerPermissions.PdfCreate)]
    [InlineData("/api/templates/csharp/execute", "POST", PvadDesignerPermissions.CodeExecute)]
    [InlineData("/api/templates/render", "POST", PvadDesignerPermissions.PdfExport)]
    [InlineData("/api/pvad/v1/document-jobs/import", "POST", PvadDesignerPermissions.PdfImport)]
    [InlineData("/api/pvad/v1/document-jobs/export", "POST", PvadDesignerPermissions.PdfExport)]
    [InlineData("/api/pvad/v1/document-jobs/code-migration", "POST", PvadDesignerPermissions.MigrationExecute)]
    [InlineData("/api/pvad/v1/migrations/catalog", "GET", PvadDesignerPermissions.MigrationRead)]
    [InlineData("/api/pvad/v1/migrations/pdf/viewer/convert", "POST", PvadDesignerPermissions.MigrationPdfExecute)]
    [InlineData("/api/pvad/v1/jobs/58b50226-34ea-480c-936a-837b03445006", "GET", PvadDesignerPermissions.JobsRead)]
    [InlineData("/api/pvad/v1/jobs/58b50226-34ea-480c-936a-837b03445006/cancel", "POST", PvadDesignerPermissions.JobsCancel)]
    [InlineData("/api/pvad/v1/designer/projects/pdf/export", "POST", PvadDesignerPermissions.PdfExport)]
    [InlineData("/api/pvad/v1/designer/projects/pdf/open", "POST", PvadDesignerPermissions.PdfImport)]
    [InlineData("/api/pvad/v1/designer/projects/spreadsheet/export", "POST", PvadDesignerPermissions.SpreadsheetExport)]
    [InlineData("/api/pvad/v1/designer/projects/spreadsheet/open", "POST", PvadDesignerPermissions.SpreadsheetImport)]
    [InlineData("/api/pvad/v1/designer/projects/pdf/validate", "POST", PvadDesignerPermissions.PdfTemplatesRead)]
    [InlineData("/api/pvad/v1/designer/projects/spreadsheet/validate", "POST", PvadDesignerPermissions.SpreadsheetRead)]
    [InlineData("/api/pvad/v1/designer/projects/pdf/inspect", "POST", PvadDesignerPermissions.PdfTemplatesRead)]
    [InlineData("/api/pvad/v1/designer/projects/spreadsheet/inspect", "POST", PvadDesignerPermissions.SpreadsheetRead)]
    [InlineData("/api/pvad/v1/pdf/trust/inspect", "POST", PvadDesignerPermissions.PdfTrustInspect)]
    [InlineData("/api/pvad/v1/pdf/compliance/validate", "POST", PvadDesignerPermissions.PdfComplianceValidate)]
    [InlineData("/api/pvad/v1/pdf/compliance/preflight", "POST", PvadDesignerPermissions.PdfComplianceValidate)]
    [InlineData("/api/pvad/v1/pdf/e-invoices/inspect", "POST", PvadDesignerPermissions.PdfEInvoicesRead)]
    [InlineData("/api/pvad/v1/pdf/e-invoices/extract", "POST", PvadDesignerPermissions.PdfEInvoicesRead)]
    [InlineData("/api/pvad/v1/pdf/e-invoices/validate", "POST", PvadDesignerPermissions.PdfEInvoicesValidate)]
    [InlineData("/api/pvad/v1/pdf/e-invoices/generate", "POST", PvadDesignerPermissions.PdfEInvoicesGenerate)]
    [InlineData("/api/pvad/v1/pdf/e-invoices/create", "POST", PvadDesignerPermissions.PdfEInvoicesCreate)]
    [InlineData("/api/pvad/v1/pdf/e-invoices/attach", "POST", PvadDesignerPermissions.PdfEInvoicesAttach)]
    [InlineData("/api/pvad/v1/pdf/e-invoices/attach/preflight", "POST", PvadDesignerPermissions.PdfEInvoicesAttach)]
    [InlineData("/api/pvad/v1/pdf/compliance/convert", "POST", PvadDesignerPermissions.PdfComplianceConvert)]
    [InlineData("/api/pvad/v1/pdf/trust/policies", "PUT", PvadDesignerPermissions.PdfSignatureTrustPoliciesManage)]
    [InlineData("/api/pvad/v1/pdf/trust/key-providers", "PUT", PvadDesignerPermissions.PdfSignatureKeyProvidersManage)]
    public void Designer_shared_api_routes_resolve_to_the_required_permission(
        string path,
        string method,
        string expected)
    {
        Assert.Equal(expected, PvadDesignerPermissionResolver.Resolve(new PathString(path), method));
    }

    [Fact]
    public void Dedicated_designer_controller_actions_declare_a_registered_permission_policy()
    {
        Type[] controllerTypes =
        [
            typeof(DesignerAssetsController),
            typeof(DesignerCodeWorkspacesController),
            typeof(DesignerElementFavoritesController),
            typeof(DesignerFeaturesController),
            typeof(DesignerNotificationsController),
            typeof(DesignerProjectsController),
            typeof(DesignerSymbolsController),
            typeof(DesignerTemplatesController),
        ];

        foreach (var method in controllerTypes.SelectMany(controller => controller
                     .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                     .Where(candidate => candidate.GetCustomAttributes<HttpMethodAttribute>(true).Any())))
        {
            var policies = method.GetCustomAttributes<AuthorizeAttribute>(true)
                .Select(attribute => attribute.Policy)
                .Where(policy => !string.IsNullOrWhiteSpace(policy))
                .ToArray();
            Assert.True(
                policies.Length == 1 && PvadDesignerPermissions.All.Contains(policies[0]!),
                $"{method.DeclaringType?.Name}.{method.Name} must declare exactly one registered Designer permission policy.");
        }
    }

    [Theory]
    [InlineData("Syncfusion", "pdf", "designer.migration.pdf.provider.syncfusion-pdf.execute")]
    [InlineData("syncfusion-pdf", "pdf", "designer.migration.pdf.provider.syncfusion-pdf.execute")]
    [InlineData("Nutrient", "pdf", "designer.migration.pdf.provider.nutrient.execute")]
    [InlineData("GdPicture.NET", "pdf", "designer.migration.pdf.provider.nutrient.execute")]
    [InlineData("AsposeCells", "spreadsheet", "designer.migration.spreadsheet.provider.aspose-cells.execute")]
    [InlineData("closedxml", "spreadsheet", "designer.migration.spreadsheet.provider.closedxml.execute")]
    public void Migration_framework_aliases_resolve_to_stable_provider_permissions(
        string framework,
        string domain,
        string expected)
    {
        Assert.Equal(expected, PvadDesignerMigrationPermissions.ResolveCodeProvider(framework, domain));
    }

    [Theory]
    [InlineData("JrxmlReport", "designer.migration.report.provider.jasperreports.execute")]
    [InlineData("rdl", "designer.migration.report.provider.rdl.execute")]
    [InlineData("DevExpressReport", "designer.migration.report.provider.devexpress-report.execute")]
    public void Report_migration_aliases_resolve_to_stable_provider_permissions(
        string provider,
        string expected)
    {
        Assert.Equal(expected, PvadDesignerMigrationPermissions.ResolveReportProvider(provider));
    }

    [Theory]
    [InlineData("nutrient-viewer")]
    [InlineData("NutrientViewer")]
    [InlineData("PSPDFKit")]
    public void Viewer_migration_aliases_resolve_to_stable_provider_permissions(string provider)
    {
        Assert.Equal(
            "designer.migration.pdf.provider.nutrient-viewer.execute",
            PvadDesignerMigrationPermissions.ResolveViewerProvider(provider));
    }

    [Fact]
    public void Designer_migration_requires_the_selected_provider_permission()
    {
        var controller = new MigrationController(new MigrationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(PvadClaimTypes.Permission, PvadDesignerPermissions.MigrationPdfExecute),
                    ], "test")),
                },
            },
        };
        controller.Request.Headers["X-PVAD-Application"] = "designer";

        var result = controller.Convert(new MigrationRequest("Syncfusion", "var document = new PdfDocument();"));

        var denied = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
        Assert.Contains("syncfusion-pdf", System.Text.Json.JsonSerializer.Serialize(denied.Value));
    }

    [Fact]
    public void Limited_designer_role_can_execute_only_its_selected_migration_providers()
    {
        var permissions = new[]
        {
            PvadDesignerPermissions.Access,
            PvadDesignerPermissions.MigrationPdfRead,
            PvadDesignerPermissions.MigrationPdfExecute,
            PvadDesignerPermissions.MigrationSpreadsheetRead,
            PvadDesignerPermissions.MigrationSpreadsheetExecute,
            PvadDesignerMigrationPermissions.ResolveCodeProvider("Syncfusion", "pdf")!,
            PvadDesignerMigrationPermissions.ResolveCodeProvider("AsposeCells", "spreadsheet")!,
        };
        var controller = new MigrationController(new MigrationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        permissions.Select(permission => new Claim(PvadClaimTypes.Permission, permission)),
                        "limited-role")),
                },
            },
        };
        controller.Request.Headers["X-PVAD-Application"] = "designer";

        Assert.NotEqual(StatusCodes.Status403Forbidden,
            (controller.Convert(new MigrationRequest("Syncfusion", "var document = new PdfDocument();")) as ObjectResult)?.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden,
            (controller.Convert(new MigrationRequest("AsposeCells", "var workbook = new Workbook();")) as ObjectResult)?.StatusCode);

        var blockedPdf = Assert.IsType<ObjectResult>(
            controller.Convert(new MigrationRequest("Aspose", "var document = new Document();")));
        Assert.Equal(StatusCodes.Status403Forbidden, blockedPdf.StatusCode);
        Assert.Contains("aspose-pdf", System.Text.Json.JsonSerializer.Serialize(blockedPdf.Value));

        var blockedSpreadsheet = Assert.IsType<ObjectResult>(
            controller.Convert(new MigrationRequest("ClosedXmlSpreadsheet", "var workbook = new XLWorkbook();")));
        Assert.Equal(StatusCodes.Status403Forbidden, blockedSpreadsheet.StatusCode);
        Assert.Contains("closedxml", System.Text.Json.JsonSerializer.Serialize(blockedSpreadsheet.Value));
    }

    [Fact]
    public void Migration_controller_returns_linked_pdf_trust_manual_actions()
    {
        var controller = new MigrationController(new MigrationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        var result = controller.Convert(new MigrationRequest(
            "iText7",
            "class Sample { void Run(dynamic pdf) { pdf.PdfSigner(); } }"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = System.Text.Json.JsonSerializer.SerializeToElement(ok.Value);
        var trust = body.GetProperty("pdfTrust");
        Assert.Equal("ReviewRequired", trust.GetProperty("overallDisposition").GetString());
        var action = Assert.Single(trust.GetProperty("manualActions").EnumerateArray());
        Assert.Equal("DigitalSignature", action.GetProperty("capability").GetString());
        Assert.Equal("CANMIGPDFTRUST100", action.GetProperty("actionId").GetString());
        Assert.StartsWith("/docs/", action.GetProperty("documentationPath").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Account_permissions_are_all_registered_and_mapped_to_at_least_one_role()
    {
        var mappedPermissions = PvadRoles.Permissions.SelectMany(value => value.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var permission in PvadAccountPermissions.All)
            Assert.Contains(permission, mappedPermissions);

        // The privileged System Administrator vocabulary and the customer Account
        // vocabulary must not overlap, so Admin authorization can never be satisfied
        // by an Account-only permission claim (checklist: keep customer authorization
        // separate from System Administrator access).
        Assert.Empty(PvadAccountPermissions.All.ToHashSet(StringComparer.Ordinal)
            .Intersect(PvadPermissions.All, StringComparer.Ordinal));
        Assert.Empty(PvadDesignerPermissions.All.ToHashSet(StringComparer.Ordinal)
            .Intersect(PvadPermissions.All, StringComparer.Ordinal));
        Assert.Empty(PvadDesignerPermissions.All.ToHashSet(StringComparer.Ordinal)
            .Intersect(PvadAccountPermissions.All, StringComparer.Ordinal));
    }
}
