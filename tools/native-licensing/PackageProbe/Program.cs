using System.IO.Compression;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVAD.Licensing;

if (args.Length != 3)
    throw new ArgumentException("Expected license path, candidate package, and protected assembly path.");
if (PvadLicense.GetStatus().State != PvadLicenseState.Unlicensed)
    throw new InvalidOperationException("Unexpected implicit activation.");
try
{
    PvadLicense.Require(PvadLicenseProducts.Pdf);
    throw new InvalidOperationException("Unlicensed operation was admitted.");
}
catch (PvadLicenseException exception) when (exception.Code == "PVADLIC001") { }

using var stream = File.OpenRead(args[0]);
var status = PvadLicense.LoadFromStream(stream);
if (!stream.CanRead || !status.CanStartOperation || PvadLicense.Require("pdf").LicenseId != status.LicenseId)
    throw new InvalidOperationException("Protected package failed offline activation.");
if (PvadLicense.LoadFromFile(args[0]).LicenseId != status.LicenseId)
    throw new InvalidOperationException("File and stream activation disagree.");
try
{
    PvadLicense.Require("word");
    throw new InvalidOperationException("An unlicensed product was admitted.");
}
catch (PvadLicenseException exception) when (exception.Code == "PVADLIC012") { }

using var malformed = new MemoryStream("{}"u8.ToArray());
try
{
    PvadLicense.LoadFromStream(malformed);
    throw new InvalidOperationException("Malformed replacement was admitted.");
}
catch (PvadLicenseException exception) when (exception.Code == "PVADLIC004") { }
if (PvadLicense.GetStatus().LicenseId != status.LicenseId)
    throw new InvalidOperationException("Invalid replacement discarded the active license.");

using var zip = ZipFile.OpenRead(args[1]);
foreach (var entry in zip.Entries)
{
    if (new[] { ".cs", ".pdb", ".pem", ".pfx", ".snk", ".map" }.Any(extension => entry.FullName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("A package includes forbidden source, symbol, or key files.");
    using var content = entry.Open();
    using var bytes = new MemoryStream();
    content.CopyTo(bytes);
    var text = Encoding.UTF8.GetString(bytes.ToArray());
    if (text.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal) || text.Contains("-----BEGIN EC PRIVATE KEY-----", StringComparison.Ordinal))
        throw new InvalidOperationException("Private key material was found in the package.");
}
var assemblyEntry = zip.GetEntry("lib/net10.0/PVAD.Licensing.dll")
    ?? throw new InvalidOperationException("The package does not contain its expected library.");
using (var packaged = assemblyEntry.Open())
using (var expected = File.OpenRead(args[2]))
{
    if (!SHA256.HashData(packaged).AsSpan().SequenceEqual(SHA256.HashData(expected)))
        throw new InvalidOperationException("The package contains a different, possibly unprotected assembly.");
}
using (var protectedFile = File.OpenRead(args[2]))
using (var pe = new PEReader(protectedFile))
{
    if (pe.ReadDebugDirectory().Any(entry => entry.Type is DebugDirectoryEntryType.CodeView or DebugDirectoryEntryType.EmbeddedPortablePdb))
        throw new InvalidOperationException("Private debug information is present in the protected assembly.");
}

var assembly = typeof(PvadLicense).Assembly;
var signatures = assembly.GetExportedTypes().SelectMany(type =>
    type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Select(member => $"{type.FullName}:{member.MemberType}:{member}"))
    .Order(StringComparer.Ordinal).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new
{
    offlineActivation = true,
    missingProductRejected = true,
    malformedReplacementPreservedLicense = true,
    embeddedDebugInformationAbsent = true,
    dotnetProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
    dotnetRuntime = RuntimeInformation.FrameworkDescription,
    dotnetRuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
    protectedPackageVerified = true,
    publicApi = signatures,
}));
