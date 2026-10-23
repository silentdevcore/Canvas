import assert from 'node:assert/strict';
import test from 'node:test';
import { normalizePvadEnvironment, readPvadEnvironment, resolvePvadDevApiTarget } from './environment.mjs';

test('canonical and legacy environment keys resolve without changing input', () => {
  for (const environment of [
    { PVAD_BUILD_COMMIT: 'example' },
    { PXA_BUILD_COMMIT: 'example' },
    { PVAD_BUILD_COMMIT: 'example', PXA_BUILD_COMMIT: 'example' },
  ]) {
    const before = { ...environment };
    assert.equal(readPvadEnvironment('PVAD_BUILD_COMMIT', environment), 'example');
    assert.deepEqual(environment, before);
  }
  assert.equal(readPvadEnvironment('PVAD_BUILD_COMMIT', {}), undefined);
});

test('conflicting configuration fails without disclosing values', () => {
  assert.throws(() => readPvadEnvironment('PVAD_API_KEY', {
    PVAD_API_KEY: 'new-secret', PXA_API_KEY: 'old-secret',
  }), error => {
    assert.match(error.message, /PVAD_API_KEY and PXA_API_KEY/);
    assert.doesNotMatch(error.message, /new-secret|old-secret/);
    return true;
  });
});

test('explicit empty canonical settings cannot silently fall back', () => {
  assert.equal(readPvadEnvironment('PVAD_API_KEY', { PVAD_API_KEY: '' }), '');
  assert.throws(() => readPvadEnvironment('PVAD_API_KEY', {
    PVAD_API_KEY: '', PXA_API_KEY: 'old-secret',
  }), /Conflicting/);
});

test('normalization supports browser-build aliases and preserves neutral keys', () => {
  const source = { VITE_PXA_DOCUMENTATION_URL: 'https://docs.example.test', PORT: '8080' };
  const result = normalizePvadEnvironment(source);
  assert.equal(result.VITE_PVAD_DOCUMENTATION_URL, source.VITE_PXA_DOCUMENTATION_URL);
  assert.equal(result.PORT, '8080');
  assert.equal(source.VITE_PVAD_DOCUMENTATION_URL, undefined);
  assert.throws(() => readPvadEnvironment('PATH', source), /Expected a PVAD/);
});

test('development API origins support both names without allowing credentials or unsafe URLs', () => {
  assert.equal(resolvePvadDevApiTarget({}), 'http://localhost:5086');
  for (const key of ['PVAD_DEV_API_URL', 'PXA_DEV_API_URL']) {
    assert.equal(resolvePvadDevApiTarget({ [key]: 'http://localhost:15086/' }), 'http://localhost:15086');
    for (const value of ['', 'file:///private', 'https://user:secret@example.test',
      'http://example.test/private', 'http://example.test/?secret=value', 'http://exam\nple.test']) {
      assert.throws(() => resolvePvadDevApiTarget({ [key]: value }),
        { message: 'Invalid configuration key: PVAD_DEV_API_URL.' });
    }
  }
  assert.throws(() => resolvePvadDevApiTarget({ PVAD_DEV_API_URL: 'http://new.test', PXA_DEV_API_URL: 'http://old.test' }), /Conflicting/);
});
