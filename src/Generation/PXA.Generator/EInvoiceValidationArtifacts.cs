using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Xsl;

namespace PXA.Generator;

public sealed record PxaEInvoiceValidationArtifact(
    string Path,
    string Sha256);

public sealed record PxaEInvoiceValidationArtifactManifest
{
    public int SchemaVersion { get; init; } = 1;
    public required string ArtifactSetId { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter<PxaEInvoiceStandard>))]
    public required PxaEInvoiceStandard Standard { get; init; }
    public required string StandardVersion { get; init; }
    public required string Profile { get; init; }
    public required string Syntax { get; init; }
    public required Uri SourceUri { get; init; }
    public required string License { get; init; }
    public required IReadOnlyList<PxaEInvoiceValidationArtifact> XmlSchemas { get; init; }
    public required PxaEInvoiceValidationArtifact SchematronXslt { get; init; }
}

public interface IPxaEInvoiceConformanceValidator
{
    PxaEInvoiceValidationReport Validate(
        ReadOnlyMemory<byte> xml,
        PxaEInvoiceOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates invoice XML using a hash-pinned local XSD and precompiled Schematron
/// XSLT bundle. The validator never downloads validation material.
/// </summary>
public sealed class PxaEInvoiceConformanceValidator : IPxaEInvoiceConformanceValidator
{
    private const int MaximumValidationOutputBytes = 4 * 1024 * 1024;
    private readonly string _artifactRoot;
    private readonly PxaEInvoiceValidationArtifactManifest _manifest;

    public PxaEInvoiceConformanceValidator(
        string artifactRoot,
        PxaEInvoiceValidationArtifactManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactRoot);
        ArgumentNullException.ThrowIfNull(manifest);
        _artifactRoot = Path.GetFullPath(artifactRoot);
        _manifest = manifest;
    }

    public PxaEInvoiceValidationArtifactManifest Manifest => _manifest;

    public static PxaEInvoiceConformanceValidator FromManifest(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var fullManifestPath = Path.GetFullPath(manifestPath);
        var manifestBytes = File.ReadAllBytes(fullManifestPath);
        if (manifestBytes.Length > 1024 * 1024)
            throw new InvalidDataException("The validation artifact manifest is too large.");

        var manifest = JsonSerializer.Deserialize<PxaEInvoiceValidationArtifactManifest>(
            manifestBytes,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            }) ?? throw new InvalidDataException("The validation artifact manifest is empty.");

        return new PxaEInvoiceConformanceValidator(
            Path.GetDirectoryName(fullManifestPath)!,
            manifest);
    }

    public PxaEInvoiceValidationReport Validate(
        ReadOnlyMemory<byte> xml,
        PxaEInvoiceOptions options,
        CancellationToken cancellationToken = default)
    {
        var structural = EInvoice.Validate(xml, options);
        if (!structural.IsStructurallyValid)
            return structural;

        var diagnostics = structural.Diagnostics
            .Where(static item => item.Code != "PXAEINV000")
            .ToList();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateManifest(options, structural.Syntax);
            var files = VerifyArtifacts();
            ValidateXsd(xml, files, diagnostics, cancellationToken);
            if (diagnostics.Any(static item => item.Severity == "error"))
                return Report(structural, diagnostics, "invalid");

            ValidateSchematron(xml, files, diagnostics, cancellationToken);
            if (diagnostics.Any(static item => item.Severity == "error"))
                return Report(structural, diagnostics, "invalid");

            diagnostics.Add(new PxaEInvoiceDiagnostic(
                "PXAEINV019",
                "information",
                $"XSD and Schematron validation passed with artifact set '{_manifest.ArtifactSetId}'."));
            return Report(structural, diagnostics, "valid", true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArtifactValidationException exception)
        {
            diagnostics.Add(new PxaEInvoiceDiagnostic(exception.Code, "error", exception.Message));
            return Report(structural, diagnostics, "unavailable");
        }
        catch (Exception exception) when (exception is XmlException or XmlSchemaException or XsltException or IOException)
        {
            diagnostics.Add(new PxaEInvoiceDiagnostic(
                "PXAEINV025",
                "error",
                "The pinned electronic-invoice validation rules could not be executed safely."));
            return Report(structural, diagnostics, "unavailable");
        }
    }

    private void ValidateManifest(PxaEInvoiceOptions options, string? syntax)
    {
        if (_manifest.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(_manifest.ArtifactSetId) ||
            _manifest.ArtifactSetId.Length > 128 ||
            string.IsNullOrWhiteSpace(_manifest.StandardVersion) ||
            string.IsNullOrWhiteSpace(_manifest.Profile) ||
            string.IsNullOrWhiteSpace(_manifest.Syntax))
            throw ArtifactError("PXAEINV021", "The validation artifact manifest is invalid.");
        if (_manifest.Standard != options.Standard ||
            !string.Equals(_manifest.StandardVersion, options.Version.Trim(), StringComparison.Ordinal) ||
            !string.Equals(Normalize(_manifest.Profile), Normalize(options.Profile), StringComparison.Ordinal) ||
            !string.Equals(_manifest.Syntax, syntax, StringComparison.Ordinal))
        {
            throw ArtifactError("PXAEINV026", "The validation artifact set does not match the requested standard, version, profile, and syntax.");
        }
        if (_manifest.SourceUri is null || !_manifest.SourceUri.IsAbsoluteUri ||
            string.IsNullOrWhiteSpace(_manifest.License) || _manifest.License.Length > 512)
            throw ArtifactError("PXAEINV021", "The validation artifact source and license are required.");
        if (_manifest.XmlSchemas is null || _manifest.XmlSchemas.Count == 0 || _manifest.SchematronXslt is null)
            throw ArtifactError("PXAEINV021", "At least one XML schema is required.");
    }

    private IReadOnlyDictionary<string, string> VerifyArtifacts()
    {
        var artifacts = _manifest.XmlSchemas.Append(_manifest.SchematronXslt).ToArray();
        if (artifacts.Select(static artifact => artifact.Path).Distinct(StringComparer.Ordinal).Count() != artifacts.Length)
            throw ArtifactError("PXAEINV021", "Validation artifact paths must be unique.");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var artifact in artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.Path) ||
                Path.IsPathRooted(artifact.Path) ||
                artifact.Path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
            {
                throw ArtifactError("PXAEINV021", "Validation artifact paths must remain inside the artifact directory.");
            }

            var fullPath = Path.GetFullPath(Path.Combine(_artifactRoot, artifact.Path));
            if (!fullPath.StartsWith(_artifactRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                !File.Exists(fullPath))
            {
                throw ArtifactError("PXAEINV020", "A pinned validation artifact is unavailable.");
            }
            if (File.ResolveLinkTarget(fullPath, returnFinalTarget: true) is not null)
                throw ArtifactError("PXAEINV021", "Validation artifacts cannot be symbolic links.");

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length > EInvoice.MaximumXmlBytes)
                throw ArtifactError("PXAEINV021", "A validation artifact exceeds the configured size limit.");
            using var artifactStream = File.OpenRead(fullPath);
            var actualHash = Convert.ToHexString(SHA256.HashData(artifactStream)).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(artifact.Sha256) || artifact.Sha256.Trim().Length != 64 ||
                !string.Equals(actualHash, artifact.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw ArtifactError("PXAEINV022", "A validation artifact failed SHA-256 verification.");
            files[artifact.Path] = fullPath;
        }
        return files;
    }

    private void ValidateXsd(
        ReadOnlyMemory<byte> xml,
        IReadOnlyDictionary<string, string> files,
        ICollection<PxaEInvoiceDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var resolver = new PinnedArtifactXmlResolver(files.Values);
        var schemas = new XmlSchemaSet { XmlResolver = resolver };
        foreach (var schemaArtifact in _manifest.XmlSchemas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var schemaReader = XmlReader.Create(files[schemaArtifact.Path], SecureReaderSettings(resolver));
            schemas.Add(null, schemaReader);
        }
        schemas.Compile();

        var settings = SecureReaderSettings(null);
        settings.Schemas = schemas;
        settings.ValidationType = ValidationType.Schema;
        settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, args) => diagnostics.Add(new PxaEInvoiceDiagnostic(
            "PXAEINV023",
            args.Severity == XmlSeverityType.Error ? "error" : "warning",
            args.Message));

        using var input = new MemoryStream(xml.ToArray(), writable: false);
        using var reader = XmlReader.Create(input, settings);
        while (reader.Read())
            cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateSchematron(
        ReadOnlyMemory<byte> xml,
        IReadOnlyDictionary<string, string> files,
        ICollection<PxaEInvoiceDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var transform = new XslCompiledTransform();
        using (var stylesheet = XmlReader.Create(
            files[_manifest.SchematronXslt.Path],
            SecureReaderSettings(null)))
        {
            transform.Load(stylesheet, new XsltSettings(false, false), null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var input = new MemoryStream(xml.ToArray(), writable: false);
        using var inputReader = XmlReader.Create(input, SecureReaderSettings(null));
        using var output = new LimitedMemoryStream(MaximumValidationOutputBytes);
        using (var writer = XmlWriter.Create(output, transform.OutputSettings))
            transform.Transform(inputReader, null, writer);

        output.Position = 0;
        using var resultReader = XmlReader.Create(output, SecureReaderSettings(null));
        var result = XDocument.Load(resultReader, LoadOptions.None);
        foreach (var failedAssert in result.Descendants().Where(static element => element.Name.LocalName == "failed-assert"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var location = failedAssert.Attribute("location")?.Value;
            var message = failedAssert.Value.Trim();
            diagnostics.Add(new PxaEInvoiceDiagnostic(
                "PXAEINV024",
                "error",
                string.IsNullOrWhiteSpace(location) ? message : $"{message} ({location})"));
        }
    }

    private PxaEInvoiceValidationReport Report(
        PxaEInvoiceValidationReport structural,
        IReadOnlyList<PxaEInvoiceDiagnostic> diagnostics,
        string status,
        bool conformanceValidated = false) =>
        structural with
        {
            IsConformanceValidated = conformanceValidated,
            Diagnostics = diagnostics,
            ConformanceStatus = status,
            ValidatorArtifactSetId = _manifest.ArtifactSetId,
            ValidatorArtifactSetSha256 = TryComputeArtifactSetHash()
        };

    private string? TryComputeArtifactSetHash()
    {
        if (_manifest.SourceUri is null || _manifest.XmlSchemas is null || _manifest.SchematronXslt is null ||
            _manifest.XmlSchemas.Any(static item => string.IsNullOrWhiteSpace(item.Path) || string.IsNullOrWhiteSpace(item.Sha256)) ||
            string.IsNullOrWhiteSpace(_manifest.SchematronXslt.Path) ||
            string.IsNullOrWhiteSpace(_manifest.SchematronXslt.Sha256))
        {
            return null;
        }
        var canonical = new StringBuilder()
            .Append(_manifest.SchemaVersion).Append('\n')
            .Append(_manifest.ArtifactSetId).Append('\n')
            .Append(_manifest.Standard).Append('\n')
            .Append(_manifest.StandardVersion).Append('\n')
            .Append(Normalize(_manifest.Profile)).Append('\n')
            .Append(_manifest.Syntax).Append('\n')
            .Append(_manifest.SourceUri.AbsoluteUri).Append('\n')
            .Append(_manifest.License).Append('\n');
        foreach (var artifact in _manifest.XmlSchemas.Append(_manifest.SchematronXslt)
                     .OrderBy(static item => item.Path, StringComparer.Ordinal))
        {
            canonical.Append(artifact.Path).Append('|')
                .Append(artifact.Sha256.Trim().ToLowerInvariant()).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static XmlReaderSettings SecureReaderSettings(XmlResolver? resolver) => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = resolver,
        MaxCharactersInDocument = EInvoice.MaximumXmlBytes,
        MaxCharactersFromEntities = 0
    };

    private static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant().Replace('_', ' ') ?? string.Empty;

    private static ArtifactValidationException ArtifactError(string code, string message) =>
        new(code, message);

    private sealed class ArtifactValidationException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    private sealed class PinnedArtifactXmlResolver : XmlResolver
    {
        private readonly HashSet<string> _allowedPaths;

        public PinnedArtifactXmlResolver(IEnumerable<string> allowedPaths) =>
            _allowedPaths = allowedPaths.Select(Path.GetFullPath).ToHashSet(StringComparer.Ordinal);

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (!absoluteUri.IsFile)
                throw new XmlException("Only pinned local validation artifacts may be resolved.");
            var path = Path.GetFullPath(absoluteUri.LocalPath);
            if (!_allowedPaths.Contains(path))
                throw new XmlException("The XML schema referenced an unpinned artifact.");
            return File.OpenRead(path);
        }

        public override System.Net.ICredentials? Credentials { set { } }
    }

    private sealed class LimitedMemoryStream(int maximumBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacityLimit(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacityLimit(buffer.Length);
            base.Write(buffer);
        }

        private void EnsureCapacityLimit(int count)
        {
            if (Position + count > maximumBytes)
                throw new InvalidDataException("PXAEINV025: Schematron output exceeded the configured limit.");
        }
    }
}
