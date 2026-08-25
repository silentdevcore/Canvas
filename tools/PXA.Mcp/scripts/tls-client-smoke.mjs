import assert from 'node:assert/strict';

import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';

const endpoint = new URL(process.env.PXA_MCP_SMOKE_ENDPOINT ?? 'https://localhost:3443/mcp');
const tokenEndpoint = process.env.PXA_MCP_SMOKE_TOKEN_ENDPOINT ?? 'http://127.0.0.1:39090/token';
const tokenResponse = await fetch(tokenEndpoint);
assert.equal(tokenResponse.status, 200);
const token = (await tokenResponse.json()).access_token;
assert.equal(typeof token, 'string');

const health = await fetch(new URL('/health/ready', endpoint));
assert.equal(health.status, 200);

const metadata = await fetch(new URL('/.well-known/oauth-protected-resource/mcp', endpoint));
assert.equal(metadata.status, 200);
assert.deepEqual((await metadata.json()).authorization_servers, ['https://account.pxa.test']);

const client = new Client({ name: 'pxa-tls-smoke', version: '1.0.0' });
try {
  await client.connect(new StreamableHTTPClientTransport(endpoint, {
    requestInit: { headers: { authorization: `Bearer ${token}` } },
  }));
  const tools = await client.listTools();
  assert.ok(tools.tools.some((tool) => tool.name === 'get_access_context'));
  const context = await client.callTool({ name: 'get_access_context', arguments: {} });
  assert.equal(context.isError, false);
  assert.equal(context.structuredContent.data.organizationId, 'smoke-organization');
} finally {
  await client.close();
}
