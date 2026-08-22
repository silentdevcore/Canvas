using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Controllers;
using PXA.WebApi.Security;

namespace PXA.Api.Tests;

public sealed class DesignerElementFavoritesControllerTests
{
    [Fact]
    public async Task Favorites_are_user_scoped_and_ordered_by_the_catalog()
    {
        await using var dbContext = CreateContext();
        var userId = Guid.NewGuid();
        var first = CreateController(dbContext, userId, Guid.NewGuid());

        var replaced = await first.Replace(
            new ReplaceDesignerElementFavoritesRequest(["chart", "text"]),
            CancellationToken.None);
        var response = Assert.IsType<DesignerElementFavoritesResponse>(
            Assert.IsType<OkObjectResult>(replaced.Result).Value);
        Assert.Equal(["text", "chart"], response.ElementTypes);

        var anotherOrganization = CreateController(dbContext, userId, Guid.NewGuid());
        var listed = await anotherOrganization.List(CancellationToken.None);
        var persisted = Assert.IsType<DesignerElementFavoritesResponse>(
            Assert.IsType<OkObjectResult>(listed.Result).Value);
        Assert.Equal(["text", "chart"], persisted.ElementTypes);
    }

    [Fact]
    public async Task Replace_rejects_unknown_element_types_without_changing_existing_values()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext, Guid.NewGuid(), Guid.NewGuid());
        await controller.Replace(
            new ReplaceDesignerElementFavoritesRequest(["text"]),
            CancellationToken.None);

        var rejected = await controller.Replace(
            new ReplaceDesignerElementFavoritesRequest(["unknown-widget"]),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(rejected.Result);
        var listed = await controller.List(CancellationToken.None);
        var persisted = Assert.IsType<DesignerElementFavoritesResponse>(
            Assert.IsType<OkObjectResult>(listed.Result).Value);
        Assert.Equal(["text"], persisted.ElementTypes);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        await using var dbContext = CreateContext();
        var controller = CreateController(dbContext, null, null);

        Assert.IsType<UnauthorizedResult>((await controller.List(CancellationToken.None)).Result);
        Assert.IsType<UnauthorizedResult>((await controller.Replace(
            new ReplaceDesignerElementFavoritesRequest([]), CancellationToken.None)).Result);
    }

    private static PxaDbContext CreateContext() => new(
        new DbContextOptionsBuilder<PxaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DesignerElementFavoritesController CreateController(
        PxaDbContext dbContext,
        Guid? userId,
        Guid? organizationId) => new(
            dbContext,
            new TestTenantContext(userId, organizationId));

    private sealed record TestTenantContext(Guid? UserId, Guid? OrganizationId) : IPxaTenantContext;
}
