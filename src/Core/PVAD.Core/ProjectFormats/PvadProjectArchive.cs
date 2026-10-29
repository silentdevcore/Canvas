using System.Buffers.Binary;
using System.IO.Compression;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using PVAD.Core.Contracts;
using PVAD.Core.Security;

namespace PVAD.Core.ProjectFormats;

public static class PvadProjectArchive
{
    private static readonly DateTimeOffset DeterministicTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] WritePdf(
        DesignExportDto document,
        PvadProjectWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        RejectPdfSecrets(document);
        return Write(PvadProjectKind.Pdf, document, document.Id, document.Name, options, cancellationToken);
    }

    public static byte[] WriteSpreadsheet(
        SpreadsheetDto document,
        PvadProjectWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        RejectSpreadsheetSecrets(document);
        return Write(PvadProjectKind.Spreadsheet, document, document.Id, document.Name, options, cancellationToken);
    }

    public static async Task WritePdfAsync(
        Stream destination,
        DesignExportDto document,
        PvadProjectWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        var package = WritePdf(document, options, cancellationToken);
        await destination.WriteAsync(package, cancellationToken);
    }

    public static async Task WriteSpreadsheetAsync(
        Stream destination,
        SpreadsheetDto document,
        PvadProjectWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        var package = WriteSpreadsheet(document, options, cancellationToken);
        await destination.WriteAsync(package, cancellationToken);
    }

    public static Task WritePdfFileAtomicallyAsync(
        string destinationPath,
        DesignExportDto document,
        PvadProjectWriteOptions? options = null,
        CancellationToken cancellationToken = default) =>
        WriteFileAtomicallyAsync(destinationPath, WritePdf(document, options, cancellationToken), cancellationToken);

    public static Task WriteSpreadsheetFileAtomicallyAsync(
        string destinationPath,
        SpreadsheetDto document,
        PvadProjectWriteOptions? options = null,
        CancellationToken cancellationToken = default) =>
        WriteFileAtomicallyAsync(destinationPath, WriteSpreadsheet(document, options, cancellationToken), cancellationToken);

    public static PvadProjectPackage<DesignExportDto> ReadPdf(
        Stream source,
        PvadProjectReadLimits? limits = null,
        CancellationToken cancellationToken = default) =>
        Read<DesignExportDto>(source, PvadProjectKind.Pdf, limits, cancellationToken);

    public static PvadProjectPackage<SpreadsheetDto> ReadSpreadsheet(
        Stream source,
        PvadProjectReadLimits? limits = null,
        CancellationToken cancellationToken = default) =>
        Read<SpreadsheetDto>(source, PvadProjectKind.Spreadsheet, limits, cancellationToken);

    public static PvadProjectManifest Inspect(
        Stream source,
        PvadProjectKind expectedKind,
        PvadProjectReadLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveLimits = limits ?? new PvadProjectReadLimits();
        ValidateLimits(effectiveLimits);
        var budget = new ProcessingBudget(effectiveLimits.MaximumProcessingDuration, cancellationToken);
        var archiveBytes = ReadBounded(source, effectiveLimits.MaximumCompressedBytes, budget);
        ValidateCentralDirectory(archiveBytes, effectiveLimits, budget);

        try
        {
            using var archive = new ZipArchive(
                new MemoryStream(archiveBytes, writable: false),
                ZipArchiveMode.Read,
                leaveOpen: false,
                entryNameEncoding: Encoding.UTF8);
            if (archive.Entries.Count is 0 || archive.Entries.Count > effectiveLimits.MaximumEntries)
                throw Error("PXAPROJECT005", "The project package contains an unsupported number of entries.");

            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            var normalizedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                budget.ThrowIfExceeded();
                ValidatePath(entry.FullName);
                if (!normalizedPaths.Add(entry.FullName) || !entries.TryAdd(entry.FullName, entry))
                    throw Error("PXAPROJECT004", "The project package contains duplicate paths.");
                if (IsSymlink(entry))
                    throw Error("PXAPROJECT003", "The project package contains an unsupported symbolic link.");
                if (entry.Length > effectiveLimits.MaximumEntryBytes)
                    throw Error("PXAPROJECT005", "A project entry exceeds its size limit.");
            }

            if (!entries.TryGetValue(PvadProjectFormat.MimeTypeEntry, out var mimeEntry) ||
                !entries.TryGetValue(PvadProjectFormat.ManifestEntry, out var manifestEntry) ||
                !entries.ContainsKey(PvadProjectFormat.DocumentEntry))
                throw Error("PXAPROJECT008", "The project package is missing required envelope entries.");

            var mimeBytes = ReadEntryBounded(mimeEntry, 256, budget);
            var mediaType = Encoding.ASCII.GetString(mimeBytes);
            if (!PvadProjectFormat.AcceptsMediaType(expectedKind, mediaType))
                throw Error("PXAPROJECT002", "The project package format does not match the requested project type.");

            var manifestBytes = ReadEntryBounded(manifestEntry, effectiveLimits.MaximumEntryBytes, budget);
            var manifest = PvadCanonicalJson.Deserialize<PvadProjectManifest>(
                manifestBytes, effectiveLimits.MaximumJsonDepth);
            if (manifest.MediaType != mediaType)
                throw Error("PXAPROJECT002", "The project MIME entry does not match its manifest.");
            ValidateManifestEnvelope(manifest, expectedKind, entries, effectiveLimits, budget);
            return manifest;
        }
        catch (PvadProjectFormatException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw Error("PXAPROJECT001", "The file is not a valid PVAD project package.");
        }
    }

    private static byte[] Write<TDocument>(
        PvadProjectKind kind,
        TDocument document,
        string documentId,
        string title,
        PvadProjectWriteOptions? options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new PvadProjectWriteOptions();
        ValidateWriteOptions(options);
        var limits = new PvadProjectReadLimits();
        ValidateDocument(document, limits, cancellationToken);

        var entries = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [PvadProjectFormat.MimeTypeEntry] = Encoding.ASCII.GetBytes(PvadProjectFormat.MediaType(kind)),
            [PvadProjectFormat.DocumentEntry] = PvadCanonicalJson.Serialize(document),
        };
        var assetDescriptors = new List<PvadProjectAssetDescriptor>();
        var componentDescriptors = new List<PvadProjectComponentDescriptor>();
        var assetIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var asset in options.Assets.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAsset(asset, limits);
            if (!assetIds.Add(asset.Id))
                throw Error("PXAPROJECT004", "The project contains a duplicate asset ID.");
            var path = AssetPath(asset.Kind, asset.Id, asset.ContentType);
            if (!entries.TryAdd(path, asset.Content.ToArray()))
                throw Error("PXAPROJECT004", "The project contains a duplicate package path.");
            assetDescriptors.Add(new PvadProjectAssetDescriptor
            {
                Id = asset.Id,
                Kind = asset.Kind,
                Path = path,
                FileName = asset.FileName,
                ContentType = asset.ContentType,
                Length = asset.Content.LongLength,
                Sha256 = Sha256(asset.Content),
            });
        }

        if (options.CodeWorkspace is not null)
        {
            if (kind != PvadProjectKind.Pdf)
                throw Error("PXAPROJECT016", "Code Workspace components are supported only by PVADP projects.");
            ValidateCodeWorkspace(options.CodeWorkspace, limits);
            var bytes = PvadCanonicalJson.Serialize(options.CodeWorkspace);
            entries.Add(PvadProjectFormat.CodeWorkspaceEntry, bytes);
            componentDescriptors.Add(new PvadProjectComponentDescriptor
            {
                Kind = PvadProjectComponentKinds.CodeWorkspace,
                Path = PvadProjectFormat.CodeWorkspaceEntry,
                SchemaVersion = options.CodeWorkspace.SchemaVersion,
                SecurityClassification = PvadProjectComponentKinds.ConfidentialSource,
                Length = bytes.LongLength,
                Sha256 = Sha256(bytes),
            });
        }

        var checksums = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checksums.Add(entry.Key, Sha256(entry.Value));
        }

        var manifest = new PvadProjectManifest
        {
            Magic = PvadProjectFormat.Magic(kind),
            FormatId = PvadProjectFormat.FormatId(kind),
            MediaType = PvadProjectFormat.MediaType(kind),
            DocumentId = RequireBounded(options.DocumentId ?? documentId, 200, "document ID"),
            Title = RequireBounded(options.Title ?? title, 200, "title"),
            Locale = RequireBounded(options.Locale, 35, "locale"),
            MinimumPvadVersion = RequireVersion(options.MinimumPvadVersion),
            CreatedAtUtc = options.CreatedAtUtc.ToUniversalTime(),
            ModifiedAtUtc = options.ModifiedAtUtc.ToUniversalTime(),
            Assets = assetDescriptors,
            Components = componentDescriptors,
            EntryChecksums = checksums,
            ContentHash = ContentHash(entries),
            RequiredFeatures = DetectRequiredFeatures(document),
            Diagnostics = CollectDiagnostics(document),
            Extensions = options.ManifestExtensions is null
                ? null
                : new Dictionary<string, System.Text.Json.JsonElement>(options.ManifestExtensions, StringComparer.Ordinal),
        };
        ValidateExtensionData([manifest.Extensions], limits);

        var manifestBytes = PvadCanonicalJson.Serialize(manifest);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Encoding.UTF8))
        {
            WriteEntry(archive, PvadProjectFormat.MimeTypeEntry, entries[PvadProjectFormat.MimeTypeEntry], CompressionLevel.NoCompression);
            WriteEntry(archive, PvadProjectFormat.ManifestEntry, manifestBytes, CompressionLevel.Optimal);
            foreach (var entry in entries.Where(item => item.Key != PvadProjectFormat.MimeTypeEntry))
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteEntry(archive, entry.Key, entry.Value, CompressionLevel.Optimal);
            }
        }
        var result = output.ToArray();
        if (result.LongLength > PvadProjectReadLimits.DefaultMaximumCompressedBytes)
            throw Error("PXAPROJECT005", "The generated project package exceeds its compressed-size limit.");
        return result;
    }

    private static PvadProjectPackage<TDocument> Read<TDocument>(
        Stream source,
        PvadProjectKind expectedKind,
        PvadProjectReadLimits? limits,
        CancellationToken cancellationToken)
    {
        var validated = ReadValidatedArchive(
            source, expectedKind, limits ?? new PvadProjectReadLimits(), cancellationToken);
        var documentBytes = validated.Entries[PvadProjectFormat.DocumentEntry];
        var upgrade = PvadProjectUpgradePipeline.Upgrade(
            expectedKind,
            validated.Manifest.DocumentSchemaVersion,
            PvadProjectFormat.DocumentSchemaVersion,
            documentBytes,
            validated.Limits.MaximumJsonDepth);
        documentBytes = upgrade.DocumentBytes;
        if (upgrade.Report is not null)
        {
            validated.Manifest.DocumentSchemaVersion = PvadProjectFormat.DocumentSchemaVersion;
            validated.Manifest.Diagnostics.AddRange(upgrade.Report.Diagnostics);
        }
        validated.Budget.ThrowIfExceeded();
        var document = PvadCanonicalJson.Deserialize<TDocument>(documentBytes, validated.Limits.MaximumJsonDepth);
        validated.Budget.ThrowIfExceeded();
        ValidateDocument(document, validated.Limits, cancellationToken);
        validated.Budget.ThrowIfExceeded();

        if (document is DesignExportDto pdf)
        {
            RejectPdfSecrets(pdf);
            if (!StringComparer.Ordinal.Equals(pdf.Id, validated.Manifest.DocumentId))
                throw Error("PXAPROJECT013", "The PDF project document ID does not match its manifest.");
        }
        else if (document is SpreadsheetDto spreadsheet)
        {
            RejectSpreadsheetSecrets(spreadsheet);
            if (!StringComparer.Ordinal.Equals(spreadsheet.Id, validated.Manifest.DocumentId))
                throw Error("PXAPROJECT013", "The Spreadsheet project document ID does not match its manifest.");
        }

        var assets = validated.Manifest.Assets
            .Select(descriptor => new PvadProjectAsset(
                descriptor.Id,
                descriptor.FileName,
                descriptor.ContentType,
                validated.Entries[descriptor.Path].ToArray(),
                descriptor.Kind))
            .ToArray();
        PvadCodeWorkspaceProjectComponent? codeWorkspace = null;
        var workspaceDescriptor = validated.Manifest.Components.SingleOrDefault(component =>
            component.Kind == PvadProjectComponentKinds.CodeWorkspace);
        if (workspaceDescriptor is not null)
        {
            codeWorkspace = PvadCanonicalJson.Deserialize<PvadCodeWorkspaceProjectComponent>(
                validated.Entries[workspaceDescriptor.Path], validated.Limits.MaximumJsonDepth);
            ValidateCodeWorkspace(codeWorkspace, validated.Limits);
        }
        var packageHash = Sha256(validated.ArchiveBytes);
        if (upgrade.Report is not null)
            upgrade.Report.OriginalPackageSha256 = packageHash;
        return new PvadProjectPackage<TDocument>(
            validated.Manifest,
            document,
            assets,
            packageHash)
        {
            UpgradeReport = upgrade.Report,
            CodeWorkspace = codeWorkspace,
        };
    }

    private static ValidatedArchive ReadValidatedArchive(
        Stream source,
        PvadProjectKind expectedKind,
        PvadProjectReadLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLimits(limits);
        var budget = new ProcessingBudget(limits.MaximumProcessingDuration, cancellationToken);
        var archiveBytes = ReadBounded(source, limits.MaximumCompressedBytes, budget);
        ValidateCentralDirectory(archiveBytes, limits, budget);

        try
        {
            using var archive = new ZipArchive(
                new MemoryStream(archiveBytes, writable: false),
                ZipArchiveMode.Read,
                leaveOpen: false,
                entryNameEncoding: Encoding.UTF8);
            if (archive.Entries.Count is 0 || archive.Entries.Count > limits.MaximumEntries)
                throw Error("PXAPROJECT005", "The project package contains an unsupported number of entries.");

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            long expandedTotal = 0;
            foreach (var entry in archive.Entries)
            {
                budget.ThrowIfExceeded();
                ValidatePath(entry.FullName);
                if (!paths.Add(entry.FullName))
                    throw Error("PXAPROJECT004", "The project package contains duplicate paths.");
                if (IsSymlink(entry))
                    throw Error("PXAPROJECT003", "The project package contains an unsupported symbolic link.");
                if (entry.Length > limits.MaximumEntryBytes || expandedTotal > limits.MaximumExpandedBytes - entry.Length)
                    throw Error("PXAPROJECT005", "The project package exceeds its expanded-size limit.");
                if (entry.CompressedLength > 0 && entry.Length > 1_048_576 &&
                    entry.Length / (double)entry.CompressedLength > limits.MaximumCompressionRatio)
                    throw Error("PXAPROJECT005", "The project package exceeds its compression-ratio limit.");
                var content = ReadEntryBounded(entry, limits.MaximumEntryBytes, budget);
                if (entry.FullName.StartsWith("assets/", StringComparison.Ordinal) && LooksLikeArchive(content))
                    throw Error("PXAPROJECT011", "Nested archives are not accepted as project assets.");
                entries.Add(entry.FullName, content);
                expandedTotal += content.LongLength;
            }

            RequireEntry(entries, PvadProjectFormat.MimeTypeEntry);
            RequireEntry(entries, PvadProjectFormat.ManifestEntry);
            RequireEntry(entries, PvadProjectFormat.DocumentEntry);
            var mediaType = Encoding.ASCII.GetString(entries[PvadProjectFormat.MimeTypeEntry]);
            if (!PvadProjectFormat.AcceptsMediaType(expectedKind, mediaType))
                throw Error("PXAPROJECT002", "The project package format does not match the requested project type.");

            var manifest = PvadCanonicalJson.Deserialize<PvadProjectManifest>(
                entries[PvadProjectFormat.ManifestEntry], limits.MaximumJsonDepth);
            if (manifest.MediaType != mediaType)
                throw Error("PXAPROJECT002", "The project MIME entry does not match its manifest.");
            ValidateManifest(manifest, expectedKind, entries, limits, budget);
            return new ValidatedArchive(manifest, entries, archiveBytes, limits, budget);
        }
        catch (PvadProjectFormatException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw Error("PXAPROJECT001", "The file is not a valid PVAD project package.");
        }
    }

    private static void ValidateManifest(
        PvadProjectManifest manifest,
        PvadProjectKind kind,
        IReadOnlyDictionary<string, byte[]> entries,
        PvadProjectReadLimits limits,
        ProcessingBudget budget)
    {
        ValidateManifestIdentity(manifest, kind, limits);

        var expectedPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            PvadProjectFormat.MimeTypeEntry,
            PvadProjectFormat.ManifestEntry,
            PvadProjectFormat.DocumentEntry,
        };
        var assetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in manifest.Assets)
        {
            budget.ThrowIfExceeded();
            ValidateAssetDescriptor(asset);
            if (!assetIds.Add(asset.Id) || !expectedPaths.Add(asset.Path))
                throw Error("PXAPROJECT004", "The project manifest contains duplicate assets.");
            if (!entries.TryGetValue(asset.Path, out var content))
                throw Error("PXAPROJECT008", "A declared project asset is missing.");
            if (content.LongLength != asset.Length || Sha256(content) != asset.Sha256)
                throw Error("PXAPROJECT007", "A project asset failed integrity validation.");
            ValidateAssetContent(asset.ContentType, content, limits);
        }
        AddAndValidateComponents(manifest, entries, expectedPaths, limits, budget);
        if (!expectedPaths.SetEquals(entries.Keys))
            throw Error("PXAPROJECT008", "The project package contains undeclared entries.");

        var payload = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(item => item.Key != PvadProjectFormat.ManifestEntry))
        {
            budget.ThrowIfExceeded();
            if (!manifest.EntryChecksums.TryGetValue(entry.Key, out var checksum) || checksum != Sha256(entry.Value))
                throw Error("PXAPROJECT007", "A project entry failed integrity validation.");
            payload.Add(entry.Key, entry.Value);
        }
        if (manifest.EntryChecksums.Count != payload.Count || manifest.ContentHash != ContentHash(payload))
            throw Error("PXAPROJECT007", "The project package content hash is invalid.");
    }

    private static void ValidateManifestEnvelope(
        PvadProjectManifest manifest,
        PvadProjectKind kind,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        PvadProjectReadLimits limits,
        ProcessingBudget budget)
    {
        ValidateManifestIdentity(manifest, kind, limits);
        var expectedPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            PvadProjectFormat.MimeTypeEntry,
            PvadProjectFormat.ManifestEntry,
            PvadProjectFormat.DocumentEntry,
        };
        var assetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in manifest.Assets)
        {
            budget.ThrowIfExceeded();
            ValidateAssetDescriptor(asset);
            if (!assetIds.Add(asset.Id) || !expectedPaths.Add(asset.Path))
                throw Error("PXAPROJECT004", "The project manifest contains duplicate assets.");
            if (!entries.TryGetValue(asset.Path, out var entry) || entry.Length != asset.Length)
                throw Error("PXAPROJECT008", "A declared project asset is missing or has an invalid length.");
        }
        AddAndValidateComponentEnvelopes(manifest, entries, expectedPaths, limits, budget);
        if (!expectedPaths.SetEquals(entries.Keys))
            throw Error("PXAPROJECT008", "The project package contains undeclared entries.");

        var payloadPaths = expectedPaths.Where(path => path != PvadProjectFormat.ManifestEntry).ToHashSet(StringComparer.Ordinal);
        if (manifest.EntryChecksums.Count != payloadPaths.Count ||
            !payloadPaths.SetEquals(manifest.EntryChecksums.Keys) ||
            manifest.EntryChecksums.Values.Any(checksum => !IsSha256(checksum)) ||
            !IsSha256(manifest.ContentHash))
            throw Error("PXAPROJECT007", "The project manifest contains invalid integrity metadata.");
    }

    private static void ValidateManifestIdentity(
        PvadProjectManifest manifest,
        PvadProjectKind kind,
        PvadProjectReadLimits limits)
    {
        if (manifest.Assets is null || manifest.Components is null || manifest.EntryChecksums is null)
            throw Error("PXAPROJECT008", "The project manifest is incomplete.");
        manifest.RequiredFeatures ??= [];
        manifest.Diagnostics ??= [];
        var legacy = manifest.Magic == PvadProjectFormat.LegacyMagic(kind);
        if (manifest.Magic != (legacy ? PvadProjectFormat.LegacyMagic(kind) : PvadProjectFormat.Magic(kind)) ||
            manifest.FormatId != (legacy ? PvadProjectFormat.LegacyFormatId(kind) : PvadProjectFormat.FormatId(kind)) ||
            manifest.MediaType != (legacy ? PvadProjectFormat.LegacyMediaType(kind) : PvadProjectFormat.MediaType(kind)) ||
            manifest.EntryDocument != PvadProjectFormat.DocumentEntry)
            throw Error("PXAPROJECT002", "The project manifest format is invalid or mismatched.");
        ValidateSupportedVersion(manifest.EnvelopeVersion, "envelope");
        ValidateSupportedVersion(manifest.DocumentSchemaVersion, "document schema", kind);
        RequireBounded(manifest.DocumentId, 200, "document ID");
        RequireBounded(manifest.Title, 200, "title");
        RequireBounded(manifest.Locale, 35, "locale");
        RequireVersion(manifest.MinimumPvadVersion);
        if (!PvadProjectVersionPolicy.IsProductVersionAtLeast(
                PvadProjectFormat.CurrentProductVersion,
                manifest.MinimumPvadVersion))
            throw Error("PXAPROJECT014",
                $"The project requires PVAD {manifest.MinimumPvadVersion} or newer.");
        if (manifest.ModifiedAtUtc < manifest.CreatedAtUtc)
            throw Error("PXAPROJECT008", "The project modification time cannot precede its creation time.");
        ValidateCapabilitiesAndDiagnostics(manifest);
        ValidateExtensionData([manifest.Extensions], limits);
    }

    private static List<string> DetectRequiredFeatures<TDocument>(TDocument document)
    {
        var features = new SortedSet<string>(StringComparer.Ordinal);
        if (document is DesignExportDto pdf)
        {
            features.Add(PvadProjectCapabilities.PdfDesigner);
            var elements = pdf.Pages.SelectMany(page => page.Elements).Concat(pdf.SharedElements).ToArray();
            if (pdf.Pages.Any(page => page.Layout is not null))
                features.Add(PvadProjectCapabilities.PdfPerPageLayouts);
            if (elements.Any(element => element.Chart is not null || element.Type == "chart"))
                features.Add(PvadProjectCapabilities.PdfCharts);
            if (!string.IsNullOrWhiteSpace(pdf.PageSettings?.AccessibilityProfile) ||
                elements.Any(element => element.Accessibility is not null))
                features.Add(PvadProjectCapabilities.PdfAccessibility);
            if ((pdf.PageSettings?.ActiveLanguages?.Count ?? 0) > 0 ||
                elements.Any(element => !string.IsNullOrWhiteSpace(element.ElementLanguage) ||
                                        (element.LangOverrides?.Count ?? 0) > 0))
                features.Add(PvadProjectCapabilities.PdfLocalization);
            if (elements.Any(element => !string.IsNullOrWhiteSpace(element.Binding) ||
                                        !string.IsNullOrWhiteSpace(element.Expression) || element.Repeat is not null))
                features.Add(PvadProjectCapabilities.PdfBindings);
            if (!string.IsNullOrWhiteSpace(pdf.PageSettings?.ComplianceProfile) ||
                pdf.PageSettings?.Encryption?.Enabled == true || pdf.PageSettings?.Protection?.Enabled == true)
                features.Add(PvadProjectCapabilities.PdfTrustSettings);
        }
        else if (document is SpreadsheetDto spreadsheet)
        {
            features.Add(PvadProjectCapabilities.SpreadsheetDesigner);
            if (spreadsheet.Sheets.SelectMany(sheet => sheet.Cells).Any(cell => !string.IsNullOrWhiteSpace(cell.Formula)))
                features.Add(PvadProjectCapabilities.SpreadsheetFormulas);
            if (spreadsheet.Sheets.Any(sheet => sheet.Images.Count > 0))
                features.Add(PvadProjectCapabilities.SpreadsheetImages);
            if (spreadsheet.Sheets.Any(sheet => sheet.DataValidations.Count > 0))
                features.Add(PvadProjectCapabilities.SpreadsheetValidation);
            if (spreadsheet.Sheets.Any(sheet => sheet.ConditionalFormats.Count > 0))
                features.Add(PvadProjectCapabilities.SpreadsheetConditionalFormatting);
            if (spreadsheet.Sheets.Any(sheet => sheet.PageSetup is not null))
                features.Add(PvadProjectCapabilities.SpreadsheetPrintLayout);
        }
        return [.. features];
    }

    private static List<PvadProjectDiagnostic> CollectDiagnostics<TDocument>(TDocument document)
    {
        var diagnostics = document switch
        {
            DesignExportDto pdf => ConvertDiagnostics(pdf.ImportDiagnostics),
            SpreadsheetDto spreadsheet => ConvertDiagnostics(spreadsheet.ImportDiagnostics),
            _ => [],
        };
        if (document is not SpreadsheetDto workbook)
            return diagnostics;

        foreach (var (sheet, index) in workbook.Sheets.Select((sheet, index) => (sheet, index)))
        {
            if (sheet.Cells.Any(cell => IsExternalFormula(cell.Formula)))
                AddDiagnostic(diagnostics, "PXAPROJECT_EXTERNAL_LINK", "warning",
                    "The worksheet contains an external workbook reference that requires review.", $"sheets[{index}]");
            if (ContainsExtensionMarker(sheet.Extensions, "macro", "vba"))
                AddDiagnostic(diagnostics, "PXAPROJECT_MACRO_PRESERVED", "warning",
                    "Macro metadata is preserved as inert extension data and is never executed.", $"sheets[{index}]");
            if (ContainsExtensionMarker(sheet.Extensions, "provider"))
                AddDiagnostic(diagnostics, "PXAPROJECT_PROVIDER_EXTENSION", "warning",
                    "Provider-specific worksheet metadata may require review.", $"sheets[{index}]");
        }
        return diagnostics;
    }

    private static List<PvadProjectDiagnostic> ConvertDiagnostics(IEnumerable<ImportDiagnosticDto>? diagnostics) =>
        diagnostics?.Select(diagnostic => new PvadProjectDiagnostic
        {
            Code = diagnostic.Code,
            Severity = diagnostic.Severity,
            Message = diagnostic.Message,
            Source = diagnostic.Source,
        }).ToList() ?? [];

    private static bool IsExternalFormula(string? formula) =>
        !string.IsNullOrWhiteSpace(formula) && formula.Contains('[') && formula.Contains(']');

    private static bool ContainsExtensionMarker(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement>? extensions,
        params string[] markers) =>
        extensions?.Keys.Any(key => markers.Any(marker =>
            key.Contains(marker, StringComparison.OrdinalIgnoreCase))) == true;

    private static void AddDiagnostic(
        ICollection<PvadProjectDiagnostic> diagnostics,
        string code,
        string severity,
        string message,
        string source)
    {
        if (diagnostics.Any(item => item.Code == code && item.Source == source))
            return;
        diagnostics.Add(new PvadProjectDiagnostic
        {
            Code = code, Severity = severity, Message = message, Source = source,
        });
    }

    private static void ValidateCapabilitiesAndDiagnostics(PvadProjectManifest manifest)
    {
        if (manifest.RequiredFeatures.Count > 128 || manifest.Diagnostics.Count > 512)
            throw Error("PXAPROJECT005", "The project manifest contains too many requirements or diagnostics.");
        if (manifest.RequiredFeatures.Any(feature =>
                string.IsNullOrWhiteSpace(feature) || feature.Length > 100 || feature.Any(char.IsControl)) ||
            manifest.RequiredFeatures.Distinct(StringComparer.Ordinal).Count() != manifest.RequiredFeatures.Count)
            throw Error("PXAPROJECT008", "The project manifest contains invalid feature requirements.");
        foreach (var diagnostic in manifest.Diagnostics)
        {
            if (diagnostic is null || string.IsNullOrWhiteSpace(diagnostic.Code) || diagnostic.Code.Length > 80 ||
                diagnostic.Code.Any(char.IsControl) ||
                diagnostic.Severity is not ("info" or "warning" or "error") ||
                string.IsNullOrWhiteSpace(diagnostic.Message) || diagnostic.Message.Length > 1_000 ||
                diagnostic.Message.Any(char.IsControl) ||
                diagnostic.Source?.Length > 300 || diagnostic.Source?.Any(char.IsControl) == true)
                throw Error("PXAPROJECT008", "The project manifest contains an invalid diagnostic.");
        }
    }

    private static void ValidateCentralDirectory(
        byte[] bytes,
        PvadProjectReadLimits limits,
        ProcessingBudget budget)
    {
        if (bytes.Length < 22 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x04034b50)
            throw Error("PXAPROJECT001", "The file is not a ZIP-based PVAD project package.");
        var eocd = FindEndOfCentralDirectory(bytes);
        var disk = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(eocd + 4));
        var centralDisk = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(eocd + 6));
        var entries = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(eocd + 10));
        var centralSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(eocd + 12));
        var centralOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(eocd + 16));
        if (disk != 0 || centralDisk != 0 || entries == ushort.MaxValue || entries > limits.MaximumEntries ||
            centralOffset > bytes.Length || centralSize > bytes.Length - centralOffset)
            throw Error("PXAPROJECT005", "The project archive structure exceeds supported limits.");

        var cursor = checked((int)centralOffset);
        long expandedTotal = 0;
        for (var index = 0; index < entries; index++)
        {
            budget.ThrowIfExceeded();
            if (cursor > bytes.Length - 46 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor)) != 0x02014b50)
                throw Error("PXAPROJECT001", "The project archive central directory is invalid.");
            var flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor + 8));
            var method = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor + 10));
            var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 20));
            var expandedSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 24));
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor + 28));
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor + 30));
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor + 32));
            var externalAttributes = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 38));
            var localHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 42));
            var entryLength = 46L + nameLength + extraLength + commentLength;
            if ((flags & 0x41) != 0)
                throw Error("PXAPROJECT006", "Encrypted project archive entries are not supported.");
            if (method is not 0 and not 8)
                throw Error("PXAPROJECT006", "The project archive uses an unsupported compression method.");
            if (((externalAttributes >> 16) & 0xF000) == 0xA000)
                throw Error("PXAPROJECT003", "The project archive contains a symbolic link.");
            if (entryLength > bytes.Length - cursor)
                throw Error("PXAPROJECT001", "The project archive entry metadata is truncated.");
            var nameBytes = bytes.AsSpan(cursor + 46, nameLength);
            if (nameBytes.ContainsAnyExceptInRange((byte)0x20, (byte)0x7e))
                throw Error("PXAPROJECT003", "Project package paths must use printable ASCII characters.");
            var name = Encoding.ASCII.GetString(nameBytes);
            if (index == 0 && (name != PvadProjectFormat.MimeTypeEntry || method != 0))
                throw Error("PXAPROJECT002", "The project mimetype must be the first uncompressed package entry.");
            if (centralOffset < 4 || localHeaderOffset > int.MaxValue || localHeaderOffset > centralOffset - 4 ||
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(checked((int)localHeaderOffset))) != 0x04034b50)
                throw Error("PXAPROJECT001", "The project archive local entry header is invalid.");
            if (expandedSize > limits.MaximumEntryBytes || expandedTotal > limits.MaximumExpandedBytes - expandedSize)
                throw Error("PXAPROJECT005", "The project archive declares unsupported entry sizes.");
            if (compressedSize > 0 && expandedSize > 1_048_576 &&
                expandedSize / (double)compressedSize > limits.MaximumCompressionRatio)
                throw Error("PXAPROJECT005", "The project archive exceeds its compression-ratio limit.");
            expandedTotal += expandedSize;
            cursor += checked((int)entryLength);
        }
        if (cursor != checked((int)(centralOffset + centralSize)))
            throw Error("PXAPROJECT001", "The project archive central directory length is invalid.");
    }

    private static int FindEndOfCentralDirectory(byte[] bytes)
    {
        var minimum = Math.Max(0, bytes.Length - 65_557);
        for (var index = bytes.Length - 22; index >= minimum; index--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index)) != 0x06054b50)
                continue;
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index + 20));
            if (index + 22 + commentLength == bytes.Length)
                return index;
        }
        throw Error("PXAPROJECT001", "The project archive is incomplete or invalid.");
    }

    private static byte[] ReadBounded(Stream source, long maximumBytes, ProcessingBudget budget)
    {
        if (!source.CanRead)
            throw Error("PXAPROJECT001", "The project stream cannot be read.");
        using var output = new MemoryStream();
        var buffer = new byte[81_920];
        long total = 0;
        while (true)
        {
            budget.ThrowIfExceeded();
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            if (total > maximumBytes - read)
                throw Error("PXAPROJECT005", "The project package exceeds its compressed-size limit.");
            output.Write(buffer, 0, read);
            total += read;
        }
        return output.ToArray();
    }

    private static byte[] ReadEntryBounded(
        ZipArchiveEntry entry,
        long maximumBytes,
        ProcessingBudget budget)
    {
        using var input = entry.Open();
        using var output = new MemoryStream(entry.Length <= int.MaxValue ? (int)entry.Length : 0);
        var buffer = new byte[81_920];
        long total = 0;
        while (true)
        {
            budget.ThrowIfExceeded();
            var read = input.Read(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            if (total > maximumBytes - read)
                throw Error("PXAPROJECT005", "A project entry exceeds its size limit.");
            output.Write(buffer, 0, read);
            total += read;
        }
        if (total != entry.Length)
            throw Error("PXAPROJECT001", "A project archive entry is truncated.");
        return output.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] bytes, CompressionLevel compression)
    {
        var entry = archive.CreateEntry(path, compression);
        entry.LastWriteTime = DeterministicTimestamp;
        entry.ExternalAttributes = 0;
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 240 || path.StartsWith('/') ||
            path.Contains('\\') || path.Contains(':') || path.EndsWith('/') ||
            path != path.Normalize(NormalizationForm.FormC) ||
            path.Split('/').Any(segment => segment is "" or "." or ".." || segment.Length > 100 ||
                segment.Any(char.IsControl)))
            throw Error("PXAPROJECT003", "The project package contains an unsafe path.");
    }

    private static void ValidateAsset(PvadProjectAsset asset, PvadProjectReadLimits limits)
    {
        if (!IsSafeId(asset.Id))
            throw Error("PXAPROJECT008", "A project asset ID is invalid.");
        if (string.IsNullOrWhiteSpace(asset.FileName) || asset.FileName.Length > 200 || !IsSafeFileName(asset.FileName))
            throw Error("PXAPROJECT003", "A project asset filename is invalid.");
        if (asset.Content is null || asset.Content.LongLength > 20 * 1024 * 1024)
            throw Error("PXAPROJECT005", "A project asset exceeds its size limit.");
        ValidateAssetKind(asset.Kind, asset.ContentType);
        ValidateAssetContent(asset.ContentType, asset.Content, limits);
    }

    private static void ValidateAssetDescriptor(PvadProjectAssetDescriptor asset)
    {
        asset.Kind = NormalizeAssetKind(asset.Kind, asset.ContentType);
        ValidateAssetKind(asset.Kind, asset.ContentType);
        var canonicalPath = AssetPath(asset.Kind, asset.Id, asset.ContentType);
        var legacyImagePath = $"assets/{asset.Id}{ExtensionFor(asset.ContentType)}";
        if (!IsSafeId(asset.Id) ||
            (asset.Path != canonicalPath &&
             !(asset.Kind is PvadProjectAssetKinds.Image or PvadProjectAssetKinds.Font && asset.Path == legacyImagePath)) ||
            string.IsNullOrWhiteSpace(asset.FileName) || asset.FileName.Length > 200 ||
            !IsSafeFileName(asset.FileName) ||
            asset.Length < 0 || !IsSha256(asset.Sha256))
            throw Error("PXAPROJECT008", "A project asset descriptor is invalid.");
        ValidatePath(asset.Path);
    }

    private static string NormalizeAssetKind(string kind, string contentType)
    {
        if (!string.IsNullOrEmpty(kind))
            return kind;
        if (contentType.StartsWith("image/", StringComparison.Ordinal))
            return PvadProjectAssetKinds.Image;
        if (contentType.StartsWith("font/", StringComparison.Ordinal))
            return PvadProjectAssetKinds.Font;
        return kind;
    }

    private static void ValidateAssetKind(string kind, string contentType)
    {
        var valid = kind switch
        {
            PvadProjectAssetKinds.Image => contentType.StartsWith("image/", StringComparison.Ordinal),
            PvadProjectAssetKinds.Font => contentType.StartsWith("font/", StringComparison.Ordinal),
            PvadProjectAssetKinds.Preview => contentType is "image/png" or "image/jpeg" or "image/webp",
            PvadProjectAssetKinds.Source => contentType == "application/pdf",
            PvadProjectAssetKinds.Data => contentType == "application/json",
            _ => false,
        };
        if (!valid)
            throw Error("PXAPROJECT010", "The project asset kind and content type are incompatible.");
    }

    private static void ValidateAssetContent(
        string contentType,
        ReadOnlySpan<byte> content,
        PvadProjectReadLimits limits)
    {
        var valid = contentType switch
        {
            "image/png" => content.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "image/jpeg" => content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff,
            "image/gif" => content.StartsWith("GIF87a"u8) || content.StartsWith("GIF89a"u8),
            "image/webp" => content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8),
            "image/svg+xml" => IsSafeSvg(content),
            "font/ttf" => content.Length >= 4 && (content[..4].SequenceEqual(new byte[] { 0x00, 0x01, 0x00, 0x00 }) || content[..4].SequenceEqual("true"u8)),
            "font/otf" => content.StartsWith("OTTO"u8),
            "font/woff" => content.StartsWith("wOFF"u8),
            "font/woff2" => content.StartsWith("wOF2"u8),
            "application/pdf" => content.Length >= 8 && content.StartsWith("%PDF-"u8),
            "application/json" => IsSafeJsonAsset(content, limits.MaximumJsonDepth),
            _ => false,
        };
        if (!valid)
            throw Error("PXAPROJECT010", "A project asset does not match an allowed content type.");
        if (TryReadRasterDimensions(contentType, content, out var width, out var height) &&
            (width <= 0 || height <= 0 || width > limits.MaximumImagePixels / height))
            throw Error("PXAPROJECT005", "A project image exceeds its decoded-pixel limit.");
    }

    private static bool TryReadRasterDimensions(
        string contentType,
        ReadOnlySpan<byte> content,
        out long width,
        out long height)
    {
        width = 0;
        height = 0;
        switch (contentType)
        {
            case "image/png":
                if (content.Length < 24 || !content.Slice(12, 4).SequenceEqual("IHDR"u8))
                    throw Error("PXAPROJECT010", "A PNG project asset has an invalid header.");
                width = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(16, 4));
                height = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(20, 4));
                return true;
            case "image/jpeg":
                return TryReadJpegDimensions(content, out width, out height);
            case "image/gif":
                if (content.Length < 10)
                    throw Error("PXAPROJECT010", "A GIF project asset has an invalid header.");
                width = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(6, 2));
                height = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(8, 2));
                return true;
            case "image/webp":
                return TryReadWebpDimensions(content, out width, out height);
            default:
                return false;
        }
    }

    private static bool TryReadJpegDimensions(ReadOnlySpan<byte> content, out long width, out long height)
    {
        width = 0;
        height = 0;
        var offset = 2;
        while (offset < content.Length)
        {
            while (offset < content.Length && content[offset] == 0xff)
                offset++;
            if (offset >= content.Length)
                break;
            var marker = content[offset++];
            if (marker is 0xd8 or 0xd9 || marker is >= 0xd0 and <= 0xd7)
                continue;
            if (offset > content.Length - 2)
                break;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset, 2));
            if (segmentLength < 2 || segmentLength > content.Length - offset)
                break;
            if (IsJpegStartOfFrame(marker))
            {
                if (segmentLength < 7)
                    break;
                height = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 5, 2));
                return true;
            }
            offset += segmentLength;
        }
        throw Error("PXAPROJECT010", "A JPEG project asset has no valid image dimensions.");
    }

    private static bool IsJpegStartOfFrame(byte marker) =>
        marker is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf;

    private static bool TryReadWebpDimensions(ReadOnlySpan<byte> content, out long width, out long height)
    {
        width = 0;
        height = 0;
        if (content.Length < 30)
            throw Error("PXAPROJECT010", "A WebP project asset has an invalid header.");
        var chunk = content.Slice(12, 4);
        if (chunk.SequenceEqual("VP8X"u8))
        {
            width = 1L + content[24] + (content[25] << 8) + (content[26] << 16);
            height = 1L + content[27] + (content[28] << 8) + (content[29] << 16);
            return true;
        }
        if (chunk.SequenceEqual("VP8L"u8) && content[20] == 0x2f)
        {
            width = 1L + content[21] + ((content[22] & 0x3f) << 8);
            height = 1L + ((content[22] >> 6) | (content[23] << 2) | ((content[24] & 0x0f) << 10));
            return true;
        }
        if (chunk.SequenceEqual("VP8 "u8) && content.Length >= 30 &&
            content[23] == 0x9d && content[24] == 0x01 && content[25] == 0x2a)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(26, 2)) & 0x3fff;
            height = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(28, 2)) & 0x3fff;
            return true;
        }
        throw Error("PXAPROJECT010", "A WebP project asset has no valid image dimensions.");
    }

    private static bool IsSafeSvg(ReadOnlySpan<byte> content)
    {
        if (content.Length is 0 or > 5 * 1024 * 1024)
            return false;
        try
        {
            var text = new UTF8Encoding(false, true).GetString(content).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            using var stringReader = new StringReader(text);
            using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 5 * 1024 * 1024,
                MaxCharactersFromEntities = 0,
            });
            var document = XDocument.Load(xmlReader, LoadOptions.None);
            if (document.Root?.Name.LocalName != "svg")
                return false;
            foreach (var element in document.Root.DescendantsAndSelf())
            {
                if (element.Name.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                    element.Name.LocalName.Equals("foreignObject", StringComparison.OrdinalIgnoreCase))
                    return false;
                foreach (var attribute in element.Attributes())
                {
                    var name = attribute.Name.LocalName;
                    var value = attribute.Value.Trim();
                    if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        value.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                        (name.Equals("href", StringComparison.OrdinalIgnoreCase) &&
                         value.Length > 0 && !value.StartsWith('#')) ||
                        value.Contains("url(http", StringComparison.OrdinalIgnoreCase) ||
                        value.Contains("@import", StringComparison.OrdinalIgnoreCase) ||
                        value.Contains("expression(", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                if (element.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase))
                {
                    var style = element.Value;
                    if (style.Contains("url(http", StringComparison.OrdinalIgnoreCase) ||
                        style.Contains("@import", StringComparison.OrdinalIgnoreCase) ||
                        style.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        style.Contains("expression(", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            return true;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or XmlException)
        {
            return false;
        }
    }

    private static bool IsSafeJsonAsset(ReadOnlySpan<byte> content, int maximumDepth)
    {
        if (content.Length is 0 or > 5 * 1024 * 1024)
            return false;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(content.ToArray(), new System.Text.Json.JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = System.Text.Json.JsonCommentHandling.Disallow,
                MaxDepth = maximumDepth,
            });
            return document.RootElement.ValueKind is System.Text.Json.JsonValueKind.Object or
                System.Text.Json.JsonValueKind.Array;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static string AssetPath(string kind, string id, string contentType)
    {
        var folder = kind switch
        {
            PvadProjectAssetKinds.Image => "images",
            PvadProjectAssetKinds.Font => "fonts",
            PvadProjectAssetKinds.Preview => "previews",
            PvadProjectAssetKinds.Source => "sources",
            PvadProjectAssetKinds.Data => "data",
            _ => throw Error("PXAPROJECT010", "The project asset kind is not supported."),
        };
        return $"assets/{folder}/{id}{ExtensionFor(contentType)}";
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "image/svg+xml" => ".svg",
        "font/ttf" => ".ttf",
        "font/otf" => ".otf",
        "font/woff" => ".woff",
        "font/woff2" => ".woff2",
        "application/pdf" => ".pdf",
        "application/json" => ".json",
        _ => throw Error("PXAPROJECT010", "The project asset content type is not supported."),
    };

    private static void RejectPdfSecrets(DesignExportDto document)
    {
        if (!string.IsNullOrEmpty(document.PageSettings?.Encryption?.UserPassword) ||
            !string.IsNullOrEmpty(document.PageSettings?.Encryption?.OwnerPassword) ||
            !string.IsNullOrEmpty(document.PageSettings?.Protection?.PasswordHash))
            throw Error("PXAPROJECT012", "Project packages cannot contain document passwords or password hashes.");
    }

    private static void RejectSpreadsheetSecrets(SpreadsheetDto document)
    {
        if (document.Sheets.Any(sheet => !string.IsNullOrEmpty(sheet.Protection?.Password)))
            throw Error("PXAPROJECT012", "Project packages cannot contain spreadsheet passwords.");
    }

    private static string ContentHash(IReadOnlyDictionary<string, byte[]> entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[8];
        foreach (var entry in entries.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var path = Encoding.UTF8.GetBytes(entry.Key);
            BinaryPrimitives.WriteInt64LittleEndian(length, path.LongLength);
            hash.AppendData(length);
            hash.AppendData(path);
            BinaryPrimitives.WriteInt64LittleEndian(length, entry.Value.LongLength);
            hash.AppendData(length);
            hash.AppendData(entry.Value);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSafeId(string value) =>
        value.Length is > 0 and <= 64 && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool IsSafeFileName(string value) =>
        value is not "." and not ".." &&
        value == value.Normalize(NormalizationForm.FormC) &&
        Path.GetFileName(value) == value &&
        value.All(character => !char.IsControl(character) &&
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
            System.Globalization.UnicodeCategory.Format);

    private static bool LooksLikeArchive(ReadOnlySpan<byte> content) =>
        content.Length >= 4 && (content[..4].SequenceEqual(new byte[] { 0x50, 0x4b, 0x03, 0x04 }) ||
                                content[..4].SequenceEqual(new byte[] { 0x50, 0x4b, 0x05, 0x06 }) ||
                                content[..4].SequenceEqual(new byte[] { 0x50, 0x4b, 0x07, 0x08 }));

    private static bool IsSymlink(ZipArchiveEntry entry) =>
        ((uint)entry.ExternalAttributes >> 16 & 0xF000) == 0xA000;

    private static void RequireEntry(IReadOnlyDictionary<string, byte[]> entries, string path)
    {
        if (!entries.ContainsKey(path))
            throw Error("PXAPROJECT008", $"The project package is missing {path}.");
    }

    private static string RequireBounded(string value, int maximumLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || value.Any(char.IsControl))
            throw Error("PXAPROJECT008", $"The project {field} is invalid.");
        return value;
    }

    private static string RequireVersion(string value)
    {
        if (!PvadProjectVersionPolicy.IsCanonicalProductVersion(value))
            throw Error("PXAPROJECT008", "The project version is invalid.");
        return value;
    }

    private static void ValidateSupportedVersion(string value, string field, PvadProjectKind? kind = null)
    {
        var currentVersion = field == "envelope"
            ? PvadProjectFormat.EnvelopeVersion
            : PvadProjectFormat.DocumentSchemaVersion;
        var hasUpgradePath = kind.HasValue &&
            PvadProjectUpgradePipeline.CanUpgrade(kind.Value, value, currentVersion);
        var assessment = PvadProjectVersionPolicy.AssessSchemaVersion(value, currentVersion, hasUpgradePath);
        if (!assessment.CanOpen)
            throw Error(assessment.DiagnosticCode, $"The project {field}: {assessment.Message}");
    }

    private static void ValidateWriteOptions(PvadProjectWriteOptions options)
    {
        if (options.ModifiedAtUtc < options.CreatedAtUtc)
            throw Error("PXAPROJECT008", "The project modification time cannot precede its creation time.");
        if (options.Assets.Count > 250)
            throw Error("PXAPROJECT005", "The project contains too many assets.");
    }

    private static void ValidateCodeWorkspace(
        PvadCodeWorkspaceProjectComponent workspace,
        PvadProjectReadLimits limits)
    {
        if (workspace.SchemaVersion != PvadCodeWorkspaceProjectComponent.CurrentSchemaVersion ||
            workspace.Revision < 0 || workspace.BaseTemplateRevision < 0)
            throw Error("PXAPROJECT016", "The Code Workspace component schema is unsupported.");
        if (workspace.SourceMap is { } sourceMap &&
            sourceMap.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object or JsonValueKind.Null))
            throw Error("PXAPROJECT016", "The Code Workspace source map is invalid.");
        byte[] bytes;
        try
        {
            bytes = PvadCanonicalJson.Serialize(workspace);
            using var canonical = JsonDocument.Parse(workspace.CanonicalDesignJson, new JsonDocumentOptions
            {
                MaxDepth = limits.MaximumJsonDepth,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
            });
            if (canonical.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("The canonical design must be a JSON object.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw Error("PXAPROJECT016", "The Code Workspace component contains invalid JSON.");
        }
        if (bytes.LongLength > 10 * 1024 * 1024)
            throw Error("PXAPROJECT005", "The Code Workspace component exceeds its 10 MiB size limit.");
    }

    private static void AddAndValidateComponents(
        PvadProjectManifest manifest,
        IReadOnlyDictionary<string, byte[]> entries,
        ISet<string> expectedPaths,
        PvadProjectReadLimits limits,
        ProcessingBudget budget)
    {
        if (manifest.Components.Count > 1)
            throw Error("PXAPROJECT016", "The project contains unsupported duplicate components.");
        foreach (var component in manifest.Components)
        {
            budget.ThrowIfExceeded();
            ValidateComponentDescriptor(component);
            if (!expectedPaths.Add(component.Path) || !entries.TryGetValue(component.Path, out var content) ||
                content.LongLength != component.Length || Sha256(content) != component.Sha256)
                throw Error("PXAPROJECT007", "A project component failed integrity validation.");
            var workspace = PvadCanonicalJson.Deserialize<PvadCodeWorkspaceProjectComponent>(content, limits.MaximumJsonDepth);
            ValidateCodeWorkspace(workspace, limits);
        }
    }

    private static void AddAndValidateComponentEnvelopes(
        PvadProjectManifest manifest,
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        ISet<string> expectedPaths,
        PvadProjectReadLimits limits,
        ProcessingBudget budget)
    {
        if (manifest.Components.Count > 1)
            throw Error("PXAPROJECT016", "The project contains unsupported duplicate components.");
        foreach (var component in manifest.Components)
        {
            budget.ThrowIfExceeded();
            ValidateComponentDescriptor(component);
            if (!expectedPaths.Add(component.Path) || !entries.TryGetValue(component.Path, out var entry) ||
                entry.Length != component.Length || entry.Length > limits.MaximumEntryBytes)
                throw Error("PXAPROJECT008", "A declared project component is missing or has an invalid length.");
        }
    }

    private static void ValidateComponentDescriptor(PvadProjectComponentDescriptor component)
    {
        if (component.Kind != PvadProjectComponentKinds.CodeWorkspace ||
            component.Path != PvadProjectFormat.CodeWorkspaceEntry ||
            component.SchemaVersion != PvadCodeWorkspaceProjectComponent.CurrentSchemaVersion ||
            component.SecurityClassification != PvadProjectComponentKinds.ConfidentialSource ||
            component.Length < 0 || component.Length > 10 * 1024 * 1024 || !IsSha256(component.Sha256))
            throw Error("PXAPROJECT016", "A project component descriptor is invalid.");
        ValidatePath(component.Path);
    }

    private static async Task WriteFileAtomicallyAsync(
        string destinationPath,
        byte[] package,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The destination must include a directory.", nameof(destinationPath));
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"The destination directory does not exist: '{directory}'.");
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(package, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void ValidateDocument<TDocument>(
        TDocument document,
        PvadProjectReadLimits limits,
        CancellationToken cancellationToken)
    {
        if (document is DesignExportDto pdf)
        {
            RequireBounded(pdf.Id, 200, "document ID");
            RequireBounded(pdf.Name, 200, "document name");
            RequireOptionalBounded(pdf.Category, 200, "document category");
            RequireOptionalBounded(pdf.Description, 4_000, "document description", allowLineBreaks: true);
            ValidatePdfMetadata(pdf.PageSettings?.Metadata);
            var extensions = new List<Dictionary<string, System.Text.Json.JsonElement>?>
            {
                pdf.Extensions,
                pdf.PageSettings?.Extensions,
            };
            if (pdf.Pages.Count > limits.MaximumPages)
                throw Error("PXAPROJECT005", "The PDF project exceeds its page limit.");
            long elements = pdf.SharedElements.Count;
            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            var elementIds = new HashSet<string>(StringComparer.Ordinal);
            var styleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var style in pdf.PageSettings?.NamedStyles ?? [])
            {
                RequireBounded(style.Id, 200, "named style ID");
                if (!styleIds.Add(style.Id))
                    throw Error("PXAPROJECT004", "The PDF project contains a duplicate named style ID.");
            }
            extensions.AddRange(pdf.SharedElements.Select(element => element.Extensions));
            foreach (var page in pdf.Pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireBounded(page.Id, 200, "page ID");
                if (!pageIds.Add(page.Id))
                    throw Error("PXAPROJECT004", "The PDF project contains a duplicate page ID.");
                extensions.Add(page.Extensions);
                extensions.AddRange(page.Elements.Select(element => element.Extensions));
                foreach (var element in page.Elements)
                {
                    ValidateElementContent(element);
                    if (!elementIds.Add(element.Id))
                        throw Error("PXAPROJECT004", "The PDF project contains a duplicate element ID.");
                }
                elements += page.Elements.Count;
                if (elements > limits.MaximumElements)
                    throw Error("PXAPROJECT005", "The PDF project exceeds its element limit.");
            }
            foreach (var element in pdf.SharedElements)
            {
                ValidateElementContent(element);
                if (!elementIds.Add(element.Id))
                    throw Error("PXAPROJECT004", "The PDF project contains a duplicate element ID.");
            }
            ValidateExtensionData(extensions, limits);
        }
        else if (document is SpreadsheetDto spreadsheet)
        {
            RequireBounded(spreadsheet.Id, 200, "workbook ID");
            RequireBounded(spreadsheet.Name, 200, "workbook name");
            var extensions = new List<Dictionary<string, System.Text.Json.JsonElement>?>
            {
                spreadsheet.Extensions,
            };
            if (spreadsheet.Sheets.Count > limits.MaximumSheets)
                throw Error("PXAPROJECT005", "The Spreadsheet project exceeds its sheet limit.");
            long cells = 0;
            long formulas = 0;
            var sheetIds = new HashSet<string>(StringComparer.Ordinal);
            var imageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sheet in spreadsheet.Sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireBounded(sheet.Id, 200, "worksheet ID");
                RequireBounded(sheet.Name, 200, "worksheet name");
                if (!sheetIds.Add(sheet.Id))
                    throw Error("PXAPROJECT004", "The Spreadsheet project contains a duplicate worksheet ID.");
                foreach (var image in sheet.Images)
                {
                    RequireBounded(image.Id, 200, "worksheet image ID");
                    if (!imageIds.Add(image.Id))
                        throw Error("PXAPROJECT004", "The Spreadsheet project contains a duplicate image ID.");
                }
                extensions.Add(sheet.Extensions);
                extensions.AddRange(sheet.Cells.Select(cell => cell.Extensions));
                extensions.AddRange(sheet.Images.Select(image => image.Extensions));
                cells += sheet.Cells.Count;
                formulas += sheet.Cells.Count(cell => !string.IsNullOrWhiteSpace(cell.Formula));
                if (cells > limits.MaximumCells || formulas > limits.MaximumFormulas)
                    throw Error("PXAPROJECT005", "The Spreadsheet project exceeds its cell or formula limit.");
            }
            ValidateExtensionData(extensions, limits);
        }
    }

    private static void ValidateElementContent(ElementDto element)
    {
        RequireBounded(element.Id, 200, "element ID");
        RequireBounded(element.Type, 100, "element type");
        RequireOptionalBounded(element.Name, 500, "element name");
        RequireOptionalBounded(element.HtmlContent, PvadRichTextSanitizer.MaximumHtmlLength,
            "rich-text content", allowLineBreaks: true);
        RequireOptionalBounded(element.Content, PvadRichTextSanitizer.MaximumHtmlLength,
            "element content", allowLineBreaks: true);
    }

    private static void ValidatePdfMetadata(PdfMetadataDto? metadata)
    {
        if (metadata is null)
            return;
        RequireOptionalBounded(metadata.Title, 1_000, "metadata title");
        RequireOptionalBounded(metadata.Author, 1_000, "metadata author");
        RequireOptionalBounded(metadata.Subject, 2_000, "metadata subject", allowLineBreaks: true);
        RequireOptionalBounded(metadata.Keywords, 4_000, "metadata keywords");
    }

    private static void RequireOptionalBounded(
        string? value,
        int maximumLength,
        string field,
        bool allowLineBreaks = false)
    {
        if (value is null)
            return;
        var hasForbiddenControl = value.Any(character => char.IsControl(character) &&
            !(allowLineBreaks && character is '\r' or '\n' or '\t'));
        if (value.Length > maximumLength || hasForbiddenControl)
            throw Error("PXAPROJECT008", $"The project {field} is invalid.");
    }

    private static void ValidateExtensionData(
        IEnumerable<Dictionary<string, System.Text.Json.JsonElement>?> extensionSets,
        PvadProjectReadLimits limits)
    {
        var propertyCount = 0;
        long byteCount = 0;
        foreach (var extensions in extensionSets)
        {
            if (extensions is null)
                continue;
            foreach (var (name, value) in extensions)
            {
                if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Any(char.IsControl) ||
                    value.ValueKind == System.Text.Json.JsonValueKind.Undefined ||
                    IsSensitiveExtensionName(name) || ContainsUnsafeExtensionValue(value))
                    throw Error("PXAPROJECT008", "The project contains invalid extension data.");
                propertyCount++;
                byteCount += Encoding.UTF8.GetByteCount(name);
                byteCount += Encoding.UTF8.GetByteCount(value.GetRawText());
                if (propertyCount > limits.MaximumExtensionProperties || byteCount > limits.MaximumExtensionBytes)
                    throw Error("PXAPROJECT005", "The project exceeds its forward-compatible extension-data limit.");
            }
        }
    }

    private static bool IsSensitiveExtensionName(string name)
    {
        var normalized = new string(name.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return normalized.Contains("password", StringComparison.Ordinal) ||
               normalized.Contains("accesstoken", StringComparison.Ordinal) ||
               normalized.Contains("refreshtoken", StringComparison.Ordinal) ||
               normalized.Contains("apikey", StringComparison.Ordinal) ||
               normalized.Contains("clientsecret", StringComparison.Ordinal) ||
               normalized.Contains("signedurl", StringComparison.Ordinal) ||
               normalized.Contains("tenantid", StringComparison.Ordinal);
    }

    private static bool ContainsUnsafeExtensionValue(System.Text.Json.JsonElement value)
    {
        switch (value.ValueKind)
        {
            case System.Text.Json.JsonValueKind.String:
                var text = value.GetString();
                if (string.IsNullOrEmpty(text))
                    return false;
                return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                       text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                       text.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                       text.StartsWith('/') ||
                       (text.Length >= 3 && char.IsAsciiLetter(text[0]) && text[1] == ':' &&
                        (text[2] == '\\' || text[2] == '/'));
            case System.Text.Json.JsonValueKind.Object:
                return value.EnumerateObject().Any(property =>
                    IsSensitiveExtensionName(property.Name) || ContainsUnsafeExtensionValue(property.Value));
            case System.Text.Json.JsonValueKind.Array:
                return value.EnumerateArray().Any(ContainsUnsafeExtensionValue);
            default:
                return false;
        }
    }

    private static void ValidateLimits(PvadProjectReadLimits limits)
    {
        if (limits.MaximumCompressedBytes <= 0 || limits.MaximumExpandedBytes <= 0 ||
            limits.MaximumEntries is <= 0 or > 10_000 || limits.MaximumEntryBytes <= 0 ||
            limits.MaximumCompressionRatio < 1 || limits.MaximumJsonDepth is <= 0 or > 512 ||
            limits.MaximumPages <= 0 || limits.MaximumElements <= 0 || limits.MaximumSheets <= 0 ||
            limits.MaximumCells <= 0 || limits.MaximumFormulas <= 0 || limits.MaximumImagePixels <= 0 ||
            limits.MaximumProcessingDuration <= TimeSpan.Zero ||
            limits.MaximumProcessingDuration > TimeSpan.FromMinutes(5) ||
            limits.MaximumExtensionProperties <= 0 || limits.MaximumExtensionBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(limits));
    }

    private static PvadProjectFormatException Error(string code, string message) => new(code, message);

    private sealed record ValidatedArchive(
        PvadProjectManifest Manifest,
        SortedDictionary<string, byte[]> Entries,
        byte[] ArchiveBytes,
        PvadProjectReadLimits Limits,
        ProcessingBudget Budget);

    private sealed class ProcessingBudget
    {
        private readonly CancellationToken cancellationToken;
        private readonly long deadline;

        public ProcessingBudget(TimeSpan maximumDuration, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            var durationTicks = maximumDuration.TotalSeconds * Stopwatch.Frequency;
            deadline = Stopwatch.GetTimestamp() + checked((long)Math.Ceiling(durationTicks));
        }

        public void ThrowIfExceeded()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Stopwatch.GetTimestamp() >= deadline)
                throw Error("PXAPROJECT005", "The project package exceeds its processing-time limit.");
        }
    }
}
