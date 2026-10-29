using System.Reflection;
using System.Text.Json;

namespace PVAD.WebApi.Application.Compliance;

public sealed class PvadDependencyComplianceCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public PvadDependencyComplianceCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "PVAD.ProductMetadata.dependency-compliance.json")
            ?? throw new InvalidOperationException("Embedded dependency compliance metadata was not found.");
        Status = JsonSerializer.Deserialize<PvadDependencyComplianceResponse>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Embedded dependency compliance metadata is invalid.");
        Validate();
    }

    public PvadDependencyComplianceResponse Status { get; }

    private void Validate()
    {
        if (Status.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported dependency compliance schema.");
        if (Status.Sbom.Artifacts.Count == 0 || Status.LicenseDecisions.Count == 0)
            throw new InvalidOperationException("Dependency compliance metadata is incomplete.");
        if (Status.ProductionReady == Status.LicenseDecisions.Any(value => !value.ProductionApproved))
            throw new InvalidOperationException("Dependency production readiness contradicts license decisions.");
        if (Status.LicenseDecisions.Select(value => value.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != Status.LicenseDecisions.Count)
            throw new InvalidOperationException("Dependency license decision IDs must be unique.");
    }
}

public sealed record PvadDependencyComplianceResponse(
    int SchemaVersion,
    bool ProductionReady,
    PvadVulnerabilityPolicy VulnerabilityPolicy,
    PvadSbomPolicy Sbom,
    IReadOnlyList<PvadDependencyLicenseDecision> LicenseDecisions);

public sealed record PvadVulnerabilityPolicy(
    PvadPackageVulnerabilityPolicy Nuget,
    PvadPackageVulnerabilityPolicy Npm);

public sealed record PvadPackageVulnerabilityPolicy(
    string Scope,
    string MaximumAllowedSeverity,
    bool CiGate);

public sealed record PvadSbomPolicy(
    string Format,
    string Version,
    bool CiGate,
    IReadOnlyList<string> Artifacts);

public sealed record PvadDependencyLicenseDecision(
    string Id,
    string Package,
    string Version,
    string Usage,
    string License,
    string Status,
    bool ProductionApproved,
    string RequiredAction);
