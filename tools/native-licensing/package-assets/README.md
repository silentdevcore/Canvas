# PVADE Native .NET Libraries

The package identity and product version are recorded in
`documentation-manifest.json`. The technical library name is PVAD; the product
name is PVADE. Target framework: .NET 10.

- [Activate, renew and use the offline C# example](ACTIVATION.md).
- [License-reference status and release restrictions](LICENSE-REFERENCE.md).
- Dependency versions, declared licenses, available original notice files and
  unresolved license-text requirements: `third-party/inventory.json`.
- IntelliSense XML is next to the assembly under `lib/net10.0`. It retains
  existing public API comments; it is not a claim of complete API commentary.

Install matching versions from an approved supplied feed. For an isolated
network, obtain the complete first-party and third-party dependency feed and
the appropriate native runtime assets before disconnecting. Configure only
those local sources in NuGet.Config, then restore with that configuration.
Package installation may require a local feed; license verification itself
does not require an internet connection.

A signed native license enables the purchased processing products/providers.
It does not authenticate HTTP clients or confer an organization subscription.
Never include a manufacturer private key in a customer application. The
activation example accepts paths from application configuration, not user code.

Missing or invalid rights produce typed failures rather than output watermarks.
Keep licensing diagnostics separate from customer content and do not log
license bytes, keys or configured paths. Neither this package nor its examples
grant redistribution/OEM rights. Publication, approved keys, legal review,
third-party notices and platform acceptance remain separate release gates.
