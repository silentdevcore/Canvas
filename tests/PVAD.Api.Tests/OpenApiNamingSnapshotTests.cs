using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using PVAD.WebApi.Infrastructure.OpenApi;

namespace PVAD.Api.Tests;

public sealed class OpenApiNamingSnapshotTests
{
    [Fact]
    public async Task Committed_descriptions_match_actual_transformers_without_changing_operation_ids()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVAD.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var root = directory.FullName;
        var snapshot = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "docs/schema/openapi.json")))!;
        var document = new OpenApiDocument { Paths = [] };
        foreach (var (path, item) in snapshot["paths"]!.AsObject())
        {
            if (!(path.StartsWith("/api/pvad/v1/designer/projects/", StringComparison.Ordinal) ||
                  path.StartsWith("/api/pvad/v1/pdf/e-invoices/", StringComparison.Ordinal)))
                continue;
            if (item?["post"] is not { } operation)
                continue;
            if (path.StartsWith("/api/pvad/v1/pdf/e-invoices/validations/", StringComparison.Ordinal))
                continue;
            document.Paths[path] = new OpenApiPathItem
            {
                Operations = new Dictionary<HttpMethod, OpenApiOperation>
                {
                    [HttpMethod.Post] = new() { OperationId = operation["operationId"]?.GetValue<string>() },
                },
            };
        }
        await new PvadDesignerProjectsOpenApiTransformer().TransformAsync(document, null!, CancellationToken.None);
        await new PvadEInvoiceOpenApiTransformer().TransformAsync(document, null!, CancellationToken.None);
        var descriptions = document.Paths.Select(pair =>
        {
            var operation = pair.Value.Operations![HttpMethod.Post];
            Assert.Equal(snapshot["paths"]![pair.Key]!["post"]!["operationId"]?.GetValue<string>(), operation.OperationId);
            Assert.NotNull(operation.Description);
            Assert.DoesNotContain("pxa", operation.Description, StringComparison.OrdinalIgnoreCase);
            return new { path = pair.Key, method = "post", operationId = operation.OperationId, description = operation.Description };
        }).ToArray();
        Assert.Equal(16, descriptions.Length);

        var output = Environment.GetEnvironmentVariable("PVAD_OPENAPI_NAMING_PATCH");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var target = Path.GetFullPath(output);
            var artifacts = Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar;
            Assert.StartsWith(artifacts, target, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, JsonSerializer.Serialize(descriptions));
            return;
        }
        foreach (var entry in descriptions)
            Assert.Equal(entry.description, snapshot["paths"]![entry.path]![entry.method]!["description"]!.GetValue<string>());
    }
}
