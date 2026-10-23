import assert from 'node:assert/strict';
import test from 'node:test';

import { readRemoteMcpConfig } from './config.js';

test('canonical PVAD environment matches legacy configuration and rejects conflicting aliases', () => {
  const canonical = {
    PVAD_MCP_RESOURCE_URI: 'https://mcp.example.test/mcp',
    PVAD_MCP_OAUTH_ISSUER: 'https://account.example.test',
    PVAD_MCP_JWKS_URI: 'https://account.example.test/.well-known/jwks.json',
    PVAD_API_KEY: 'synthetic-key',
  };
  const legacy = Object.fromEntries(Object.entries(canonical).map(([key, value]) =>
    [key.replace('PVAD_', 'PXA_'), value]));
  assert.deepEqual(readRemoteMcpConfig(canonical), readRemoteMcpConfig(legacy));
  assert.throws(() => readRemoteMcpConfig({ ...canonical, PXA_API_KEY: 'different-key' }),
    /Conflicting configuration keys: PVAD_API_KEY and PXA_API_KEY/);
});

test('remote configuration requires canonical OAuth and MCP URLs', () => {
  const config = readRemoteMcpConfig({
    PXA_MCP_RESOURCE_URI: 'https://mcp.example.test/mcp',
    PXA_MCP_OAUTH_ISSUER: 'https://account.example.test',
    PXA_MCP_JWKS_URI: 'https://account.example.test/.well-known/jwks.json',
    PXA_MCP_ALLOWED_ORIGINS: 'https://client.example.test',
    PXA_MCP_ALLOWED_HOSTS: 'proxy.internal',
    PXA_API_URL: 'http://pxa-api.internal:8080',
    PXA_API_KEY: 'internal-test-key',
  });
  assert.equal(config.resourceUri.href, 'https://mcp.example.test/mcp');
  assert.ok(config.allowedHosts.has('mcp.example.test'));
  assert.ok(config.allowedHosts.has('proxy.internal'));
  assert.ok(config.allowedOrigins.has('https://client.example.test'));
  assert.equal(config.apiUrl.href, 'http://pxa-api.internal:8080/');
  assert.equal(config.apiKey, 'internal-test-key');
  assert.equal(config.readinessTimeoutMs, 3_000);
  assert.equal(config.allowInsecureInternalDependencies, false);
});

test('remote configuration permits explicit internal clear-text JWKS access', () => {
  const config = readRemoteMcpConfig({
    PXA_MCP_RESOURCE_URI: 'https://mcp.example.test/mcp',
    PXA_MCP_OAUTH_ISSUER: 'https://account.example.test',
    PXA_MCP_JWKS_URI: 'http://pxa-webapi:8080/.well-known/jwks.json',
    PXA_MCP_ALLOW_INSECURE_INTERNAL_DEPENDENCIES: 'true',
  });
  assert.equal(config.jwksUri.href, 'http://pxa-webapi:8080/.well-known/jwks.json');
});

test('remote configuration rejects public clear-text resource URLs', () => {
  assert.throws(() => readRemoteMcpConfig({
    PXA_MCP_RESOURCE_URI: 'http://mcp.example.test/mcp',
    PXA_MCP_OAUTH_ISSUER: 'https://account.example.test',
    PXA_MCP_JWKS_URI: 'https://account.example.test/.well-known/jwks.json',
  }), /HTTPS/);
});
