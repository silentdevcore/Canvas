import test from 'node:test';
import assert from 'node:assert/strict';
import { resolveSiteLinks, siteLinkDefaults } from './siteLinks.js';

test('each website URL accepts canonical and legacy keys in both modes', () => {
  for (const name of Object.keys(siteLinkDefaults.production)) {
    for (const prefix of ['VITE_PVAD_', 'VITE_PXA_']) {
      for (const mode of ['development', 'production']) {
        const result = resolveSiteLinks(mode, { [`${prefix}${name.toUpperCase()}_URL`]: 'https://sites.example/base' });
        assert.equal(result[name], 'https://sites.example/base/');
      }
    }
  }
});

test('defaults remain stable and aliases must agree without exposing values', () => {
  assert.deepEqual(resolveSiteLinks('production'), siteLinkDefaults.production);
  assert.deepEqual(resolveSiteLinks('development'), siteLinkDefaults.local);
  assert.equal(resolveSiteLinks('production', { VITE_PVAD_COMPANY_URL: 'https://same.example/', VITE_PXA_COMPANY_URL: 'https://same.example/' }).company, 'https://same.example/');
  assert.throws(() => resolveSiteLinks('production', { VITE_PVAD_COMPANY_URL: 'https://private-new.example/', VITE_PXA_COMPANY_URL: 'https://private-old.example/' }), error => {
    assert.match(error.message, /VITE_PVAD_COMPANY_URL/);
    assert.doesNotMatch(error.message, /private|https:/);
    return true;
  });
});

test('production defaults use the selected domain while local ports remain unchanged', () => {
  assert.deepEqual(resolveSiteLinks('production'), {
    company: 'https://pvade.com/',
    documentation: 'https://docs.pvade.com/',
    demo: 'https://demos.pvade.com/',
    designer: 'https://designer.pvade.com/',
    account: 'https://account.pvade.com/',
    operator: 'https://operator.pvade.com/',
  });
  assert.deepEqual(resolveSiteLinks('development'), {
    company: 'http://localhost:5173/',
    documentation: 'http://localhost:5174/',
    demo: 'http://localhost:5175/',
    designer: 'http://localhost:5176/',
    account: 'http://localhost:5178/',
    operator: 'http://localhost:3001/',
  });
});

test('invalid website URL values never become links or appear in errors', () => {
  for (const value of ['javascript:alert(1)', 'https://user:secret@example.test/', '/relative', 'https://example.test/?secret=value']) {
    assert.throws(() => resolveSiteLinks('production', { VITE_PVAD_ACCOUNT_URL: value }), error => {
      assert.equal(error.message, 'Invalid website URL configuration key: VITE_PVAD_ACCOUNT_URL.');
      return true;
    });
  }
});

test('ambiguous browser URL normalization cannot silently change configured hosts or paths', () => {
  for (const value of ['https://example.test\\other', 'https://exam\nple.test/',
    'https://example.test/base path', ' https://example.test/']) {
    assert.throws(() => resolveSiteLinks('production', { VITE_PVAD_ACCOUNT_URL: value }),
      { message: 'Invalid website URL configuration key: VITE_PVAD_ACCOUNT_URL.' });
  }
  assert.equal(resolveSiteLinks('production', {
    VITE_PVAD_ACCOUNT_URL: 'https://accounts.example/customer%20workspace',
  }).account, 'https://accounts.example/customer%20workspace/');
});
