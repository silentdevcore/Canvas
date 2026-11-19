import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { test } from 'node:test';
import { readFileSync } from 'node:fs';
import { checkRepository, validateCodeMigrationEntries, validateReportMigrationEntries, validateFileImporterEntries, validateDependencies, validateRelease, validateTrust } from './pvad-native-licensing.mjs';

test('repository metadata and operation directories are consistent', () => {
  assert.equal(checkRepository().products, 10);
  assert.equal(checkRepository().enforcementStatus, 'all-native-entry-points');
});

test('release date and version cannot drift or be replaced by build metadata', () => {
  const releases = { releases: [{ version: '1.2.3', publishedAt: '2026-08-03' }] };
  validateRelease('1.2.3', releases, { version: '1.2.3', publishedAt: '2026-08-03' });
  assert.throws(() => validateRelease('1.2.4', releases, { version: '1.2.4', publishedAt: '2026-08-03' }));
  assert.throws(() => validateRelease('1.2.3', releases, { version: '1.2.3', publishedAt: '2026-11-15' }));
  assert.throws(() => validateRelease('1.2.3', releases, { version: '2.0.0', publishedAt: '2026-08-03' }));
});

test('all code providers have canonical source-level guards and unimplemented migration kinds stay explicit', () => {
  const inventory = JSON.parse(readFileSync(new URL('../../product-metadata/native-code-migration-entries.v1.json', import.meta.url)));
  validateCodeMigrationEntries(inventory);
  for (const patch of [{ product: 'migration.pdf.viewer' }, { provider: 'auto' }, { provider: 'Aspose' }, { source: '../private' }, { type: 'MissingProvider' }, { bypass: true }]) {
    const invalid = structuredClone(inventory); Object.assign(invalid.entries[0], patch);
    assert.throws(() => validateCodeMigrationEntries(invalid));
  }
  const duplicate = structuredClone(inventory); duplicate.entries[1] = duplicate.entries[0];
  assert.throws(() => validateCodeMigrationEntries(duplicate));
  const matrix = JSON.parse(readFileSync(new URL('../../product-metadata/native-library-products.v1.json', import.meta.url)));
  for (const product of matrix.products.filter(product => product.id.startsWith('migration.')))
    assert.equal(product.enforced, true);
  for (const id of ['migration.pdf.code', 'migration.spreadsheet.code'])
    assert.equal(matrix.products.find(product => product.id === id).coverageStatus, 'provider-analysis-and-built-in-conversion');
});

test('all report native converter and wrapper entries match fixed provider admission', () => {
  const inventory = JSON.parse(readFileSync(new URL('../../product-metadata/native-report-migration-entries.v1.json', import.meta.url)));
  validateReportMigrationEntries(inventory);
  for (const patch of [{ product: 'migration.pdf.designer' }, { provider: 'auto' }, { source: '../private' }, { publicConverterOverloads: 99 }, { bypass: true }]) {
    const invalid = structuredClone(inventory); Object.assign(invalid.entries[0], patch);
    assert.throws(() => validateReportMigrationEntries(invalid));
  }
  const invalid = structuredClone(inventory); invalid.entries[1] = invalid.entries[0];
  assert.throws(() => validateReportMigrationEntries(invalid));
});

test('file importer inventory covers all registry types without claiming complete package acceptance', () => {
  const inventory = JSON.parse(readFileSync(new URL('../../product-metadata/native-file-importer-entries.v1.json', import.meta.url)));
  validateFileImporterEntries(inventory);
  for (const patch of [{ key: 'unknown' }, { type: 'MissingImporter' }, { publicProcessingOverloads: 3 }, { bypass: true }]) {
    const invalid = structuredClone(inventory); Object.assign(invalid.entries[0], patch);
    assert.throws(() => validateFileImporterEntries(invalid));
  }
  const duplicate = structuredClone(inventory); duplicate.entries[1] = duplicate.entries[0];
  assert.throws(() => validateFileImporterEntries(duplicate));
  const missing = structuredClone(inventory); missing.entries.pop();
  assert.throws(() => validateFileImporterEntries(missing));
  const matrix = JSON.parse(readFileSync(new URL('../../product-metadata/native-library-products.v1.json', import.meta.url)));
  const importer = matrix.products.find(product => product.id === 'importer');
  assert.equal(importer.enforced, true);
  assert.ok(importer.acceptanceStatus.startsWith('partial-'));
});

test('empty production trust is allowed for development but not release readiness', () => {
  validateTrust({ schemaVersion: 1, keys: [] });
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [] }, { requireKeys: true }));
});

test('only explicit public P-256 keys are valid trust anchors', () => {
  const { publicKey, privateKey } = generateKeyPairSync('ec', { namedCurve: 'prime256v1',
    publicKeyEncoding: { type: 'spki', format: 'pem' }, privateKeyEncoding: { type: 'pkcs8', format: 'pem' } });
  const key = { keyId: 'synthetic-key', publicKeyPem: publicKey };
  validateTrust({ schemaVersion: 1, keys: [key] }, { requireKeys: true });
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [key, key] }));
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [{ ...key, publicKeyPem: privateKey }] }));
  assert.throws(() => validateTrust({ schemaVersion: 2, keys: [key] }));
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [{ ...key, privateKey }] }));
  const wrong = generateKeyPairSync('ec', { namedCurve: 'secp384r1', publicKeyEncoding: { type: 'spki', format: 'pem' } });
  assert.throws(() => validateTrust({ schemaVersion: 1, keys: [{ ...key, publicKeyPem: wrong.publicKey }] }));
});

test('dependency inventory distinguishes implemented product boundaries from future integrations', () => {
  const matrix = JSON.parse(readFileSync(new URL('../../product-metadata/native-library-products.v1.json', import.meta.url)));
  const inventory = JSON.parse(readFileSync(new URL('../../product-metadata/native-library-dependencies.v1.json', import.meta.url)));
  const products = matrix.products.map(product => product.id);
  validateDependencies(inventory, products);
  const rules = new Map(inventory.rules.map(rule => [rule.id, rule]));
  for (const id of ['word-export', 'word-signing', 'spreadsheet-export-calculate-save', 'pdf-serialization'])
    assert.deepEqual(rules.get(id).additionalGrants, []);
  assert.deepEqual(rules.get('importer-configured-generator').additionalGrants, ['pdf']);
  assert.equal(rules.get('converter-exports').status, 'implemented');
  assert.deepEqual(rules.get('converter-exports').additionalGrants, []);
  assert.equal(rules.get('report-designer-migration').status, 'implemented');
  for (const id of ['pdf-viewer-migration', 'pdf-designer-migration', 'nutrient-designer-source-pdf-import', 'nutrient-designer-viewer-mapping']) {
    assert.equal(rules.get(id).status, 'implemented');
    assert.deepEqual(rules.get(id).additionalGrants, []);
    assert.equal(rules.get(id).privateDependency, null);
  }
  assert.equal(rules.get('spreadsheet-subsequent-pdf-render').product, 'pdf');
  for (const patch of [{ product: 'unknown' }, { boundary: 'ambient-unrestricted' }, { status: 'assumed' },
    { source: '../private' }, { privateDependency: 'UniversalPdf' }, { additionalGrants: ['unknown'] }, { bypass: true }]) {
    const invalid = structuredClone(inventory); Object.assign(invalid.rules[0], patch);
    assert.throws(() => validateDependencies(invalid, products));
  }
  const duplicate = structuredClone(inventory); duplicate.rules.push(duplicate.rules[0]);
  assert.throws(() => validateDependencies(duplicate, products));
});
