using Microsoft.Extensions.Options;
using PXA.Generator;

namespace PXA.WebApi.Services.Pdf;

public sealed class PxaEInvoiceValidationOptions
{
    public const string SectionName = "Pxa:EInvoices:Validation";

    public List<string> ManifestPaths { get; init; } = [];
}

public interface IPxaEInvoiceValidationService
{
    PxaEInvoiceValidationReport Validate(
        ReadOnlyMemory<byte> xml,
        PxaEInvoiceOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Selects an exact, locally configured validation bundle. Missing bundles fall
/// back to structural recognition and never produce a conformance claim.
/// </summary>
public sealed class PxaEInvoiceValidationService : IPxaEInvoiceValidationService
{
    private readonly IReadOnlyList<PxaEInvoiceConformanceValidator> _validators;

    public PxaEInvoiceValidationService(
        IOptions<PxaEInvoiceValidationOptions> options,
        IWebHostEnvironment environment)
    {
        _validators = options.Value.ManifestPaths
            .Select(path => ResolveManifestPath(path, environment.ContentRootPath))
            .Select(PxaEInvoiceConformanceValidator.FromManifest)
            .ToArray();

        var duplicates = _validators
            .GroupBy(static validator => Key(validator.Manifest), StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicates is not null)
            throw new InvalidOperationException($"Duplicate electronic-invoice validation artifact set '{duplicates.Key}'.");
    }

    public PxaEInvoiceValidationReport Validate(
        ReadOnlyMemory<byte> xml,
        PxaEInvoiceOptions options,
        CancellationToken cancellationToken = default)
    {
        var structural = EInvoice.Validate(xml, options);
        if (!structural.IsStructurallyValid)
            return structural;

        var validator = _validators.SingleOrDefault(candidate =>
            candidate.Manifest.Standard == options.Standard &&
            string.Equals(candidate.Manifest.StandardVersion, options.Version.Trim(), StringComparison.Ordinal) &&
            string.Equals(Normalize(candidate.Manifest.Profile), Normalize(options.Profile), StringComparison.Ordinal) &&
            string.Equals(candidate.Manifest.Syntax, structural.Syntax, StringComparison.Ordinal));

        return validator is null
            ? structural
            : validator.Validate(xml, options, cancellationToken);
    }

    private static string ResolveManifestPath(string path, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Electronic-invoice validation manifest paths cannot be empty.");
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, contentRoot);
    }

    private static string Key(PxaEInvoiceValidationArtifactManifest manifest) =>
        $"{manifest.Standard}|{manifest.StandardVersion}|{Normalize(manifest.Profile)}|{manifest.Syntax}";

    private static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant().Replace('_', ' ') ?? string.Empty;
}

internal sealed class StructuralPxaEInvoiceValidationService : IPxaEInvoiceValidationService
{
    public PxaEInvoiceValidationReport Validate(
        ReadOnlyMemory<byte> xml,
        PxaEInvoiceOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return EInvoice.Validate(xml, options);
    }
}
