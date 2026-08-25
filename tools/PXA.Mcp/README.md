# pxa-mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server that lets AI agents generate and verify
Power Dox Automation / PXA documents. A transport-independent core supplies the tools and resources;
the local host uses `stdio` for IDE and desktop integrations, while the Beta remote host uses stateless
Streamable HTTP with OAuth bearer-token validation. The compiled distribution bundles
the element catalog and carries the versioned schemas and documentation as packaged resources.

The server exposes PXA resources through the `pxa://...` scheme.

## Tools

| Tool | Description |
| --- | --- |
| `list_elements` | All element types (optionally by category) with description + format support. |
| `get_element_schema` | Full docs for one element: properties, allowed values, defaults, example. |
| `get_example` | A ready-to-use example — `surface: "json"` (a `DesignExportDto`) or `"csharp"` (PXA-compatible PDF C#). |
| `search_docs` | Full-text search across `llms-full.txt` and the C# cookbook. |
| `validate_design` | Validate a `DesignExportDto` JSON string against the complete canonical JSON Schema. |
| `render_preview` | Validate then render through PXA WebApi; accepts only a bounded PDF response and writes a private temporary file. |
| `list_export_formats` | Current WebApi export formats, MIME types, and page capabilities. |
| `list_migration_providers` | Current migration providers with domain, kind, and coverage status. |
| `list_templates` | Paginated Designer templates from the authenticated active organization. |
| `get_template` | One tenant-scoped Designer template with canonical Design JSON and revision metadata. |
| `get_access_context` | Effective remote client, subject, organization, and granted scopes. |

Every tool declares an output schema and returns `structuredContent` with `status`, `data`, and stable
`PXAMCP...` diagnostics. All current tools are annotated as read-only and idempotent.

## Resources

Primary resources:

`pxa://schema/design-export`, `pxa://schema/pxa-workbook`, `pxa://openapi`, `pxa://docs/llms-full`, `pxa://docs/cookbook`.

Use the `pxa://` scheme for all resources.

## Run

```bash
cd tools/PXA.Mcp
npm install
npm run typecheck
npm test
npm run build

# Source host for development
PXA_API_URL=http://localhost:5086 npm start

# Compiled host used by the package binary
PXA_API_URL=http://localhost:5086 node dist/pxa-mcp.js

npm run smoke
npm run smoke:dist
npm pack --dry-run
```

`render_preview` requires PXA WebApi. Set `PXA_API_KEY` to a PXA service-account API key when product
access enforcement is enabled. The key is sent as a Bearer credential and is never written to MCP
output or logs. `PXA_MCP_CONTENT_ROOT` may override the packaged content directory for controlled
development and testing.

## Remote Beta host

The remote host is a stateless OAuth 2.1 protected resource. It accepts MCP JSON-RPC over `POST /mcp`;
`GET` and `DELETE` return `405` because server-side sessions and resumable streams are intentionally
not enabled in this phase. Browser clients must send an allowlisted `Origin`. Non-loopback resource
and issuer URLs must use HTTPS.

```bash
PXA_MCP_RESOURCE_URI=https://mcp.example.com/mcp \
PXA_MCP_OAUTH_ISSUER=https://account.example.com \
PXA_MCP_JWKS_URI=https://account.example.com/.well-known/jwks.json \
PXA_MCP_ALLOWED_ORIGINS=https://client.example.com \
PXA_MCP_ALLOWED_HOSTS=mcp.example.com \
PXA_API_URL=http://pxa-api:8080 \
PXA_API_KEY='<internal-service-account-key>' \
PXA_MCP_DELEGATION_SECRET='<mounted-32-byte-or-longer-secret>' \
npm run start:remote
```

The OAuth access token is validated for issuer, signature, expiry, audience/resource, subject,
organization, and scopes. It is never forwarded to PXA WebApi. `PXA_API_KEY` is a separate internal
credential used only for fixed global PXA API routes. Tenant template calls use a separately signed,
30-second internal delegation created from the already verified subject, organization, and scopes;
the shared `PXA_MCP_DELEGATION_SECRET` must be mounted into MCP Server and as
`OAuth__McpDelegationSecret` into WebApi. The remote
baseline requires `pxa.docs.read`; `validate_design` additionally requires
`pxa.documents.validate`, while template tools require `pxa.templates.read`. Remote
`render_preview` remains disabled until it can return a tenant-bound, expiring artifact instead of
a local file path.

Health endpoints and protected-resource metadata are public:

- `GET /health/live`
- `GET /health/ready`
- `GET /.well-known/oauth-protected-resource/mcp`

PXA Account publishes OAuth discovery and supports interactive Authorization Code with PKCE as well
as service-account `client_credentials`. Compatible interactive MCP clients may register a public
PKCE client through the discovery-advertised `/connect/register` endpoint. Registration accepts
only HTTPS or loopback HTTP callbacks and never issues a client secret. Service-account key creation returns `oAuthClientId` beside
the one-time secret; revoking the key or service account also revokes the OAuth client.

`/health/ready` checks both PXA WebApi readiness and the configured JWKS endpoint and returns `503`
when either dependency is unavailable. `PXA_MCP_READINESS_TIMEOUT_MS` controls the bounded probe.
Clear-text JWKS access is rejected outside loopback unless
`PXA_MCP_ALLOW_INSECURE_INTERNAL_DEPENDENCIES=true` explicitly enables an isolated container-network
connection.

Build the non-root Linux container from the repository root:

```bash
docker build -f tools/PXA.Mcp/Dockerfile -t pxa-mcp:dev .
```

The supported PXA Server Compose bundle and reverse proxy are documented in
`deploy/api/README.md`.

Client-specific setup is documented in `docs/mcp-client-setup.md`. Security boundaries, the threat
model, pseudonymous structured logging, metrics, retention, and incident response are documented in
`docs/mcp-security-and-operations.md`.

## Use from Claude Desktop / Claude Code

Add to your MCP config (e.g. Claude Desktop `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "pxa": {
      "command": "node",
      "args": ["dist/pxa-mcp.js"],
      "cwd": "/absolute/path/to/PXA/tools/PXA.Mcp",
      "env": {
        "PXA_API_URL": "http://localhost:5086",
        "PXA_API_KEY": "<service-account-api-key>"
      }
    }
  }
}
```

The source build imports the authoritative Designer element catalog. The build bundles that catalog
into the executable and copies all versioned runtime resources into `dist/resources`, so an installed
package does not require the PXA repository source tree.
