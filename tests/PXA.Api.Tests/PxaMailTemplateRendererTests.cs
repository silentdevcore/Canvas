using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.WebApi.Services.Mail;

namespace PXA.Api.Tests;

public sealed class PxaMailTemplateRendererTests
{
    private static readonly string[] TemplateKeys =
    [
        "identity.invitation",
        "identity.password-reset",
        "identity.password-changed",
        "identity.email-verification",
        "identity.email-changed",
        "identity.registration-verification",
        "identity.welcome",
        "identity.new-login",
        "identity.lockout",
        "identity.trial-expiring",
        "subscription.changed",
        "license.changed",
        "security.organization-changed",
    ];

    private static readonly string[] Locales = ["en", "de", "fr", "es", "it", "ar"];

    [Fact]
    public void Every_transactional_template_renders_in_all_supported_locales()
    {
        var renderer = CreateRenderer();

        foreach (var templateKey in TemplateKeys)
        {
            var english = renderer.Render(Message(templateKey, "en"), Payload());
            foreach (var locale in Locales)
            {
                var rendered = renderer.Render(Message(templateKey, locale), Payload());
                Assert.False(string.IsNullOrWhiteSpace(rendered.Subject));
                Assert.Contains($"lang=\"{locale}\"", rendered.HtmlBody, StringComparison.Ordinal);
                Assert.Contains("Power Dox Automation", rendered.HtmlBody, StringComparison.Ordinal);
                Assert.Contains("https://account.pxa.test", rendered.HtmlBody, StringComparison.Ordinal);
                Assert.Contains("https://designer.pxa.test", rendered.HtmlBody, StringComparison.Ordinal);
                Assert.Contains("https://support.pxa.test/help", rendered.HtmlBody, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(rendered.TextBody));
                if (locale != "en")
                    Assert.NotEqual(english.Subject, rendered.Subject);
            }
        }
    }

    [Fact]
    public void Arabic_uses_rtl_and_regional_or_unknown_locales_fall_back_safely()
    {
        var renderer = CreateRenderer();

        var arabic = renderer.Render(Message("identity.welcome", "ar-SA"), Payload());
        Assert.Contains("lang=\"ar\"", arabic.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("dir=\"rtl\"", arabic.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("مرحبًا", arabic.HtmlBody, StringComparison.Ordinal);

        var german = renderer.Render(Message("identity.welcome", "de-DE"), Payload());
        Assert.Contains("Willkommen", german.Subject, StringComparison.Ordinal);

        var fallback = renderer.Render(Message("identity.welcome", "nl-NL"), Payload());
        Assert.Equal("Welcome to Power Dox Automation", fallback.Subject);
        Assert.Contains("lang=\"en\"", fallback.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Renderer_escapes_customer_values_and_rejects_non_http_action_urls()
    {
        var renderer = CreateRenderer();
        var payload = new Dictionary<string, string>
        {
            ["displayName"] = "<script>alert('x')</script>",
            ["actionUrl"] = "javascript:alert('x')",
            ["summary"] = "private internal detail",
        };

        var rendered = renderer.Render(Message("identity.invitation", "en"), payload);

        Assert.DoesNotContain("<script>", rendered.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", rendered.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private internal detail", rendered.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", rendered.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Trial_date_is_formatted_for_the_recipient_locale()
    {
        var renderer = CreateRenderer();
        var payload = Payload();
        payload["trialEndsAt"] = "2026-07-31T00:00:00+00:00";

        var rendered = renderer.Render(Message("identity.trial-expiring", "de"), payload);

        Assert.Contains("31.07.2026", rendered.TextBody, StringComparison.Ordinal);
    }

    private static PxaMailTemplateRenderer CreateRenderer() =>
        new(Options.Create(new PxaMailOptions
        {
            CompanyBaseUrl = "https://company.pxa.test",
            AccountBaseUrl = "https://account.pxa.test",
            DesignerBaseUrl = "https://designer.pxa.test",
            AdminBaseUrl = "https://admin.pxa.test",
            SupportUrl = "https://support.pxa.test/help",
        }));

    private static MailOutboxMessage Message(string templateKey, string locale) => new()
    {
        RecipientEmail = "recipient@pxa.test",
        TemplateKey = templateKey,
        ProtectedPayload = "not-used",
        IdempotencyKey = $"{templateKey}:{locale}",
        Locale = locale,
    };

    private static Dictionary<string, string> Payload() => new()
    {
        ["displayName"] = "Taylor",
        ["actionUrl"] = "https://account.pxa.test/action?token=opaque",
        ["trialEndsAt"] = "2026-07-31T00:00:00+00:00",
    };
}
