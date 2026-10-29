using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PVAD.Core.Contracts;

namespace PVAD.WebApi.Infrastructure.OpenApi;

public sealed class PvadChartOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (document.Components?.Schemas?.TryGetValue(nameof(ChartDefinitionDto), out var chart) == true &&
            chart.Properties?.TryGetValue("type", out var type) == true && type is OpenApiSchema chartType)
            chartType.Enum = PvadChartTypes.Supported.Select(value => (JsonNode)JsonValue.Create(value)!).ToList();

        foreach (var (path, item) in document.Paths)
        {
            if (!path.EndsWith("/document/import-pdf-engine", StringComparison.Ordinal) || item.Operations is null) continue;
            foreach (var operation in item.Operations.Values)
            {
                var parameter = operation.Parameters?.FirstOrDefault(value => value.Name == "chartRecognition");
                if (parameter?.Schema is not OpenApiSchema schema) continue;
                schema.Enum = new[] { "off", "safe", "review" }.Select(value => (JsonNode)JsonValue.Create(value)!).ToList();
                schema.Default = JsonValue.Create("safe");
            }
        }
        return Task.CompletedTask;
    }
}
