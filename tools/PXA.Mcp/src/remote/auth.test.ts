import assert from 'node:assert/strict';
import test from 'node:test';

import { InvalidTokenError } from '@modelcontextprotocol/sdk/server/auth/errors.js';
import { SignJWT, createLocalJWKSet, exportJWK, generateKeyPair } from 'jose';

import { PxaJwtTokenVerifier } from './auth.js';
import type { RemoteMcpConfig } from './config.js';

const baseConfig: RemoteMcpConfig = {
  host: '127.0.0.1',
  port: 3000,
  resourceUri: new URL('https://mcp.example.test/mcp'),
  issuer: 'https://account.example.test',
  jwksUri: new URL('https://account.example.test/.well-known/jwks.json'),
  allowedOrigins: new Set(['https://client.example.test']),
  allowedHosts: new Set(['mcp.example.test']),
  organizationClaim: 'pxa_organization',
  requestLimit: '11mb',
  requestsPerMinute: 120,
  apiUrl: new URL('http://127.0.0.1:5086'),
  readinessTimeoutMs: 3_000,
  allowInsecureInternalDependencies: false,
};

test('JWT verifier enforces issuer, audience, expiry, subject, organization, and scopes', async () => {
  const { privateKey, publicKey } = await generateKeyPair('RS256');
  const publicJwk = await exportJWK(publicKey);
  publicJwk.kid = 'test-key';
  const verifier = new PxaJwtTokenVerifier(
    baseConfig,
    createLocalJWKSet({ keys: [publicJwk] }),
  );
  const token = await new SignJWT({
    pxa_organization: 'organization-1',
    scope: 'pxa.docs.read pxa.documents.render',
    client_id: 'client-1',
  })
    .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
    .setIssuer(baseConfig.issuer)
    .setAudience(baseConfig.resourceUri.href)
    .setSubject('user-1')
    .setIssuedAt()
    .setExpirationTime('5m')
    .sign(privateKey);

  const auth = await verifier.verifyAccessToken(token);
  assert.equal(auth.clientId, 'client-1');
  assert.deepEqual(auth.scopes, ['pxa.docs.read', 'pxa.documents.render']);
  assert.equal(auth.extra?.organizationId, 'organization-1');

  const wrongAudience = await new SignJWT({ pxa_organization: 'organization-1', scope: 'pxa.docs.read' })
    .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
    .setIssuer(baseConfig.issuer)
    .setAudience('https://another.example.test/mcp')
    .setSubject('user-1')
    .setExpirationTime('5m')
    .sign(privateKey);
  await assert.rejects(() => verifier.verifyAccessToken(wrongAudience), InvalidTokenError);

  const missingExpiry = await new SignJWT({ pxa_organization: 'organization-1', scope: 'pxa.docs.read' })
    .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
    .setIssuer(baseConfig.issuer)
    .setAudience(baseConfig.resourceUri.href)
    .setSubject('user-1')
    .sign(privateKey);
  await assert.rejects(() => verifier.verifyAccessToken(missingExpiry), InvalidTokenError);
});
