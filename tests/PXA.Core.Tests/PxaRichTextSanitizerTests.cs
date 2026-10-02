using PXA.Core.Security;

namespace PXA.Core.Tests;

public sealed class PxaRichTextSanitizerTests
{
    [Fact]
    public void Sanitizer_removes_active_content_handlers_and_unsafe_css()
    {
        var result = PxaRichTextSanitizer.Sanitize(
            "<p onclick=\"alert(1)\" style=\"color:red;position:fixed;background-color:url(javascript:alert(1))\">" +
            "Safe<script>alert(1)</script><iframe src=\"https://example.test\" /></p>");

        Assert.Equal("<p style=\"color:red\">Safe</p>", result);
    }

    [Theory]
    [InlineData("javascript:alert%281%29")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("file:///etc/passwd")]
    [InlineData("vbscript:msgbox%281%29")]
    [InlineData("//attacker.example/path")]
    public void Sanitizer_removes_unsafe_link_targets(string target)
    {
        var result = PxaRichTextSanitizer.Sanitize($"<p><a href=\"{target}\">open</a></p>");

        Assert.Equal("<p><a>open</a></p>", result);
    }

    [Fact]
    public void Sanitizer_preserves_supported_formatting_safe_links_and_html_void_elements()
    {
        var result = PxaRichTextSanitizer.Sanitize(
            "<div style=\"text-align:right;direction:rtl\"><strong>Safe</strong><br>" +
            "<a href=\"https://example.com\" target=\"_blank\">docs</a></div>");

        Assert.Contains("style=\"text-align:right;direction:rtl\"", result);
        Assert.Contains("<strong>Safe</strong><br />", result);
        Assert.Contains("href=\"https://example.com\"", result);
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", result);
    }

    [Fact]
    public void Malformed_html_is_encoded_as_text_instead_of_interpreted()
    {
        var result = PxaRichTextSanitizer.Sanitize("<p><strong>unfinished<script>alert(1)</script>");

        Assert.StartsWith("&lt;p&gt;", result);
        Assert.DoesNotContain("<script>", result, StringComparison.OrdinalIgnoreCase);
    }
}
