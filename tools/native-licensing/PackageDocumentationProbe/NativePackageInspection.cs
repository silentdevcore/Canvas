using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class NativePackageInspection
{
    private const long MaximumArchiveBytes = 256L * 1024 * 1024;
    private const long MaximumPayloadBytes = 128L * 1024 * 1024;
    private const long MaximumExpandedBytes = 512L * 1024 * 1024;

    internal static bool IsPayload(string path) =>
        path.Split('/').Contains("native", StringComparer.OrdinalIgnoreCase) ||
        Regex.IsMatch(path, @"\.(?:so(?:\.\d+)*|dylib|a|lib|wasm)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
        Regex.IsMatch(path, @"^(?:x86|x64|arm64)/.*\.dll$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl) ||
            path.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("Unsafe native package entry path.");
        return path;
    }

    private static string RegularFile(string root, string relative)
    {
        var current = Path.GetFullPath(root);
        if (!Directory.Exists(current) || (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Unsafe native package cache root.");
        foreach (var part in ValidatePath(relative).Split('/'))
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Native package symlinks are not accepted.");
        }
        if (!File.Exists(current)) throw new InvalidDataException("Missing native package file.");
        return current;
    }

    internal static object Inspect(string archivePath, string cacheRoot, string expectedJson)
    {
        if (expectedJson.Length > 128 * 1024) throw new InvalidDataException("Native manifest is too large.");
        var expected = JsonSerializer.Deserialize<string[]>(expectedJson) ?? throw new InvalidDataException("Missing native manifest.");
        if (expected.Length is 0 or > 512 || expected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != expected.Length ||
            expected.Any(path => !IsPayload(ValidatePath(path)))) throw new InvalidDataException("Invalid native manifest.");
        var archive = Path.GetFullPath(archivePath);
        if (Path.GetDirectoryName(archive) != Path.GetFullPath(cacheRoot)) throw new InvalidDataException("Archive is outside its cache root.");
        archive = RegularFile(cacheRoot, Path.GetFileName(archive));
        var info = new FileInfo(archive);
        if (info.Length is <= 0 or > MaximumArchiveBytes) throw new InvalidDataException("Invalid native archive size.");
        var digestPath = RegularFile(cacheRoot, Path.GetFileName(archive) + ".sha512");
        if (new FileInfo(digestPath).Length > 1024) throw new InvalidDataException("Invalid native archive digest size.");
        var expectedDigest = Convert.FromBase64String(File.ReadAllText(digestPath).Trim());
        if (expectedDigest.Length != 64) throw new InvalidDataException("Invalid native archive digest.");
        using var stream = File.OpenRead(archive);
        var archiveSha512 = SHA512.HashData(stream);
        if (!CryptographicOperations.FixedTimeEquals(archiveSha512, expectedDigest)) throw new InvalidDataException("Native archive digest changed.");
        stream.Position = 0;
        var archiveSha256 = Convert.ToHexStringLower(SHA256.HashData(stream));
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        if (zip.Entries.Count > 10000) throw new InvalidDataException("Too many native archive entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            var path = ValidatePath(entry.FullName.TrimEnd('/'));
            if (!names.Add(path) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                throw new InvalidDataException("Duplicate or symbolic native archive entry.");
            expanded = checked(expanded + entry.Length);
            if (expanded > MaximumExpandedBytes) throw new InvalidDataException("Native archive expanded size limit exceeded.");
        }
        var payloads = zip.Entries.Where(entry => !entry.FullName.EndsWith('/') && IsPayload(entry.FullName))
            .OrderBy(entry => entry.FullName, StringComparer.Ordinal).ToArray();
        if (!payloads.Select(entry => entry.FullName).SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Native archive and restore manifest disagree.");
        var assets = new List<object>();
        foreach (var entry in payloads)
        {
            if (entry.Length > MaximumPayloadBytes) throw new InvalidDataException("Native payload size limit exceeded.");
            var local = RegularFile(cacheRoot, entry.FullName);
            if (new FileInfo(local).Length != entry.Length) throw new InvalidDataException("Native cache payload size changed.");
            using var archived = entry.Open();
            var archivedHash = SHA256.HashData(archived);
            using var cached = File.OpenRead(local);
            if (!CryptographicOperations.FixedTimeEquals(archivedHash, SHA256.HashData(cached)))
                throw new InvalidDataException("Native cache payload differs from archive.");
            assets.Add(new { file = entry.FullName, bytes = entry.Length, sha256 = Convert.ToHexStringLower(archivedHash) });
        }
        return new { schemaVersion = 1, archiveSha256, archiveSha512 = Convert.ToBase64String(archiveSha512),
            cacheDigestVerified = true, archiveAuthenticity = "not-verified", assets };
    }
}
