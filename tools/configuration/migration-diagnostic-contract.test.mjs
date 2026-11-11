import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../..', import.meta.url));
const baseline = JSON.parse(readFileSync(resolve(root, 'product-metadata/legacy-migration-diagnostics.json'), 'utf8'));
function sourceFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    if (['bin', 'obj', 'node_modules', '.git'].includes(entry.name)) return [];
    const path = resolve(directory, entry.name);
    return entry.isDirectory() ? sourceFiles(path) : entry.name.endsWith('.cs') ? [path] : [];
  });
}

test('historical migration diagnostics remain source contracts, not product branding', () => {
  assert.equal(baseline.schemaVersion, 1);
  assert.equal(baseline.baselineCommit, 'fce423cf8c5f5e71eff6178f712fe4e5eb5af69a');
  assert.equal(baseline.identifiers.length, 432);
  assert.equal(new Set(baseline.identifiers).size, baseline.identifiers.length);
  const current = new Set(['src', 'PVAD.WebApi'].flatMap(directory =>
    sourceFiles(resolve(root, directory))).flatMap(path => readFileSync(path, 'utf8').match(/\bCANMIG[A-Z]+[0-9]{3}\b/g) ?? []));
  const missing = baseline.identifiers.filter(identifier => !current.has(identifier));
  assert.deepEqual(missing, [], `Historical diagnostic IDs disappeared: ${missing.join(', ')}`);
});

test('production sources issue no diagnostic or configuration identifier with the former prefix', () => {
  // Applied EF migrations are history; the data migration maps former values to current ones.
  const current = ['src', 'PVAD.WebApi'].flatMap(directory => sourceFiles(resolve(root, directory)))
    .filter(path => !/[\\/]Migrations[\\/]20\d{12}_/.test(path))
    .flatMap(path => (readFileSync(path, 'utf8').match(/\b(?:PXA[A-Z]+[0-9]{3}|PXA[0-9]{4}|PXA_[A-Z_]+|PXA-[A-Z]+-[0-9]{3})\b/g) ?? [])
      .map(code => `${path.slice(root.length)}: ${code}`));
  assert.deepEqual(current, []);
  assert.match(readFileSync(resolve(root, 'src/Generation/PVAD.Pdf/PdfDocument.cs'), 'utf8'),
    /DiagnosticId\s*=\s*"PVAD0001"/);
});
