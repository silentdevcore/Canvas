import assert from 'node:assert/strict';
import test from 'node:test';

import { readRemoteMcpConfig } from './config.js';

test('former environment keys are not read', () => {
  const canonical = {
    PVAD_MCP_RESOURCE_URI: 'https://mcp.example.test/mcp',
    PVAD_MCP_OAUTH_ISSUER: 'https://account.example.test',
    PVAD_MCP_JWKS_URI: 'https://account.example.test/.well-known/jwks.json',
    PVAD_API_KEY: 'synthetic-key',
  };
  const former = Object.fromEntries(Object.entries(canonical).map(([key, value]) =>
    [key.replace('PVAD_', 'PXA_'), value]));
  assert.doesNotThrow(() => readRemoteMcpConfig(canonical));
  assert.throws(() => readRemoteMcpConfig(former));
});

test('remote configuration requires canonical OAuth and MCP URLs', () => {
  const config = readRemoteMcpConfig({
    PVAD_MCP_RESOURCE_URI: 'https://mcp.example.test/mcp',
    PVAD_MCP_OAUTH_ISSUER: 'https://account.example.test',
    PVAD_MCP_JWKS_URI: 'https://account.example.test/.well-known/jwks.json',
    PVAD_MCP_ALLOWED_ORIGINS: 'https://client.example.test',
    PVAD_MCP_ALLOWED_HOSTS: 'proxy.internal',
    PVAD_API_URL: 'http://pvad-api.internal:8080',
    PVAD_API_KEY: 'internal-test-key',
  });
  assert.equal(config.resourceUri.href, 'https://mcp.example.test/mcp');
  assert.ok(config.allowedHosts.has('mcp.example.test'));
  assert.ok(config.allowedHosts.has('proxy.internal'));
  assert.ok(config.allowedOrigins.has('https://client.example.test'));
  assert.equal(config.apiUrl.href, 'http://pvad-api.internal:8080/');
  assert.equal(config.apiKey, 'internal-test-key');
  assert.equal(config.readinessTimeoutMs, 3_000);
  assert.equal(config.allowInsecureInternalDependencies, false);
});

test('remote configuration permits explicit internal clear-text JWKS access', () => {
  const config = readRemoteMcpConfig({
    PVAD_MCP_RESOURCE_URI: 'https://mcp.example.test/mcp',
    PVAD_MCP_OAUTH_ISSUER: 'https://account.example.test',
    PVAD_MCP_JWKS_URI: 'http://pvad-webapi:8080/.well-known/jwks.json',
    PVAD_MCP_ALLOW_INSECURE_INTERNAL_DEPENDENCIES: 'true',
  });
  assert.equal(config.jwksUri.href, 'http://pvad-webapi:8080/.well-known/jwks.json');
});

test('remote configuration rejects public clear-text resource URLs', () => {
  assert.throws(() => readRemoteMcpConfig({
    PVAD_MCP_RESOURCE_URI: 'http://mcp.example.test/mcp',
    PVAD_MCP_OAUTH_ISSUER: 'https://account.example.test',
    PVAD_MCP_JWKS_URI: 'https://account.example.test/.well-known/jwks.json',
  }), /HTTPS/);
});
