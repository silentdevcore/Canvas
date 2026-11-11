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
  organizationClaim: 'pvad_organization',
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
    pvad_organization: 'organization-1',
    scope: 'pvad.docs.read pvad.documents.validate',
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
  assert.deepEqual(auth.scopes, ['pvad.docs.read', 'pvad.documents.validate']);
  assert.equal(auth.extra?.organizationId, 'organization-1');

  const wrongAudience = await new SignJWT({ pvad_organization: 'organization-1', scope: 'pvad.docs.read' })
    .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
    .setIssuer(baseConfig.issuer)
    .setAudience('https://another.example.test/mcp')
    .setSubject('user-1')
    .setExpirationTime('5m')
    .sign(privateKey);
  await assert.rejects(() => verifier.verifyAccessToken(wrongAudience), InvalidTokenError);

  const missingExpiry = await new SignJWT({ pvad_organization: 'organization-1', scope: 'pvad.docs.read' })
    .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
    .setIssuer(baseConfig.issuer)
    .setAudience(baseConfig.resourceUri.href)
    .setSubject('user-1')
    .sign(privateKey);
  await assert.rejects(() => verifier.verifyAccessToken(missingExpiry), InvalidTokenError);
});

test('only canonical organization claims and scopes are accepted', async () => {
  const { privateKey, publicKey } = await generateKeyPair('ES256');
  const publicJwk = await exportJWK(publicKey);
  publicJwk.kid = 'claim-key';
  const verifier = new PvadJwtTokenVerifier({ ...baseConfig, organizationClaim: 'pvad_organization' },
    createLocalJWKSet({ keys: [publicJwk] }));
  const sign = (payload: Record<string, unknown>) => new SignJWT(payload)
    .setProtectedHeader({ alg: 'ES256', kid: 'claim-key' }).setIssuer(baseConfig.issuer)
    .setAudience(baseConfig.resourceUri.href).setSubject('synthetic-user').setExpirationTime('5m').sign(privateKey);
  const auth = await verifier.verifyAccessToken(await sign({ pvad_organization: 'org', scope: 'pvad.docs.read', scp: ['pvad.docs.read'] }));
  assert.equal(auth.extra?.organizationId, 'org');
  assert.deepEqual(auth.scopes, ['pvad.docs.read']);
  for (const claims of [
    { pxa_organization: 'org', scope: 'pvad.docs.read' },
    { pvad_organization: '', scope: 'pvad.docs.read' },
    { pvad_organization: 'org', scope: 'pvad.docs.read', scp: ['pvad.templates.read'] },
    { pvad_organization: 'org', scope: 42 },
    { pvad_organization: 'org', scp: ['pvad.docs.read', 42] },
  ]) await assert.rejects(() => sign(claims).then((token) => verifier.verifyAccessToken(token)), InvalidTokenError);
});

test('former scopes are preserved verbatim without promotion to current capabilities', async () => {
  const { privateKey, publicKey } = await generateKeyPair('RS256');
  const publicJwk = await exportJWK(publicKey);
  publicJwk.kid = 'unregistered-scope-test';
  const verifier = new PvadJwtTokenVerifier(baseConfig, createLocalJWKSet({ keys: [publicJwk] }));
  const token = await new SignJWT({
    pvad_organization: 'synthetic-organization', scope: 'pxa.documents.render pxa.docs.read',
  })
    .setProtectedHeader({ alg: 'RS256', kid: publicJwk.kid })
    .setIssuer(baseConfig.issuer).setAudience(baseConfig.resourceUri.href)
    .setSubject('synthetic-user').setExpirationTime('5m').sign(privateKey);

  const auth = await verifier.verifyAccessToken(token);

  assert.deepEqual(auth.scopes, ['pxa.documents.render', 'pxa.docs.read']);
  assert.ok(!auth.scopes.includes('pvad.documents.render'));
  assert.ok(!auth.scopes.includes('pvad.documents.validate'));
  assert.ok(!auth.scopes.includes('pvad.docs.read'));
});
