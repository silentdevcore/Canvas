import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const main = readFileSync(new URL('../src/main.js', import.meta.url), 'utf8');
const api = readFileSync(new URL('../src/api.js', import.meta.url), 'utf8');

test('Admin exposes tenant-owned custom role lifecycle actions', () => {
  for (const action of ['createAdminRole', 'updateAdminRole', 'cloneAdminRole', 'setAdminRoleArchived']) {
    assert.match(api, new RegExp(`function ${action}\\b`));
    assert.match(main, new RegExp(`\\b${action}\\b`));
  }
  assert.match(main, /Create custom role/);
  assert.match(main, /Custom role definition/);
  assert.match(main, /Archive role/);
  assert.match(main, /Clone as custom role/);
});

test('permission editor keeps permissions separate from subscriptions', () => {
  assert.match(main, /name="permissions"/);
  assert.match(main, /Product entitlements remain separate/);
  assert.match(main, /active sessions so new claims take effect immediately/);
});
