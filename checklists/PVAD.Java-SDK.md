# PVAD Java SDK Checklist

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Current headings, goals and open tasks use PVAD/PVADE. Checked records, original examples and compatibility identifiers retain their historical names; task statuses are unchanged.

## Goal

Deliver a production-ready Java SDK for PVAD Cloud and PVAD Server with an idiomatic public API and a generated OpenAPI transport layer.

## Priority And Dependencies

- [ ] P1: Deliver the first public non-.NET PVAD SDK.
- [ ] Complete the P0 contract work in `PVAD.SDK-Roadmap.md` first.
- [ ] Verify domain ownership and reserve the proposed `com.pvade` Maven Central namespace before publication.

## Package And Runtime

- [ ] Target Java 17 or newer.
- [ ] Publish the proposed `com.pvade:pvad-java` artifact to Maven Central after publisher and artifact availability are verified.
- [ ] Use `com.pvade.pvad` as the proposed Java package namespace, subject to publisher approval.
- [ ] Generate the internal HTTP transport and DTO layer from OpenAPI.
- [ ] Expose a handwritten `PvadClient` builder and product-specific clients.
- [ ] Keep generated types out of primary documentation and examples.

## Client Features

- [ ] Add PDF, Spreadsheet, Templates, Migration, Import, and Export clients.
- [ ] Support synchronous and asynchronous operations.
- [ ] Stream uploads and downloads without buffering complete documents by default.
- [ ] Support API-key and bearer-token authentication.
- [ ] Add configurable base URL, timeouts, proxy, retry policy, and user agent.
- [ ] Map Problem Details responses to a stable `PvadException` hierarchy.
- [ ] Support deterministic resource cleanup with `AutoCloseable` where required.

## Distribution And Documentation

- [ ] Publish binary, sources, and Javadoc artifacts with signatures.
- [ ] Provide Maven and Gradle installation examples.
- [ ] Document Cloud and local Docker connection examples.
- [ ] Provide runnable invoice, migration, import, spreadsheet, and streaming examples.
- [ ] Automate generation, tests, signing, and Maven Central publication.

## Tests

- [ ] Compile on supported Java LTS versions.
- [ ] Unit-test facade mapping, authentication, errors, and configuration.
- [ ] Run integration tests against the versioned PVAD Server container.
- [ ] Test large multipart uploads, streamed downloads, cancellation, and timeouts.
- [ ] Verify public API compatibility before every release.

## Acceptance Criteria

- [ ] A customer installs one documented Maven dependency.
- [ ] The same code runs against PVAD Cloud and local PVAD Server.
- [ ] Common workflows do not require direct use of generated classes.
- [ ] The SDK performs no document processing locally and documents that boundary clearly.
