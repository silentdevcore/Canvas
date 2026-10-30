# PVAD Python SDK Checklist

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Current headings, goals and open tasks use PVAD/PVADE. Checked records, original examples and compatibility identifiers retain their historical names; task statuses are unchanged.

## Goal

Deliver a typed Python SDK with synchronous and asynchronous clients for automation, data, and AI-assisted document workflows.

## Priority And Dependencies

- [ ] P1: Release after Java and TypeScript/JavaScript.
- [ ] Complete the P0 contract work in `PVAD.SDK-Roadmap.md` first.
- [ ] Verify and reserve the proposed `pvade-pvad` distribution name on PyPI.

## Package And Runtime

- [ ] Support Python 3.10 or newer.
- [ ] Publish the proposed `pvade-pvad` distribution with the `pvad` import namespace after publisher and package availability are verified.
- [ ] Use `httpx` for synchronous and asynchronous HTTP clients.
- [ ] Use Pydantic models for stable public request and response types.
- [ ] Generate the internal transport and schemas from OpenAPI.
- [ ] Expose handwritten `PvadClient` and `AsyncPvadClient` facades.

## Client Features

- [ ] Add PDF, Spreadsheet, Templates, Migration, Import, and Export clients.
- [ ] Support paths, binary file objects, byte streams, and async streams.
- [ ] Provide context managers for client and response resource cleanup.
- [ ] Support API-key and bearer-token authentication.
- [ ] Add configurable base URL, timeouts, proxy, retry policy, and user agent.
- [ ] Map Problem Details to a stable `PvadError` hierarchy.

## Distribution And Documentation

- [ ] Publish wheels and source distributions with type metadata.
- [ ] Provide pip, uv, and Poetry installation examples.
- [ ] Document synchronous, asynchronous, Cloud, and local-server usage.
- [ ] Provide runnable automation, migration, data-binding, and streaming examples.
- [ ] Automate generation, tests, signing/provenance, and PyPI publication.

## Tests

- [ ] Test all supported Python versions and major operating systems.
- [ ] Unit-test facade mapping, validation, errors, and client lifecycle.
- [ ] Run synchronous and asynchronous integration tests against PVAD Server.
- [ ] Test large file streams, cancellation, timeouts, and malformed responses.
- [ ] Verify static typing with representative consumer projects.

## Acceptance Criteria

- [ ] Installation requires one documented PyPI package.
- [ ] Synchronous and asynchronous clients expose equivalent capabilities.
- [ ] Common workflows do not expose generated transport classes.
- [ ] The same user code can target Cloud or local PVAD Server through configuration.
