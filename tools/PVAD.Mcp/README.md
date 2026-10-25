# pvad-mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets AI agents generate and verify
PVADE / PVAD documents. A transport-independent core supplies the tools and resources;
the local host uses `stdio` for IDE and desktop integrations, while the Beta remote host uses stateless
Streamable HTTP with OAuth bearer-token validation. The compiled distribution bundles
the element catalog and carries the versioned schemas and documentation as packaged resources.

The server exposes PVAD resources through the `pvad://...` scheme.

## Tools

| Tool | Description |
| --- | --- |
| `list_elements` | All element types (optionally by category) with description + format support. |
| `get_element_schema` | Full docs for one element: properties, allowed values, defaults, example. |
| `get_example` | A ready-to-use example — `surface: "json"` (a `DesignExportDto`) or `"csharp"` (PVAD-compatible PDF C#). |
| `search_docs` | Full-text search across `llms-full.txt` and the C# cookbook. |
| `validate_design` | Validate a `DesignExportDto` JSON string against the complete canonical JSON Schema. |
| `render_preview` | Validate then render through PVAD WebApi; accepts only a bounded PDF response and writes a private temporary file. |
| `list_export_formats` | Current WebApi export formats, MIME types, and page capabilities. |
| `list_migration_providers` | Current migration providers with domain, kind, and coverage status. |
| `list_templates` | Paginated Designer templates from the authenticated active organization. |
| `get_template` | One tenant-scoped Designer template with canonical Design JSON and revision metadata. |
| `get_access_context` | Effective remote client, subject, organization, and granted scopes. |

Every tool declares an output schema and returns `structuredContent` with `status`, `data`, and stable
`PXAMCP...` diagnostics. All current tools are annotated as read-only and idempotent.

## Resources

Primary resources:

`pvad://schema/design-export`, `pvad://schema/pvad-workbook`, `pvad://openapi`, `pvad://docs/llms-full`, `pvad://docs/cookbook`.

Use `pvad://` for new clients. The five historical `pxa://` resource URIs remain readable as deprecated compatibility aliases. The legacy workbook resource retains its original schema identity; canonical and historical schema IDs resolve offline through the packaged alias manifest.

Maintained schema IDs use `https://pvade.com/schemas/...`. The previous domain
and older schema IDs remain explicit offline aliases, including `$defs` fragment
references. The package contains every schema definition listed in the registry,
not operational metadata documents or approval data. Legacy native schemas retain
their generation-specific constraints. Schema URLs are identifiers, not proof
that public endpoints are deployed.

The `pxa-mcp`, `pxa-mcp-server`, and `canvas-mcp` commands and `PXA_*` environment names remain compatibility aliases. Conflicting canonical and legacy environment values fail startup. Organization claims default to `pvad_organization` with a conflict-checked legacy reader. Internal delegation now writes `PVAD-MCP` with canonical issuer/audience and read-only scopes; the API accepts both explicit historical formats. Canonical `pvad.*` scopes have explicit historical `pxa.*` equivalents. Existing stored grants are not rewritten automatically: apply and verify the opt-in store migration before switching public OAuth requests. See [identity compatibility](../../docs/pvad-identity-compatibility.md) before deploying mixed versions.

## Run

```bash
cd tools/PVAD.Mcp
npm install
npm run typecheck
npm test
npm run build

# Source host for development
PVAD_API_URL=http://localhost:5086 npm start

# Compiled host used by the package binary
PVAD_API_URL=http://localhost:5086 node dist/pvad-mcp.js

npm run smoke
npm run smoke:dist
npm pack --dry-run
```

`render_preview` requires PVAD WebApi. Set `PVAD_API_KEY` to a PVAD service-account API key when product
access enforcement is enabled. The key is sent as a Bearer credential and is never written to MCP
output or logs. `PVAD_MCP_CONTENT_ROOT` may override the packaged content directory for controlled
development and testing.

## Remote Beta host

The remote host is a stateless OAuth 2.1 protected resource. It accepts MCP JSON-RPC over `POST /mcp`;
`GET` and `DELETE` return `405` because server-side sessions and resumable streams are intentionally
not enabled in this phase. Browser clients must send an allowlisted `Origin`. Non-loopback resource
and issuer URLs must use HTTPS.

```bash
PVAD_MCP_RESOURCE_URI=https://mcp.example.com/mcp \
PVAD_MCP_OAUTH_ISSUER=https://account.example.com \
PVAD_MCP_JWKS_URI=https://account.example.com/.well-known/jwks.json \
PVAD_MCP_ALLOWED_ORIGINS=https://client.example.com \
PVAD_MCP_ALLOWED_HOSTS=mcp.example.com \
PVAD_API_URL=http://pvad-webapi:8080 \
PVAD_API_KEY='<internal-service-account-key>' \
PVAD_MCP_DELEGATION_SECRET='<mounted-32-byte-or-longer-secret>' \
npm run start:remote
```

The OAuth access token is validated for issuer, signature, expiry, audience/resource, subject,
organization, and scopes. It is never forwarded to PVAD WebApi. `PVAD_API_KEY` is a separate internal
credential used only for fixed global PVAD API routes. Tenant template calls use a separately signed,
30-second internal delegation created from the already verified subject, organization, and scopes;
the shared `PVAD_MCP_DELEGATION_SECRET` must be mounted into MCP Server and as
`OAuth__McpDelegationSecret` into WebApi. The remote
baseline requires `pvad.docs.read`; `validate_design` additionally requires
`pvad.documents.validate`, while template tools require `pvad.templates.read`.
The corresponding historical `pxa.*` scopes remain compatibility aliases.
Protected-resource discovery now advertises canonical scope spellings.
Scope normalization still accepts either form; existing stored OAuth grants
are not rewritten automatically. Apply the opt-in scope migration before
requesting canonical scopes with a retained registration.
The example API hostname assumes the shared Compose network; use the actual
reachable API origin when running outside that network. Remote
`render_preview` remains disabled until it can return a tenant-bound, expiring artifact instead of
a local file path.

Health endpoints and protected-resource metadata are public:

- `GET /health/live`
- `GET /health/ready`
- `GET /.well-known/oauth-protected-resource/mcp`

PVAD Account publishes OAuth discovery and supports interactive Authorization Code with PKCE as well
as service-account `client_credentials`. Compatible interactive MCP clients may register a public
PKCE client through the discovery-advertised `/connect/register` endpoint. Registration accepts
only HTTPS or loopback HTTP callbacks and never issues a client secret. Service-account key creation returns `oAuthClientId` beside
the one-time secret; revoking the key or service account also revokes the OAuth client.
New keys receive `pvad-sa-<id>` clients. Retained `pxa-sa-<id>` registrations keep
their stored IDs and credentials; use the ID returned by Account, never construct
one from a prefix. Revocation checks both generations. Built-in client defaults
are `pvad-mcp-public` and `pvad-mcp-server`. Stores containing their old counterparts
require explicit maintenance approval before parallel registrations are created.
See [OAuth client transition](../../docs/pvad-oauth-client-transition.md).

`/health/ready` checks both PVAD WebApi readiness and the configured JWKS endpoint and returns `503`
when either dependency is unavailable. `PVAD_MCP_READINESS_TIMEOUT_MS` controls the bounded probe.
Clear-text JWKS access is rejected outside loopback unless
`PVAD_MCP_ALLOW_INSECURE_INTERNAL_DEPENDENCIES=true` explicitly enables an isolated container-network
connection.

Build the non-root Linux container from the repository root:

```bash
docker build -f tools/PVAD.Mcp/Dockerfile -t pvad-mcp:dev .
```

The supported PVAD Server Compose bundle and reverse proxy are documented in
`deploy/api/README.md`.

Client-specific setup is documented in `docs/mcp-client-setup.md`. Security boundaries, the threat
model, pseudonymous structured logging, metrics, retention, and incident response are documented in
`docs/mcp-security-and-operations.md`.

## Use from Claude Desktop / Claude Code

Add to your MCP config (e.g. Claude Desktop `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "pvad": {
      "command": "node",
      "args": ["dist/pvad-mcp.js"],
      "cwd": "/absolute/path/to/PVAD/tools/PVAD.Mcp",
      "env": {
        "PVAD_API_URL": "http://localhost:5086",
        "PVAD_API_KEY": "<service-account-api-key>"
      }
    }
  }
}
```

The source build imports the authoritative Designer element catalog. The build bundles that catalog
into the executable and copies all versioned runtime resources into `dist/resources`, so an installed
package does not require the PVAD repository source tree.
