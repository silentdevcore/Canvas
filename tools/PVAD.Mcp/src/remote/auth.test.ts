import assert from 'node:assert/strict';
import test from 'node:test';

import { InvalidTokenError } from '@modelcontextprotocol/sdk/server/auth/errors.js';
import { SignJWT, createLocalJWKSet, exportJWK, generateKeyPair } from 'jose';

import { PvadJwtTokenVerifier } from './auth.js';
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
  const verifier = new PvadJwtTokenVerifier(
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
  assert.deepEqual(auth.scopes, ['pvad.docs.read', 'pxa.docs.read', 'pxa.documents.render']);
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

test('claim and scope aliases stay equivalent while conflicting or malformed aliases fail closed', async () => {
  const { privateKey, publicKey } = await generateKeyPair('ES256');
  const publicJwk = await exportJWK(publicKey);
  publicJwk.kid = 'alias-key';
  const verifier = new PvadJwtTokenVerifier({ ...baseConfig, organizationClaim: 'pvad_organization' },
    createLocalJWKSet({ keys: [publicJwk] }));
  const sign = (payload: Record<string, unknown>) => new SignJWT(payload)
    .setProtectedHeader({ alg: 'ES256', kid: 'alias-key' }).setIssuer(baseConfig.issuer)
    .setAudience(baseConfig.resourceUri.href).setSubject('synthetic-user').setExpirationTime('5m').sign(privateKey);
  for (const claims of [
    { pxa_organization: 'org', scope: 'pxa.docs.read' },
    { pvad_organization: 'org', scope: 'pvad.docs.read' },
    { pvad_organization: 'org', pxa_organization: 'org', scope: 'pvad.docs.read', scp: ['pxa.docs.read'] },
  ]) {
    const auth = await verifier.verifyAccessToken(await sign(claims));
    assert.equal(auth.extra?.organizationId, 'org');
    assert.ok(auth.scopes.includes('pvad.docs.read'));
    assert.ok(auth.scopes.includes('pxa.docs.read'));
  }
  for (const claims of [
    { pvad_organization: 'first', pxa_organization: 'second', scope: 'pxa.docs.read' },
    { pvad_organization: '', pxa_organization: 'org', scope: 'pxa.docs.read' },
    { pvad_organization: 'org', scope: 'pxa.docs.read', scp: ['pxa.templates.read'] },
    { pvad_organization: 'org', scope: 42 },
    { pvad_organization: 'org', scp: ['pxa.docs.read', 42] },
  ]) await assert.rejects(() => sign(claims).then((token) => verifier.verifyAccessToken(token)), InvalidTokenError);
});
