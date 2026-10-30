# PVAD C# REST SDK Checklist

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Current headings, goals and open tasks use PVAD/PVADE. Checked records, original examples and compatibility identifiers retain their historical names; task statuses are unchanged.

## Goal

Deliver a lightweight C# REST client for remote PVAD Cloud and PVAD Server usage while keeping it clearly separate from the native embedded PVAD .NET libraries.

## Priority And Dependencies

- [ ] P2: Release after the first three cross-language SDKs.
- [ ] Complete the shared contract work in `PVAD.SDK-Roadmap.md` first.
- [ ] Define naming guidance that distinguishes remote and embedded execution.

## Package And Runtime

- [ ] Target .NET 8 or newer.
- [ ] Publish the proposed `PVAD.Client` package to NuGet only after publisher ownership and package availability are verified.
- [ ] Generate the internal transport and DTO layer from OpenAPI.
- [ ] Use `HttpClient`, `System.Text.Json`, and standard dependency injection.
- [ ] Expose handwritten client interfaces and product-specific clients.

## Client Features

- [ ] Add PDF, Spreadsheet, Templates, Migration, Import, and Export clients.
- [ ] Support asynchronous APIs, cancellation tokens, and streamed content.
- [ ] Add `IHttpClientFactory` registration and options-based configuration.
- [ ] Support API-key and bearer-token authentication.
- [ ] Map Problem Details to stable typed exceptions and result metadata.
- [ ] Add configurable resilience without hiding non-idempotent failures.

## Documentation And Tests

- [ ] Explain when to use `PVAD.Client` versus native PVAD libraries.
- [ ] Provide ASP.NET Core, worker-service, console, Cloud, and On-Premise examples.
- [ ] Run unit and integration tests against the PVAD Server container.
- [ ] Test dependency injection, cancellation, uploads, downloads, and errors.
- [ ] Validate NuGet packaging and public API compatibility.

## Acceptance Criteria

- [ ] Customers can identify remote and embedded PVADE products without ambiguity.
- [ ] The client follows standard .NET hosting and dependency-injection patterns.
- [ ] No native document engine is included transitively in `PVAD.Client`.
