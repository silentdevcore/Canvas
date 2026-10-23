using System.Globalization;
using System.Text.RegularExpressions;

namespace PVAD.Core.ProjectFormats;

public enum PvadProjectVersionCompatibility
{
    Current,
    BackwardCompatible,
    UpgradeRequired,
    NewerApplicationRequired,
    Unsupported,
}

public sealed record PvadProjectVersionAssessment(
    string SourceVersion,
    string CurrentVersion,
    PvadProjectVersionCompatibility Compatibility,
    bool CanOpen,
    bool RequiresUpgrade,
    string DiagnosticCode,
    string Message);

public static partial class PvadProjectVersionPolicy
{
    public static PvadProjectVersionAssessment AssessSchemaVersion(
        string sourceVersion,
        string currentVersion,
        bool hasMajorUpgradePath = false)
    {
        if (!TryParseSchemaVersion(sourceVersion, out var source) ||
            !TryParseSchemaVersion(currentVersion, out var current))
            return Unsupported(sourceVersion, currentVersion, "The project schema version is invalid.");

        if (source == current)
            return new(sourceVersion, currentVersion, PvadProjectVersionCompatibility.Current,
                true, false, "PXAPROJECT_VERSION_CURRENT", "The project uses the current schema version.");

        if (source.Major == current.Major && source.Minor < current.Minor)
            return new(sourceVersion, currentVersion, PvadProjectVersionCompatibility.BackwardCompatible,
                true, false, "PXAPROJECT_VERSION_COMPATIBLE",
                "The project uses an older backward-compatible minor schema version.");

        if (source.Major == current.Major && source.Minor > current.Minor)
            return new(sourceVersion, currentVersion, PvadProjectVersionCompatibility.NewerApplicationRequired,
                false, false, "PXAPROJECT014", "The project schema requires a newer PVAD version.");

        if (source.Major == current.Major - 1 && hasMajorUpgradePath)
            return new(sourceVersion, currentVersion, PvadProjectVersionCompatibility.UpgradeRequired,
                true, true, "PXAPROJECT_VERSION_UPGRADE_REQUIRED",
                "The project requires an explicit schema upgrade before it can be saved.");

        if (source.Major > current.Major)
            return new(sourceVersion, currentVersion, PvadProjectVersionCompatibility.NewerApplicationRequired,
                false, false, "PXAPROJECT014", "The project schema requires a newer PVAD version.");

        return Unsupported(sourceVersion, currentVersion, "The project schema version is no longer supported.");
    }

    public static bool TryParseSchemaVersion(string value, out PvadProjectSchemaVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var match = SchemaVersionPattern().Match(value);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
            return false;
        version = new PvadProjectSchemaVersion(major, minor);
        return true;
    }

    public static bool IsCanonicalProductVersion(string value)
    {
        return TryParseProductVersion(value, out _);
    }

    public static bool IsProductVersionAtLeast(string currentVersion, string minimumVersion)
    {
        if (!TryParseProductVersion(currentVersion, out var current) ||
            !TryParseProductVersion(minimumVersion, out var minimum))
            return false;
        return current.CompareTo(minimum) >= 0;
    }

    private static bool TryParseProductVersion(string value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var match = ProductVersionPattern().Match(value);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
            return false;
        version = new Version(major, minor, patch);
        return true;
    }

    private static PvadProjectVersionAssessment Unsupported(
        string sourceVersion,
        string currentVersion,
        string message) =>
        new(sourceVersion, currentVersion, PvadProjectVersionCompatibility.Unsupported,
            false, false, "PXAPROJECT008", message);

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex SchemaVersionPattern();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ProductVersionPattern();
}

public readonly record struct PvadProjectSchemaVersion(int Major, int Minor) : IComparable<PvadProjectSchemaVersion>
{
    public int CompareTo(PvadProjectSchemaVersion other)
    {
        var major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }
}
