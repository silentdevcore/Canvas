using System.Reflection;
using System.Text.Json;

namespace PXA.WebApi.Application.Compliance;

public sealed class PxaDependencyComplianceCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public PxaDependencyComplianceCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "PXA.ProductMetadata.dependency-compliance.json")
            ?? throw new InvalidOperationException("Embedded dependency compliance metadata was not found.");
        Status = JsonSerializer.Deserialize<PxaDependencyComplianceResponse>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Embedded dependency compliance metadata is invalid.");
        Validate();
    }

    public PxaDependencyComplianceResponse Status { get; }

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

public sealed record PxaDependencyComplianceResponse(
    int SchemaVersion,
    bool ProductionReady,
    PxaVulnerabilityPolicy VulnerabilityPolicy,
    PxaSbomPolicy Sbom,
    IReadOnlyList<PxaDependencyLicenseDecision> LicenseDecisions);

public sealed record PxaVulnerabilityPolicy(
    PxaPackageVulnerabilityPolicy Nuget,
    PxaPackageVulnerabilityPolicy Npm);

public sealed record PxaPackageVulnerabilityPolicy(
    string Scope,
    string MaximumAllowedSeverity,
    bool CiGate);

public sealed record PxaSbomPolicy(
    string Format,
    bool CiGate,
    IReadOnlyList<string> Artifacts);

public sealed record PxaDependencyLicenseDecision(
    string Id,
    string Package,
    string Version,
    string Usage,
    string License,
    string Status,
    bool ProductionApproved,
    string RequiredAction);
