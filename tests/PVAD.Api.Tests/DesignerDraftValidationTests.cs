using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PVAD.WebApi.Controllers;

namespace PVAD.Api.Tests;

public sealed class DesignerDraftValidationTests
{
    [Fact]
    public void Draft_request_uses_property_validation_instead_of_record_constructor_mapping()
    {
        using var services = CreateServices();
        var metadata = services.GetRequiredService<IModelMetadataProvider>()
            .GetMetadataForType(typeof(UpdateDesignerTemplateDraftRequest));
        Assert.Null(metadata.BoundConstructor);
        Assert.Equal(new[] { "Revision", "DesignDocument", "SchemaVersion", "DesignerVersion" }.Order(),
            metadata.Properties.Select(property => property.Name).Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Draft_json_preserves_required_and_optional_fields(bool includeVersions)
    {
        var payload = includeVersions
            ? """{"revision":7,"designDocument":{"template":{"name":"Synthetic"}},"schemaVersion":"2.0","designerVersion":"1.1.0"}"""
            : """{"revision":7,"designDocument":{"template":{"name":"Synthetic"}}}""";
        var request = JsonSerializer.Deserialize<UpdateDesignerTemplateDraftRequest>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(7, request.Revision);
        Assert.Equal("Synthetic", request.DesignDocument.GetProperty("template").GetProperty("name").GetString());
        Assert.Equal(includeVersions ? "2.0" : null, request.SchemaVersion);
        Assert.Equal(includeVersions ? "1.1.0" : null, request.DesignerVersion);
        var (revision, design, schema, designer) = request;
        Assert.Equal(request, new UpdateDesignerTemplateDraftRequest(revision, design, schema, designer));
    }

    [Fact]
    public async Task Concurrent_first_validations_succeed_with_fresh_metadata_providers()
    {
        using var document = JsonDocument.Parse("""{"template":{"name":"Synthetic"}}""");
        for (var attempt = 0; attempt < 16; attempt++)
        {
            using var services = CreateServices();
            var validator = services.GetRequiredService<IObjectModelValidator>();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var validations = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
            {
                await start.Task;
                var context = new ActionContext(new DefaultHttpContext { RequestServices = services },
                    new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
                validator.Validate(context, null, string.Empty,
                    new UpdateDesignerTemplateDraftRequest(1, document.RootElement, "1.0", null));
                Assert.True(context.ModelState.IsValid);
            })).ToArray();
            start.SetResult();
            await Task.WhenAll(validations);
        }
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        return services.BuildServiceProvider();
    }
}
