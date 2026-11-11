import assert from 'node:assert/strict';
import test from 'node:test';
import { normalizePvadEnvironment, readPvadEnvironment, resolvePvadDevApiTarget } from './environment.mjs';

test('canonical environment keys resolve without changing input', () => {
  const environment = { PVAD_BUILD_COMMIT: 'example' };
  const before = { ...environment };
  assert.equal(readPvadEnvironment('PVAD_BUILD_COMMIT', environment), 'example');
  assert.deepEqual(environment, before);
  assert.equal(readPvadEnvironment('PVAD_BUILD_COMMIT', {}), undefined);
  assert.equal(readPvadEnvironment('VITE_PVAD_COMPANY_URL', { VITE_PVAD_COMPANY_URL: 'https://pvade.com/' }), 'https://pvade.com/');
});

test('former environment keys are never read', () => {
  assert.equal(readPvadEnvironment('PVAD_API_KEY', { PXA_API_KEY: 'former-secret' }), undefined);
  assert.equal(readPvadEnvironment('PVAD_RUN_LIVE_AUTH_TRUST', { PXA_RUN_LIVE_AUTH_TRUST: '1' }), undefined);
  assert.throws(() => readPvadEnvironment('PXA_API_KEY', {}), /Expected a PVAD/);
});

test('explicit empty canonical settings are preserved', () => {
  assert.equal(readPvadEnvironment('PVAD_API_KEY', { PVAD_API_KEY: '' }), '');
});

test('normalization preserves canonical and neutral keys', () => {
  const source = { VITE_PVAD_DOCUMENTATION_URL: 'https://docs.example.test', PORT: '8080' };
  const result = normalizePvadEnvironment(source);
  assert.deepEqual(result, source);
  assert.notEqual(result, source);
  assert.throws(() => readPvadEnvironment('PATH', source), /Expected a PVAD/);
});

test('development API origins reject credentials and unsafe URLs', () => {
  assert.equal(resolvePvadDevApiTarget({}), 'http://localhost:5086');
  assert.equal(resolvePvadDevApiTarget({ PVAD_DEV_API_URL: 'http://localhost:15086/' }), 'http://localhost:15086');
  assert.equal(resolvePvadDevApiTarget({ PXA_DEV_API_URL: 'http://localhost:15086/' }), 'http://localhost:5086');
  for (const value of ['', 'file:///private', 'https://user:secret@example.test',
    'http://example.test/private', 'http://example.test/?secret=value', 'http://exam\nple.test']) {
    assert.throws(() => resolvePvadDevApiTarget({ PVAD_DEV_API_URL: value }),
      { message: 'Invalid configuration key: PVAD_DEV_API_URL.' });
  }
});
