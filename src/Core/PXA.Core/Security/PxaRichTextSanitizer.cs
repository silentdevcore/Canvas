using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PXA.Core.Security;

/// <summary>Sanitizes the bounded rich-text subset supported by PXA Designer.</summary>
public static partial class PxaRichTextSanitizer
{
    public const int MaximumHtmlLength = 1_048_576;

    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "b", "blockquote", "br", "code", "del", "div", "em", "h1", "h2", "h3",
        "h4", "h5", "h6", "hr", "i", "li", "ol", "p", "pre", "s", "span", "strong",
        "sub", "sup", "table", "tbody", "td", "th", "thead", "tr", "u", "ul",
    };

    private static readonly HashSet<string> RemovedWithContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed",
    };

    private static readonly HashSet<string> AllowedStyleProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "background-color", "color", "direction", "font-family", "font-size", "font-style",
        "font-weight", "letter-spacing", "line-height", "text-align", "text-decoration", "white-space",
    };

    private static readonly string[] UnsafeCssTokens =
    {
        "url(", "expression", "@import", "javascript:", "vbscript:", "data:",
        "-moz-binding", "behavior:",
    };

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;
        if (html.Length > MaximumHtmlLength)
            throw new ArgumentException($"Rich text cannot exceed {MaximumHtmlLength} characters.", nameof(html));

        try
        {
            var normalizedEntities = VoidElementRegex().Replace(
                html.Replace("&nbsp;", "&#160;", StringComparison.OrdinalIgnoreCase),
                match => match.Value.EndsWith("/>", StringComparison.Ordinal)
                    ? match.Value
                    : $"{match.Value[..^1]} />");
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumHtmlLength + 32,
            };
            using var textReader = new StringReader($"<root>{normalizedEntities}</root>");
            using var reader = XmlReader.Create(textReader, settings);
            var root = XElement.Load(reader, LoadOptions.PreserveWhitespace);
            SanitizeChildren(root);
            return string.Concat(root.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)));
        }
        catch (XmlException)
        {
            // Malformed or non-XHTML input is rendered as text rather than interpreted as markup.
            return WebUtility.HtmlEncode(html);
        }
    }

    public static string? SanitizeLinkTarget(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var decoded = Uri.UnescapeDataString(normalized);
                if (decoded == normalized)
                    break;
                normalized = decoded;
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        if (normalized.StartsWith('#') || normalized.StartsWith('/') || normalized.StartsWith("./") ||
            normalized.StartsWith("../") || normalized.StartsWith('?'))
            return normalized.StartsWith("//", StringComparison.Ordinal) ? null : value.Trim();

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return uri.Scheme is "http" or "https" or "mailto" ? value.Trim() : null;
        return normalized.Contains(':', StringComparison.Ordinal) ? null : value.Trim();
    }

    private static void SanitizeChildren(XContainer parent)
    {
        foreach (var node in parent.Nodes().ToArray())
        {
            if (node is XComment or XProcessingInstruction)
            {
                node.Remove();
                continue;
            }
            if (node is not XElement element)
                continue;

            var tag = element.Name.LocalName.ToLowerInvariant();
            if (RemovedWithContent.Contains(tag))
            {
                element.Remove();
                continue;
            }

            SanitizeChildren(element);
            if (!AllowedTags.Contains(tag))
            {
                element.ReplaceWith(new XText(element.Value));
                continue;
            }

            element.Name = tag;
            foreach (var attribute in element.Attributes().ToArray())
            {
                var name = attribute.Name.LocalName.ToLowerInvariant();
                var allowed = name is "style" or "dir" or "lang" ||
                              (tag is "td" or "th" && name is "colspan" or "rowspan") ||
                              (tag == "a" && name is "href" or "title" or "target" or "rel");
                if (!allowed || name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    attribute.Remove();
                    continue;
                }
                if (attribute.Name.Namespace != XNamespace.None || attribute.Name.LocalName != name)
                {
                    element.SetAttributeValue(name, attribute.Value);
                    attribute.Remove();
                }
            }

            SanitizeStyle(element);
            if (tag == "a")
                SanitizeAnchor(element);
        }
    }

    private static void SanitizeStyle(XElement element)
    {
        var attribute = element.Attributes().FirstOrDefault(value =>
            value.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase));
        if (attribute is null)
            return;
        var safe = new StringBuilder();
        foreach (var declaration in attribute.Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = declaration.IndexOf(':');
            if (separator <= 0)
                continue;
            var property = declaration[..separator].Trim().ToLowerInvariant();
            var value = declaration[(separator + 1)..].Trim();
            if (!AllowedStyleProperties.Contains(property) || string.IsNullOrEmpty(value) ||
                UnsafeCssTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (safe.Length > 0)
                safe.Append(';');
            safe.Append(property).Append(':').Append(value);
        }
        if (safe.Length == 0)
            attribute.Remove();
        else
            attribute.Value = safe.ToString();
    }

    private static void SanitizeAnchor(XElement element)
    {
        var href = element.Attributes().FirstOrDefault(value =>
            value.Name.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase));
        if (href is not null)
        {
            var safeHref = SanitizeLinkTarget(href.Value);
            if (safeHref is null)
                href.Remove();
            else
                href.Value = safeHref;
        }

        var target = element.Attributes().FirstOrDefault(value =>
            value.Name.LocalName.Equals("target", StringComparison.OrdinalIgnoreCase));
        if (target?.Value == "_blank")
            element.SetAttributeValue("rel", "noopener noreferrer");
        else
        {
            target?.Remove();
            element.Attribute("rel")?.Remove();
        }
    }

    [GeneratedRegex(@"<(br|hr)(?:\s[^<>]*?)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 100)]
    private static partial Regex VoidElementRegex();
}
