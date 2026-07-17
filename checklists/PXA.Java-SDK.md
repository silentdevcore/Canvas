# PXA Java SDK Checklist

## Goal

Deliver a production-ready Java SDK for PXA Cloud and PXA Server with an idiomatic public API and a generated OpenAPI transport layer.

## Priority And Dependencies

- [ ] P1: Deliver the first public non-.NET PXA SDK.
- [ ] Complete the P0 contract work in `PXA.SDK-Roadmap.md` first.
- [ ] Reserve and verify the `com.powerdox` Maven Central namespace.

## Package And Runtime

- [ ] Target Java 17 or newer.
- [ ] Publish `com.powerdox:pxa-java` to Maven Central.
- [ ] Use `com.powerdox.pxa` as the public Java package namespace.
- [ ] Generate the internal HTTP transport and DTO layer from OpenAPI.
- [ ] Expose a handwritten `PxaClient` builder and product-specific clients.
- [ ] Keep generated types out of primary documentation and examples.

## Client Features

- [ ] Add PDF, Spreadsheet, Templates, Migration, Import, and Export clients.
- [ ] Support synchronous and asynchronous operations.
- [ ] Stream uploads and downloads without buffering complete documents by default.
- [ ] Support API-key and bearer-token authentication.
- [ ] Add configurable base URL, timeouts, proxy, retry policy, and user agent.
- [ ] Map Problem Details responses to a stable `PxaException` hierarchy.
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
- [ ] Run integration tests against the versioned PXA Server container.
- [ ] Test large multipart uploads, streamed downloads, cancellation, and timeouts.
- [ ] Verify public API compatibility before every release.

## Acceptance Criteria

- [ ] A customer installs one documented Maven dependency.
- [ ] The same code runs against PXA Cloud and local PXA Server.
- [ ] Common workflows do not require direct use of generated classes.
- [ ] The SDK performs no document processing locally and documents that boundary clearly.
