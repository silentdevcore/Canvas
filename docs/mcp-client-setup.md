# PXA MCP Client Setup

PXA MCP Beta supports local `stdio` and remote Streamable HTTP. The remote endpoint uses OAuth 2.1,
Protected Resource Metadata, Dynamic Client Registration (DCR), Authorization Code with PKCE, and
service-account client credentials. Never place a user access token or one-time service-account
secret in a checked-in client configuration.

## Supported Protocol Revisions

The Beta build uses the pinned official TypeScript MCP SDK and negotiates these revisions:

- `2025-11-25`
- `2025-06-18`
- `2025-03-26`
- `2024-11-05`
- `2024-10-07`

PXA tests every listed revision. A later SDK update may add a revision in a backward-compatible PXA
release. Removing a revision requires a documented compatibility notice and at least one minor
release of advance notice.

## Codex

Register the remote server and start OAuth login with DCR:

```bash
codex mcp add pxa --url https://mcp.powerdoxautomation.com/mcp
codex mcp login pxa \
  --oauth-client-registration DCR \
  --scopes pxa.docs.read,pxa.templates.read,pxa.documents.validate
codex mcp get pxa
```

For a local repository build:

```bash
codex mcp add pxa-local -- node /absolute/path/to/tools/PXA.Mcp/dist/pxa-mcp.js
```

## Claude Code

Add the Streamable HTTP endpoint and then use `/mcp` inside Claude Code to complete browser login:

```bash
claude mcp add --transport http pxa https://mcp.powerdoxautomation.com/mcp
claude mcp get pxa
```

For project-shared configuration, review the generated `.mcp.json` before committing it. The file
must contain only the endpoint and no access token, client secret, or customer identifier.

## Visual Studio Code

Create `.vscode/mcp.json` with the remote HTTP endpoint:

```json
{
  "servers": {
    "pxa": {
      "type": "http",
      "url": "https://mcp.powerdoxautomation.com/mcp"
    }
  }
}
```

Start the server from the MCP view. VS Code follows the protected-resource challenge and opens the
browser for OAuth. Do not add a static `Authorization` header to a shared workspace file.

## Service Accounts

Automation obtains the OAuth client ID and one-time secret from PXA Account. Request only required
scopes from `/connect/token` using `client_credentials`. Store the secret in the operating-system or
CI credential store, rotate it regularly, and revoke it immediately when a workload is retired.

## Troubleshooting

- `401`: complete OAuth login or verify service-account credentials and token audience.
- `403`: verify scope, active organization, entitlement, and browser Origin allowlist.
- `429`: wait for the rate-limit reset instead of retrying immediately.
- `503` from `/health/ready`: PXA WebApi or the identity JWKS endpoint is unavailable.
- OAuth callback rejection: use the exact HTTPS or loopback HTTP redirect URI registered by the
  client; redirects with fragments, credentials, or public clear-text HTTP are rejected.

Official client references:

- Codex configuration: <https://developers.openai.com/codex/mcp>
- Claude Code MCP: <https://docs.anthropic.com/en/docs/claude-code/mcp>
- Visual Studio Code MCP configuration: <https://code.visualstudio.com/docs/agents/reference/mcp-configuration>
