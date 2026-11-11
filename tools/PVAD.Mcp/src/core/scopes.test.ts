import assert from 'node:assert/strict';
import test from 'node:test';
import { hasScope, uniqueScopes } from './scopes.js';

test('scopes are matched exactly without former-name aliases', () => {
  assert.deepEqual(uniqueScopes(['pvad.templates.read', 'pvad.templates.read']), ['pvad.templates.read']);
  assert.ok(hasScope(['pvad.templates.read'], 'pvad.templates.read'));
  assert.ok(!hasScope(['pxa.templates.read'], 'pvad.templates.read'));
  assert.ok(!hasScope(['pvad.docs.read'], 'pvad.templates.read'));
});
