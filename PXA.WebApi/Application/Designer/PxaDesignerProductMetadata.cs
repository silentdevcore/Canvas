using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PXA.WebApi.Application.Designer;

public sealed class PxaDesignerProductMetadata
{
    private static readonly Regex SemanticVersion = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public PxaDesignerProductMetadata()
    {
        Features = Read<DesignerFeatureManifest>("PXA.ProductMetadata.designer-features.json");
        Releases = Read<DesignerReleaseManifest>("PXA.ProductMetadata.pxa-releases.json");
        Validate();
    }

    public DesignerFeatureManifest Features { get; }
    public DesignerReleaseManifest Releases { get; }

    public DesignerFeatureDefinition? FindFeature(string featureId) =>
        Features.Features.SingleOrDefault(value =>
            string.Equals(value.Id, featureId, StringComparison.OrdinalIgnoreCase));

    public DesignerReleaseDefinition? FindRelease(string version) =>
        Releases.Releases.SingleOrDefault(value =>
            string.Equals(value.Version, version, StringComparison.OrdinalIgnoreCase));

    private static T Read<T>(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded product metadata '{resourceName}' was not found.");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Embedded product metadata '{resourceName}' is invalid.");
    }

    private void Validate()
    {
        if (Features.SchemaVersion != 1 || Releases.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported Designer product metadata schema.");
        if (Features.Features.Select(value => value.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Features.Features.Count)
            throw new InvalidOperationException("Designer feature IDs must be unique.");
        if (Releases.Releases.Select(value => value.Version).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Releases.Releases.Count)
            throw new InvalidOperationException("Designer release versions must be unique.");
        if (Releases.Releases.Any(value => !SemanticVersion.IsMatch(value.Version)))
            throw new InvalidOperationException("Every Designer release must use Semantic Versioning.");
        if (Features.Features.Any(value =>
                !SemanticVersion.IsMatch(value.IntroducedIn) ||
                (value.NewUntilVersion is not null && !SemanticVersion.IsMatch(value.NewUntilVersion))))
            throw new InvalidOperationException("Designer feature version markers must use Semantic Versioning.");
        if (Features.Features.Any(value =>
                value.Maturity is not ("alpha" or "beta" or "stable")))
            throw new InvalidOperationException("Designer feature maturity must be Alpha, Beta, or Stable.");
        if (Releases.Releases.Any(value =>
                value.Channel is not ("alpha" or "beta" or "stable")))
            throw new InvalidOperationException("Designer release channel must be Alpha, Beta, or Stable.");
        if (Releases.Releases.Any(value => value.Components.Count == 0))
            throw new InvalidOperationException("Every PXA release must identify its affected components.");
        if (Releases.Releases.Zip(Releases.Releases.Skip(1))
            .Any(pair => pair.First.PublishedAt < pair.Second.PublishedAt))
            throw new InvalidOperationException("Designer releases must be ordered newest first.");
        var featureIds = Features.Features.Select(value => value.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Releases.Releases.SelectMany(value => value.FeatureIds)
            .Any(featureId => !featureIds.Contains(featureId)))
            throw new InvalidOperationException("Designer releases may reference only registered feature IDs.");
    }
}

public sealed record DesignerFeatureManifest(
    int SchemaVersion,
    string Product,
    IReadOnlyList<DesignerFeatureDefinition> Features);

public sealed record DesignerFeatureDefinition(
    string Id,
    string TitleKey,
    string DescriptionKey,
    string FallbackTitle,
    string FallbackDescription,
    string Maturity,
    string IntroducedIn,
    string? NewUntilVersion,
    bool DefaultEnabled,
    string? RequiredEntitlement,
    string DocumentationPath);

public sealed record DesignerReleaseManifest(
    int SchemaVersion,
    string Product,
    IReadOnlyList<DesignerReleaseDefinition> Releases);

public sealed record DesignerReleaseDefinition(
    string Version,
    DateOnly PublishedAt,
    string Channel,
    string Title,
    string Summary,
    string DocumentationPath,
    IReadOnlyList<string> Components,
    IReadOnlyList<string> FeatureIds,
    DesignerReleaseChanges Changes);

public sealed record DesignerReleaseChanges(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Improved,
    IReadOnlyList<string> Fixed,
    IReadOnlyList<string> Security,
    IReadOnlyList<string> Deprecated,
    IReadOnlyList<string> Breaking);
