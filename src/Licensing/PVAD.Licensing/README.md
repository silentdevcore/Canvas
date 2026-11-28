# PVAD.Licensing

Offline verification foundation for native PVAD .NET 10 libraries.

Supports explicit signed Trials, subscriptions with seven days of grace, and
perpetual licenses with release-date-based update eligibility. Loading performs
no network calls and never creates an implicit evaluation license.

Version-two artifacts bind account type and explicit feature/format capabilities
to the signature. Product grants remain mandatory; `Require(product)` alone
does not authorize a restricted format. Missing capability rights fail with
`PVADLIC016`. Version-one artifacts keep their product/provider semantics, and
older libraries reject version-two artifacts rather than ignoring restrictions.
Deploy a matching upgraded assembly set before loading a scoped artifact.

The developer lifecycle guide is available in the repository at
`docs/guides/PVAD.Native-Library-Activation.md` and in PVADE Documentation under
Code SDK > Code path > Related links > Native library activation guide.
Native activation is distinct from Account login, server subscriptions and REST
SDK authentication. Valid replacements replace the complete grant set; loading
or downloading an artifact is not an automatic cross-process reload.

```csharp
using PVAD.Licensing;

var status = PvadLicense.LoadFromFile(licensePath);
var admission = PvadLicense.Require(PvadLicenseProducts.Pdf);
```

Optional offline revocations must be imported explicitly, including after restart:

```csharp
PvadLicense.ImportRevocationsFromFile(revocationListPath);
var revocations = PvadLicense.GetRevocationStatus();
```

Only cumulative manufacturer-signed lists are accepted. Revoked IDs cannot be
restored by loading another list or reloading their license. Stale lists retain
known revocations and report freshness metadata; they do not require networking.
Rollback rejection is process-local, not persistent hardware-backed protection.
Preserve and reimport the latest approved file. Previously admitted operations
may complete; new processing with a revoked ID fails with `PVADLIC014`.

A manufacturer-signed license and an approved embedded public verification key
are required. The development source trust register is intentionally empty.
Current PVAD product integrations and manufacturer issuance use this verifier;
joint protected packaging and public distribution are still being completed.

Obfuscation raises inspection effort; it does not prevent reverse engineering.
Offline use cannot guarantee immediate revocation or global seat counting.
No sources, private debug symbols, or private signing keys belong in this package.
