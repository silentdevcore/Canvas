using System.Text;
using System.Text.Json;
using PVAD.Licensing;

if (args.Length != 3) return 64;
var checks = new List<string>();
void Check(bool value, string name)
{
    if (!value || checks.Contains(name)) throw new InvalidOperationException(name);
    checks.Add(name);
}
void Denied(Action action, string code, string name)
{
    try { action(); throw new InvalidOperationException(name); }
    catch (PvadLicenseException error) { Check(error.Code == code, name); }
}
var licenses = args[0];
var integrated = Path.Combine(licenses, "integrated.json");
var pdf = Path.Combine(licenses, "pdf.json");
var word = Path.Combine(licenses, "word.json");
Check(NativeActivationExample.Inspect().State == PvadLicenseState.Unlicensed, "inspection-does-not-activate");
Denied(() => NativeActivationExample.CreatePdf(), "PVADLIC001", "missing-license-denied");
NativeActivationExample.Activate(word);
Check(NativeActivationExample.Inspect().CanStartOperation, "state-is-not-a-product-grant");
Denied(() => NativeActivationExample.CreatePdf(), "PVADLIC012", "wrong-product-denied");
Check(NativeActivationExample.Activate(integrated).CanStartOperation, "explicit-file-activation");
Check(NativeActivationExample.CreatePdf().AsSpan().StartsWith("%PDF-"u8), "example-generates-real-pdf");

using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("prefix").Concat(File.ReadAllBytes(pdf)).ToArray()))
{
    stream.Position = 6;
    Check(NativeActivationExample.ActivateFromStream(stream).CanStartOperation, "stream-current-position");
    Check(stream.CanRead, "stream-remains-open");
}
var prior = NativeActivationExample.Inspect().LicenseId;
using (var invalid = File.OpenRead(Path.Combine(licenses, "expired.json")))
{
    Check(!NativeActivationExample.TryLoadReplacement(invalid, out var code) && code == "PVADLIC009", "expired-replacement-rejected");
    Check(invalid.CanRead, "replacement-stream-remains-open-on-failure");
}
Check(NativeActivationExample.Inspect().LicenseId == prior, "failed-renewal-keeps-snapshot");
Check(NativeActivationExample.CreatePdf().AsSpan().StartsWith("%PDF-"u8), "failed-renewal-keeps-pdf-rights");
using (var replacement = File.OpenRead(word))
    Check(NativeActivationExample.TryLoadReplacement(replacement, out var code) && code is null, "valid-replacement-accepted");
Denied(() => NativeActivationExample.CreatePdf(), "PVADLIC012", "replacement-replaces-not-merges-grants");
using (var replacement = File.OpenRead(pdf))
    Check(NativeActivationExample.TryLoadReplacement(replacement, out var code) && code is null, "renewal-needs-no-process-restart");
var bytes = NativeActivationExample.CreatePdf();
Check(bytes.AsSpan().StartsWith("%PDF-"u8), "renewed-example-generates-pdf");
File.WriteAllBytes(args[1], bytes);
Denied(() => NativeActivationExample.Activate(pdf, args[2]), "PVADLIC014", "revocations-imported-before-activation");
Check(NativeActivationExample.Inspect().State == PvadLicenseState.Revoked, "revoked-state-visible");
Denied(() => NativeActivationExample.CreatePdf(), "PVADLIC014", "revoked-generation-denied");
NativeActivationExample.Activate(integrated);
Check(PvadLicense.GetRevocationStatus().RevokedLicenseCount == 2, "replacement-does-not-clear-revocations");
Check(NativeActivationExample.CreatePdf().AsSpan().StartsWith("%PDF-"u8), "unrevoked-replacement-remains-usable");
Console.WriteLine(JsonSerializer.Serialize(new { checks, architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() }));
return 0;
