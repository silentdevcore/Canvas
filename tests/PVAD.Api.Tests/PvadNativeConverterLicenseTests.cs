using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PVAD.Application.UseCases;
using PVAD.Core.Abstractions;
using PVAD.Core.Contracts;
using PVAD.Licensing;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadNativeConverterLicenseTests
{
    [Theory]
    [InlineData("html")]
    [InlineData("md")]
    [InlineData("csv")]
    [InlineData("xml")]
    [InlineData("svg")]
    [InlineData("odt")]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("tiff")]
    public async Task Export_preserves_native_failure_for_the_central_problem_handler(string format)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-native-license.json");
        var failure = Assert.Throws<PvadLicenseException>(() => PvadLicense.LoadFromFile(path));
        var exporter = new NativeFailureExporter(format, failure);
        var controller = new ExportController(new ExportDocumentUseCase([exporter]));
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddProblemDetails();
                services.AddExceptionHandler<PvadNativeLicenseExceptionHandler>();
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.Run(context =>
                {
                    controller.Export(format, null, null, null, new DesignExportDto());
                    context.Response.StatusCode = 400;
                    return Task.CompletedTask;
                });
            }));

        var response = await server.CreateClient().GetAsync("/converter-native-error-propagation");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("PVADLIC013", problem.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(path, body);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Equal(1, exporter.Calls);
    }

    private sealed class NativeFailureExporter(string format, PvadLicenseException failure) : IDocumentExporter
    {
        public string FormatKey => format;
        public string MimeType => "application/octet-stream";
        public string FileExtension => ".synthetic";
        public int Calls { get; private set; }
        public byte[] Export(DesignExportDto design) { Calls++; throw failure; }
    }
}
