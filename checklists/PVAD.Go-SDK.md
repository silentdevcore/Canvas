# PVAD Go SDK Checklist

## Goal

Deliver an idiomatic Go client for server, cloud, and automation workloads that communicate with PVAD Cloud or PVAD Server.

## Priority And Dependencies

- [ ] P2: Start after the primary SDK releases are stable.
- [ ] Complete the shared contract work in `PVAD.SDK-Roadmap.md` first.
- [ ] Select and verify a publisher-owned GitHub account and the proposed `pvad-go` repository; the publisher is not yet selected or registered for this SDK.

## Package And API

- [ ] Publish a versioned `pvad-go` module under the verified publisher; record its final import path before implementing generated imports.
- [ ] Generate an internal transport and model layer from OpenAPI.
- [ ] Expose handwritten service clients from one root client.
- [ ] Accept `context.Context` on every network operation.
- [ ] Use `io.Reader` and `io.Writer` for document streaming.
- [ ] Support API-key and bearer-token authentication.
- [ ] Map Problem Details to inspectable idiomatic Go errors.
- [ ] Allow custom `http.Client`, transport, timeouts, proxy, and base URL.

## Distribution And Documentation

- [ ] Tag releases using semantic versions compatible with the Go module proxy.
- [ ] Provide Cloud, local Docker, upload, download, and migration examples.
- [ ] Generate package documentation and release notes.
- [ ] Automate generation, formatting checks, tests, and tagged releases.

## Tests

- [ ] Unit-test requests, authentication, errors, contexts, and streaming.
- [ ] Run integration tests against the versioned PVAD Server container.
- [ ] Test cancellation, deadlines, retries, large uploads, and partial downloads.
- [ ] Run race detection and static analysis in CI.

## Acceptance Criteria

- [ ] The SDK follows standard Go context, error, and streaming conventions.
- [ ] Common workflows use the handwritten API rather than generated operations.
- [ ] One code path supports Cloud and local PVAD Server endpoints.
