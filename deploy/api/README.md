# PXA API and MCP Compose Overlay

This stack contains PostgreSQL, the hardened WebApi image, and the optional remote MCP server.
Start the MCP profile from the repository root:

```bash
docker compose -f deploy/api/docker-compose.api.yml --profile mcp up --build
```

The gateway listens on `127.0.0.1:8080` by default and exposes `/mcp`, OAuth protected-resource
metadata, and the WebApi routes. Production deployments must terminate TLS before the gateway and
set the canonical external values for `PXA_MCP_RESOURCE_URI` and `PXA_OAUTH_ISSUER`.

Remote MCP requires OAuth. Set `PXA_OAUTH_ENABLED=true`, a production signing certificate, a
random delegation secret of at least 32 bytes, and an internal API key when global capability tools
are enabled. `PXA_MCP_DELEGATION_SECRET` must have the same value in WebApi and MCP. The internal
clear-text JWKS URL is explicitly allowed only inside the Compose network; public issuer and MCP
resource URLs remain HTTPS-only outside loopback development.

Interactive authorization also requires the separately deployed PXA Account frontend at the
configured issuer. Service-account clients use their OAuth client ID and one-time secret directly.
Do not expose the WebApi or MCP container ports publicly; expose only the TLS reverse proxy.

Set `PXA_OBSERVABILITY_ENABLED=true` and `PXA_OTLP_ENDPOINT` when connecting this stack to the
separate PXA observability deployment.

The MCP host emits structured JSON request events to `stderr` and exposes Prometheus metrics at
`http://pxa-mcp:3000/metrics` on its internal network. The public gateway deliberately does not
route `/metrics`. Connect the collector to that internal service and copy
`deploy/observability/prometheus/targets/mcp-targets.json.example` to `mcp-targets.json` only in an
environment where Prometheus shares an approved network with MCP.

Run the isolated TLS and dependency-failure test from the repository root:

```bash
deploy/api/mcp-tls-smoke-test.sh
```
