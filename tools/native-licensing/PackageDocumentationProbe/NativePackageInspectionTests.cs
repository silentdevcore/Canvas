using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class NativePackageInspectionTests
{
    private const string Payload = "runtimes/linux-x64/native/libsynthetic.so";

    internal static object Run()
    {
        var root = Directory.CreateTempSubdirectory("pvad-native-package-tests-").FullName;
        var checks = new List<string>();
        try
        {
            (string Directory, string Archive) Fixture(params (string File, string Text, int Attributes)[] entries)
            {
                var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                var archive = Path.Combine(directory, "synthetic.1.0.0.nupkg");
                using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
                    foreach (var item in entries)
                    {
                        var entry = zip.CreateEntry(item.File);
                        entry.ExternalAttributes = item.Attributes;
                        using var stream = entry.Open(); stream.Write(Encoding.UTF8.GetBytes(item.Text));
                        if (item.File == Payload)
                        {
                            var cached = Path.Combine(directory, Payload.Replace('/', Path.DirectorySeparatorChar));
                            Directory.CreateDirectory(Path.GetDirectoryName(cached)!); File.WriteAllText(cached, item.Text);
                        }
                    }
                File.WriteAllText(archive + ".sha512", Convert.ToBase64String(SHA512.HashData(File.ReadAllBytes(archive))));
                return (directory, archive);
            }
            void Denied(string name, Action action)
            {
                try { action(); }
                catch (Exception error) when (error is InvalidDataException or FormatException or JsonException or IOException)
                { checks.Add(name); return; }
                throw new InvalidOperationException("Expected native inspection rejection: " + name);
            }
            object Inspect((string Directory, string Archive) fixture, string? manifest = null) =>
                NativePackageInspection.Inspect(fixture.Archive, fixture.Directory, manifest ?? JsonSerializer.Serialize(new[] { Payload }));

            var valid = Fixture((Payload, "synthetic native payload", 0), ("LICENSE", "synthetic notice", 0));
            var report = JsonSerializer.SerializeToElement(Inspect(valid));
            var asset = report.GetProperty("assets").EnumerateArray().Single();
            if (asset.GetProperty("file").GetString() != Payload || asset.GetProperty("bytes").GetInt32() != 24 ||
                asset.GetProperty("sha256").GetString() != Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("synthetic native payload"))) ||
                report.GetProperty("archiveAuthenticity").GetString() != "not-verified")
                throw new InvalidOperationException("Native positive evidence mismatch.");
            checks.Add("exact-archive-cache-payload-and-unverified-authenticity");

            File.WriteAllText(Path.Combine(valid.Directory, Payload), "synthetic native payloae");
            Denied("same-size-cache-tampering", () => Inspect(valid));
            File.WriteAllText(Path.Combine(valid.Directory, Payload), "short");
            Denied("cache-size-drift", () => Inspect(valid));

            var digest = Fixture((Payload, "payload", 0));
            File.WriteAllText(digest.Archive + ".sha512", "not a digest");
            Denied("malformed-cache-digest", () => Inspect(digest));
            File.WriteAllText(digest.Archive + ".sha512", Convert.ToBase64String(new byte[64]));
            Denied("different-cache-digest", () => Inspect(digest));

            var changed = Fixture((Payload, "payload", 0));
            using (var file = File.Open(changed.Archive, FileMode.Append)) file.WriteByte(1);
            Denied("changed-archive", () => Inspect(changed));
            var incomplete = Fixture((Payload, "payload", 0), ("runtimes/win-x64/native/extra.dll", "extra", 0));
            Denied("archive-native-entry-omitted-from-restore", () => Inspect(incomplete));
            Denied("restore-native-entry-missing-from-archive", () => Inspect(valid, JsonSerializer.Serialize(new[] { Payload, "native/extra.dll" })));
            Denied("duplicate-restore-path", () => Inspect(valid, JsonSerializer.Serialize(new[] { Payload, Payload })));
            Denied("empty-restore-manifest", () => Inspect(valid, "[]"));
            Denied("oversized-restore-json", () => Inspect(valid, new string(' ', 128 * 1024 + 1)));
            Denied("too-many-restore-paths", () => Inspect(valid, JsonSerializer.Serialize(Enumerable.Range(0, 513).Select(index => $"native/{index}.dll"))));
            foreach (var path in new[] { "../escape.so", "/absolute.so", "native/../escape.so", "native\\escape.dll", "C:/escape.dll" })
            {
                var unsafeArchive = Fixture((Payload, "payload", 0), (path, "unsafe", 0));
                Denied("unsafe-archive-path:" + path, () => Inspect(unsafeArchive));
            }
            var duplicate = Fixture((Payload, "first", 0), (Payload, "second", 0));
            Denied("duplicate-archive-entry", () => Inspect(duplicate));
            var caseDuplicate = Fixture((Payload, "first", 0), (Payload.ToUpperInvariant(), "second", 0));
            Denied("case-colliding-archive-entry", () => Inspect(caseDuplicate));
            var symbolic = Fixture((Payload, "payload", 0), ("native/link.so", "target", unchecked((int)0xa1ff0000)));
            Denied("archive-symlink", () => Inspect(symbolic));
            Denied("archive-outside-cache-root", () => NativePackageInspection.Inspect(valid.Archive, root, JsonSerializer.Serialize(new[] { Payload })));

            foreach (var path in new[] { "x64/leptonica.dll", "runtimes/osx-arm64/native/libSkiaSharp.dylib", "native/data.bin", "libs/libextra.so.2", "assets/module.wasm" })
                if (!NativePackageInspection.IsPayload(path)) throw new InvalidOperationException("Missing native classification: " + path);
            if (NativePackageInspection.IsPayload("lib/net10.0/Managed.dll") || NativePackageInspection.IsPayload("runtimes/win-x64/lib/net10.0/Managed.dll"))
                throw new InvalidOperationException("Managed runtime library misclassified.");
            checks.Add("native-and-managed-path-classification");
        }
        finally { Directory.Delete(root, recursive: true); }
        return new { schemaVersion = 1, checks, temporaryFixturesRemoved = !Directory.Exists(root),
            legalApprovalGranted = false, publicationReady = false };
    }
}
