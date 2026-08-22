import assert from 'node:assert/strict';
import { access, readFile } from 'node:fs/promises';
import test from 'node:test';

const source = await readFile(new URL('../src/main.js', import.meta.url), 'utf8');

test('element library is a focused Editor documentation topic', () => {
  assert.match(source, /title: 'Element Library'/);
  assert.match(source, /id="element-library-workflow"/);
  assert.match(source, /item\.title === 'Element Library' \? renderElementLibraryGuide\(\)/);
  assert.match(source, /Personal favorites/);
  assert.match(source, /Content Library versus Template Gallery/);
});

test('element and content categories are completely documented', () => {
  for (const category of [
    'Text and Data', 'Forms and Input', 'Tables and Charts', 'Media and Codes',
    'Shapes and Layout', 'Document and Navigation', 'Word Elements',
    'Forms', 'Invoices', 'Books', 'Certificates', 'Presentations', 'Reports',
  ]) {
    assert.match(source, new RegExp(`\\['${category}'`));
  }
  for (const concept of ['Blocks', 'Page kits', 'Safe insertion', 'fresh identifier', 'one undoable transaction']) {
    assert.match(source, new RegExp(concept, 'i'));
  }
});

test('synthetic Designer screenshots exist and are referenced with alt text', async () => {
  const assets = [
    'element-library-favorites.png',
    'content-library-preview.png',
  ];
  for (const asset of assets) {
    await access(new URL(`../public/images/designer/${asset}`, import.meta.url));
    assert.match(source, new RegExp(`/images/designer/${asset}`));
  }
  assert.match(source, /alt="PXA Designer Elements panel/);
  assert.match(source, /alt="PXA Designer Content Library preview/);
  assert.doesNotMatch(source, /m\.barrat|outlook\.com/i);
});
