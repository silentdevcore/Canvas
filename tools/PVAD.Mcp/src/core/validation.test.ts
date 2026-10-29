import assert from 'node:assert/strict';
import test from 'node:test';

import { PvadMcpContentRepository } from './content.js';
import { createSchemaRegistry, DesignValidator } from './validation.js';

const content = new PvadMcpContentRepository();
const validator = new DesignValidator(content.readJson('docs/schema/design-export.schema.json'));

test('canonical schemas and historical identities resolve offline with identical validation', () => {
  const registry = createSchemaRegistry(content);
  const manifest = content.readJson('docs/schema/schema-aliases.json') as {
    schemas: Array<{ canonicalId: string; path: string; legacyIds: string[] }>;
  };
  for (const entry of manifest.schemas) {
    const canonical = registry.getSchema(entry.canonicalId)!;
    assert.ok(canonical);
    for (const alias of entry.legacyIds) {
      const legacy = registry.getSchema(alias)!;
      assert.ok(legacy);
      for (const sample of [{}, null, { pages: [] }]) {
        assert.equal(legacy(sample), canonical(sample), alias);
      }
    }
    assert.doesNotThrow(() => new DesignValidator(content.readJson(entry.path), registry));
  }
});

test('canonical and legacy schema fragments resolve offline without changing validation', () => {
  const registry = createSchemaRegistry(content);
  const manifest = content.readJson('docs/schema/schema-aliases.json') as {
    schemas: Array<{ canonicalId: string; path: string; legacyIds: string[] }>;
  };
  for (const entry of manifest.schemas) {
    const schema = content.readJson(entry.path) as { $defs?: Record<string, unknown> };
    for (const name of Object.keys(schema.$defs ?? {})) {
      const fragment = `#/$defs/${name.replaceAll('~', '~0').replaceAll('/', '~1')}`;
      const canonical = registry.getSchema(entry.canonicalId + fragment);
      assert.ok(canonical, entry.canonicalId + fragment);
      for (const alias of entry.legacyIds) {
        const historical = registry.getSchema(alias + fragment);
        assert.ok(historical, alias + fragment);
        for (const sample of [{}, null, [], '', 1])
          assert.equal(historical(sample), canonical(sample), alias + fragment);
      }
    }
  }
});

test('native reader schema accepts each generation and rejects mixed identity tuples offline', () => {
  const registry = createSchemaRegistry(content);
  const validate = registry.getSchema('https://pvade.com/schemas/pvad-project-manifest-reader.schema.json')!;
  const common = { envelopeVersion: '1.0', documentSchemaVersion: '1.0', documentId: 'synthetic',
    title: 'Synthetic', createdAtUtc: '2026-01-01T00:00:00Z', modifiedAtUtc: '2026-01-01T00:00:00Z',
    locale: 'en', entryDocument: 'document.json', contentHash: 'a'.repeat(64), assets: [],
    entryChecksums: { mimetype: 'b'.repeat(64), 'document.json': 'a'.repeat(64) } };
  for (const [magic, formatId, mediaType, versionKey] of [
    ['PVAD-PVADP-1', 'pvadp', 'application/vnd.pvad.pvadp+zip', 'minimumPvadVersion'],
    ['PVAD-PVADS-1', 'pvads', 'application/vnd.pvad.pvads+zip', 'minimumPvadVersion'],
    ['PXA-PXAP-1', 'pxap', 'application/vnd.powerdox.pxap+zip', 'minimumPxaVersion'],
    ['PXA-PXAS-1', 'pxas', 'application/vnd.powerdox.pxas+zip', 'minimumPxaVersion'],
  ]) {
    const manifest = { ...common, magic, formatId, mediaType, [versionKey]: '1.1.0' };
    assert.equal(validate(manifest), true, `${formatId}: ${JSON.stringify(validate.errors)}`);
    assert.equal(validate({ ...manifest, mediaType: 'application/vnd.pvad.pvads+zip', magic: 'PXA-PXAP-1' }), false);
    assert.equal(validate({ ...manifest, formatId: 'unknown' }), false);
    assert.equal(validate({ ...manifest, contentHash: 'invalid' }), false);
  }
});

test('validates the complete canonical design schema', () => {
  const valid = validator.validateJson(JSON.stringify({
    id: 'document-1',
    name: 'Example',
    pages: [{
      id: 'page-1',
      elements: [{ id: 'text-1', type: 'text', x: 10, y: 10, width: 100, height: 20 }],
    }],
  }));
  assert.equal(valid.valid, true);

  const invalid = validator.validateJson(JSON.stringify({
    id: 'document-1',
    name: 'Example',
    pages: [{ id: 'page-1', elements: [{ id: 'bad', type: 'unknown' }] }],
  }));
  assert.equal(invalid.valid, false);
  assert.ok(invalid.diagnostics.some((diagnostic) => diagnostic.code === 'PXAMCP003'));
});

test('returns stable diagnostics for malformed and oversized JSON', () => {
  assert.equal(validator.validateJson('{').diagnostics[0].code, 'PXAMCP001');
  assert.equal(
    validator.validateJson(`"${'x'.repeat(10 * 1024 * 1024)}"`).diagnostics[0].code,
    'PXAMCP002',
  );
});
