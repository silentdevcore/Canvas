import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { test } from 'node:test';
import { checkRepository, validateRelease, validateTrust } from './pvad-native-licensing.mjs';

test('repository metadata and operation directories are consistent', () => {
  assert.equal(checkRepository().products, 10);
  assert.equal(checkRepository().enforcementStatus, 'pdf-word-and-spreadsheet');
});

test('release date and version cannot drift or be replaced by build metadata', () => {
  const releases = { releases: [{ version: '1.2.3', publishedAt: '2026-08-03' }] };
  validateRelease('1.2.3', releases, { version: '1.2.3', publishedAt: '2026-08-03' });
  assert.throws(() => validateRelease('1.2.4', releases, { version: '1.2.4', publishedAt: '2026-08-03' }));
  assert.throws(() => validateRelease('1.2.3', releases, { version: '1.2.3', publishedAt: '2026-11-15' }));
  assert.throws(() => validateRelease('1.2.3', releases, { version: '2.0.0', publishedAt: '2026-08-03' }));
});

test('empty production trust is allowed for development but not release readiness', () => {
  validateTrust({ schemaVersion: 1, keys: [] });
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [] }, { requireKeys: true }));
});

test('only explicit public P-256 keys are valid trust anchors', () => {
  const { publicKey, privateKey } = generateKeyPairSync('ec', { namedCurve: 'prime256v1',
    publicKeyEncoding: { type: 'spki', format: 'pem' }, privateKeyEncoding: { type: 'pkcs8', format: 'pem' } });
  const key = { keyId: 'synthetic-key', publicKeyPem: publicKey };
  validateTrust({ schemaVersion: 1, keys: [key] }, { requireKeys: true });
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [key, key] }));
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [{ ...key, publicKeyPem: privateKey }] }));
  assert.throws(() => validateTrust({ schemaVersion: 2, keys: [key] }));
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [{ ...key, privateKey }] }));
  const wrong = generateKeyPairSync('ec', { namedCurve: 'secp384r1', publicKeyEncoding: { type: 'spki', format: 'pem' } });
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [{ ...key, publicKeyPem: wrong.publicKey }] }));
});
