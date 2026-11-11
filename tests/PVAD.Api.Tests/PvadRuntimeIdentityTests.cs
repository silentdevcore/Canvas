using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PVAD.WebApi.Controllers;
using PVAD.WebApi.Observability;

namespace PVAD.Api.Tests;

public sealed class PvadRuntimeIdentityTests
{
    [Theory]
    [InlineData(HealthStatus.Healthy, "Healthy")]
    [InlineData(HealthStatus.Degraded, "Degraded")]
    [InlineData(HealthStatus.Unhealthy, "Unhealthy")]
    public void Health_readers_use_canonical_registrations_without_exposing_details(HealthStatus status, string expected)
    {
        foreach (var (name, key, display) in new[]
                 {
                     (PvadHealthCheckNames.Database, "database", "PostgreSQL"),
                     (PvadHealthCheckNames.Mail, "mail", "Mail delivery"),
                 })
        {
            var component = PvadSystemHealthService.FromHealthCheck(Report((name, status)), name, key, display);

            Assert.Equal(key, component.Key);
            Assert.Equal(expected, component.Status);
            Assert.DoesNotContain("private", component.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Host=", component.Summary, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Missing_or_former_health_registrations_fail_closed()
    {
        var missing = PvadSystemHealthService.FromHealthCheck(Report(),
            PvadHealthCheckNames.Database, "database", "PostgreSQL");
        var former = PvadSystemHealthService.FromHealthCheck(Report(("pxa-database", HealthStatus.Healthy)),
            PvadHealthCheckNames.Database, "database", "PostgreSQL");

        Assert.Equal("Unhealthy", missing.Status);
        Assert.Equal("Unhealthy", former.Status);
    }

    [Fact]
    public void Browser_trace_writer_uses_canonical_names_and_preserves_the_bounded_payload()
    {
        using var activity = new Activity("runtime-identity-test").Start();
        var controller = new BrowserTelemetryController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = controller.Record(new BrowserTelemetryBatch("designer",
            [new BrowserTelemetryEvent("status_page", "shown", "designer", "offline", CorrelationId: "synthetic-trace_1")],
            "1.1.0"));

        Assert.IsType<AcceptedResult>(result);
        Assert.Equal("designer", activity.GetTagItem("pvad.browser.application"));
        Assert.Equal("1.1.0", activity.GetTagItem("pvad.browser.version"));
        Assert.Equal(1, activity.GetTagItem("pvad.browser.event_count"));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.StartsWith("pxa.", StringComparison.Ordinal));
        var correlation = Assert.Single(activity.Events);
        Assert.Equal("pvad.browser.status.correlation", correlation.Name);
        Assert.Equal("synthetic-trace_1", Assert.Single(correlation.Tags).Value);
        Assert.Equal("pvad.browser.source_correlation_id", Assert.Single(correlation.Tags).Key);
    }

    [Fact]
    public void Invalid_browser_values_are_rejected_before_trace_attributes_are_written()
    {
        using var activity = new Activity("runtime-invalid-test").Start();
        var controller = new BrowserTelemetryController();

        var result = controller.Record(new BrowserTelemetryBatch("designer",
            [new BrowserTelemetryEvent("status_page", "shown", "designer", "offline", CorrelationId: "private@example.test")],
            "1.1.0"));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(activity.TagObjects);
        Assert.Empty(activity.Events);
    }

    private static HealthReport Report(params (string Name, HealthStatus Status)[] entries) =>
        new(entries.ToDictionary(entry => entry.Name, entry => new HealthReportEntry(
            entry.Status, "private Host=database;Password=private", TimeSpan.Zero, null,
            new Dictionary<string, object> { ["private"] = "private" })), TimeSpan.Zero);
}
