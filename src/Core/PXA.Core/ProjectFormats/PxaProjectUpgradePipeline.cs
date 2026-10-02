using System.Text.Json;
using System.Text.Json.Nodes;

namespace PXA.Core.ProjectFormats;

public interface IPxaProjectUpgradeStep
{
    PxaProjectKind Kind { get; }
    string FromVersion { get; }
    string ToVersion { get; }
    PxaProjectUpgradeStepResult Apply(JsonObject source);
}

public sealed record PxaProjectUpgradeStepResult(
    JsonObject Document,
    bool IsLossy = false,
    IReadOnlyList<PxaProjectDiagnostic>? Diagnostics = null);

public sealed class PxaProjectUpgradeReport
{
    public string SourceVersion { get; init; } = "";
    public string TargetVersion { get; init; } = "";
    public string OriginalPackageSha256 { get; set; } = "";
    public bool IsLossy { get; init; }
    public List<PxaProjectUpgradeStepInfo> Steps { get; init; } = [];
    public List<PxaProjectDiagnostic> Diagnostics { get; init; } = [];
}

public sealed record PxaProjectUpgradeStepInfo(string FromVersion, string ToVersion, bool IsLossy);

public sealed record PxaProjectUpgradeResult(byte[] DocumentBytes, PxaProjectUpgradeReport? Report);

public static class PxaProjectUpgradePipeline
{
    // Add reviewed production migrations here when a document schema actually changes.
    private static readonly IReadOnlyList<IPxaProjectUpgradeStep> BuiltInSteps = [];

    public static bool CanUpgrade(PxaProjectKind kind, string sourceVersion, string targetVersion) =>
        TryBuildPath(kind, sourceVersion, targetVersion, BuiltInSteps, out _);

    public static PxaProjectUpgradeResult Upgrade(
        PxaProjectKind kind,
        string sourceVersion,
        string targetVersion,
        ReadOnlySpan<byte> documentBytes,
        int maximumJsonDepth,
        IReadOnlyCollection<IPxaProjectUpgradeStep>? steps = null)
    {
        if (!PxaProjectVersionPolicy.TryParseSchemaVersion(sourceVersion, out var source) ||
            !PxaProjectVersionPolicy.TryParseSchemaVersion(targetVersion, out var target))
            throw new PxaProjectFormatException("PXAPROJECT008", "The project upgrade version is invalid.");
        if (sourceVersion == targetVersion)
            return new(documentBytes.ToArray(), null);
        if (source.Major == target.Major && source.Minor < target.Minor)
            return new(documentBytes.ToArray(), null);

        var availableSteps = steps ?? BuiltInSteps;
        if (!TryBuildPath(kind, sourceVersion, targetVersion, availableSteps, out var path))
            throw new PxaProjectFormatException(
                "PXAPROJECT008",
                $"No safe {kind} project upgrade path exists from schema {sourceVersion} to {targetVersion}.");

        JsonObject node;
        try
        {
            node = JsonNode.Parse(documentBytes, documentOptions: new JsonDocumentOptions
            {
                MaxDepth = maximumJsonDepth,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
            }) as JsonObject ?? throw new PxaProjectFormatException(
                "PXAPROJECT009", "The project document must be a JSON object before it can be upgraded.");
        }
        catch (JsonException)
        {
            throw new PxaProjectFormatException("PXAPROJECT009", "The project contains invalid upgrade JSON.");
        }
        var report = new PxaProjectUpgradeReport
        {
            SourceVersion = sourceVersion,
            TargetVersion = targetVersion,
        };

        foreach (var step in path)
        {
            var protectedInput = node.DeepClone().AsObject();
            var inputFingerprint = PxaCanonicalJson.Serialize(protectedInput);
            var result = step.Apply(protectedInput.DeepClone().AsObject())
                ?? throw new PxaProjectFormatException("PXAPROJECT015", "A project upgrade step returned no result.");
            var verification = step.Apply(protectedInput.DeepClone().AsObject())
                ?? throw new PxaProjectFormatException("PXAPROJECT015", "A project upgrade step returned no result.");
            if (!inputFingerprint.AsSpan().SequenceEqual(PxaCanonicalJson.Serialize(protectedInput)))
                throw new PxaProjectFormatException("PXAPROJECT015", "A project upgrade step modified its source document.");
            if (!PxaCanonicalJson.Serialize(result).AsSpan().SequenceEqual(PxaCanonicalJson.Serialize(verification)))
                throw new PxaProjectFormatException("PXAPROJECT015", "A project upgrade step is not deterministic.");

            node = result.Document ?? throw new PxaProjectFormatException(
                "PXAPROJECT015", "A project upgrade step returned no document.");
            var diagnostics = result.Diagnostics ?? [];
            report.Steps.Add(new(step.FromVersion, step.ToVersion, result.IsLossy));
            report.Diagnostics.AddRange(diagnostics);
            if (result.IsLossy && !diagnostics.Any(item => item.Severity == "warning" || item.Severity == "error"))
                report.Diagnostics.Add(new PxaProjectDiagnostic
                {
                    Code = "PXAPROJECT_UPGRADE_LOSSY",
                    Severity = "warning",
                    Message = $"The upgrade from {step.FromVersion} to {step.ToVersion} simplified unsupported data.",
                    Source = "document",
                });
        }

        report.Diagnostics.Insert(0, new PxaProjectDiagnostic
        {
            Code = "PXAPROJECT_UPGRADED",
            Severity = "warning",
            Message = $"The project was upgraded in memory from schema {sourceVersion} to {targetVersion}. " +
                      "The original file remains unchanged until the upgraded project is saved.",
            Source = "document",
        });
        report = new PxaProjectUpgradeReport
        {
            SourceVersion = report.SourceVersion,
            TargetVersion = report.TargetVersion,
            IsLossy = report.Steps.Any(item => item.IsLossy),
            Steps = report.Steps,
            Diagnostics = report.Diagnostics,
        };
        return new(PxaCanonicalJson.Serialize(node), report);
    }

    private static bool TryBuildPath(
        PxaProjectKind kind,
        string sourceVersion,
        string targetVersion,
        IReadOnlyCollection<IPxaProjectUpgradeStep> steps,
        out IReadOnlyList<IPxaProjectUpgradeStep> path)
    {
        path = [];
        if (!PxaProjectVersionPolicy.TryParseSchemaVersion(sourceVersion, out var source) ||
            !PxaProjectVersionPolicy.TryParseSchemaVersion(targetVersion, out var target) ||
            source.CompareTo(target) >= 0)
            return false;

        var current = sourceVersion;
        var result = new List<IPxaProjectUpgradeStep>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { current };
        while (current != targetVersion)
        {
            var candidates = steps.Where(step => step.Kind == kind && step.FromVersion == current).ToArray();
            if (candidates.Length != 1 ||
                !PxaProjectVersionPolicy.TryParseSchemaVersion(candidates[0].ToVersion, out var next) ||
                !PxaProjectVersionPolicy.TryParseSchemaVersion(current, out var currentParsed) ||
                next.CompareTo(currentParsed) <= 0 || next.CompareTo(target) > 0 ||
                !visited.Add(candidates[0].ToVersion))
                return false;
            result.Add(candidates[0]);
            current = candidates[0].ToVersion;
        }
        path = result;
        return true;
    }
}
