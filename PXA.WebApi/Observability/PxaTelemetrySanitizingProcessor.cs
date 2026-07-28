using System.Diagnostics;
using OpenTelemetry;

namespace PXA.WebApi.Observability;

internal sealed class PxaTelemetrySanitizingProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> RemovedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "url.query",
        "http.target",
        "http.request.header.authorization",
        "http.request.header.cookie",
        "http.response.header.set_cookie",
        "db.statement",
        "db.query.text",
        "exception.message",
        "exception.stacktrace",
        "enduser.id",
        "user.id",
        "user.email",
    };
    private static readonly string[] ForbiddenNormalizedNames =
    [
        "password",
        "authorization",
        "cookie",
        "setcookie",
        "apikey",
        "accesstoken",
        "refreshtoken",
        "actiontoken",
        "secret",
        "keyid",
        "licensekey",
        "mailbody",
        "requestbody",
        "responsebody",
        "documentcontent",
        "templatejson",
        "filename",
        "filepath",
        "contentroot",
        "useremail",
        "userid",
        "enduserid",
        "tenantid",
        "organizationid",
        "customerid",
        "jobid",
        "documentid",
    ];

    public override void OnEnd(Activity activity)
    {
        foreach (var attribute in activity.TagObjects.Select(tag => tag.Key).ToArray())
        {
            if (IsForbiddenAttribute(attribute))
                activity.SetTag(attribute, null);
        }

        SanitizeUrl(activity, "url.full");
        SanitizeUrl(activity, "http.url");
    }

    internal static bool IsForbiddenAttribute(string attribute)
    {
        if (RemovedAttributes.Contains(attribute))
            return true;
        var normalized = new string(attribute
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return ForbiddenNormalizedNames.Any(normalized.Contains);
    }

    private static void SanitizeUrl(Activity activity, string attribute)
    {
        var value = activity.GetTagItem(attribute)?.ToString();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return;

        activity.SetTag(attribute, uri.GetLeftPart(UriPartial.Path));
    }
}
