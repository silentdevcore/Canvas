using Microsoft.OpenApi;
using PVAD.WebApi.Infrastructure.OpenApi;

namespace PVAD.Api.Tests;

public sealed class DesignerProjectsOpenApiTests
{
    [Fact]
    public async Task Transformer_defines_stable_operations_and_multipart_stream_contracts()
    {
        var document = new OpenApiDocument { Paths = [] };
        var routes = new[]
        {
            "/api/pvad/v1/designer/projects/pdf/export",
            "/api/pvad/v1/designer/projects/pdf/export-with-workspace",
            "/api/pvad/v1/designer/projects/spreadsheet/export",
            "/api/pvad/v1/designer/projects/pdf/open",
            "/api/pvad/v1/designer/projects/spreadsheet/open",
            "/api/pvad/v1/designer/projects/pdf/validate",
            "/api/pvad/v1/designer/projects/spreadsheet/validate",
            "/api/pvad/v1/designer/projects/pdf/inspect",
            "/api/pvad/v1/designer/projects/spreadsheet/inspect",
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

        await new PvadDesignerProjectsOpenApiTransformer()
            .TransformAsync(document, null!, CancellationToken.None);

        var operationIds = routes.Select(route =>
        {
            var operations = Assert.IsAssignableFrom<IDictionary<HttpMethod, OpenApiOperation>>(document.Paths[route].Operations);
            return operations[HttpMethod.Post].OperationId;
        }).ToArray();
        Assert.Equal(routes.Length, operationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(operationIds, operationId => Assert.False(string.IsNullOrWhiteSpace(operationId)));
        foreach (var route in routes.Where(route => route.Contains("/export", StringComparison.Ordinal)))
        {
            var operation = document.Paths[route].Operations![HttpMethod.Post];
            var response = Assert.IsType<OpenApiResponse>(operation.Responses!["200"]);
            var format = route.Contains("/spreadsheet/", StringComparison.Ordinal) ? "pvads" : "pvadp";
            var schema = Assert.IsType<OpenApiSchema>(response.Content![$"application/vnd.pvad.{format}+zip"].Schema);
            Assert.Equal("binary", schema.Format);
            Assert.Contains("Content-Disposition", response.Headers!.Keys);
        }
        foreach (var route in routes.Where(route =>
                     route.EndsWith("/open", StringComparison.Ordinal) ||
                     route.EndsWith("/validate", StringComparison.Ordinal) ||
                     route.EndsWith("/inspect", StringComparison.Ordinal)))
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
