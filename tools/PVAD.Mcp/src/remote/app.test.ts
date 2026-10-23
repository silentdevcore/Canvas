import assert from 'node:assert/strict';
import { createServer, type Server } from 'node:http';
import test from 'node:test';

import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';
import { SUPPORTED_PROTOCOL_VERSIONS } from '@modelcontextprotocol/sdk/types.js';
import type { OAuthTokenVerifier } from '@modelcontextprotocol/sdk/server/auth/provider.js';

import { createRemoteMcpApp } from './app.js';
import type { RemoteMcpAppOptions } from './app.js';
import type { McpLogEvent } from './observability.js';
import type { RemoteMcpConfig } from './config.js';

const verifier: OAuthTokenVerifier = {
  async verifyAccessToken(token) {
    if (token !== 'valid-token') throw new Error('invalid');
    return {
      token,
      clientId: 'client-1',
      scopes: ['pxa.docs.read'],
      expiresAt: Math.floor(Date.now() / 1000) + 300,
      resource: new URL('http://127.0.0.1/mcp'),
      extra: { subject: 'user-1', organizationId: 'organization-1' },
    };
  },
};

test('canonical scope grants pass the HTTP bearer gate and access context stays canonical', async () => {
  const running = await listen({ verifier: {
    async verifyAccessToken(token) {
      const auth = await verifier.verifyAccessToken(token);
      return { ...auth, scopes: ['pvad.docs.read'] };
    },
  } });
  const client = new Client({ name: 'canonical-scope-test', version: '1.0.0' });
  try {
    await client.connect(new StreamableHTTPClientTransport(new URL('/mcp', running.baseUrl), {
      requestInit: { headers: { authorization: 'Bearer valid-token' } },
    }));
    const result = await client.callTool({ name: 'get_access_context', arguments: {} });
    const data = (result.structuredContent as { data: { scopes: string[] } }).data;
    assert.deepEqual(data.scopes, ['pvad.docs.read']);
  } finally {
    await client.close();
    await close(running.server);
  }
});

async function listen(options: RemoteMcpAppOptions = {}): Promise<{ server: Server; baseUrl: URL }> {
  const config: RemoteMcpConfig = {
    host: '127.0.0.1',
    port: 0,
    resourceUri: new URL('http://127.0.0.1/mcp'),
    issuer: 'http://127.0.0.1:5178',
    jwksUri: new URL('http://127.0.0.1:5178/.well-known/jwks.json'),
    allowedOrigins: new Set(['https://client.example.test']),
    allowedHosts: new Set(['127.0.0.1']),
    organizationClaim: 'pxa_organization',
    requestLimit: '11mb',
    requestsPerMinute: 120,
    apiUrl: new URL('http://127.0.0.1:5086'),
    readinessTimeoutMs: 3_000,
    allowInsecureInternalDependencies: false,
  };
  const server = createServer(createRemoteMcpApp(config, {
    verifier: options.verifier ?? verifier,
    readinessProbe: options.readinessProbe ?? (async () => ({ ready: true, api: 'ready', identity: 'ready' })),
    metrics: options.metrics,
    logger: options.logger ?? (() => undefined),
  }));
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  const address = server.address();
  assert.ok(address && typeof address === 'object');
  return { server, baseUrl: new URL(`http://127.0.0.1:${address.port}`) };
}

async function close(server: Server): Promise<void> {
  await new Promise<void>((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
}

test('remote host exposes health and protected-resource metadata without authentication', async () => {
  const running = await listen();
  try {
    const health = await fetch(new URL('/health/ready', running.baseUrl));
    assert.equal(health.status, 200);
    const metadata = await fetch(new URL('/.well-known/oauth-protected-resource/mcp', running.baseUrl));
    assert.equal(metadata.status, 200);
    const body = await metadata.json() as { resource_name: string; authorization_servers: string[] };
    assert.equal(body.resource_name, 'PVADE MCP');
    assert.deepEqual(body.authorization_servers, [
      'http://127.0.0.1:5178',
    ]);
  } finally {
    await close(running.server);
  }
});

test('readiness fails closed when a required dependency is unavailable', async () => {
  const config: RemoteMcpConfig = {
    host: '127.0.0.1',
    port: 0,
    resourceUri: new URL('http://127.0.0.1/mcp'),
    issuer: 'http://127.0.0.1:5178',
    jwksUri: new URL('http://127.0.0.1:5178/.well-known/jwks.json'),
    allowedOrigins: new Set(),
    allowedHosts: new Set(['127.0.0.1']),
    organizationClaim: 'pxa_organization',
    requestLimit: '11mb',
    requestsPerMinute: 120,
    apiUrl: new URL('http://127.0.0.1:5086'),
    readinessTimeoutMs: 3_000,
    allowInsecureInternalDependencies: false,
  };
  const server = createServer(createRemoteMcpApp(config, {
    verifier,
    readinessProbe: async () => ({ ready: false, api: 'unavailable', identity: 'ready' }),
  }));
  await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
  const address = server.address();
  assert.ok(address && typeof address === 'object');
  try {
    const response = await fetch(`http://127.0.0.1:${address.port}/health/ready`);
    assert.equal(response.status, 503);
    assert.equal((await response.json() as { status: string }).status, 'not_ready');
  } finally {
    await close(server);
  }
});

test('remote MCP rejects missing authorization and untrusted browser origins', async () => {
  const running = await listen();
  try {
    const unauthorized = await fetch(new URL('/mcp', running.baseUrl), {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: '{}',
    });
    assert.equal(unauthorized.status, 401);
    assert.match(unauthorized.headers.get('www-authenticate') ?? '', /resource_metadata=/);

    const forbiddenOrigin = await fetch(new URL('/mcp', running.baseUrl), {
      method: 'POST',
      headers: {
        authorization: 'Bearer valid-token',
        'content-type': 'application/json',
        origin: 'https://attacker.example.test',
      },
      body: '{}',
    });
    assert.equal(forbiddenOrigin.status, 403);
  } finally {
    await close(running.server);
  }
});

test('remote MCP negotiates every protocol revision supported by the pinned SDK', async () => {
  const running = await listen();
  try {
    for (const protocolVersion of SUPPORTED_PROTOCOL_VERSIONS) {
      const response = await fetch(new URL('/mcp', running.baseUrl), {
        method: 'POST',
        headers: {
          authorization: 'Bearer valid-token',
          accept: 'application/json, text/event-stream',
          'content-type': 'application/json',
        },
        body: JSON.stringify({
          jsonrpc: '2.0',
          id: protocolVersion,
          method: 'initialize',
          params: {
            protocolVersion,
            capabilities: {},
            clientInfo: { name: 'protocol-matrix', version: '1.0.0' },
          },
        }),
      });
      assert.equal(response.status, 200, protocolVersion);
      const result = await response.json() as { result?: { protocolVersion?: string } };
      assert.equal(result.result?.protocolVersion, protocolVersion);
    }
  } finally {
    await close(running.server);
  }
});

test('authenticated Streamable HTTP client receives its tenant context', async () => {
  const events: McpLogEvent[] = [];
  const running = await listen({ logger: (event) => events.push(event) });
  const client = new Client({ name: 'remote-test', version: '1.0.0' });
  try {
    const transport = new StreamableHTTPClientTransport(new URL('/mcp', running.baseUrl), {
      requestInit: { headers: { authorization: 'Bearer valid-token' } },
    });
    await client.connect(transport);
    const result = await client.callTool({ name: 'get_access_context', arguments: {} });
    assert.equal(result.isError, false);
    const data = (result.structuredContent as {
      data: { authenticated: boolean; organizationId: string; subject: string };
    }).data;
    assert.equal(data.authenticated, true);
    assert.equal(data.organizationId, 'organization-1');
    assert.equal(data.subject, 'user-1');

    const remotePreview = await client.callTool({ name: 'render_preview', arguments: { design: '{}' } });
    assert.equal(remotePreview.isError, true);
    assert.equal((remotePreview.structuredContent as {
      diagnostics: Array<{ code: string }>;
    }).diagnostics[0].code, 'PXAMCP032');

    const metrics = await (await fetch(new URL('/metrics', running.baseUrl))).text();
    assert.match(metrics, /pvad_mcp_tool_calls_total\{tool="get_access_context",status="200"\} 1/);
    assert.match(metrics, /pvad_mcp_dependency_ready\{dependency="api"\} 0/);
    assert.ok(events.some((event) => event.tool === 'get_access_context' && event.organizationRef?.length === 16));
    assert.doesNotMatch(JSON.stringify(events), /valid-token/);
    assert.doesNotMatch(JSON.stringify(events), /organization-1/);
  } finally {
    await client.close();
    await close(running.server);
  }
});
