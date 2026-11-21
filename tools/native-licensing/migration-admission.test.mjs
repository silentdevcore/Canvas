import assert from 'node:assert/strict';
import { test } from 'node:test';
import { expectedMigrationCases, validateMigrationAdmission } from './migration-admission.mjs';

// Only report validation is exercised here; runtime evidence comes from the native probes.
function fixture(kind) {
  const expected = expectedMigrationCases(kind);
  const cases = kind === 'migration-report' ? expected.flatMap(entry => [
    ...Array.from({ length: entry.overloads }, (_, i) => ({ ...entry, path: `${entry.provider}:direct:Convert:${i}` })),
    ...[0, 1].map(i => ({ ...entry, path: `${entry.provider}:factory:${i}` })),
    ...(entry.provider === 'devexpress-report' ? ['resource:direct', 'resource:compatibility'].map(path => ({ ...entry, path })) : []),
  ]) : expected;
  const code = kind === 'migration-code';
  const outputChecksums = {};
  const rows = cases.map(({ product, provider, path }) => {
    const key = code ? `${product}:${provider}:${path}` : path;
    outputChecksums[code ? `${product}:${provider}` : path] = 'A'.repeat(64);
    return { product, provider, path, missing: code ? `${key}:unlicensed` : `missing:${key}`,
      incorrect: code ? `${key}:pdf-not-migration-right` : `pdf:denied:${key}`,
      valid: code ? `${key}:licensed` : `valid:${key}`, outputChecksum: 'A'.repeat(64) };
  });
  const analysisEvidence = code ? [...new Set(cases.filter(row => row.product === 'migration.pdf.code').map(row => row.provider))].flatMap(provider =>
    ['trust', 'invoice'].map(operation => ({ product: 'migration.pdf.code', provider, operation,
      missing: `analysis:unlicensed-${operation}:${provider}`, incorrect: `analysis:wrong-product-${operation}:${provider}`,
      valid: `analysis:migration.pdf.code:${provider}:${provider}:${operation}-output`, outputChecksum: 'A'.repeat(64) }))) : [];
  return { checks: [...rows, ...analysisEvidence].flatMap(row => [row.missing, row.incorrect, row.valid]), admissionEvidence: rows, analysisEvidence,
    outputChecksums, entries: rows.length, providers: code ? 24 : kind === 'migration-report' ? 8 : 1,
    ...(code ? { pdfProviders: 16, spreadsheetProviders: 8 } : {}) };
}

for (const [kind, paths, providers] of [['migration-code', 96, 24], ['migration-report', 37, 8], ['migration-viewer', 3, 1]]) {
  test(`${kind} reconciles all providers and paths against executed phase assertions`, () => {
    const result = validateMigrationAdmission(fixture(kind), kind);
    assert.equal(result.paths, paths); assert.equal(result.providers, providers);
    assert.equal(result.checkedPhases, (paths + (kind === 'migration-code' ? 32 : 0)) * 3);
  });
  test(`${kind} rejects missing, duplicate, unrelated and unexecuted evidence`, () => {
    for (const mutate of [
      r => r.admissionEvidence.pop(),
      r => r.admissionEvidence[1] = structuredClone(r.admissionEvidence[0]),
      r => r.admissionEvidence[0].provider = 'unknown-provider',
      r => r.admissionEvidence[0].valid = r.admissionEvidence[1].valid,
      r => r.checks.splice(r.checks.indexOf(r.admissionEvidence[0].incorrect), 1),
      r => r.admissionEvidence[0].outputChecksum = 'B'.repeat(64),
      r => r.checks.push(r.checks[0]),
      ...(kind === 'migration-code' ? [
        r => r.analysisEvidence.pop(),
        r => r.checks.splice(r.checks.indexOf(r.analysisEvidence[0].valid), 1),
        r => r.analysisEvidence[0].provider = 'unknown-analysis-provider',
      ] : []),
    ]) { const report = fixture(kind); mutate(report); assert.throws(() => validateMigrationAdmission(report, kind)); }
  });
}

test('unknown scopes and legacy symbol-only reports are not phase coverage', () => {
  assert.throws(() => validateMigrationAdmission({}, 'unknown'));
  assert.throws(() => validateMigrationAdmission({ checks: ['old-success'] }, 'migration-code'));
});
