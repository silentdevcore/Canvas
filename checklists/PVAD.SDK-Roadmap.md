# PVAD SDK Roadmap Checklist

Native .NET distribution and protection are tracked separately in
[Native Library Licensing And Protection](PVAD.Native-Library-Licensing-And-Protection.md).

## Goal

Create a shared, versioned SDK platform that exposes one professional developer experience across PVAD Cloud and customer-managed PVAD Server installations.

## Priorities

- [ ] P0: Stabilize and validate the OpenAPI contract.
- [ ] P0: Establish shared generation, testing, and release infrastructure.
- [ ] P1: Release Java, TypeScript/JavaScript, and Python SDKs in that order.
- [ ] P2: Release C# REST, Go, PHP, and PowerShell clients.

## Public API Contract

- [ ] Treat the generated OpenAPI document as the single SDK source of truth.
- [ ] Generate SDKs against canonical routes under `/api/pvad/v1`.
- [ ] Preserve existing routes as documented compatibility aliases.
- [ ] Assign a unique, stable `operationId` to every public operation.
- [ ] Remove compatibility aliases from generated SDK surfaces.
- [ ] Define stable schemas for multipart uploads and binary downloads.
- [ ] Standardize errors on Problem Details with stable machine diagnostic codes and request IDs; PVAD display aliases do not rename existing wire identifiers.
- [ ] Document authentication, idempotency, pagination, cancellation, timeout, and retry behavior where applicable.
- [ ] Publish the exact OpenAPI artifact used for every SDK release.

## SDK Architecture

This roadmap covers remote transport clients, not the embedded .NET engine.
[Native Library Activation And Renewal](../docs/guides/PVAD.Native-Library-Activation.md)
describes activation only for applications loading native PVAD assemblies.
Java/Python/TypeScript/JavaScript and C# REST clients authenticate to PVADE Server;
the operator activates the server's native processing license separately.
Clients contain neither an engine-activation bypass nor manufacturer signing
keys. Native-license HTTP 503 diagnostics remain separate from 401/403 user,
tenant, permission and entitlement failures. Renewing a server subscription or
downloading a native artifact does not automatically reload a running native
engine or its workers. This clarification does not implement an SDK, publish a
package, select a publisher or complete the unchecked client tasks below.

- [ ] Generate an internal transport layer from OpenAPI for each language.
- [ ] Add a handwritten public facade with PDF, Spreadsheet, Templates, Migration, Import, and Export clients.
- [ ] Keep generated files isolated and never edit them manually.
- [ ] Support Cloud and On-Premise by changing only client configuration and `baseUrl`.
- [ ] Provide safe defaults for timeouts, uploads, downloads, and user-agent identification.
- [ ] Normalize authentication and errors across all languages.
- [ ] Keep commercial licensing and entitlement enforcement on PVAD Server.

## Release And Compatibility

- [ ] Use semantic versioning for every SDK.
- [ ] Define compatibility rules between SDK, OpenAPI, and PVAD Server versions.
- [ ] Publish supported-version and end-of-support matrices.
- [ ] Generate changelogs and migration notes for breaking changes.
- [ ] Publish packages to public language registries.
- [ ] Sign release artifacts and publish checksums and provenance metadata where supported.
- [ ] Automate releases from protected version tags after contract and integration tests pass.

## Shared Tests

- [ ] Validate OpenAPI syntax, schemas, examples, and unique operation IDs.
- [ ] Detect breaking contract changes in CI.
- [ ] Run generated-client compilation tests for every supported language.
- [ ] Run one shared behavior suite against a versioned PVAD Server container.
- [ ] Cover authentication, errors, timeouts, retries, multipart uploads, binary downloads, and large streams.
- [ ] Run the same examples against Cloud-style and local-server base URLs.

## Acceptance Criteria

- [ ] One OpenAPI artifact can generate every SDK without manual schema patches.
- [ ] Public SDK APIs do not expose generated operation names directly.
- [ ] Legacy HTTP routes do not create duplicate SDK methods.
- [ ] Every released SDK declares its compatible PVAD Server range.
- [ ] JavaScript and TypeScript share one implementation and package.
- [ ] Client packages contain no embedded customer or server credentials.

## Publisher Status

No SDK publisher or package name is currently registered or approved for publication.
Names in the language checklists are proposals only. `pvade.com` is the selected
product domain; this does not establish ownership of npm, NuGet, Maven Central,
PyPI, Go, Packagist, PowerShell Gallery, or container-registry namespaces.
Publishing remains a separate approval gate. No SDK or package is published by
this documentation update.
