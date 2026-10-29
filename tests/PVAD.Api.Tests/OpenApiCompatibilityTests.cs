using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

public sealed class OpenApiCompatibilityTests
{
    [Fact]
    public async Task Controller_document_generates_expected_routes_and_resolves_local_references()
    {
        // Exercise the real controller schemas without starting workers or connecting to a database.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ApplicationName = typeof(VersionController).Assembly.GetName().Name,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(VersionController).Assembly);
        builder.Services.AddOpenApi();
        await using var app = builder.Build();
        app.MapControllers();
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.StartsWith("3.", root.GetProperty("openapi").GetString());
        var paths = root.GetProperty("paths");
        foreach (var (path, method) in new[]
        {
            ("/api/pvad/v1/version", "get"),
            ("/api/pvad/v1/auth/login", "post"),
            ("/api/auth/login", "post"),
            ("/api/pvad/v1/designer/templates", "get"),
            ("/api/pvad/v1/designer/templates/{id}/draft", "put"),
            ("/api/export", "post"),
            ("/api/pvad/export", "post"),
        })
        {
            Assert.True(paths.TryGetProperty(path, out var operation), $"Missing API path {path}.");
            Assert.True(operation.TryGetProperty(method, out _), $"Missing {method} operation for {path}.");
        }
        Assert.True(paths.GetProperty("/api/pvad/v1/auth/login").GetProperty("post")
            .TryGetProperty("requestBody", out _));
        Assert.True(root.GetProperty("components").GetProperty("schemas").EnumerateObject().Any());
        ValidateReferences(root, root);
    }

    private static void ValidateReferences(JsonElement root, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("$ref", out var reference))
            {
                var pointer = reference.GetString();
                Assert.StartsWith("#/", pointer);
                var target = root;
                foreach (var segment in pointer![2..].Split('/'))
                {
                    var property = segment.Replace("~1", "/").Replace("~0", "~");
                    Assert.True(target.TryGetProperty(property, out var next), $"Unresolved schema reference {pointer}.");
                    target = next;
                }
            }
            foreach (var property in element.EnumerateObject()) ValidateReferences(root, property.Value);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) ValidateReferences(root, item);
        }
    }
}
