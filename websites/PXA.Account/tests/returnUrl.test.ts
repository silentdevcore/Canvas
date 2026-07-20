import assert from 'node:assert/strict';
import test from 'node:test';
import { sanitizeReturnUrl } from '../../shared/returnUrl.js';

test('accepts absolute URLs on allowlisted local product origins', () => {
  assert.equal(sanitizeReturnUrl('http://localhost:5176/templates/42'), 'http://localhost:5176/templates/42');
  assert.equal(sanitizeReturnUrl('http://localhost:5175/'), 'http://localhost:5175/');
  assert.equal(sanitizeReturnUrl('http://localhost:5174/getting-started'), 'http://localhost:5174/getting-started');
  assert.equal(sanitizeReturnUrl('http://localhost:5178/organization'), 'http://localhost:5178/organization');
  assert.equal(sanitizeReturnUrl('http://localhost:5173/pricing'), 'http://localhost:5173/pricing');
});

test('accepts absolute URLs on allowlisted production product origins', () => {
  assert.equal(
    sanitizeReturnUrl('https://designer.powerdoxautomation.com/doc/1'),
    'https://designer.powerdoxautomation.com/doc/1',
  );
  assert.equal(
    sanitizeReturnUrl('https://account.powerdoxautomation.com/profile'),
    'https://account.powerdoxautomation.com/profile',
  );
  assert.equal(
    sanitizeReturnUrl('https://powerdoxautomation.com/pricing'),
    'https://powerdoxautomation.com/pricing',
  );
});

test('rejects protocol-relative URLs', () => {
  assert.equal(sanitizeReturnUrl('//evil.com/phish'), null);
});

test('rejects external hosts even when they look like a PXA path', () => {
  assert.equal(sanitizeReturnUrl('https://evil.com/designer'), null);
  assert.equal(sanitizeReturnUrl('https://powerdoxautomation.com.evil.com/'), null);
});

test('rejects non-http(s) schemes', () => {
  assert.equal(sanitizeReturnUrl('javascript:alert(1)'), null);
  assert.equal(sanitizeReturnUrl('data:text/html,<script>alert(1)</script>'), null);
  assert.equal(sanitizeReturnUrl('mailto:someone@example.com'), null);
});

test('rejects relative paths (no scheme/host to validate against)', () => {
  assert.equal(sanitizeReturnUrl('/dashboard'), null);
  assert.equal(sanitizeReturnUrl('dashboard'), null);
});

test('rejects any Admin-shaped origin', () => {
  assert.equal(sanitizeReturnUrl('http://localhost:5177/'), null);
  assert.equal(sanitizeReturnUrl('https://admin.powerdoxautomation.com/'), null);
});

test('rejects empty, missing, or non-string values', () => {
  assert.equal(sanitizeReturnUrl(''), null);
  assert.equal(sanitizeReturnUrl(null), null);
  assert.equal(sanitizeReturnUrl(undefined), null);
  assert.equal(sanitizeReturnUrl('   '), null);
});
