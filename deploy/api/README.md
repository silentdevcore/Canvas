# PVAD API and MCP Compose Overlay

> **Hard cut (2026-11-09):** Former-name aliases, legacy readers, dual writers and
> transition overlays described in this document were retired. Current builds read
> and write PVAD names only; retained evidence and external bindings are listed in the
> [legacy name retirement](../../checklists/PVAD.Legacy-Name-Retirement.md) checklist. Statements below that describe
> compatibility behavior are kept as dated history.

This stack contains PostgreSQL, the hardened WebApi image, and the optional remote MCP server.

## License Verification And Manufacturer Issuance

The default API stack is a customer verification deployment:

- `Licensing:IssuerEnabled=false`; issue/replace operations are rejected even for System Administrators.
- Only `PVAD_LICENSE_PUBLIC_KEY_FILE` is required and mounted read-only. No private license key is mounted.
- License listing, downloads, and validation use existing stored artifacts without rewriting them.
- Preserve the installed public key and key ID. Moving signing to an issuer does not require rotation or reissuing existing licenses.

A manufacturer-operated issuer must explicitly add the separate overlay:

```bash
node tools/configuration/pvad-compose.mjs \
  -f deploy/api/docker-compose.api.yml \
  -f deploy/api/docker-compose.license-issuer.yml config
```

That overlay enables issuance and additionally requires
`PVAD_LICENSE_PRIVATE_KEY_FILE`. Apply it only to the manufacturer's protected
issuer, never to a customer deployment. Keep existing key bytes unchanged and
transfer signing keys only through an approved secure procedure. Do not delete
old host key files automatically or mount signing keys in a customer container.

No environment, including Development and Testing, generates missing license
keys automatically. The signer loads a provisioned key only when an authorized
issue/replace operation actually signs and checks that it matches the configured
verification key. Key/configuration changes require a process restart.

`PVAD_LICENSE_ISSUANCE_DISABLED` means this is a verification-only server.
`PVAD_LICENSE_ISSUER_UNAVAILABLE` means an enabled issuer lacks a usable matching
key pair. `PVAD_LICENSE_VERIFIER_UNAVAILABLE` identifies missing/invalid public
verification configuration without exposing key material or file paths.

The server-license format and its schema-1/schema-2 compatibility remain
unchanged. Native library licensing is a separate profile and is not enabled by
this overlay; see the [native library guide](../../docs/native-library-licensing.md).

## API And MCP

Native PDF processing now independently requires a signed native `pdf` license.
Provision the approved manufacturer public keys in the build and mount the
signed native license read-only, configuring `NativeLicensing__LicensePath`.
The current base Compose intentionally does not invent a license or mount an
unprovisioned native secret. An absent native configuration leaves PDF work
unlicensed; an explicitly invalid file fails startup. The code worker receives
only this file path and verifies the license itself. Existing server-license
keys and issuer mode do not grant native SDK rights. See the
[native activation guide](../../docs/native-library-licensing.md#api-and-worker-activation).

Use the launcher below for legacy PVAD environment aliases and conflict validation.
For an env file outside the current directory, pass an explicit `--env-file`.
The launcher supports literal dotenv values; resolve `${...}` interpolation before
using such files. It never prints the resolved environment. Raw `docker compose`
expects canonical PVAD variables and bypasses alias conflict validation.
Start the MCP profile from the repository root:

```bash
node tools/configuration/pvad-compose.mjs -f deploy/api/docker-compose.api.yml --profile mcp up --build
```

Fresh installations use PVAD names for services, volumes, networks and secret
mounts, with database/user `pvad`. Existing PVAD data requires the explicit
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
