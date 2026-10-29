import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import test from 'node:test';

// Every src/pages/*.ts module renders directly into <main class="account-content">
// (see shell.ts / main.ts's portalPages dispatch). Several of them call
// fetch-backed API functions as a side effect of rendering (see the
// module-level `state`/loadX() pattern in organization.ts, profile.ts,
// dashboard.ts, etc.), so actually importing and invoking their exported
// page functions here would trigger real network calls with no server
// present. This mirrors accessibility-contract.test.ts's own technique
// (regex against raw source text, no execution) instead, extended to every
// page module rather than just shell.ts/main.ts.
const pagesDir = new URL('../src/pages/', import.meta.url);
const files = (await readdir(pagesDir)).filter((name) => name.endsWith('.ts'));
const sources = new Map(
  await Promise.all(files.map(async (name) => [name, await readFile(new URL(name, pagesDir), 'utf8')] as const)),
);

test('no page module hard-codes a positive tabindex', () => {
  for (const [name, source] of sources) {
    assert.doesNotMatch(source, /tabindex="[1-9]/, `${name} should not set a positive tabindex`);
  }
});

test('every <input> in a page module is wrapped by a nearby <label>', () => {
  for (const [name, source] of sources) {
    for (const match of source.matchAll(/<input/g)) {
      const windowStart = Math.max(0, match.index - 150);
      const preceding = source.slice(windowStart, match.index);
      assert.match(preceding, /<label/, `${name} has an <input> at index ${match.index} with no nearby <label>`);
    }
  }
});

test('every account-form-error region is announced via role="alert"', () => {
  for (const [name, source] of sources) {
    for (const match of source.matchAll(/<div class="account-form-error"[^>]*>/g)) {
      assert.match(match[0], /role="alert"/, `${name} has an account-form-error div without role="alert"`);
    }
  }
});
