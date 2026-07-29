using Microsoft.AspNetCore.Mvc;
using PXA.WebApi.Controllers;

namespace PXA.Api.Tests;

public sealed class VersionControllerTests
{
    [Fact]
    public void Get_ReturnsProductAndIndependentApiContractVersions()
    {
        var result = new VersionController().Get();

        var response = Assert.IsType<VersionResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("PXA", response.Product);
        Assert.Equal("1.0.0", response.ProductVersion);
        Assert.Equal("v1", response.ApiContractVersion);
        Assert.False(string.IsNullOrWhiteSpace(response.InformationalVersion));
    }
}
