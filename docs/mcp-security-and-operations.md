# PXA MCP Security and Operations

## Security Boundary

PXA MCP is an OAuth-protected, tenant-aware adapter. PXA WebApi remains authoritative for business
rules, tenant filtering, entitlements, persistence, rendering, and jobs. MCP input schemas improve
client usability but are not an authorization or application-validation boundary.

The remote MCP container has no direct PostgreSQL access. In the PXA Server Compose bundle it uses
an internal network shared only with WebApi and the reverse proxy. The container cannot make
arbitrary internet requests. Public traffic reaches MCP only through the TLS reverse proxy.

## Threat Model

| Threat | Required control | Residual handling |
| --- | --- | --- |
| Prompt injection requests unauthorized data or actions | Read-only Beta tools, explicit scopes, WebApi authorization and tenant filtering | Treat tool output as untrusted content; mutating tools require a later confirmation design |
| Confused-deputy or cross-tenant access | Audience-bound OAuth token, active organization claim, 30-second signed internal delegation, server-resolved tenant | Cross-tenant tests are required for every customer-data tool |
| Stolen bearer token | Ten-minute access token, TLS, no downstream forwarding, no token logging | Revoke the Account session or service-account client and investigate audit events |
| OAuth redirect manipulation | Exact redirect matching, PKCE, HTTPS or loopback HTTP only, no fragments or credentials | Dynamic registration is rate-limited and public clients receive no secret |
| SSRF or arbitrary network access | No URL arguments in Beta tools and internal-only MCP container network | Any future import URL requires a dedicated allowlist and resolver defense |
| Path traversal or local file disclosure | Remote tools accept no filesystem paths; packaged resources use fixed identifiers | Local stdio credentials and paths remain under the local user's trust boundary |
| Oversized or malicious payload | Header, JSON body, schema, document, response, duration and rate limits | Import and decompression-bomb defenses remain mandatory before binary tools ship |
| Resource exhaustion | Stateless requests, bounded concurrency through the proxy/runtime, CPU, memory and PID limits | Alert on latency, server errors and rate limiting; scale only after load validation |
| Secret or customer-data leakage | Arguments/results are never logged; identities are represented by truncated SHA-256 references | Operators must keep container and proxy debug logging disabled in production |
| Dependency compromise or outage | Locked npm graph, dependency audit, readiness probes and fail-closed JWT verification | Readiness alerts and rollback use immutable PXA releases |

## Logging and Metrics

The remote host writes one JSON completion event to `stderr` per non-health request. Events contain
request ID, bounded route, RPC method, tool name, HTTP status, duration, and pseudonymous client,
subject, and organization references. They never contain authorization headers, tokens, cookies,
tool arguments, document JSON, source code, Base64 payloads, asset names, or tool results.

`GET /metrics` exposes Prometheus text format inside the MCP container network. It reports active
requests, request count and duration, tool-call count, and API/identity readiness. The production
gateway does not expose this route. Prometheus alerts cover dependency outages, elevated server
errors, and elevated authorization failures.

Recommended retention:

- request logs: 30 days unless a shorter customer policy applies;
- aggregated metrics: 90 days by default;
- no document payload retention in MCP;
- authorization and service-account lifecycle events follow the PXA audit policy.

Beta operational objectives are internal targets, not a contractual SLA:

- monthly remote-endpoint availability at least 99.5%, excluding announced maintenance;
- 95th-percentile latency below two seconds for local catalog and schema tools;
- dependency outage detection within two minutes;
- elevated authorization failures and server errors detected within ten and five minutes.

## Operational Response

1. Check `/health/live` and `/health/ready` through the internal service endpoint.
2. Correlate the returned `X-Request-ID` with MCP JSON logs and WebApi operations.
3. For identity failures, verify issuer discovery and JWKS without printing tokens.
4. For tenant denials, verify active organization, membership, scope, entitlement, and service-account
   state. Do not bypass tenant checks.
5. Revoke affected sessions or service-account credentials before restarting compromised workloads.
6. Roll back to an existing immutable image tag; never move a published tag.

OpenTelemetry trace export, stateful-session metrics, and job-duration metrics remain later work
because the Beta server is stateless and does not expose long-running operations.
