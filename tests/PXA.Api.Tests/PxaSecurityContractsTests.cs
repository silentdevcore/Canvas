using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;
using PXA.WebApi.Services;

namespace PXA.Api.Tests;

public sealed class PxaSecurityContractsTests
{
    [Fact]
    public void Nutrient_viewer_migration_requires_domain_provider_and_rate_limit_policies()
    {
        var controllerType = typeof(NutrientViewerMigrationController);
        var policies = controllerType.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();
        var rateLimit = controllerType.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.Contains(PxaDesignerPermissions.MigrationPdfExecute, policies);
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

        Assert.Contains(PxaDesignerPermissions.MigrationPdfExecute, policies);
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
                    new Claim(PxaClaimTypes.ActiveOrganization, organizationId.ToString()),
                ], "test")),
            },
        };

        var context = new PxaTenantContext(accessor);

        Assert.Equal(userId, context.UserId);
        Assert.Equal(organizationId, context.OrganizationId);

        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "not-a-user-id"),
            new Claim(PxaClaimTypes.ActiveOrganization, "not-an-organization-id"),
        ], "test"));
        Assert.Null(context.UserId);
        Assert.Null(context.OrganizationId);
    }

    [Fact]
    public void Built_in_roles_use_only_registered_permissions_and_keep_commercial_access_separate()
    {
        var registeredPermissions = PxaPermissions.All
            .Concat(PxaAccountPermissions.All)
            .Concat(PxaDesignerPermissions.All)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var permission in PxaRoles.Permissions.SelectMany(value => value.Value))
            Assert.Contains(permission, registeredPermissions);

        Assert.Equal(
            PxaPermissions.All.Concat(PxaDesignerPermissions.All)
                .ToHashSet(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
            PxaRoles.Permissions[PxaRoles.SystemAdministrator].Order(StringComparer.Ordinal));
        Assert.Contains(PxaPermissions.SubscriptionsRead,
            PxaRoles.Permissions[PxaRoles.OrganizationAdministrator]);
        Assert.DoesNotContain(PxaPermissions.SubscriptionsManage,
            PxaRoles.Permissions[PxaRoles.OrganizationAdministrator]);

        // Editor/Viewer are customer-facing member roles: no Admin-app permissions,
        // but they do get the self-scoped Account permissions every member needs.
        Assert.DoesNotContain(PxaRoles.Permissions[PxaRoles.Editor], PxaPermissions.All.Contains);
        Assert.DoesNotContain(PxaRoles.Permissions[PxaRoles.Viewer], PxaPermissions.All.Contains);
        Assert.Contains(PxaAccountPermissions.ProfileManage, PxaRoles.Permissions[PxaRoles.Editor]);
        Assert.Contains(PxaAccountPermissions.ProfileManage, PxaRoles.Permissions[PxaRoles.Viewer]);
        Assert.DoesNotContain(PxaAccountPermissions.MembersInvite, PxaRoles.Permissions[PxaRoles.Editor]);
        Assert.DoesNotContain(PxaAccountPermissions.MembersInvite, PxaRoles.Permissions[PxaRoles.Viewer]);
    }

    [Fact]
    public void Designer_permissions_are_unique_registered_and_separated_by_role()
    {
        Assert.Equal(
            PxaDesignerPermissions.All.Count,
            PxaDesignerPermissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(PxaDesignerPermissions.All,
            permission => Assert.StartsWith("designer.", permission, StringComparison.Ordinal));

        Assert.All(PxaDesignerPermissions.All,
            permission => Assert.Contains(permission, PxaRoles.Permissions[PxaRoles.SystemAdministrator]));
        Assert.All(PxaDesignerPermissions.All,
            permission => Assert.Contains(permission, PxaRoles.Permissions[PxaRoles.OrganizationAdministrator]));

        var manager = PxaRoles.Permissions[PxaRoles.Manager];
        Assert.Contains(PxaDesignerPermissions.TemplatesPublish, manager);
        Assert.Contains(PxaDesignerPermissions.PdfViewerRedact, manager);
        Assert.Contains(PxaDesignerPermissions.PdfCreate, manager);
        Assert.Contains(PxaDesignerPermissions.MigrationPdfExecute, manager);
        Assert.All(PxaDesignerMigrationPermissions.All, permission => Assert.Contains(permission, manager));
        Assert.DoesNotContain(PxaDesignerPermissions.FeaturesManage, manager);

        var editor = PxaRoles.Permissions[PxaRoles.Editor];
        Assert.Contains(PxaDesignerPermissions.TemplatesEdit, editor);
        Assert.Contains(PxaDesignerPermissions.PdfEdit, editor);
        Assert.Contains(PxaDesignerPermissions.SpreadsheetCreate, editor);
        Assert.Contains(PxaDesignerPermissions.CodeExecute, editor);
        Assert.Contains(PxaDesignerPermissions.MigrationExecute, editor);
        Assert.All(PxaDesignerMigrationPermissions.All, permission => Assert.Contains(permission, editor));
        Assert.Contains(PxaDesignerPermissions.JobsCancel, editor);
        Assert.DoesNotContain(PxaDesignerPermissions.TemplatesPublish, editor);
        Assert.DoesNotContain(PxaDesignerPermissions.TemplatesArchive, editor);
        Assert.DoesNotContain(PxaDesignerPermissions.PdfViewerRedact, editor);

        var viewer = PxaRoles.Permissions[PxaRoles.Viewer];
        Assert.Contains(PxaDesignerPermissions.TemplatesRead, viewer);
        Assert.Contains(PxaDesignerPermissions.PdfAccess, viewer);
        Assert.Contains(PxaDesignerPermissions.PdfTemplatesRead, viewer);
        Assert.Contains(PxaDesignerPermissions.Export, viewer);
        Assert.Contains(PxaDesignerPermissions.PdfViewerRead, viewer);
        Assert.Contains(PxaDesignerPermissions.JobsRead, viewer);
        Assert.DoesNotContain(PxaDesignerPermissions.TemplatesEdit, viewer);
        Assert.DoesNotContain(PxaDesignerPermissions.PdfCreate, viewer);
        Assert.DoesNotContain(PxaDesignerPermissions.PdfEdit, viewer);
        Assert.DoesNotContain(PxaDesignerPermissions.Import, viewer);
        Assert.DoesNotContain(PxaDesignerPermissions.MigrationExecute, viewer);
        Assert.All(PxaDesignerMigrationPermissions.All, permission => Assert.DoesNotContain(permission, viewer));
        Assert.DoesNotContain(PxaDesignerPermissions.PdfViewerAnnotate, viewer);
        Assert.DoesNotContain(PxaDesignerPermissions.JobsCancel, viewer);
    }

    [Fact]
    public void Migration_permissions_cover_every_supported_provider_by_domain()
    {
        Assert.Equal(16, PxaDesignerMigrationPermissions.PdfProviders.Values.Distinct().Count());
        Assert.Equal(8, PxaDesignerMigrationPermissions.SpreadsheetProviders.Values.Distinct().Count());
        Assert.Equal(8, PxaDesignerMigrationPermissions.ReportProviders.Values.Distinct().Count());
        Assert.Single(PxaDesignerMigrationPermissions.ViewerProviders.Values.Distinct());
        Assert.Equal(33, PxaDesignerMigrationPermissions.All.Count);
        Assert.All(PxaDesignerMigrationPermissions.PdfProviders.Values,
            permission => Assert.StartsWith("designer.migration.pdf.provider.", permission, StringComparison.Ordinal));
        Assert.All(PxaDesignerMigrationPermissions.SpreadsheetProviders.Values,
            permission => Assert.StartsWith("designer.migration.spreadsheet.provider.", permission, StringComparison.Ordinal));
        Assert.All(PxaDesignerMigrationPermissions.ReportProviders.Values,
            permission => Assert.StartsWith("designer.migration.report.provider.", permission, StringComparison.Ordinal));
        Assert.All(PxaDesignerMigrationPermissions.ViewerProviders.Values,
            permission => Assert.StartsWith("designer.migration.pdf.provider.nutrient-viewer", permission, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/api/export/pdf", "POST", PxaDesignerPermissions.PdfExport)]
    [InlineData("/api/document/import-pdf", "POST", PxaDesignerPermissions.PdfImport)]
    [InlineData("/api/document/convert-image-to-pdf", "POST", PxaDesignerPermissions.PdfConvert)]
    [InlineData("/api/document/validate", "POST", PxaDesignerPermissions.PdfEdit)]
    [InlineData("/api/migration/frameworks", "GET", PxaDesignerPermissions.Access)]
    [InlineData("/api/migration/convert", "POST", PxaDesignerPermissions.Access)]
    [InlineData("/api/spreadsheet/import", "POST", PxaDesignerPermissions.SpreadsheetImport)]
    [InlineData("/api/spreadsheet/export", "POST", PxaDesignerPermissions.SpreadsheetExport)]
    [InlineData("/api/spreadsheet/validate", "POST", PxaDesignerPermissions.SpreadsheetRead)]
    [InlineData("/api/pdf-viewer/document", "GET", PxaDesignerPermissions.PdfViewerRead)]
    [InlineData("/api/pdf-viewer/forms/fill", "POST", PxaDesignerPermissions.PdfViewerForms)]
    [InlineData("/api/pdf-viewer/redact", "POST", PxaDesignerPermissions.PdfViewerRedact)]
    [InlineData("/api/pdf-viewer/annotations", "POST", PxaDesignerPermissions.PdfViewerAnnotate)]
    [InlineData("/api/templates", "GET", PxaDesignerPermissions.PdfTemplatesRead)]
    [InlineData("/api/templates", "POST", PxaDesignerPermissions.PdfCreate)]
    [InlineData("/api/templates/csharp/execute", "POST", PxaDesignerPermissions.CodeExecute)]
    [InlineData("/api/templates/render", "POST", PxaDesignerPermissions.PdfExport)]
    [InlineData("/api/pxa/v1/document-jobs/import", "POST", PxaDesignerPermissions.PdfImport)]
    [InlineData("/api/pxa/v1/document-jobs/export", "POST", PxaDesignerPermissions.PdfExport)]
    [InlineData("/api/pxa/v1/document-jobs/code-migration", "POST", PxaDesignerPermissions.MigrationExecute)]
    [InlineData("/api/pxa/v1/migrations/pdf/viewer/convert", "POST", PxaDesignerPermissions.MigrationPdfExecute)]
    [InlineData("/api/pxa/v1/jobs/58b50226-34ea-480c-936a-837b03445006", "GET", PxaDesignerPermissions.JobsRead)]
    [InlineData("/api/pxa/v1/jobs/58b50226-34ea-480c-936a-837b03445006/cancel", "POST", PxaDesignerPermissions.JobsCancel)]
    public void Designer_shared_api_routes_resolve_to_the_required_permission(
        string path,
        string method,
        string expected)
    {
        Assert.Equal(expected, PxaDesignerPermissionResolver.Resolve(new PathString(path), method));
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
                policies.Length == 1 && PxaDesignerPermissions.All.Contains(policies[0]!),
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
        Assert.Equal(expected, PxaDesignerMigrationPermissions.ResolveCodeProvider(framework, domain));
    }

    [Theory]
    [InlineData("JrxmlReport", "designer.migration.report.provider.jasperreports.execute")]
    [InlineData("rdl", "designer.migration.report.provider.rdl.execute")]
    [InlineData("DevExpressReport", "designer.migration.report.provider.devexpress-report.execute")]
    public void Report_migration_aliases_resolve_to_stable_provider_permissions(
        string provider,
        string expected)
    {
        Assert.Equal(expected, PxaDesignerMigrationPermissions.ResolveReportProvider(provider));
    }

    [Theory]
    [InlineData("nutrient-viewer")]
    [InlineData("NutrientViewer")]
    [InlineData("PSPDFKit")]
    public void Viewer_migration_aliases_resolve_to_stable_provider_permissions(string provider)
    {
        Assert.Equal(
            "designer.migration.pdf.provider.nutrient-viewer.execute",
            PxaDesignerMigrationPermissions.ResolveViewerProvider(provider));
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
                        new Claim(PxaClaimTypes.Permission, PxaDesignerPermissions.MigrationPdfExecute),
                    ], "test")),
                },
            },
        };
        controller.Request.Headers["X-PXA-Application"] = "designer";

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
            PxaDesignerPermissions.Access,
            PxaDesignerPermissions.MigrationPdfRead,
            PxaDesignerPermissions.MigrationPdfExecute,
            PxaDesignerPermissions.MigrationSpreadsheetRead,
            PxaDesignerPermissions.MigrationSpreadsheetExecute,
            PxaDesignerMigrationPermissions.ResolveCodeProvider("Syncfusion", "pdf")!,
            PxaDesignerMigrationPermissions.ResolveCodeProvider("AsposeCells", "spreadsheet")!,
        };
        var controller = new MigrationController(new MigrationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        permissions.Select(permission => new Claim(PxaClaimTypes.Permission, permission)),
                        "limited-role")),
                },
            },
        };
        controller.Request.Headers["X-PXA-Application"] = "designer";

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
    public void Account_permissions_are_all_registered_and_mapped_to_at_least_one_role()
    {
        var mappedPermissions = PxaRoles.Permissions.SelectMany(value => value.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var permission in PxaAccountPermissions.All)
            Assert.Contains(permission, mappedPermissions);

        // The privileged System Administrator vocabulary and the customer Account
        // vocabulary must not overlap, so Admin authorization can never be satisfied
        // by an Account-only permission claim (checklist: keep customer authorization
        // separate from System Administrator access).
        Assert.Empty(PxaAccountPermissions.All.ToHashSet(StringComparer.Ordinal)
            .Intersect(PxaPermissions.All, StringComparer.Ordinal));
        Assert.Empty(PxaDesignerPermissions.All.ToHashSet(StringComparer.Ordinal)
            .Intersect(PxaPermissions.All, StringComparer.Ordinal));
        Assert.Empty(PxaDesignerPermissions.All.ToHashSet(StringComparer.Ordinal)
            .Intersect(PxaAccountPermissions.All, StringComparer.Ordinal));
    }
}
