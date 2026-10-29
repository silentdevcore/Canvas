# PXA TypeScript And JavaScript SDK Checklist

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

## Goal

Deliver one TypeScript implementation that provides typed TypeScript APIs and production JavaScript builds for Node.js and modern browsers.

## Priority And Dependencies

- [ ] P1: Release after the Java SDK contract has proven stable.
- [ ] Complete the P0 contract work in `PVAD.SDK-Roadmap.md` first.
- [ ] Reserve the `@powerdox` npm organization and package scope.

## Package And Runtime

- [ ] Publish `@powerdox/pxa` to npm.
- [ ] Support Node.js 20 or newer and current evergreen browsers.
- [ ] Produce ESM, CommonJS, source maps, and TypeScript declarations.
- [ ] Use native Fetch-compatible transport and avoid Node-only code in browser entry points.
- [ ] Generate the internal transport and model layer from OpenAPI.
- [ ] Expose a handwritten `PxaClient` and product-specific clients.

## Client Features

- [ ] Add PDF, Spreadsheet, Templates, Migration, Import, and Export clients.
- [ ] Support `Blob`, `File`, `ArrayBuffer`, Node streams, and web streams where appropriate.
- [ ] Support API-key and bearer-token authentication without browser secret leakage.
- [ ] Add `AbortSignal`, configurable timeouts, retry policy, and user agent where available.
- [ ] Map Problem Details to stable typed SDK errors.
- [ ] Keep browser and server authentication guidance separate.

## Distribution And Documentation

- [ ] Publish one package for both TypeScript and JavaScript consumers.
- [ ] Declare package exports explicitly and verify tree-shaking.
- [ ] Provide Node, browser, React, and vanilla JavaScript examples.
- [ ] Document Cloud and local PXA Server connection patterns.
- [ ] Automate generation, tests, provenance, and npm publication.

## Tests

- [ ] Run type-level API tests and compile representative TypeScript projects.
- [ ] Run unit tests in Node and a browser environment.
- [ ] Run integration tests against the PXA Server container.
- [ ] Test browser and Node uploads, downloads, cancellation, and errors.
- [ ] Verify ESM and CommonJS consumers before release.

## Acceptance Criteria

- [ ] TypeScript and JavaScript use the same package and implementation.
- [ ] Consumers receive correct declarations without additional type packages.
- [ ] Browser builds do not contain Node-only dependencies or embedded secrets.
- [ ] The same public workflow works against Cloud and local PXA Server.
