# PXA SDK Roadmap Checklist

## Goal

Create a shared, versioned SDK platform that exposes one professional developer experience across PXA Cloud and customer-managed PXA Server installations.

## Priorities

- [ ] P0: Stabilize and validate the OpenAPI contract.
- [ ] P0: Establish shared generation, testing, and release infrastructure.
- [ ] P1: Release Java, TypeScript/JavaScript, and Python SDKs in that order.
- [ ] P2: Release C# REST, Go, PHP, and PowerShell clients.

## Public API Contract

- [ ] Treat the generated OpenAPI document as the single SDK source of truth.
- [ ] Introduce canonical SDK routes under `/api/pxa/v1`.
- [ ] Preserve existing routes as documented compatibility aliases.
- [ ] Assign a unique, stable `operationId` to every public operation.
- [ ] Remove compatibility aliases from generated SDK surfaces.
- [ ] Define stable schemas for multipart uploads and binary downloads.
- [ ] Standardize errors on Problem Details with stable PXA error codes and request IDs.
- [ ] Document authentication, idempotency, pagination, cancellation, timeout, and retry behavior where applicable.
- [ ] Publish the exact OpenAPI artifact used for every SDK release.

## SDK Architecture

- [ ] Generate an internal transport layer from OpenAPI for each language.
- [ ] Add a handwritten public facade with PDF, Spreadsheet, Templates, Migration, Import, and Export clients.
- [ ] Keep generated files isolated and never edit them manually.
- [ ] Support Cloud and On-Premise by changing only client configuration and `baseUrl`.
- [ ] Provide safe defaults for timeouts, uploads, downloads, and user-agent identification.
- [ ] Normalize authentication and errors across all languages.
- [ ] Keep commercial licensing and entitlement enforcement on PXA Server.

## Release And Compatibility

- [ ] Use semantic versioning for every SDK.
- [ ] Define compatibility rules between SDK, OpenAPI, and PXA Server versions.
- [ ] Publish supported-version and end-of-support matrices.
- [ ] Generate changelogs and migration notes for breaking changes.
- [ ] Publish packages to public language registries.
- [ ] Sign release artifacts and publish checksums and provenance metadata where supported.
- [ ] Automate releases from protected version tags after contract and integration tests pass.

## Shared Tests

- [ ] Validate OpenAPI syntax, schemas, examples, and unique operation IDs.
- [ ] Detect breaking contract changes in CI.
- [ ] Run generated-client compilation tests for every supported language.
- [ ] Run one shared behavior suite against a versioned PXA Server container.
- [ ] Cover authentication, errors, timeouts, retries, multipart uploads, binary downloads, and large streams.
- [ ] Run the same examples against Cloud-style and local-server base URLs.

## Acceptance Criteria

- [ ] One OpenAPI artifact can generate every SDK without manual schema patches.
- [ ] Public SDK APIs do not expose generated operation names directly.
- [ ] Legacy HTTP routes do not create duplicate SDK methods.
- [ ] Every released SDK declares its compatible PXA Server range.
- [ ] JavaScript and TypeScript share one implementation and package.
- [ ] Client packages contain no embedded customer or server credentials.
