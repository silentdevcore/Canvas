import test from 'node:test';
import assert from 'node:assert/strict';
import {
  classifyBrowserApiOutcome,
  normalizeBrowserRoute,
} from './browserTelemetry.js';

test('normalizes routes to bounded application groups without query or identifiers', () => {
  assert.equal(normalizeBrowserRoute('company', '/products/generator?campaign=secret'), 'products');
  assert.equal(normalizeBrowserRoute('account', '/profile#security'), 'profile');
  assert.equal(normalizeBrowserRoute('admin', '/users/8f350da9-15a0-4d90-b3ba-1ee44234f402'), 'users');
  assert.equal(normalizeBrowserRoute('designer', '/create/document-123'), 'designer');
  assert.equal(normalizeBrowserRoute('company', '/customer/private-document'), 'other');
});

test('classifies API statuses without exposing endpoint or response details', () => {
  assert.equal(classifyBrowserApiOutcome(401), 'unauthorized');
  assert.equal(classifyBrowserApiOutcome(403), 'forbidden');
  assert.equal(classifyBrowserApiOutcome(429), 'rate_limited');
  assert.equal(classifyBrowserApiOutcome(500), 'server_error');
  assert.equal(classifyBrowserApiOutcome(422), 'client_error');
  assert.equal(classifyBrowserApiOutcome(204), 'completed');
});
