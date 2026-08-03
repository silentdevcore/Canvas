import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [main, page, api] = await Promise.all([
  readFile(new URL('../src/main.ts', import.meta.url), 'utf8'),
  readFile(new URL('../src/pages/legalReview.ts', import.meta.url), 'utf8'),
  readFile(new URL('../src/api.ts', import.meta.url), 'utf8'),
]);

test('signed-in users are gated by exact current legal obligations', () => {
  assert.match(main, /requiresLegalReview/);
  assert.match(main, /navigate\('\/legal-review', true\)/);
  assert.match(page, /currentTermsVersionId/);
  assert.match(page, /currentPrivacyVersionId/);
  assert.match(api, /termsVersionId, privacyVersionId/);
});

test('Terms acceptance and Privacy acknowledgement use distinct language', () => {
  assert.match(page, /I accept the current Terms and Conditions/);
  assert.match(page, /I acknowledge that I have received the current Privacy Notice/);
  assert.match(page, /This acknowledgement is not consent to marketing/);
});

test('unavailable or changed policies fail closed', () => {
  assert.match(page, /Legal documents unavailable/);
  assert.match(page, /apiError\.status === 409/);
  assert.match(page, /Reload and review the current versions/);
});

test('legal review exposes accessible consent and failure states', () => {
  assert.match(page, /aria-describedby="legal-review-description"/);
  assert.match(page, /role="alert" aria-live="assertive"/);
  assert.match(page, /type="checkbox" required/);
  assert.match(page, /target="_blank" rel="noopener"/);
  assert.match(page, /button\.setAttribute\('aria-busy', 'true'\)/);
  assert.match(page, /button\.removeAttribute\('aria-busy'\)/);
  assert.match(page, /legal-review-retry/);
});
