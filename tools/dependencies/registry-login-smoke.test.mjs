import assert from 'node:assert/strict';
import { request } from 'node:http';
import { request as secureRequest } from 'node:https';
import { execFileSync } from 'node:child_process';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { createRegistry, verifyCredentials } from './registry-login-smoke.mjs';

function ping(port, authorization, path = '/v2/', ca) {
  return new Promise((accept, reject) => {
    const call = (ca ? secureRequest : request)({ hostname: '127.0.0.1', port, path, ca,
      headers: authorization ? { Authorization: authorization } : {} }, (response) => {
      response.resume();
      response.on('end', () => accept(response.statusCode));
    });
    call.on('error', reject);
    call.end();
  });
}

test('loopback registry requires correct credentials and cannot publish images', async () => {
  const server = createRegistry('test', 'synthetic-password');
  await new Promise((accept) => server.listen(0, '127.0.0.1', accept));
  try {
    const port = server.address().port;
    assert.equal(server.address().address, '127.0.0.1');
    assert.equal(await ping(port), 401);
    assert.equal(await ping(port, 'Basic invalid'), 401);
    const auth = `Basic ${Buffer.from('test:synthetic-password').toString('base64')}`;
    assert.equal(await ping(port, auth), 200);
    assert.equal(await ping(port, auth, '/v2/test/manifests/latest'), 404);
  } finally {
    await new Promise((accept) => server.close(accept));
  }
});

test('TLS registry is verified using only an explicitly supplied synthetic CA', async () => {
  const path = await mkdtemp(join(tmpdir(), 'pxa-registry-tls-'));
  let server;
  try {
    execFileSync('openssl', ['req', '-x509', '-newkey', 'rsa:2048', '-nodes',
      '-keyout', join(path, 'key.pem'), '-out', join(path, 'ca.pem'), '-days', '1',
      '-subj', '/CN=synthetic-registry', '-addext', 'subjectAltName=IP:127.0.0.1'], { stdio: 'pipe' });
    const ca = await readFile(join(path, 'ca.pem'));
    server = createRegistry('test', 'synthetic-password', { key: await readFile(join(path, 'key.pem')), cert: ca });
    await new Promise((accept, reject) => {
      server.once('error', reject);
      server.listen(0, '127.0.0.1', accept);
    });
    assert.equal(await ping(server.address().port, undefined, '/v2/', ca), 401);
    const auth = `Basic ${Buffer.from('test:synthetic-password').toString('base64')}`;
    assert.equal(await ping(server.address().port, auth, '/v2/', ca), 200);
  } finally {
    if (server?.listening) await new Promise((accept) => server.close(accept));
    await rm(path, { recursive: true });
  }
});

test('credential validation rejects unscoped, changed and shared credentials', () => {
  const state = { registry: '127.0.0.1:12345', username: 'test', password: 'synthetic-password' };
  const config = { auths: { [state.registry]: {
    auth: Buffer.from(`${state.username}:${state.password}`).toString('base64'),
  } } };
  verifyCredentials(config, state);
  assert.throws(() => verifyCredentials({ ...config, credsStore: 'desktop' }, state));
  assert.throws(() => verifyCredentials({ auths: { ...config.auths, 'ghcr.io': {} } }, state));
  assert.throws(() => verifyCredentials(config, { ...state, password: 'changed' }));
  assert.throws(() => verifyCredentials({ auths: {} }, state));
});
