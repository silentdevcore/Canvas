# PVAD.Licensing

Offline verification foundation for native PVAD .NET 10 libraries.

Supports explicit signed Trials, subscriptions with seven days of grace, and
perpetual licenses with release-date-based update eligibility. Loading performs
no network calls and never creates an implicit evaluation license.

```csharp
using PVAD.Licensing;

var status = PvadLicense.LoadFromFile(licensePath);
var admission = PvadLicense.Require(PvadLicenseProducts.Pdf);
```

A manufacturer-signed license and an approved embedded public verification key
are required. The development source trust register is intentionally empty.
Product integrations, authenticated issuance, and public distribution are still
being completed. This package alone does not add enforcement to existing engines.

Obfuscation raises inspection effort; it does not prevent reverse engineering.
Offline use cannot guarantee immediate revocation or global seat counting.
No sources, private debug symbols, or private signing keys belong in this package.
