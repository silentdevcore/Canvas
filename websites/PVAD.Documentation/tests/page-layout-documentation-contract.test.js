import assert from 'node:assert/strict';
import { access, readFile } from 'node:fs/promises';
import test from 'node:test';

const source = await readFile(new URL('../src/main.js', import.meta.url), 'utf8');

test('mixed per-page layouts are a focused Editor documentation topic', () => {
  assert.match(source, /title: 'Page Layout'/);
  assert.match(source, /This page override/);
  assert.match(source, /Apply one layout to every page/);
  assert.match(source, /Reset an override/);
  assert.match(source, /Portrait, landscape, and custom pages/);
});

test('mixed-layout screenshot is synthetic, local, and accessible', async () => {
  await access(new URL('../public/images/designer/mixed-page-layout.png', import.meta.url));
  assert.match(source, /\/images\/designer\/mixed-page-layout\.png/);
  assert.match(source, /alt: 'PVADE Designer preview showing portrait, landscape, and custom-sized pages in one document'/);
  assert.doesNotMatch(source, /m\.barrat|outlook\.com/i);
});
