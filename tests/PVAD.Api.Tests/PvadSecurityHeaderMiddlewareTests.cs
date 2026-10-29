using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using PVAD.WebApi.Infrastructure;

namespace PVAD.Api.Tests;

public sealed class PvadSecurityHeaderMiddlewareTests
{
    [Theory]
    [InlineData("Application", "designer")]
    [InlineData("CSRF", "synthetic-token")]
    [InlineData("API-Key", "synthetic-key")]
    public async Task Legacy_and_canonical_inputs_normalize_to_one_value(string name, string value)
    {
        foreach (var prefix in new[] { "X-PVAD-", "X-PXA-" })
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[prefix + name] = value;
            var invoked = false;
            await new PvadSecurityHeaderMiddleware(request =>
            {
                invoked = true;
                Assert.Equal(value, request.Request.Headers["X-PVAD-" + name]);
                Assert.False(request.Request.Headers.ContainsKey("X-PXA-" + name));
                return Task.CompletedTask;
            }).InvokeAsync(context);
            Assert.True(invoked);
        }
    }

    [Theory]
    [InlineData("Application", "designer")]
    [InlineData("CSRF", "synthetic-secret")]
    [InlineData("API-Key", "synthetic-secret")]
    public async Task Conflicts_duplicates_and_empty_values_fail_before_authentication(string name, string value)
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        foreach (var variant in new[] { "conflict", "duplicate", "empty", "combined" })
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Response.Body = new MemoryStream();
            context.Request.Headers["X-PVAD-" + name] = variant switch
            {
                "duplicate" => new StringValues([value, value]),
                "empty" => "",
                "combined" => value + "," + value,
                _ => value,
            };
            if (variant == "conflict") context.Request.Headers["X-PXA-" + name] = "different-secret";
            await new PvadSecurityHeaderMiddleware(_ => throw new InvalidOperationException("must not run"))
                .InvokeAsync(context);
            Assert.Equal(400, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
            Assert.Contains("PXAAPI005", body);
            Assert.DoesNotContain("synthetic-secret", body);
            Assert.DoesNotContain("different-secret", body);
        }
    }

    [Fact]
    public async Task Matching_aliases_are_allowed_and_unknown_application_does_not_fall_back_to_account()
    {
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-PVAD-Application"] = "designer";
        context.Request.Headers["X-PXA-Application"] = "DESIGNER";
        var calls = 0;
        var middleware = new PvadSecurityHeaderMiddleware(_ => { calls++; return Task.CompletedTask; });
        await middleware.InvokeAsync(context);
        Assert.Equal(1, calls);
        context.Request.Headers["X-PVAD-Application"] = "unrecognized";
        await middleware.InvokeAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("Template-Revision")]
    [InlineData("PAdES-Profile")]
    [InlineData("PDF-Timestamp")]
    [InlineData("Operator")]
    public void Response_header_aliases_have_identical_values(string suffix)
    {
        var context = new DefaultHttpContext();
        PvadResponseHeaders.Set(context.Response, suffix, "synthetic-value");
        Assert.Equal(context.Response.Headers["X-PVAD-" + suffix], context.Response.Headers["X-PXA-" + suffix]);
    }

    [Theory]
    [InlineData("X-PVAD-CSRF")]
    [InlineData("X-PXA-CSRF")]
    public async Task Both_headers_use_real_antiforgery_validation_not_a_bypass(string header)
    {
        using var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddAntiforgery(options => options.HeaderName = "X-PVAD-CSRF");
            })
            .Configure(app =>
            {
                app.UseMiddleware<PvadSecurityHeaderMiddleware>();
                app.Run(async context =>
                {
                    var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                    if (context.Request.Method == "GET")
                    {
                        await context.Response.WriteAsJsonAsync(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
                        return;
                    }
                    try { await antiforgery.ValidateRequestAsync(context); context.Response.StatusCode = 204; }
                    catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; }
                });
            })).StartAsync();
        using var client = host.GetTestClient();
        using var csrf = await client.GetAsync("/");
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var cookie = csrf.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        foreach (var valid in new[] { true, false })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/");
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add(header, valid ? token : "invalid-token");
            using var result = await client.SendAsync(request);
            Assert.Equal(valid ? HttpStatusCode.NoContent : HttpStatusCode.BadRequest, result.StatusCode);
        }
        using var missing = await client.PostAsync("/", null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }
}

public sealed class PvadHeaderPipelineTests(PvadApiTestFactory factory)
    : IClassFixture<PvadApiTestFactory>
{
    [Theory]
    [InlineData("X-PVAD-Application")]
    [InlineData("X-PXA-Application")]
    public async Task Designer_headers_cannot_read_protected_documents_anonymously(string name)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/pvad/v1/designer/templates");
        request.Headers.Add(name, "designer");
        using var result = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }
}
