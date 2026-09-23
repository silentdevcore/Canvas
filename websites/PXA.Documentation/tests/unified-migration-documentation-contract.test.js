import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const source = readFileSync(new URL('../src/main.js', import.meta.url), 'utf8');

test('migration documentation separates product domains and uses canonical routes', () => {
  assert.match(source, /PDF Code Migration/);
  assert.match(source, /Nutrient Viewer to Designer Migration/);
  assert.match(source, /Spreadsheet Code Migration/);
  assert.match(source, /Report Designer Migration/);
  assert.match(source, /GET \/api\/pxa\/v1\/migrations\/catalog/);
  assert.match(source, /POST \/api\/pxa\/v1\/migrations\/\{domain\}\/\{kind\}\/\{provider\}\/convert/);
  assert.match(source, /pdf\/migrations\/designer/);
  assert.match(source, /pdf\/designer\/nutrient-viewer\/convert/);
  assert.match(source, /technical <code>report\/designer<\/code> API domain/);
});

test('legacy migration routes are documented only as compatibility routes', () => {
  assert.match(source, /Compatibility routes/);
  assert.match(source, /\/api\/migration\/report-to-design/);
  assert.match(source, /one major release/);
});
