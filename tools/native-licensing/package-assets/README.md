# PVADE Native .NET Libraries

The package identity and product version are recorded in
`documentation-manifest.json`. The technical library name is PVAD; the product
name is PVADE. Target framework: .NET 10.

- [Activate, renew and use the offline C# example](ACTIVATION.md).
- [License-reference status and release restrictions](LICENSE-REFERENCE.md).
- Dependency versions, declared licenses, available original notice files and
  unresolved license-text requirements: `third-party/inventory.json`.
- Declared native dependency payloads, archive/cache checksums, placement/RID
  declarations and associated original notices: `nativePackages` in
  `documentation-manifest.json`. These are not tested deployment selections,
  verified binary architectures or complete native-component license approvals.
- IntelliSense XML is next to the assembly under `lib/net10.0`. It retains
  existing public API comments; it is not a claim of complete API commentary.

Install matching versions from an approved supplied feed. For an isolated
network, obtain the complete first-party and third-party dependency feed and
the appropriate native runtime assets before disconnecting. Configure only
those local sources in NuGet.Config, then restore with that configuration.
Package installation may require a local feed; license verification itself
does not require an internet connection.

## Offline Installation

These packages are not yet published or approved for customer distribution.
The following procedure applies to an approved supplied feed, not a claim that
the packages are available on a public registry. Keep the exact package versions,
checksums and runtime selections from that delivery together.

Place the complete first-party and transitive NuGet packages in `offline-feed`
beside the application. A native license alone cannot provide missing packages,
fonts, OCR models, operating-system libraries or their redistribution rights.

Create `NuGet.Config` in that application directory:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="approved-local-feed" value="./offline-feed" />
  </packageSources>
  <auditSources>
    <clear />
  </auditSources>
</configuration>
```

Use the delivered version, not a floating wildcard. The illustrative `1.1.0`
pin below must match the approved delivery; it does not imply a public release:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="PVAD.Generator" Version="1.1.0" />
  </ItemGroup>
</Project>
```

From the application directory, resolve the approved dependency graph once:

```bash
dotnet restore --configfile ./NuGet.Config --packages ./restored-packages --use-lock-file
dotnet build --no-restore
```

Retain `packages.lock.json` with the application. Subsequent verification uses:

```bash
dotnet restore --configfile ./NuGet.Config --packages ./restored-packages --locked-mode
dotnet build --no-restore
```

The audit-source reset prevents inherited online advisory endpoints. A folder
feed does not supply vulnerability advisories, so this isolated restore is not
a security scan. Review direct and transitive dependencies in the approved
delivery pipeline and retain that evidence before disconnecting. For a local
advisory service, configure it explicitly rather than silently adding a public
endpoint. See [NuGet audit-source behavior](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).

Include the class from the [activation example](ACTIVATION.md), then call
`Activate` and `CreatePdf` from the application entry point. Load an issued
license before processing. Store license paths in application
configuration; never embed a manufacturer private key. `PVAD.Generator` brings
shared .NET libraries transitively, but does not grant all product rights.
For a direct Importer, Converter or Migration package, select that delivered
package and its corresponding product/provider grant instead.

For a target RID, restore the same delivered application with `--runtime`
and its approved RID, and use the same runtime for build/publish. A successful
restore is not evidence that Linux libraries, Windows native binaries or macOS
dependencies exist on the destination. Check the delivery's supported runtime
and original notices before moving it to an isolated machine. Do not silently
add a public feed or upgrade dependencies to work around a missing local file.

If restore reports a missing package, obtain that exact version from the
approved delivery. If activation fails, retain only the typed diagnostic and
request the correct signed product/provider artifact; changing its JSON breaks
the signature. Missing manufacturer trust is a release/configuration issue,
not a reason to install a test key or disable verification.

A signed native license enables the purchased processing products/providers.
It does not authenticate HTTP clients or confer an organization subscription.
Never include a manufacturer private key in a customer application. The
activation example accepts paths from application configuration, not user code.

Missing or invalid rights produce typed failures rather than output watermarks.
Keep licensing diagnostics separate from customer content and do not log
license bytes, keys or configured paths. Neither this package nor its examples
grant redistribution/OEM rights. Publication, approved keys, legal review,
third-party notices and platform acceptance remain separate release gates.

Native-directory inventories can include vendor symbols or platform metadata,
not only executable libraries. Archive/cache equality does not authenticate a
publisher or establish the license of every statically linked component. Keep
the original vendor notice texts; do not replace them with the wrapper package's
declared license. Container operating-system libraries and application-owned
OCR models/fonts require their separate inventories and reviews.
