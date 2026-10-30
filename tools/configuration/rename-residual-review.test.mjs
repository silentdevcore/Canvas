import assert from 'node:assert/strict';
import test from 'node:test';
import { hasLegacyIdentity, residualFiles, validateReview } from './rename-residual-review.mjs';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, writeFileSync, symlinkSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

test('the invoice attachment source is scanned despite its preserved null byte', () => {
  const root = fileURLToPath(new URL('../../', import.meta.url));
  const path = 'tests/PVAD.Importer.Tests/PdfEInvoiceAttacherTests.cs';
  const original = execFileSync('git', ['show', `aa7c18cb:${path}`], { cwd: root });
  const current = readFileSync(join(root, path));
  assert.equal([...original].filter(value => value === 0).length, 1);
  assert.equal([...current].filter(value => value === 0).length, 1);
  assert.equal([...original.toString('utf8').matchAll(/pxa/gi)].length, 10);
  assert.ok(residualFiles(root).some(file => file.path === path));
});

test('residual scanning reads only tracked regular public files and scans null-byte source', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-source-naming-'));
  try {
    execFileSync('git', ['init', '--quiet'], { cwd: root });
    for (const directory of ['.vscode', '.aws', '.codex', 'nested/.vscode']) mkdirSync(join(root, directory), { recursive: true });
    const fixtures = {
      'Source.cs': 'class Synthetic { string value = "pXa\\0PXA"; }'.replace('\\0', '\0'),
      'pXa-name.txt': 'No historical content',
      'Mixed.TS': 'const value = "PxA";',
      'normal.ts': 'const value = "PVAD";',
      'asset.bin': Buffer.from([0, 80, 88, 65]),
      '.vscode/private.ts': 'PXA', '.aws/private.txt': 'PXA', '.codex/private.cs': 'PXA',
      'nested/.vscode/private.ts': 'PXA',
    };
    for (const [path, bytes] of Object.entries(fixtures)) writeFileSync(join(root, path), bytes);
    writeFileSync(join(root, 'untracked.ts'), 'PXA');
    writeFileSync(join(root, 'ignored.ts'), 'PXA');
    writeFileSync(join(root, '.gitignore'), 'ignored.ts\n');
    symlinkSync('untracked.ts', join(root, 'linked.ts'));
    execFileSync('git', ['add', '--', ...Object.keys(fixtures), '.gitignore', 'linked.ts'], { cwd: root });
    assert.deepEqual(residualFiles(root).map(file => file.path), ['Mixed.TS', 'Source.cs', 'pXa-name.txt']);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('residual inventory includes lowercase legacy domains and spaced product names', () => {
  for (const value of ['https://powerdoxautomation.com', 'support@powerdoxautomation.com', 'Power Dox Automation', 'POWERDOX', 'pXa.Core', 'PXA.Core', 'Canvas']) {
    assert.equal(hasLegacyIdentity(value), true, value);
  }
  assert.equal(hasLegacyIdentity('PVAD / PVADE'), false);
});

const file = { path: 'synthetic/legacy-reader.ts', sha256: 'a'.repeat(64) };
const ledger = () => ({ schemaVersion: 1, files: [{ ...file, reviewStatus: 'reviewed',
  reviewNotes: 'Retains the synthetic legacy-reader identifier for compatibility testing.',
  reasons: ['compatibility-contract'] }] });

test('a matching hash and classification do not substitute for explicit review', () => {
  for (const reviewStatus of [undefined, 'pending', 'classified']) {
    const value = ledger(); value.files[0].reviewStatus = reviewStatus;
    assert.deepEqual(validateReview([file], value), [file.path]);
  }
  const unexplained = ledger(); delete unexplained.files[0].reviewNotes;
  assert.deepEqual(validateReview([file], unexplained), [file.path]);
});

test('residual review rejects new, changed and stale files instead of approving by directory', () => {
  assert.deepEqual(validateReview([file], ledger()), []);
  assert.deepEqual(validateReview([{ ...file, sha256: 'b'.repeat(64) }], ledger()), [file.path]);
  assert.deepEqual(validateReview([file, { ...file, path: 'synthetic/new-reader.ts' }], ledger()), ['synthetic/new-reader.ts']);
  assert.deepEqual(validateReview([], ledger()), [file.path]);
});

test('residual entries require known reasons, hashes and unique paths', () => {
  for (const reasons of [[], ['unreviewed-directory']]) {
    const value = ledger(); value.files[0].reasons = reasons;
    assert.throws(() => validateReview([file], value), /unexplained/);
  }
  const duplicate = ledger(); duplicate.files.push({ ...duplicate.files[0] });
  assert.throws(() => validateReview([file], duplicate), /duplicate/);
  const invalid = ledger(); invalid.files[0].sha256 = 'not-a-hash';
  assert.throws(() => validateReview([file], invalid), /Invalid/);
});
