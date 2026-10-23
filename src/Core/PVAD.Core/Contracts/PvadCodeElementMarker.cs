namespace PVAD.Core.Contracts;

public static class PvadCodeElementMarker
{
    public const string Canonical = "pvad-element-id:";
    public const string Legacy = "pxa-element-id:";

    public static string? Read(string line)
    {
        var marker = Canonical;
        var index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            marker = Legacy;
            index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        }
        if (index < 0) return null;
        var id = line[(index + marker.Length)..].Trim();
        return id.Length is > 0 and <= 200 ? id : null;
    }
}
