import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const inventory = name => JSON.parse(readFileSync(new URL(`../../product-metadata/${name}`, import.meta.url)));

export function expectedMigrationCases(kind) {
  if (kind === 'migration-code') return inventory('native-code-migration-entries.v1.json').entries.flatMap(entry =>
    ['direct', 'abstract-base', 'factory-interface', entry.product === 'migration.pdf.code' ? 'pdf-aggregate' : 'spreadsheet-aggregate']
      .map(path => ({ product: entry.product, provider: entry.provider, path })));
  if (kind === 'migration-viewer') return ['javascript', 'typescript', 'instant-json']
    .map(path => ({ product: 'migration.pdf.viewer', provider: 'nutrient-viewer', path }));
  assert.equal(kind, 'migration-report', 'Unknown migration admission scope.');
  return inventory('native-report-migration-entries.v1.json').entries.map(entry => ({
    product: 'migration.report.designer', provider: entry.provider, overloads: entry.publicConverterOverloads,
  }));
}

export function validateMigrationAdmission(report, kind) {
  const expected = expectedMigrationCases(kind);
  assert.ok(Array.isArray(report.checks) && report.checks.length > 0);
  const checks = new Set(report.checks);
  assert.equal(checks.size, report.checks.length, 'Duplicate executed check IDs.');
  const rows = report.admissionEvidence;
  assert.ok(Array.isArray(rows) && rows.length > 0, 'Missing per-path migration admission evidence.');
  const keys = rows.map(row => `${row.product}:${row.provider}:${row.path}`);
  assert.equal(new Set(keys).size, keys.length, 'Duplicate provider/path evidence.');
  const phaseChecks = new Set();
  for (const row of rows) {
    assert.ok(expected.some(entry => entry.product === row.product && entry.provider === row.provider), 'Unregistered product or provider.');
    assert.equal(typeof row.path, 'string');
    assert.match(row.outputChecksum, /^[A-F0-9]{64}$/);
    const code = kind === 'migration-code';
    const prefix = code ? `${row.product}:${row.provider}:${row.path}` : row.path;
    const labels = { missing: code ? `${prefix}:unlicensed` : `missing:${prefix}`,
      incorrect: code ? `${prefix}:pdf-not-migration-right` : `pdf:denied:${prefix}`,
      valid: code ? `${prefix}:licensed` : `valid:${prefix}` };
    for (const [phase, label] of Object.entries(labels)) {
      assert.equal(row[phase], label, 'Phase mapped to an unrelated assertion.');
      assert.ok(checks.has(label), 'Referenced phase assertion did not execute.');
      assert.ok(!phaseChecks.has(label), 'An assertion cannot stand in for two distinct paths.');
      phaseChecks.add(label);
    }
    const outputKey = code ? `${row.product}:${row.provider}` : row.path;
    assert.equal(row.outputChecksum, report.outputChecksums?.[outputKey], 'Admission output differs from tested output.');
  }
  if (kind === 'migration-report') {
    assert.equal(report.providers, 8);
    assert.equal(report.entries, rows.length);
    assert.equal(rows.length, expected.reduce((sum, entry) => sum + entry.overloads + 2, 2));
    for (const entry of expected) {
      const paths = rows.filter(row => row.provider === entry.provider).map(row => row.path);
      assert.equal(paths.filter(path => path.startsWith(entry.provider + ':direct:')).length, entry.overloads);
      assert.equal(paths.filter(path => path.startsWith(entry.provider + ':factory:')).length, 2);
      const resourcePaths = paths.filter(path => !path.startsWith(entry.provider + ':'));
      assert.deepEqual(resourcePaths.sort(), entry.provider === 'devexpress-report' ? ['resource:compatibility', 'resource:direct'] : []);
    }
  } else {
    assert.deepEqual(keys.sort(), expected.map(row => `${row.product}:${row.provider}:${row.path}`).sort());
    if (kind === 'migration-code') {
      assert.equal(report.providers, 24);
      assert.equal(report.pdfProviders, 16);
      assert.equal(report.spreadsheetProviders, 8);
      const analyses = report.analysisEvidence;
      assert.ok(Array.isArray(analyses));
      const analysisKeys = analyses.map(row => `${row.product}:${row.provider}:${row.operation}`);
      const pdfProviders = [...new Set(expected.filter(row => row.product === 'migration.pdf.code').map(row => row.provider))];
      assert.deepEqual(analysisKeys.sort(), pdfProviders.flatMap(provider => ['trust', 'invoice'].map(operation => `migration.pdf.code:${provider}:${operation}`)).sort());
      for (const row of analyses) {
        assert.match(row.outputChecksum, /^[A-F0-9]{64}$/);
        const labels = { missing: `analysis:unlicensed-${row.operation}:${row.provider}`,
          incorrect: `analysis:wrong-product-${row.operation}:${row.provider}`,
          valid: `analysis:migration.pdf.code:${row.provider}:${row.provider}:${row.operation}-output` };
        for (const [phase, label] of Object.entries(labels)) {
          assert.equal(row[phase], label);
          assert.ok(checks.has(label), 'Standalone analysis phase did not execute.');
          assert.ok(!phaseChecks.has(label)); phaseChecks.add(label);
        }
      }
    }
  }
  return { scope: kind, providers: new Set(rows.map(row => `${row.product}:${row.provider}`)).size,
    paths: rows.length, standaloneAnalyses: kind === 'migration-code' ? report.analysisEvidence.length : 0,
    checkedPhases: phaseChecks.size, allPathsReconciled: true };
}
