using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PXA.WebApi.Observability;

namespace PXA.WebApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/pxa/v1/telemetry/browser")]
[EnableRateLimiting("browser-telemetry")]
public sealed class BrowserTelemetryController : ControllerBase
{
    internal const int MaximumBatchSize = 20;

    private static readonly HashSet<string> Applications =
        ["company", "documentation", "demo", "account", "admin", "designer"];
    private static readonly HashSet<string> EventTypes =
        ["navigation", "error", "unhandled_rejection", "api_failure", "web_vital"];
    private static readonly HashSet<string> Outcomes =
        ["completed", "failed", "network_error", "client_error", "server_error",
         "rate_limited", "unauthorized", "forbidden", "good", "needs_improvement", "poor"];
    private static readonly Dictionary<string, HashSet<string>> EventOutcomes = new()
    {
        ["navigation"] = ["completed"],
        ["error"] = ["failed"],
        ["unhandled_rejection"] = ["failed"],
        ["api_failure"] = ["network_error", "client_error", "server_error", "rate_limited", "unauthorized", "forbidden"],
        ["web_vital"] = ["good", "needs_improvement", "poor"],
    };
    private static readonly HashSet<string> VitalNames = ["lcp", "cls", "inp"];
    private static readonly Dictionary<string, HashSet<string>> Routes = new()
    {
        ["company"] = ["home", "products", "pricing", "about", "support", "contact", "terms", "privacy", "license"],
        ["documentation"] = ["home", "editor", "code", "migration", "api", "cookbook"],
        ["demo"] = ["home", "pdf", "designer", "report", "migration", "spreadsheet", "import-export"],
        ["account"] = ["home", "login", "register", "verify-email", "dashboard", "profile", "organization", "subscription", "usage", "licenses", "developer-access", "security", "support", "closure"],
        ["admin"] = ["home", "login", "dashboard", "users", "organizations", "roles", "subscriptions", "licenses", "service-accounts", "mail", "audit", "settings", "documentation"],
        ["designer"] = ["home", "designer", "templates", "migrations", "importer", "converter", "spreadsheet", "docs", "pdf-viewer"],
    };

    [HttpPost]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Record(BrowserTelemetryBatch request)
    {
        if (!TryValidate(request, out var error))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid browser telemetry",
                Detail = error,
            });

        Activity.Current?.SetTag("pxa.browser.application", request.Application);
        Activity.Current?.SetTag("pxa.browser.event_count", request.Events.Count);
        foreach (var browserEvent in request.Events)
        {
            var route = NormalizeRoute(request.Application, browserEvent.Route);
            PxaTelemetry.RecordBrowserEvent(
                request.Application,
                browserEvent.Type,
                browserEvent.Outcome,
                route,
                browserEvent.Name,
                browserEvent.Value);
        }

        return Accepted();
    }

    internal static bool TryValidate(BrowserTelemetryBatch? request, out string error)
    {
        if (request is null || !Applications.Contains(request.Application))
        {
            error = "The application is not supported.";
            return false;
        }
        if (request.Events is null || request.Events.Count is < 1 or > MaximumBatchSize)
        {
            error = $"A batch must contain between 1 and {MaximumBatchSize} events.";
            return false;
        }

        foreach (var item in request.Events)
        {
            if (!EventTypes.Contains(item.Type) ||
                !Outcomes.Contains(item.Outcome) ||
                !EventOutcomes[item.Type].Contains(item.Outcome))
            {
                error = "An event type or outcome is not supported.";
                return false;
            }
            if (item.Route is null || item.Route.Length is < 1 or > 32)
            {
                error = "The route group is invalid.";
                return false;
            }
            if (item.Type == "web_vital")
            {
                if (item.Name is null || !VitalNames.Contains(item.Name) ||
                    item.Value is null || !double.IsFinite(item.Value.Value) ||
                    item.Value is < 0 or > 600_000)
                {
                    error = "The Web Vital measurement is invalid.";
                    return false;
                }
            }
            else if (item.Name is not null || item.Value is not null)
            {
                error = "Only Web Vital events may contain a name or value.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    internal static string NormalizeRoute(string application, string route) =>
        Routes.TryGetValue(application, out var routes) && routes.Contains(route)
            ? route
            : "other";
}

public sealed record BrowserTelemetryBatch(
    string Application,
    IReadOnlyList<BrowserTelemetryEvent> Events);

public sealed record BrowserTelemetryEvent(
    string Type,
    string Outcome,
    string Route,
    string? Name = null,
    double? Value = null);
