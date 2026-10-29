import assert from 'node:assert/strict';
import test from 'node:test';
import { hasLegacyIdentity, validateReview } from './rename-residual-review.mjs';

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
