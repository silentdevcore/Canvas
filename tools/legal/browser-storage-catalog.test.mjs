import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const inventory = JSON.parse(readFileSync(new URL('../../product-metadata/browser-storage.json', import.meta.url), 'utf8'));

test('browser storage inventory is complete, explicit, and contains no optional tracking', () => {
  assert.equal(inventory.optionalStorageEnabled, false);
  assert.ok(inventory.entries.length >= 10);
  for (const entry of inventory.entries) {
    assert.ok(entry.purpose.length >= 12);
    assert.ok(entry.lifetime.length >= 3);
    assert.equal(entry.optional, false);
    assert.doesNotMatch(entry.keys.join(' '), /canvas/i);
  }
});

test('source tree contains no unregistered browser storage access', () => {
  const validator = fileURLToPath(new URL('./validate-browser-storage.mjs', import.meta.url));
  const output = execFileSync(process.execPath, [validator], { encoding: 'utf8' });
  assert.match(output, /Validated \d+ browser-storage entries/);
});
