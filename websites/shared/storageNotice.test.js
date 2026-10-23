import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { initializeStorageNotice } from './storageNotice.js';

const require = createRequire(import.meta.url);
const { JSDOM } = require('../../pvad-designer/node_modules/jsdom');
function withPage(action) {
  const dom = new JSDOM('<!doctype html><html><body></body></html>', { url: 'https://account.example.test/' });
  const previous = { document: globalThis.document, location: globalThis.location };
  globalThis.document = dom.window.document;
  globalThis.location = dom.window.location;
  try { action(dom.window.document); }
  finally {
    for (const key of ['document', 'location']) {
      if (previous[key] === undefined) delete globalThis[key]; else globalThis[key] = previous[key];
    }
    dom.window.close();
  }
}
test('new acknowledgements write only the canonical cookie', () => withPage(document => {
  initializeStorageNotice();
  document.querySelector('[data-pvad-storage-understood]').click();
  assert.equal(document.cookie, 'PVAD.StorageNotice=2026-08');
  assert.equal(document.querySelector('[data-pvad-storage-notice]'), null);
}));
test('a legacy acknowledgement migrates without showing the notice again', () => withPage(document => {
  document.cookie = 'PXA.StorageNotice=2026-08; Path=/; Secure';
  initializeStorageNotice();
  assert.match(document.cookie, /PVAD\.StorageNotice=2026-08/);
  assert.equal(document.querySelector('[data-pvad-storage-notice]'), null);
}));
test('an explicit current-version mismatch cannot fall back to the old cookie', () => withPage(document => {
  document.cookie = 'PXA.StorageNotice=2026-08; Path=/; Secure';
  document.cookie = 'PVAD.StorageNotice=outdated; Path=/; Secure';
  initializeStorageNotice();
  assert.ok(document.querySelector('[data-pvad-storage-notice]'));
}));
