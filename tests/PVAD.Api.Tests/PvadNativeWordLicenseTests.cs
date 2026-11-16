using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PVAD.Licensing;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadNativeWordLicenseTests
{
    [Fact]
    public async Task Signing_preserves_native_failure_for_the_central_problem_handler()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-native-license.json");
        var error = Assert.Throws<PvadLicenseException>(() => PvadLicense.LoadFromFile(path));
        using var certificateStream = new MemoryStream([1]);
        var certificate = new FormFile(certificateStream, 0, 1, "certificate", "synthetic.pfx");
        var controller = new DocumentOpsController(null!, null!, null!, [], null!);
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddProblemDetails();
                services.AddExceptionHandler<PvadNativeLicenseExceptionHandler>();
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.Run(async context =>
                {
                    await controller.SignDocx(new NativeFailureFile(error), certificate, null);
                    context.Response.StatusCode = 400;
                });
            }));

        var response = await server.CreateClient().GetAsync("/signing-native-error-propagation");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("PVADLIC013", problem.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(path, body);
        Assert.DoesNotContain("Signing failed", body);
    }

    private sealed class NativeFailureFile(PvadLicenseException error) : IFormFile
    {
        public string ContentType => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        public string ContentDisposition => "form-data";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length => 1;
        public string Name => "docx";
        public string FileName => "synthetic.docx";
        public Stream OpenReadStream() => throw error;
        public void CopyTo(Stream target) => throw error;
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => Task.FromException(error);
    }
}
