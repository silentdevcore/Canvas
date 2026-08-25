# PXA MCP Server

## Purpose

Evolve the existing local PXA MCP documentation helper into a production-ready Model Context
Protocol server for PXA Cloud, PXA Server, and local developer workflows. The implementation must
preserve the current `stdio` experience while adding an authenticated remote server and a shared,
tenant-safe tool layer.

## Existing Baseline

- A TypeScript MCP server exists under `tools/PXA.Mcp`.
- The server uses `@modelcontextprotocol/sdk` and currently runs over `stdio`.
- Eleven tools expose the access context, element catalog, examples, documentation search, design
  validation, PDF preview rendering, export formats, migration providers, and tenant templates.
- Five `pxa://` resources expose the design schema, workbook schema, OpenAPI document, AI reference,
  and C# cookbook.
- The element catalog is imported from the Designer source of truth.
- PXA WebApi already provides organizations, service accounts, API keys, entitlements, usage
  metering, templates, assets, background jobs, audit events, and observability infrastructure.
- The current MCP type-check and smoke test pass with 38 registered Designer elements.

## Target Architecture

- [x] Split the current implementation into a transport-independent `PXA.Mcp.Core` package and thin
  local and remote hosts.
- [x] Keep `PXA.Mcp.Local` as the process-spawned `stdio` host for local IDE and desktop clients.
- [x] Add `PXA.Mcp.Server` as a separately deployable Streamable HTTP host.
- [ ] Expose the remote endpoint at `/mcp`, with the intended Cloud address
  `https://mcp.powerdoxautomation.com/mcp`.
- [x] Support an On-Premise endpoint through the PXA Server reverse proxy without requiring public
  internet access.
- [x] Keep MCP transport and protocol code outside `PXA.WebApi`; access PXA business capabilities
  only through stable application/API contracts.
- [x] Package catalog, schemas, documentation metadata, and tool definitions so the MCP server can be
  built without importing TypeScript source files from the Designer at runtime.
- [x] Derive the advertised MCP server version from the root PXA version instead of the hard-coded
  `0.1.0` value.
- [x] Document supported MCP protocol revisions and the compatibility policy.

## Transport And Protocol

### Local stdio

- [x] Preserve the existing `pxa-mcp` command and `stdio` transport.
- [ ] Remove the obsolete `canvas-mcp` binary after the documented compatibility period.
- [x] Read local credentials from environment variables or the operating-system credential store.
- [x] Guarantee that stdout contains MCP messages only and send operational logs to stderr.
- [ ] Publish an installable, compiled package instead of requiring `tsx` and repository source files.
  The compiled tarball is validated locally; registry publication remains pending.

### Remote Streamable HTTP

- [x] Implement stateless Streamable HTTP with POST on `/mcp`; return a controlled `405` for GET and
  DELETE because resumable sessions are not enabled.
- [x] Decide explicitly between stateless operation for simple tools and stateful sessions for
  progress, notifications, resumability, and long-running tasks.
- [x] Validate every supplied `Origin` header against an environment-specific allowlist.
- [x] Bind local development servers to loopback by default.
- [x] Enforce HTTPS outside local development.
- [x] Add request-body, header, request-duration, capability-response, and preview-output limits.
- [ ] Add session-count and connection-duration limits when stateful sessions are introduced.
- [ ] Implement cancellation and MCP progress notifications for eligible operations.
- [ ] Evaluate experimental MCP Tasks only behind a feature flag and retain the existing PXA job API
  as the authoritative execution state.
- [ ] Do not implement the deprecated HTTP+SSE transport unless a documented customer compatibility
  requirement appears.

## Authentication And Authorization

- [x] Treat the remote MCP host as an OAuth 2.1 protected resource.
- [x] Add OAuth Protected Resource Metadata pointing clients to the configured authorization server.
- [x] Implement and publish authorization-server discovery metadata in PXA Account.
- [x] Advertise RFC 7591 dynamic client registration and restrict registrations to public
  authorization-code clients with PKCE and exact HTTPS or loopback HTTP redirect URIs.
- [x] Support Authorization Code with PKCE for interactive users.
- [x] Support an approved non-interactive service-account flow for automation and On-Premise use.
- [x] Preserve legacy API keys without claiming OAuth support; newly rotated keys persist and expose
  their OAuth client ID while keeping the secret one-time-only.
- [x] Validate issuer, signature, expiry, audience, resource indicator, organization, user or service
  account, and granted scopes on every request.
- [x] Issue short-lived tokens specifically for the MCP resource in PXA Account.
- [x] Never pass inbound MCP tokens through to
  PXA WebApi or another downstream service.
- [x] Use a separate internal API-key credential for fixed global capability and rendering routes.
- [x] Add a separately signed, 30-second internal delegation before exposing tenant-owned WebApi
  resources; the original MCP bearer token is never forwarded.
- [x] Define least-privilege scopes:
  - `pxa.docs.read`
  - `pxa.templates.read`
  - `pxa.templates.write`
  - `pxa.assets.read`
  - `pxa.assets.write`
  - `pxa.documents.validate`
  - `pxa.documents.render`
  - `pxa.documents.export`
  - `pxa.documents.import`
  - `pxa.migrations.execute`
  - `pxa.jobs.read`
  - `pxa.jobs.cancel`
- [ ] Map scopes to existing roles, permissions, product entitlements, and active-organization context
  without treating those concepts as interchangeable.
- [ ] Require step-up confirmation for destructive, publishing, or externally visible operations.
- [ ] Add token revocation, short access-token lifetime, refresh-token rotation, and session-revocation
  behavior.
- [ ] Audit authorization grants, denied tool calls, privileged operations, and credential lifecycle
  events without logging tokens.

## Tool Contract Standards

- [x] Give every tool a stable name, title, description, input schema, output schema, and structured
  result contract.
- [x] Return `structuredContent` for machine-readable results and concise text content for clients
  that do not render structured output.
- [x] Add accurate read-only, destructive, idempotent, and open-world annotations to every tool.
- [x] Use stable `PXAMCP...` diagnostics and MCP error results instead of unstructured exception text.
- [ ] Include correlation ID, job ID, affected resource URI, warnings, and retry guidance where
  applicable.
- [ ] Add idempotency keys to mutating and job-starting operations.
- [ ] Paginate all list operations with opaque cursors and bounded page sizes.
- [ ] Apply consistent timeouts, cancellation, limits, and tenant checks to every tool.
- [ ] Never accept arbitrary local filesystem paths or unvalidated remote URLs.
- [ ] Never include document content, source code, Base64 payloads, customer identifiers, or secrets
  in logs or telemetry.

## P0 Read-Only And Validation Tools

- [x] Retain and harden `list_elements`.
- [x] Retain and harden `get_element_schema`.
- [x] Retain and harden `get_example`.
- [ ] Replace line-based `search_docs` with indexed, bounded documentation search.
- [x] Replace the partial manual `validate_design` implementation with full JSON Schema validation.
- [ ] Add canonical backend business-rule diagnostics to `validate_design`.
- [ ] Add `validate_workbook` using the canonical Spreadsheet schema and backend rules.
- [x] Add `list_export_formats` with current MIME type and page behavior metadata.
- [ ] Enrich `list_export_formats` with effective entitlement availability.
- [ ] Add `list_import_formats` with limits and fidelity information.
- [x] Add `list_migration_providers` with domain, kind, provider, and coverage metadata.
- [ ] Enrich `list_migration_providers` with supported input types.
- [x] Add `list_templates` with tenant-scoped pagination and filters.
- [x] Add `get_template` with authorization, checksum, and revision metadata.
- [ ] Add `list_template_versions` with authorization and revision metadata.
- [ ] Add read-only resources for supported formats, migration providers, template metadata, and
  public diagnostics.

## P1 Document And Template Tools

- [ ] Replace `render_preview` temporary-path output with a tenant-bound, expiring result resource or
  supported embedded content response.
- [ ] Add `render_document` for canonical Design JSON with format selection and deterministic output.
- [ ] Add `export_document` for PDF, DOCX, ODT, HTML, image, Markdown, Spreadsheet, and other supported
  formats.
- [ ] Add `create_template_draft`.
- [ ] Add `update_template_draft` with revision or ETag concurrency.
- [ ] Add `create_template_version` and `publish_template`, each with explicit confirmation and audit.
- [ ] Add `compare_template_versions` with structured document differences.
- [ ] Add `upload_asset`, `get_asset_metadata`, and `delete_asset` through tenant-bound asset IDs.
- [ ] Keep large binary data outside JSON arguments and return expiring resource links.
- [ ] Enforce file signatures, MIME validation, malware-scanning hooks, quotas, and retention rules.

## P2 Import, Migration And Long-Running Operations

- [ ] Add `import_document` for supported PDF, Word, image, presentation, and Spreadsheet inputs.
- [ ] Add `start_migration` with provider, source type, options, and target contract.
- [ ] Add `get_job`, `get_job_result`, and `cancel_job` mapped to the persistent PXA job system.
- [ ] Expose progress without leaking another tenant's task or job state.
- [ ] Return fidelity, diagnostics, unsupported elements, review requirements, and source maps.
- [ ] Integrate OCR and PDF chart recognition only through explicit options and existing feature gates.
- [ ] Apply strict upload, page, pixel, primitive, execution-time, memory, and output limits.
- [ ] Preserve original source assets for review according to configured retention policy.

## Resources And Prompts

- [ ] Preserve the existing static `pxa://` resources with explicit version and locale metadata.
- [ ] Add tenant-scoped resource templates for templates, versions, jobs, diagnostics, and generated
  artifacts.
- [ ] Prevent resource enumeration across organizations and users.
- [ ] Add cache metadata and change notifications only where clients can safely reuse content.
- [ ] Add user-controlled prompts for common workflows:
  - Create a document from requirements.
  - Validate and repair a design.
  - Convert a design to C# Model or C# PDF.
  - Import and review a document.
  - Migrate a provider report.
  - Diagnose an export mismatch.
- [ ] Keep prompts free of hidden data access or automatic mutations.
- [ ] Defer server-initiated sampling and elicitation until consent, privacy, cost, and client
  compatibility requirements are defined.

## Security And Privacy

- [x] Create a formal MCP threat model covering prompt injection, confused deputy attacks, token
  theft, cross-tenant access, malicious documents, SSRF, path traversal, oversized payloads, and
  resource-exhaustion attacks.
- [ ] Separate public documentation tools from authenticated customer-data tools.
- [ ] Require explicit client or user confirmation for mutating and destructive tool calls.
- [ ] Validate tool arguments again in the PXA application layer; MCP schemas are not a security
  boundary.
- [x] Prohibit arbitrary network egress from the MCP container except allowlisted internal PXA
  services.
- [x] Use non-root containers, read-only filesystems, temporary storage quotas, dropped capabilities,
  and restricted process limits for the Beta MCP service and TLS gateway.
- [ ] Encrypt persisted job inputs and outputs and apply short configurable retention.
- [x] Add dependency auditing, a CycloneDX SBOM, and blocking High/Critical container scanning to the
  Beta CI gate.
- [ ] Add repository-wide secret scanning and signed release images before Stable.
- [x] Define privacy and telemetry documentation for MCP use before public availability.

## Reliability And Observability

- [x] Add liveness and readiness endpoints outside the MCP protocol endpoint.
- [x] Add dependency-readiness checks for PXA WebApi and the configured identity provider.
- [x] Add structured logs with request, tool, pseudonymous organization, user or service-account
  references, and correlation identifiers, excluding sensitive arguments and results.
- [ ] Add OpenTelemetry traces across MCP, PXA WebApi, workers, storage, and database operations.
- [x] Add Beta metrics for active requests, tool calls, latency, HTTP errors, rate limits, and
  dependency readiness. Session, cancellation, payload, and job metrics remain tied to later
  stateful and long-running operations.
- [x] Define Beta operational objectives and alerts for availability, latency, authorization
  failures, server errors, and dependency outages.
- [ ] Return bounded retry information for transient downstream failures.
- [x] Add graceful shutdown for the stateless HTTP host.
- [ ] Add connection draining and stateful-session cleanup if sessions are introduced.
- [ ] Verify horizontal scaling behavior before enabling stateful sessions or server notifications.

## Packaging And Deployment

- [x] Produce compiled Local and Server bundles from the monorepo version source.
- [ ] Publish the local package through the selected public or private npm registry.
- [x] Add a multi-stage Linux container for `PXA.Mcp.Server` running as a non-root user.
- [x] Add PXA Server Docker Compose integration with WebApi, PostgreSQL, bundled workers, MCP, and a
  hardened reverse proxy.
- [ ] Support amd64 initially and arm64 after dependency validation.
- [x] Configure runtime API URLs, OAuth issuer, canonical MCP resource URI, origin allowlist, limits,
  and internal credentials through environment or mounted secrets.
- [ ] Add Cloud, On-Premise, local IDE, and isolated-network deployment guides.
- [x] Add client setup examples for Codex, Claude, VS Code, and other compatible MCP hosts without
  embedding real credentials.

## Testing

- [ ] Convert the smoke script into deterministic automated protocol tests for tools, resources,
  prompts, initialization, capability negotiation, cancellation, and shutdown.
- [ ] Test both `stdio` and Streamable HTTP against supported protocol revisions.
- [ ] Add contract tests for every input schema, output schema, annotation, error, and resource URI.
- [ ] Add drift guards covering Designer elements, backend element support, schemas, OpenAPI,
  documentation, formats, migration providers, and MCP tools.
- [ ] Test OAuth discovery, PKCE, audience validation, scope enforcement, expiry, revocation, and
  non-interactive service accounts.
- [ ] Test tenant isolation for every customer-data tool, resource, job, and artifact.
- [ ] Test prompt injection, SSRF, path traversal, oversized files, malformed PDFs, decompression
  bombs, cancellation, timeout, memory pressure, and output limits.
- [ ] Test idempotent retries and concurrency for every mutating operation.
- [ ] Run interoperability tests with representative MCP clients.
- [x] Add Docker smoke tests, TLS health checks, dependency-failure tests, and graceful-shutdown
  tests.
- [x] Run type-check, unit tests, protocol tests, PXA API integration tests, dependency audit,
  container build/smoke tests, blocking container vulnerability scanning, SBOM generation, and
  `git diff --check` in CI.

## Delivery Phases

### Phase 0 - Harden the existing local server

- [x] Extract the shared core and retain `stdio` behavior.
- [x] Add complete schema validation, structured results, diagnostics, annotations, and bounded
  documentation search.
- [x] Compile and package the local server without runtime repository dependencies.

### Phase 0 Validation Record

- [x] TypeScript type-check and esbuild distribution build pass.
- [x] Eight Core tests cover complete schema validation, stable diagnostics, structured tool results,
  bounded PDF acceptance, and rejection of non-PDF render responses.
- [x] Source and compiled-distribution smoke tests pass with eleven tools, five resources, 38 elements,
  output schemas, and read-only annotations.
- [x] `npm pack --dry-run` contains the executable bundle and all required versioned resources without
  Designer or repository source files.
- [x] The MCP dependency audit reports zero known vulnerabilities after pinning patched Hono
  `4.12.34`.

### Phase 1 - Read-only remote MCP

- [x] Add Streamable HTTP, OAuth, tenant context, read-only tools, Docker, observability, and official
  SDK interoperability tests.
- [x] Release initially as Beta with no mutating customer-data tools.

### Phase 1 Validation Record

- [x] Stateless Streamable HTTP interoperability is covered with the official MCP client transport.
- [x] Eighteen Node tests cover Core contracts, bounded API capability responses, OAuth JWT validation,
  protected-resource metadata, missing authentication, Origin rejection, and tenant context.
- [x] Fixed global routes use a separate internal credential; tenant routes use a short-lived signed
  delegation containing only the verified subject, organization, and scopes.
- [x] Remote preview rendering fails closed instead of exposing an inaccessible container path; it
  remains a Phase 2 operation until tenant-bound expiring artifacts exist.
- [x] The non-root multi-stage container builds successfully and its readiness health check passes
  in Rancher Desktop.
- [x] Type-check, compiled builds, source/distribution smoke tests, and dependency audit pass.
- [x] PXA Account provides OpenIddict discovery, authorization-code/PKCE, and service-account
  client-credentials issuance backed by PostgreSQL.
- [x] Tenant `list_templates` and `get_template` tools use independently validated internal
  delegation and server-enforced organization filtering.
- [x] A PostgreSQL integration test covers service-account creation, token issuance, delegated
  template access, OAuth-client revocation, and secret non-disclosure.
- [x] A PostgreSQL integration test covers discovery-advertised dynamic client registration, strict
  redirect validation, and PKCE enforcement for the registered client.
- [x] The MCP readiness endpoint fails closed when WebApi or JWKS is unavailable, and the standalone
  API Compose stack validates with PostgreSQL, bundled workers, MCP, and its reverse proxy.
- [x] An isolated Docker test verifies TLS termination, the official Streamable HTTP client,
  protected-resource metadata, authenticated tool discovery, dependency-failure readiness, and
  graceful shutdown.
- [x] Structured JSON logs, pseudonymous identity references, correlation propagation, Prometheus
  metrics, Beta objectives, and MCP alerts are documented and covered by tests.
- [x] The final runtime image excludes npm, passes the blocking Trivy High/Critical vulnerability
  gate with zero findings, and produces a CycloneDX SBOM in CI.
- [ ] Hands-on release-candidate runs in Codex, Claude Code, and VS Code remain required before
  promotion from Beta to Stable.

### Phase 2 - Controlled document operations

- [ ] Add rendering, exports, draft templates, assets, confirmations, idempotency, and audit.
- [ ] Promote tools individually after security and tenant-isolation review.

### Phase 3 - Jobs, import and migration

- [ ] Add persistent long-running operations, progress, cancellation, import, OCR, and migration.
- [ ] Evaluate MCP Tasks only after stable PXA job integration and client compatibility testing.

## Beta Acceptance Criteria

- [x] Compiled local `stdio` and remote Streamable HTTP hosts use the shared MCP Core.
- [x] Remote access uses standards-based OAuth discovery, dynamic client registration, PKCE, scoped
  service accounts, short-lived audience-bound tokens, and tenant-bound downstream delegation.
- [x] The Beta exposes read-only catalog, validation, capability, migration, and tenant-template tools
  without mutating customer-data operations.
- [x] PXA Server terminates TLS at a hardened gateway and keeps WebApi, MCP, and PostgreSQL on
  appropriately isolated container networks.
- [x] Liveness, fail-closed dependency readiness, structured privacy-safe logs, Prometheus metrics,
  operational objectives, and alerts are available.
- [x] Protocol revision, OAuth, tenant delegation, TLS, dependency-failure, shutdown, package,
  vulnerability, SBOM, and release-metadata gates pass.
- [x] Client setup and security/operations guidance are documented for the Beta scope.

**Beta status:** complete. Registry publication, Cloud rollout, hands-on release-candidate runs,
image signing, OpenTelemetry, and all mutating or long-running tools remain explicit Stable or later
phase work.

## Acceptance Criteria

- [ ] Local clients can install and run `pxa-mcp` without cloning the monorepo.
- [x] Remote clients can discover and connect to PXA MCP through standards-compliant OAuth and
  Streamable HTTP.
- [ ] Every customer-data operation is authenticated, tenant-isolated, entitled, scoped, metered,
  and audited.
- [ ] Tool outputs are structured, bounded, diagnosable, and free of local file paths and secrets.
- [ ] Rendered and exported artifacts are accessed through tenant-bound, expiring resources.
- [ ] Long-running operations support status, progress, cancellation, and secure result retrieval.
- [ ] Cloud and On-Premise installations expose equivalent supported MCP capabilities.
- [ ] Security, interoperability, Docker, and end-to-end suites pass before Stable release.

## Assumptions

- TypeScript remains the MCP implementation language because the existing server and official SDK
  support are already established.
- `stdio` remains the preferred local transport and Streamable HTTP is the remote transport.
- PXA Account or a compatible external identity provider will become the OAuth authorization server;
  the MCP host remains a protected resource.
- PXA WebApi and its persistent job system remain authoritative for business operations and data.
- API keys may support controlled On-Premise automation but do not replace standards-compliant OAuth
  for the public remote MCP server.
- Sampling, elicitation, experimental MCP Tasks, real-time notifications, and third-party marketplace
  distribution are deferred until the core read-only server is secure and interoperable.
