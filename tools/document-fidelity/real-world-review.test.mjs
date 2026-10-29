import assert from 'node:assert/strict';
import test from 'node:test';
import { compareTokens, selectReviewPages, validateReviewDecisions } from './real-world-review.mjs';

test('selects first, middle, and last common pages', () => {
  assert.deepEqual(selectReviewPages(62, 62), [1, 31, 62]);
  assert.deepEqual(selectReviewPages(3, 1), [1]);
  assert.deepEqual(selectReviewPages(1, 0), []);
});

test('compares normalized token multisets without inflating duplicates', () => {
  const result = compareTokens('Invoice 42 Total Total', 'invoice 42 total extra');
  assert.equal(result.sourceTokens, 4);
  assert.equal(result.outputTokens, 4);
  assert.equal(result.matchedTokens, 3);
  assert.equal(result.sourcePreservationRatio, 0.75);
  assert.equal(result.outputPrecisionRatio, 0.75);
});

test('requires complete and unique human review decisions', () => {
  const fixtures = [
    'real-world-pdf-ukgi-annual-report',
    'real-world-docx-suffolk-mapping-guidance',
    'real-world-xlsx-suffolk-buildings'
  ].map(fixtureId => ({ fixtureId, decision: 'rejected', reviewedAt: '2026-09-20', reviewer: 'PXA engineering', findings: ['Mismatch.'] }));
  assert.equal(validateReviewDecisions({ schemaVersion: 1, fixtures }).fixtures.length, 3);
  assert.throws(() => validateReviewDecisions({ schemaVersion: 1, fixtures: fixtures.slice(0, 2) }), /exactly three fixtures/);
  assert.throws(() => validateReviewDecisions({ schemaVersion: 1, fixtures: fixtures.map((item, index) => index === 0 ? { ...item, findings: [] } : item) }), /incomplete review decision/);
});
