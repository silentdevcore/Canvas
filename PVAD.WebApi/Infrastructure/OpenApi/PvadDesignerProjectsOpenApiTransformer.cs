using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PVAD.WebApi.Infrastructure.OpenApi;

public sealed class PvadDesignerProjectsOpenApiTransformer : IOpenApiDocumentTransformer
{
    private const string Prefix = "/api/pvad/v1/designer/projects";

    private static readonly IReadOnlyDictionary<string, OperationContract> Contracts =
        new Dictionary<string, OperationContract>(StringComparer.Ordinal)
        {
            [$"{Prefix}/pdf/export"] = new("exportPdfProject", "Export a PVADP PDF Project", "pvadp", true, false),
            [$"{Prefix}/pdf/export-with-workspace"] = new("exportPdfProjectWithWorkspace", "Export a PVADP PDF Project with Code Workspace", "pvadp", true, false),
            [$"{Prefix}/spreadsheet/export"] = new("exportSpreadsheetProject", "Export a PVADS Spreadsheet Project", "pvads", true, false),
            [$"{Prefix}/pdf/open"] = new("openPdfProject", "Open a PVADP PDF Project", "pvadp", false, true),
            [$"{Prefix}/spreadsheet/open"] = new("openSpreadsheetProject", "Open a PVADS Spreadsheet Project", "pvads", false, true),
            [$"{Prefix}/pdf/validate"] = new("validatePdfProject", "Validate a PVADP PDF Project", "pvadp", false, false),
            [$"{Prefix}/spreadsheet/validate"] = new("validateSpreadsheetProject", "Validate a PVADS Spreadsheet Project", "pvads", false, false),
            [$"{Prefix}/pdf/inspect"] = new("inspectPdfProject", "Inspect a PVADP PDF Project", "pvadp", false, false),
            [$"{Prefix}/spreadsheet/inspect"] = new("inspectSpreadsheetProject", "Inspect a PVADS Spreadsheet Project", "pvads", false, false),
        };

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Paths is null)
            return Task.CompletedTask;

        foreach (var (path, item) in document.Paths)
        {
            if (!Contracts.TryGetValue(path, out var contract) || item.Operations is null)
                continue;
            foreach (var operation in item.Operations.Values)
            {
                operation.OperationId = contract.OperationId;
                operation.Summary = contract.Summary;
                operation.Description = contract.Export
                    ? $"Streams a deterministic .{contract.Format} package. The response media type is the native PVAD project media type and Content-Disposition supplies the sanitized filename."
                    : contract.AcceptsReservedIds
                        ? $"Accepts a bounded .{contract.Format} package (or its matching legacy PXA extension) as multipart field `file`. Optional `reservedIds` is a JSON string array used only to remap IDs that collide with the selected target. A reviewed schema upgrade is reported through `upgradeReport` and never modifies the uploaded source package."
                        : $"Accepts a bounded .{contract.Format} package (or its matching legacy PXA extension) as multipart field `file`.";
                operation.Tags = new HashSet<OpenApiTagReference>
                {
                    new("Designer Projects", document),
                };
                if (contract.Export)
                    EnsureDownloadContract(operation, contract.Format);
                else
                    EnsureMultipartContract(operation, contract.AcceptsReservedIds);
            }
        }
        return Task.CompletedTask;
    }

    private static void EnsureDownloadContract(OpenApiOperation operation, string format)
    {
        operation.Responses ??= new OpenApiResponses();
        operation.Responses["200"] = new OpenApiResponse
        {
            Description = "Native project archive stream; Content-Disposition supplies the download filename.",
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                [$"application/vnd.pvad.{format}+zip"] = new()
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                },
            },
            Headers = new Dictionary<string, IOpenApiHeader>(StringComparer.Ordinal)
            {
                ["Content-Disposition"] = new OpenApiHeader
                {
                    Description = $"Attachment filename ending in .{format}.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            },
        };
    }

    private static void EnsureMultipartContract(OpenApiOperation operation, bool acceptsReservedIds)
    {
        var properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
        {
            ["file"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "binary",
                Description = "The native PVAD project package.",
            },
        };
        if (acceptsReservedIds)
        {
            properties["reservedIds"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Optional JSON array of stable IDs already used by the selected import target.",
            };
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                ["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Required = new HashSet<string>(StringComparer.Ordinal) { "file" },
                        Properties = properties,
                    },
                },
            },
        };
    }

    private sealed record OperationContract(
        string OperationId,
        string Summary,
        string Format,
        bool Export,
        bool AcceptsReservedIds);
}
