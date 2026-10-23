# PVAD API and MCP Compose Overlay

This stack contains PostgreSQL, the hardened WebApi image, and the optional remote MCP server.
Use the launcher below for legacy PXA environment aliases and conflict validation.
For an env file outside the current directory, pass an explicit `--env-file`.
The launcher supports literal dotenv values; resolve `${...}` interpolation before
using such files. It never prints the resolved environment. Raw `docker compose`
expects canonical PVAD variables and bypasses alias conflict validation.
Start the MCP profile from the repository root:

```bash
node tools/configuration/pvad-compose.mjs -f deploy/api/docker-compose.api.yml --profile mcp up --build
```

Fresh installations use PVAD names for services, volumes, networks and secret
mounts, with database/user `pvad`. Existing PXA data requires the explicit
`deploy/rename/docker-compose.api-legacy.yml` overlay and verified external volume
and network bindings. Old DNS aliases exist only in that compatibility overlay.
Do not start the fresh defaults as an upgrade or run two writers on one volume.
Keep the installed Compose project name, stop old writers first, and preserve
existing key files. The secret mount paths now use `pvad_license_*`; key bytes
are not rotated. See the [binding template and review commands](../../docs/pvad-infrastructure-mapping.md).
Configuration checks do not establish runtime data continuity; no stack was started.

The gateway listens on `127.0.0.1:8080` by default and exposes `/mcp`, OAuth protected-resource
metadata, and the WebApi routes. Production deployments must terminate TLS before the gateway and
set the canonical external values for `PVAD_MCP_RESOURCE_URI` and `PVAD_OAUTH_ISSUER`.

Remote MCP requires OAuth. Set `PVAD_OAUTH_ENABLED=true`, a production signing certificate, a
random delegation secret of at least 32 bytes, and an internal API key when global capability tools
are enabled. `PVAD_MCP_DELEGATION_SECRET` must have the same value in WebApi and MCP. The internal
clear-text JWKS URL is explicitly allowed only inside the Compose network; public issuer and MCP
resource URLs remain HTTPS-only outside loopback development.

Interactive authorization also requires the separately deployed PVADE Account frontend at the
configured issuer. Service-account clients use their OAuth client ID and one-time secret directly.
Do not expose the WebApi or MCP container ports publicly; expose only the TLS reverse proxy.

Set `PVAD_OBSERVABILITY_ENABLED=true` and `PVAD_OTLP_ENDPOINT` when connecting this stack to the
separate PVAD observability deployment.

The MCP host emits structured JSON request events to `stderr` and exposes Prometheus metrics at
`http://pvad-mcp:3000/metrics` on its internal network. The public gateway deliberately does not
route `/metrics`. Connect the collector to that internal service and copy
`deploy/observability/prometheus/targets/mcp-targets.json.example` to `mcp-targets.json` only in an
environment where Prometheus shares an approved network with MCP.

Run the isolated TLS and dependency-failure test from the repository root:

```bash
deploy/api/mcp-tls-smoke-test.sh
```
