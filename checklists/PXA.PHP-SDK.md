# PXA PHP SDK Checklist

## Goal

Deliver a framework-independent PHP SDK for web applications, background workers, and server-side document automation.

## Priority And Dependencies

- [ ] P2: Start after the primary SDK releases are stable.
- [ ] Complete the shared contract work in `PXA.SDK-Roadmap.md` first.
- [ ] Reserve the `powerdox/pxa` package name on Packagist.

## Package And Runtime

- [ ] Support PHP 8.2 or newer.
- [ ] Publish `powerdox/pxa` through Composer and Packagist.
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
- [ ] Run integration tests against the PXA Server container.
- [ ] Test large streams, timeouts, authentication, and structured errors.

## Acceptance Criteria

- [ ] Customers install one Composer package and choose their PSR-18 transport.
- [ ] The SDK remains framework-independent.
- [ ] Cloud and local PXA Server use the same public client API.
