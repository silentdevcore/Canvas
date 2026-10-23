import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import test from 'node:test';

const read = (path) => JSON.parse(readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8'));

test('schema alias identities are unique and point to bundled canonical schemas', () => {
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

test('every maintained schema has a canonical domain and its previous identity is an offline alias', () => {
  const manifest = read('docs/schema/schema-aliases.json');
  const paths = new Set(manifest.schemas.map(entry => entry.path));
  const historical = new Set(['docs/schema/pxa-workbook.schema.json', 'docs/schema/pxa-code-workspace.schema.json']);
  function visit(directory) {
    for (const entry of readdirSync(new URL(`../../${directory}/`, import.meta.url), { withFileTypes: true })) {
      const path = `${directory}/${entry.name}`;
      if (entry.isDirectory()) visit(path);
      else if (entry.name.endsWith('.schema.json'))
        assert.ok(paths.has(path) || historical.has(path), `Missing schema registry entry: ${path}`);
    }
  }
  for (const directory of ['docs/schema', 'product-metadata', 'tools/pdfua']) visit(directory);
  for (const entry of manifest.schemas) {
    if (entry.path === 'docs/schema/pxa-project-manifest.schema.json') continue;
    assert.ok(entry.canonicalId.startsWith('https://pvade.com/schemas/'), entry.path);
    assert.ok(entry.legacyIds.includes(entry.canonicalId.replace('https://pvade.com/', 'https://powerdoxautomation.com/')), entry.path);
  }
});

test('schema domain migration preserves maintained constraints and legacy schema bytes', () => {
  // Checkpoint before the ID migration; only root IDs and current-contract refs change.
  const constraints = {
    'docs/schema/pvad-project-manifest-reader.schema.json': '4a3a0dc93a1f981d4941142f64a28c4e219f2f836ad9c89d6047edf10c3efbae',
    'docs/schema/design-export.schema.json': 'b3ec8bd61fe0ebf9330b17716b202dfad3496a3f53923dc76781e3ca5a98d2f4',
    'docs/schema/pvad-workbook.schema.json': '46ed22a246c4c11643b438eeec51b524da8a272b2fcedf09b428c4404ec736c8',
    'docs/schema/pvad-project-manifest.schema.json': '1336faaf33ff8a194b743b3f4551711581219cf190914337cb46ca4eae48da95',
    'docs/schema/pvad-code-workspace.schema.json': 'dd88c8cb8fa9012c98c221c00c90e0ac0d16f5c35d67056d055b592638793d7d',
    'product-metadata/browser-storage.schema.json': '738fe2712adf0b62ce2a177083499ee71bcc82baeb80328fd9d5ecdfbb7b2ae8',
    'product-metadata/country-readiness.schema.json': '939507257f30e6be151eb07cf479eb9a70f0bcf48a2d00fd837c0cde15de8057',
    'product-metadata/data-processing-inventory.schema.json': '558ed3d52a3a5f73651720715408168c9c826eb0f53f8aaf3db3582bc446632d',
    'product-metadata/global-commerce-catalog.schema.json': '815cc6b939b368ea057a6458b9b99580d27939400858b439454932e880dfdd42',
    'product-metadata/release-fragment.schema.json': '1afbe5e3e7411ce49b4568cdf6d2f9488402d8345da88944f218d6697ba3b390',
    'product-metadata/document-fidelity/manifest.schema.json': 'a2d60fcc3bfc7dd041ea337bc27ec0b35e43a1627741e3a1ac73a472fd281036',
    'product-metadata/document-fidelity/result.schema.json': '2af0f26fc8fda0f5d75c4ae07602315a5fd0f03c291d2f0852c123b8c8f9f128',
    'tools/pdfua/matterhorn-evidence.schema.json': '0f801bb7ee59b14e9dd73212c920cb900cefec5b11b91debfbfa6a22adf89844',
  };
  for (const [path, expected] of Object.entries(constraints)) {
    const { $id, ...schema } = read(path);
    const normalized = JSON.stringify(schema, (key, value) => key === '$ref' && typeof value === 'string'
      ? value.replace('https://pvade.com/', 'https://powerdoxautomation.com/') : value);
    assert.equal(createHash('sha256').update(normalized).digest('hex'), expected, path);
  }
  for (const [path, expected] of Object.entries({
    'docs/schema/pxa-workbook.schema.json': '21d91af37d2c2f9479090bff96e1614b8cf3b4ed6e412b2ae1371c6c3a5930cc',
    'docs/schema/pxa-code-workspace.schema.json': 'ed8d29c8a959e10902ecded4965b4ee64e50d493914fad39704051e705fbc73b',
    'docs/schema/pxa-project-manifest.schema.json': '61bfef23ef5d20104ac29269716eac32d08e283788089bca0b07afbb5e4ff97d',
  })) {
    const bytes = readFileSync(new URL(`../../${path}`, import.meta.url));
    assert.equal(createHash('sha256').update(bytes).digest('hex'), expected, path);
  }
});

test('canonical workbook preserves historical structural constraints', () => {
  const constraints = schema => Array.isArray(schema) ? schema.map(constraints)
    : schema && typeof schema === 'object' ? Object.fromEntries(Object.entries(schema)
      .filter(([key, value]) => !(typeof value === 'string' && ['$id', 'title', 'description'].includes(key)))
      .map(([key, value]) => [key, constraints(value)])) : schema;
  assert.deepEqual(constraints(read('docs/schema/pvad-workbook.schema.json')),
    constraints(read('docs/schema/pxa-workbook.schema.json')));
});

test('committed OpenAPI advertises canonical branded routes without legacy duplicates', () => {
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
  assert.ok(descriptions.every(description => !/\bPXA business validation\b/.test(description)));
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

test('native schemas keep the two envelope generations explicit', () => {
  const current = read('docs/schema/pvad-project-manifest.schema.json');
  const legacy = read('docs/schema/pxa-project-manifest.schema.json');
  assert.deepEqual(current.properties.magic.enum, ['PVAD-PVADP-1', 'PVAD-PVADS-1']);
  assert.deepEqual(legacy.properties.magic.enum, ['PXA-PXAP-1', 'PXA-PXAS-1']);
  assert.ok(current.required.includes('minimumPvadVersion'));
  assert.ok(legacy.required.includes('minimumPxaVersion'));
  assert.equal(current.allOf[0].oneOf.length, 2);
});

test('published legacy fixtures retain their original bytes', () => {
  for (const [name, expected] of [
    ['minimal.pxap', '1913423e4ad0fda76b5bb4ea0e0fb4ae07b21e732b70bf8a39fd7694ab4b482e'],
    ['minimal.pxas', 'fdf248da92abe926ec3e81f957d657ddfd633dc47120afdc1e7e02d2e6c466e6'],
  ]) {
    const bytes = readFileSync(new URL(`../../docs/fixtures/native-projects/${name}`, import.meta.url));
    assert.equal(createHash('sha256').update(bytes).digest('hex'), expected);
  }
});
