import assert from 'node:assert/strict';
import test from 'node:test';
import { validateLegalContent } from './validate-legal-content.mjs';

test('Legal candidates are complete, English-authoritative, and Swiss-law based', () => {
  const result = validateLegalContent();

  assert.deepEqual(result.errors, []);
  assert.equal(result.manifest.authoritativeLocale, 'en');
  assert.equal(result.manifest.governingLaw, 'Switzerland');
  assert.equal(result.manifest.documents.length, 7);
});
