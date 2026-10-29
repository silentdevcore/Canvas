import assert from 'node:assert/strict';
import test from 'node:test';
import { canonicalScopes, compatibleScopes, hasCompatibleScope } from './scopeCompatibility.js';

test('only the three known read-only capabilities receive legacy aliases', () => {
  assert.deepEqual(canonicalScopes(['pxa.templates.read', 'pvad.templates.read']), ['pvad.templates.read']);
  assert.ok(hasCompatibleScope(['pvad.templates.read'], 'pxa.templates.read'));
  assert.ok(hasCompatibleScope(['pxa.templates.read'], 'pvad.templates.read'));
  assert.ok(!hasCompatibleScope(['pxa.unknown'], 'pvad.unknown'));
  assert.deepEqual(compatibleScopes(['pvad.documents.write']), ['pvad.documents.write']);
});
