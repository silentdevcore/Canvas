using System.Text;

namespace PXA.Core.Primitives;

public static class ExportFileNameSanitizer
{
    public static string Sanitize(string? name, string fallback = "document", int maxLength = 180)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);

        var source = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
        var result = new StringBuilder(Math.Min(source.Length, maxLength));
        var separatorPending = false;

        foreach (var character in source)
        {
            if (char.IsWhiteSpace(character) || invalid.Contains(character) || char.IsControl(character))
            {
                separatorPending = result.Length > 0;
                continue;
            }

            if (separatorPending && result.Length < maxLength)
                result.Append('-');

            separatorPending = false;
            if (result.Length < maxLength)
                result.Append(character);
        }

        var safe = result.ToString().Trim('-', '.');
        return string.IsNullOrWhiteSpace(safe)
            ? SanitizeFallback(fallback, maxLength)
            : safe;
    }

    private static string SanitizeFallback(string fallback, int maxLength)
    {
        var safe = new string(fallback
            .Where(character => !Path.GetInvalidFileNameChars().Contains(character) &&
                !"<>:\"/\\|?*".Contains(character) &&
                !char.IsControl(character))
            .Take(maxLength)
            .ToArray())
            .Trim('-', '.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? "document"[..Math.Min(8, maxLength)] : safe;
    }
}
