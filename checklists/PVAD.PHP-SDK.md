# PVAD PHP SDK Checklist

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Current headings, goals and open tasks use PVAD/PVADE. Checked records, original examples and compatibility identifiers retain their historical names; task statuses are unchanged.

## Goal

Deliver a framework-independent PHP SDK for web applications, background workers, and server-side document automation.

## Priority And Dependencies

- [ ] P2: Start after the primary SDK releases are stable.
- [ ] Complete the shared contract work in `PVAD.SDK-Roadmap.md` first.
- [ ] Select and verify a Packagist vendor and reserve its proposed `pvad` package; no vendor ownership is assumed.

## Package And Runtime

- [ ] Support PHP 8.2 or newer.
- [ ] Publish the `pvad` package through Composer and Packagist under the verified vendor; record the final vendor/package name before implementation.
- [ ] Generate the internal transport and model layer from OpenAPI.
- [ ] Use PSR-18 HTTP clients, PSR-7 messages, and PSR-17 factories.
- [ ] Expose handwritten PDF, Spreadsheet, Templates, Migration, Import, and Export clients.

## Client Features

- [ ] Support API-key and bearer-token authentication.
- [ ] Stream large uploads and downloads without unnecessary memory copies.
- [ ] Add configurable base URL, timeout, retry policy, and user agent.
- [ ] Map Problem Details to a stable exception hierarchy.
- [ ] Avoid hard dependencies on Laravel, Symfony, or a specific HTTP implementation.
- [ ] Provide optional integration guidance for major PHP frameworks.

## Distribution And Documentation

- [ ] Publish semantic versions and generated API documentation.
- [ ] Provide Composer, Cloud, On-Premise, Laravel, and Symfony examples.
- [ ] Automate generation, coding-standard checks, tests, and Packagist releases.

## Tests

- [ ] Test all supported PHP versions.
- [ ] Test with at least two compatible PSR-18 client implementations.
- [ ] Run integration tests against the PVAD Server container.
- [ ] Test large streams, timeouts, authentication, and structured errors.

## Acceptance Criteria

- [ ] Customers install one Composer package and choose their PSR-18 transport.
- [ ] The SDK remains framework-independent.
- [ ] Cloud and local PVAD Server use the same public client API.
