import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import test from 'node:test';

const read = (path) => JSON.parse(readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8'));

test('schema identities are unique and point to bundled canonical schemas', () => {
  const manifest = read('docs/schema/schema-aliases.json');
  assert.equal(manifest.schemaVersion, 1);
  const identities = new Set();
  for (const entry of manifest.schemas) {
    assert.equal(read(entry.path).$id, entry.canonicalId);
    for (const id of [entry.canonicalId, ...entry.legacyIds]) {
      assert.equal(identities.has(id), false, id);
      identities.add(id);
    }
  }
});

test('every maintained schema is registered under the canonical domain without former aliases', () => {
  const manifest = read('docs/schema/schema-aliases.json');
  const paths = new Set(manifest.schemas.map(entry => entry.path));
  function visit(directory) {
    for (const entry of readdirSync(new URL(`../../${directory}/`, import.meta.url), { withFileTypes: true })) {
      const path = `${directory}/${entry.name}`;
      if (entry.isDirectory()) visit(path);
      else if (entry.name.endsWith('.schema.json')) {
        assert.ok(paths.has(path), `Missing schema registry entry: ${path}`);
        assert.doesNotMatch(entry.name, /pxa/i);
      }
    }
  }
  for (const directory of ['docs/schema', 'product-metadata', 'tools/pdfua']) visit(directory);
  for (const entry of manifest.schemas) {
    assert.ok(entry.canonicalId.startsWith('https://pvade.com/schemas/'), entry.path);
    assert.deepEqual(entry.legacyIds, [], entry.path);
    assert.doesNotMatch(readFileSync(new URL(`../../${entry.path}`, import.meta.url), 'utf8'), /pxa|powerdox/i, entry.path);
  }
});

test('committed OpenAPI advertises only canonical branded routes', () => {
  const document = read('docs/schema/openapi.json');
  assert.ok(document.paths['/api/pvad/v1/version']);
  assert.ok(document.paths['/api/pvad/spreadsheet/export']);
  assert.ok(document.paths['/api/pvad/templates/render-design']);
  assert.ok(Object.keys(document.paths).every((path) => !path.startsWith('/api/pxa/')));
  assert.deepEqual(document.servers, [{ url: '/' }]);
  const descriptions = Object.values(document.paths).flatMap(path =>
    Object.values(path).filter(operation => operation && typeof operation === 'object')
      .map(operation => operation.description ?? ''));
  assert.ok(descriptions.some(description => description.includes('PVAD business validation')));
  assert.ok(descriptions.every(description => !/\bPXA\b/.test(description)));
});

test('SDK migration path and native downloads match the generated API contract', () => {
  const document = read('docs/schema/openapi.json');
  assert.ok(document.paths['/api/pvad/migration/frameworks']?.get);
  for (const [product, format] of [['pdf', 'pvadp'], ['spreadsheet', 'pvads']]) {
    const response = document.paths[`/api/pvad/v1/designer/projects/${product}/export`].post.responses['200'];
    assert.equal(response.content[`application/vnd.pvad.${format}+zip`].schema.format, 'binary');
    assert.ok(response.headers['Content-Disposition']);
  }
});

test('the native manifest schema accepts only the current envelope identities', () => {
  const current = read('docs/schema/pvad-project-manifest.schema.json');
  assert.deepEqual(current.properties.magic.enum, ['PVAD-PVADP-1', 'PVAD-PVADS-1']);
  assert.ok(current.required.includes('minimumPvadVersion'));
  assert.equal(current.allOf[0].oneOf.length, 2);
  const reader = read('docs/schema/pvad-project-manifest-reader.schema.json');
  assert.equal(reader.$ref, current.$id);
});

test('published former reference archives retain their original bytes', () => {
  for (const [name, expected] of [
    ['minimal.pxap', '1913423e4ad0fda76b5bb4ea0e0fb4ae07b21e732b70bf8a39fd7694ab4b482e'],
    ['minimal.pxas', 'fdf248da92abe926ec3e81f957d657ddfd633dc47120afdc1e7e02d2e6c466e6'],
  ]) {
    const bytes = readFileSync(new URL(`../../docs/fixtures/native-projects/${name}`, import.meta.url));
    assert.equal(createHash('sha256').update(bytes).digest('hex'), expected);
  }
});
