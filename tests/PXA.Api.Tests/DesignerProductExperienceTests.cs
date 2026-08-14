using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Application.Designer;
using PXA.WebApi.Controllers;
using PXA.WebApi.Infrastructure;
using PXA.WebApi.Security;
using PXA.WebApi.Services.Entitlements;

namespace PXA.Api.Tests;

public sealed class DesignerProductExperienceTests
{
    [Fact]
    public async Task Alpha_requires_organization_policy_and_user_opt_in()
    {
        await using var dbContext = CreateContext();
        var metadata = new PxaDesignerProductMetadata();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gate = new PxaDesignerFeatureGate(
            dbContext,
            metadata,
            new AllowEntitlements());

        var blocked = await gate.EvaluateAsync(
            organizationId, userId, "designer.ai-layout-assistant");
        Assert.False(blocked.Enabled);
        Assert.Equal("PXA_DESIGNER_ALPHA_NOT_ALLOWED", blocked.Code);

        dbContext.DesignerFeaturePolicies.Add(new DesignerFeaturePolicy
        {
            OrganizationId = organizationId,
            FeatureId = "designer.ai-layout-assistant",
            AlphaOptInAllowed = true,
            UpdatedByUserId = userId,
        });
        await dbContext.SaveChangesAsync();
        var optInRequired = await gate.EvaluateAsync(
            organizationId, userId, "designer.ai-layout-assistant");
        Assert.False(optInRequired.Enabled);
        Assert.Equal("PXA_DESIGNER_ALPHA_OPT_IN_REQUIRED", optInRequired.Code);

        dbContext.DesignerFeaturePreferences.Add(new DesignerFeaturePreference
        {
            OrganizationId = organizationId,
            UserId = userId,
            FeatureId = "designer.ai-layout-assistant",
            Enabled = true,
        });
        await dbContext.SaveChangesAsync();
        var enabled = await gate.EvaluateAsync(
            organizationId, userId, "designer.ai-layout-assistant");
        Assert.True(enabled.Enabled);
    }

    [Fact]
    public async Task Beta_is_enabled_by_default_but_policy_and_entitlement_remain_authoritative()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var metadata = new PxaDesignerProductMetadata();
        var gate = new PxaDesignerFeatureGate(
            dbContext,
            metadata,
            new AllowEntitlements());

        var enabled = await gate.EvaluateAsync(
            organizationId, userId, "designer.pdf-viewer");
        Assert.True(enabled.Enabled);

        dbContext.DesignerFeaturePolicies.Add(new DesignerFeaturePolicy
        {
            OrganizationId = organizationId,
            FeatureId = "designer.pdf-viewer",
            EnabledOverride = false,
            UpdatedByUserId = userId,
        });
        await dbContext.SaveChangesAsync();
        var disabled = await gate.EvaluateAsync(
            organizationId, userId, "designer.pdf-viewer");
        Assert.False(disabled.Enabled);
        Assert.Equal("PXA_DESIGNER_FEATURE_DISABLED", disabled.Code);

        var entitlementGate = new PxaDesignerFeatureGate(
            dbContext,
            metadata,
            new DenyEntitlements());
        var spreadsheetDenied = await entitlementGate.EvaluateAsync(
            organizationId, userId, "designer.spreadsheet");
        Assert.False(spreadsheetDenied.Enabled);
        Assert.Equal("PXA_ENTITLEMENT_DENIED", spreadsheetDenied.Code);
    }

    [Fact]
    public async Task Notifications_are_tenant_and_user_scoped()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var visibleOrganization = Notification(organizationId, null, "Organization");
        var visibleUser = Notification(null, userId, "User");
        var global = Notification(null, null, "Global");
        var hiddenOrganization = Notification(otherOrganizationId, null, "Other organization");
        var hiddenUser = Notification(null, otherUserId, "Other user");
        dbContext.DesignerNotifications.AddRange(
            visibleOrganization,
            visibleUser,
            global,
            hiddenOrganization,
            hiddenUser);
        await dbContext.SaveChangesAsync();
        var controller = CreateNotificationController(
            dbContext, organizationId, userId);

        var action = await controller.List(cancellationToken: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<DesignerNotificationPage>(ok.Value);
        Assert.Equal(3, page.Total);
        Assert.Equal(
            ["Global", "Organization", "User"],
            page.Items.Select(value => value.Title).Order());

        var forbiddenMutation = await controller.MarkRead(
            hiddenOrganization.Id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(forbiddenMutation);
        var allowedMutation = await controller.MarkRead(
            visibleOrganization.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(allowedMutation);
    }

    [Fact]
    public async Task Notifications_apply_expiry_pagination_and_dismiss_rules()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expired = Notification(organizationId, null, "Expired");
        expired.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var first = Notification(organizationId, null, "First");
        first.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var second = Notification(organizationId, null, "Second");
        second.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        second.Dismissible = false;
        var future = Notification(null, null, "Scheduled Legal update");
        future.Category = DesignerNotificationCategory.Legal;
        future.CreatedAt = DateTimeOffset.UtcNow.AddDays(1);
        dbContext.DesignerNotifications.AddRange(expired, first, second, future);
        await dbContext.SaveChangesAsync();
        var controller = CreateNotificationController(dbContext, organizationId, userId);

        var action = await controller.List(
            page: 1,
            pageSize: 1,
            cancellationToken: CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var page = Assert.IsType<DesignerNotificationPage>(ok.Value);
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
        Assert.Equal("Second", page.Items[0].Title);
        Assert.IsType<ConflictObjectResult>(
            await controller.Dismiss(second.Id, CancellationToken.None));
        Assert.IsType<NoContentResult>(
            await controller.Dismiss(first.Id, CancellationToken.None));

        var afterDismiss = await controller.List(cancellationToken: CancellationToken.None);
        var afterDismissPage = Assert.IsType<DesignerNotificationPage>(
            Assert.IsType<OkObjectResult>(afterDismiss.Result).Value);
        Assert.Single(afterDismissPage.Items);
        Assert.Equal("Second", afterDismissPage.Items[0].Title);
        await controller.MarkAllRead(CancellationToken.None);
        Assert.DoesNotContain(
            await dbContext.DesignerNotificationStates.ToListAsync(),
            state => state.NotificationId == future.Id);
    }

    [Fact]
    public async Task Release_read_state_is_returned_for_the_same_user_on_later_requests()
    {
        await using var dbContext = CreateContext();
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var firstController = CreateNotificationController(dbContext, organizationId, userId);
        Assert.IsType<NoContentResult>(
            await firstController.MarkReleaseRead("1.0.0", CancellationToken.None));

        var laterController = CreateNotificationController(dbContext, organizationId, userId);
        var action = await laterController.Releases(CancellationToken.None);
        var feed = Assert.IsType<DesignerReleaseFeedResponse>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Contains("1.0.0", feed.ReadVersions);
    }

    [Fact]
    public void Product_metadata_has_current_unique_release_and_feature_ids()
    {
        var metadata = new PxaDesignerProductMetadata();
        Assert.Contains(metadata.Releases.Releases, value => value.Version == "1.0.0");
        Assert.Equal(
            metadata.Releases.Releases.Count,
            metadata.Releases.Releases.Select(value => value.Version)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            metadata.Features.Features.Count,
            metadata.Features.Features.Select(value => value.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Disabled_designer_feature_cannot_be_bypassed_through_product_api()
    {
        var nextCalled = false;
        var middleware = new PxaDesignerFeatureGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/pdf-viewer/forms";
        context.Request.Headers["X-PXA-Application"] = "designer";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(
            context,
            new TestTenantContext(Guid.NewGuid(), Guid.NewGuid()),
            new DenyFeatureGate());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static PxaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PxaDbContext(options);
    }

    private static DesignerNotification Notification(
        Guid? organizationId,
        Guid? userId,
        string title) =>
        new()
        {
            OrganizationId = organizationId,
            UserId = userId,
            Category = DesignerNotificationCategory.System,
            Severity = DesignerNotificationSeverity.Info,
            Title = title,
            Message = $"{title} notification",
        };

    private static DesignerNotificationsController CreateNotificationController(
        PxaDbContext dbContext,
        Guid organizationId,
        Guid userId)
    {
        var controller = new DesignerNotificationsController(
            dbContext,
            new TestTenantContext(userId, organizationId),
            new PxaDesignerProductMetadata());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    private sealed record TestTenantContext(Guid? UserId, Guid? OrganizationId) : IPxaTenantContext;

    private sealed class AllowEntitlements : IPxaEntitlementService
    {
        public Task<PxaEntitlementDecision> EvaluateAsync(
            Guid organizationId,
            string capability,
            long quantity = 0,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PxaEntitlementDecision(
                true,
                "PXA_ENTITLEMENT_ALLOWED",
                "Allowed for test.",
                null,
                null,
                capability,
                null,
                null,
                null,
                0,
                null));
    }

    private sealed class DenyEntitlements : IPxaEntitlementService
    {
        public Task<PxaEntitlementDecision> EvaluateAsync(
            Guid organizationId,
            string capability,
            long quantity = 0,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PxaEntitlementDecision(
                false,
                "PXA_ENTITLEMENT_DENIED",
                "Denied for test.",
                null,
                null,
                capability,
                null,
                null,
                null,
                0,
                null));
    }

    private sealed class DenyFeatureGate : IPxaDesignerFeatureGate
    {
        public Task<DesignerFeatureDecision> EvaluateAsync(
            Guid organizationId,
            Guid userId,
            string featureId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DesignerFeatureDecision(
                false,
                "PXA_DESIGNER_FEATURE_DISABLED",
                "Disabled for test.",
                null,
                null));
    }
}
