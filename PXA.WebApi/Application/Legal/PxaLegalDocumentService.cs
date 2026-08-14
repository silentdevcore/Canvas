using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Application.Legal;

public sealed class PxaLegalDocumentService(PxaDbContext dbContext)
{
    public IQueryable<LegalDocumentVersion> EffectiveVersions(DateTimeOffset now) =>
        dbContext.LegalDocumentVersions
            .Where(value =>
                (value.Status == LegalDocumentStatus.Published ||
                 value.Status == LegalDocumentStatus.Scheduled) &&
                value.EffectiveAt != null &&
                value.EffectiveAt <= now);

    public async Task<LegalDocumentVersion?> FindCurrentAsync(
        LegalDocumentType type,
        string locale,
        LegalDocumentAudience audience,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedLocale = NormalizeLocale(locale);
        var query =
            from version in EffectiveVersions(now)
            join document in dbContext.LegalDocuments on version.LegalDocumentId equals document.Id
            where document.Type == type &&
                  version.Audience == audience
            orderby version.Locale == normalizedLocale descending,
                version.Locale == "en" descending,
                version.EffectiveAt descending,
                version.PublishedAt descending
            select version;

        return await query.FirstOrDefaultAsync(value =>
                value.Locale == normalizedLocale || value.Locale == "en",
            cancellationToken);
    }

    public static string NormalizeMarkdown(string markdown) =>
        markdown.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    public static string ComputeHash(string markdown)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeMarkdown(markdown)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string RenderSafeHtml(string markdown)
    {
        var lines = NormalizeMarkdown(markdown).Split('\n');
        var result = new StringBuilder();
        var paragraph = new List<string>();
        var inList = false;

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
                return;
            result.Append("<p>")
                .Append(string.Join("<br>", paragraph.Select(WebUtility.HtmlEncode)))
                .Append("</p>");
            paragraph.Clear();
        }

        void CloseList()
        {
            if (!inList)
                return;
            result.Append("</ul>");
            inList = false;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                FlushParagraph();
                CloseList();
                continue;
            }

            var headingLevel = line.StartsWith("### ", StringComparison.Ordinal) ? 3 :
                line.StartsWith("## ", StringComparison.Ordinal) ? 2 :
                line.StartsWith("# ", StringComparison.Ordinal) ? 1 : 0;
            if (headingLevel > 0)
            {
                FlushParagraph();
                CloseList();
                result.Append("<h").Append(headingLevel).Append('>')
                    .Append(WebUtility.HtmlEncode(line[(headingLevel + 1)..]))
                    .Append("</h").Append(headingLevel).Append('>');
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                FlushParagraph();
                if (!inList)
                {
                    result.Append("<ul>");
                    inList = true;
                }
                result.Append("<li>").Append(WebUtility.HtmlEncode(line[2..])).Append("</li>");
                continue;
            }

            CloseList();
            paragraph.Add(line);
        }

        FlushParagraph();
        CloseList();
        return result.ToString();
    }

    public static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return "en";
        var normalized = locale.Trim().ToLowerInvariant().Split('-', '_')[0];
        return normalized is "de" or "en" ? normalized : "en";
    }
}
