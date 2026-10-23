using System.Reflection;
using System.Text.Json;

namespace PVAD.WebApi.Application.Legal;

public sealed class PvadLegalContentCatalog
{
    private const string ResourcePrefix = "PVAD.LegalContent.";
    private readonly Assembly assembly = typeof(PvadLegalContentCatalog).Assembly;
    private readonly Lazy<PvadLegalCandidateCatalog> catalog;

    public PvadLegalContentCatalog()
    {
        catalog = new Lazy<PvadLegalCandidateCatalog>(Load);
    }

    public PvadLegalCandidateCatalog Current => catalog.Value;

    private PvadLegalCandidateCatalog Load()
    {
        var manifest = JsonSerializer.Deserialize<PvadLegalCandidateManifest>(
            Read("manifest.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The Legal candidate manifest is invalid.");
        if (!string.Equals(manifest.AuthoritativeLocale, "en", StringComparison.Ordinal))
            throw new InvalidOperationException("English must be the authoritative Legal locale.");

        var documents = manifest.Documents.Select(value => new PvadLegalCandidateDocument(
            value.Key,
            value.Type,
            value.DisplayName,
            manifest.AuthoritativeLocale,
            value.Audience,
            value.Version,
            value.RequiresAcceptance,
            Read(value.File))).ToArray();
        return new PvadLegalCandidateCatalog(
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

public sealed record PvadLegalCandidateCatalog(
    int SchemaVersion,
    string AuthoritativeLocale,
    string GoverningLaw,
    IReadOnlyList<PvadLegalCandidateDocument> Documents);

public sealed record PvadLegalCandidateDocument(
    string Key,
    string Type,
    string DisplayName,
    string Locale,
    string Audience,
    string Version,
    bool RequiresAcceptance,
    string SourceMarkdown);

internal sealed record PvadLegalCandidateManifest(
    int SchemaVersion,
    string AuthoritativeLocale,
    string GoverningLaw,
    IReadOnlyList<PvadLegalCandidateManifestDocument> Documents);

internal sealed record PvadLegalCandidateManifestDocument(
    string Key,
    string Type,
    string DisplayName,
    string File,
    string Audience,
    string Version,
    bool RequiresAcceptance);
