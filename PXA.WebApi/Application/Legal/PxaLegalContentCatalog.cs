using System.Reflection;
using System.Text.Json;

namespace PXA.WebApi.Application.Legal;

public sealed class PxaLegalContentCatalog
{
    private const string ResourcePrefix = "PXA.LegalContent.";
    private readonly Assembly assembly = typeof(PxaLegalContentCatalog).Assembly;
    private readonly Lazy<PxaLegalCandidateCatalog> catalog;

    public PxaLegalContentCatalog()
    {
        catalog = new Lazy<PxaLegalCandidateCatalog>(Load);
    }

    public PxaLegalCandidateCatalog Current => catalog.Value;

    private PxaLegalCandidateCatalog Load()
    {
        var manifest = JsonSerializer.Deserialize<PxaLegalCandidateManifest>(
            Read("manifest.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The Legal candidate manifest is invalid.");
        if (!string.Equals(manifest.AuthoritativeLocale, "en", StringComparison.Ordinal))
            throw new InvalidOperationException("English must be the authoritative Legal locale.");

        var documents = manifest.Documents.Select(value => new PxaLegalCandidateDocument(
            value.Key,
            value.Type,
            value.DisplayName,
            manifest.AuthoritativeLocale,
            value.Audience,
            value.Version,
            value.RequiresAcceptance,
            Read(value.File))).ToArray();
        return new PxaLegalCandidateCatalog(
            manifest.SchemaVersion,
            manifest.AuthoritativeLocale,
            manifest.GoverningLaw,
            documents);
    }

    private string Read(string filename)
    {
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + filename)
            ?? throw new InvalidOperationException($"Embedded Legal content '{filename}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

public sealed record PxaLegalCandidateCatalog(
    int SchemaVersion,
    string AuthoritativeLocale,
    string GoverningLaw,
    IReadOnlyList<PxaLegalCandidateDocument> Documents);

public sealed record PxaLegalCandidateDocument(
    string Key,
    string Type,
    string DisplayName,
    string Locale,
    string Audience,
    string Version,
    bool RequiresAcceptance,
    string SourceMarkdown);

internal sealed record PxaLegalCandidateManifest(
    int SchemaVersion,
    string AuthoritativeLocale,
    string GoverningLaw,
    IReadOnlyList<PxaLegalCandidateManifestDocument> Documents);

internal sealed record PxaLegalCandidateManifestDocument(
    string Key,
    string Type,
    string DisplayName,
    string File,
    string Audience,
    string Version,
    bool RequiresAcceptance);
