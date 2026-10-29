import assert from 'node:assert/strict';
import path from 'node:path';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { trustBaselinePath } from './trust-baseline.mjs';

test('platform selection cannot overwrite or silently substitute the existing Mac references', () => {
  for (const name of ['visual-baseline', 'operation-visual-baseline']) {
    assert.equal(trustBaselinePath('/repo', name, 'darwin'), path.join('/repo', 'test-assets/pdf-trust', `${name}.json`));
    assert.equal(trustBaselinePath('/repo', name, 'linux'), path.join('/repo', 'test-assets/pdf-trust', `${name}.linux.json`));
  }
  assert.throws(() => trustBaselinePath('/repo', 'visual-baseline', 'win32'), /No reviewed/);
  assert.throws(() => trustBaselinePath('/repo', '../other', 'linux'), /Unknown/);
});

test('approved Linux operation references retain all service pairs, geometry and embedded-font hashes', () => {
  const read = name => JSON.parse(readFileSync(new URL(`../../test-assets/pdf-trust/${name}.json`, import.meta.url), 'utf8'));
  const original = read('operation-visual-baseline');
  const linux = read('operation-visual-baseline.linux');
  assert.equal(linux.review.status, 'approved');
  assert.equal(linux.renderer.platform, 'linux');
  assert.equal(linux.renderer.poppler, '24.02.0');
  assert.equal(linux.dpi, original.dpi);
  assert.deepEqual(linux.entries.map(entry => entry.id), original.entries.map(entry => entry.id));
  for (const [index, entry] of linux.entries.entries()) {
    for (const side of ['sourcePages', 'outputPages']) {
      assert.deepEqual(entry[side].map(({ page, width, height }) => ({ page, width, height })),
        original.entries[index][side].map(({ page, width, height }) => ({ page, width, height })));
      for (const page of entry[side]) assert.match(page.pngSha256, /^[a-f0-9]{64}$/);
      if (['pdf-a-2b', 'pdf-ua-1'].includes(entry.id)) {
        assert.deepEqual(entry[side], original.entries[index][side]);
      }
    }
  }
});

test('the owner-approved Linux corpus baseline preserves exact source hashes, dimensions and all four fixtures', () => {
  const read = name => JSON.parse(readFileSync(new URL(`../../test-assets/pdf-trust/${name}.json`, import.meta.url), 'utf8'));
  const original = read('visual-baseline');
  const linux = read('visual-baseline.linux');
  assert.equal(linux.review.status, 'approved');
  assert.equal(linux.renderer.platform, 'linux');
  assert.equal(linux.renderer.poppler, '24.02.0');
  assert.equal(linux.dpi, original.dpi);
  assert.deepEqual(linux.entries.map(entry => entry.id), original.entries.map(entry => entry.id));
  for (const [index, entry] of linux.entries.entries()) {
    assert.equal(entry.pdfSha256, original.entries[index].pdfSha256);
    assert.deepEqual(entry.pages.map(({ page, width, height }) => ({ page, width, height })),
      original.entries[index].pages.map(({ page, width, height }) => ({ page, width, height })));
    for (const page of entry.pages) assert.match(page.pngSha256, /^[a-f0-9]{64}$/);
  }
});
