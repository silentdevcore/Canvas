# PXA C# REST SDK Checklist

## Goal

Deliver a lightweight C# REST client for remote PXA Cloud and PXA Server usage while keeping it clearly separate from the native embedded PXA .NET libraries.

## Priority And Dependencies

- [ ] P2: Release after the first three cross-language SDKs.
- [ ] Complete the shared contract work in `PXA.SDK-Roadmap.md` first.
- [ ] Define naming guidance that distinguishes remote and embedded execution.

## Package And Runtime

- [ ] Target .NET 8 or newer.
- [ ] Publish the `PXA.Client` package to NuGet.
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

- [ ] Explain when to use `PXA.Client` versus native PXA libraries.
- [ ] Provide ASP.NET Core, worker-service, console, Cloud, and On-Premise examples.
- [ ] Run unit and integration tests against the PXA Server container.
- [ ] Test dependency injection, cancellation, uploads, downloads, and errors.
- [ ] Validate NuGet packaging and public API compatibility.

## Acceptance Criteria

- [ ] Customers can identify remote and embedded PXA products without ambiguity.
- [ ] The client follows standard .NET hosting and dependency-injection patterns.
- [ ] No native document engine is included transitively in `PXA.Client`.
