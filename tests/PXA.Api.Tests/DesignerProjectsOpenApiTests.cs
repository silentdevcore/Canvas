using Microsoft.OpenApi;
using PXA.WebApi.Infrastructure.OpenApi;

namespace PXA.Api.Tests;

public sealed class DesignerProjectsOpenApiTests
{
    [Fact]
    public async Task Transformer_defines_stable_operations_and_multipart_stream_contracts()
    {
        var document = new OpenApiDocument { Paths = [] };
        var routes = new[]
        {
            "/api/pxa/v1/designer/projects/pdf/export",
            "/api/pxa/v1/designer/projects/spreadsheet/export",
            "/api/pxa/v1/designer/projects/pdf/open",
            "/api/pxa/v1/designer/projects/spreadsheet/open",
            "/api/pxa/v1/designer/projects/pdf/validate",
            "/api/pxa/v1/designer/projects/spreadsheet/validate",
            "/api/pxa/v1/designer/projects/pdf/inspect",
            "/api/pxa/v1/designer/projects/spreadsheet/inspect",
        };
        foreach (var route in routes)
        {
            document.Paths[route] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Post] = new(),
                },
            };
        }

        await new PxaDesignerProjectsOpenApiTransformer()
            .TransformAsync(document, null!, CancellationToken.None);

        var operationIds = routes.Select(route =>
        {
            var operations = Assert.IsAssignableFrom<IDictionary<HttpMethod, OpenApiOperation>>(document.Paths[route].Operations);
            return operations[HttpMethod.Post].OperationId;
        }).ToArray();
        Assert.Equal(routes.Length, operationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(operationIds, operationId => Assert.False(string.IsNullOrWhiteSpace(operationId)));
        foreach (var route in routes.Where(route => !route.EndsWith("/export", StringComparison.Ordinal)))
        {
            var operations = Assert.IsAssignableFrom<IDictionary<HttpMethod, OpenApiOperation>>(document.Paths[route].Operations);
            var body = Assert.IsType<OpenApiRequestBody>(operations[HttpMethod.Post].RequestBody);
            var content = Assert.IsAssignableFrom<IDictionary<string, OpenApiMediaType>>(body.Content);
            var media = content["multipart/form-data"];
            var schema = Assert.IsType<OpenApiSchema>(media.Schema);
            var required = Assert.IsAssignableFrom<ISet<string>>(schema.Required);
            var properties = Assert.IsAssignableFrom<IDictionary<string, IOpenApiSchema>>(schema.Properties);
            Assert.Contains("file", required);
            Assert.Equal("binary", Assert.IsType<OpenApiSchema>(properties["file"]).Format);
            if (route.EndsWith("/open", StringComparison.Ordinal))
                Assert.Contains("reservedIds", properties.Keys);
            else
                Assert.DoesNotContain("reservedIds", properties.Keys);
        }
    }
}
