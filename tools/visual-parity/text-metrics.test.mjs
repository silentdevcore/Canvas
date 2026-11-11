import test from 'node:test';
import assert from 'node:assert/strict';
import { comparePdfTextRun } from './text-metrics.mjs';

const actual = { text: 'Title', x: 30, baseline: 42, advance: 125 };
const expected = { ...actual, page: 1, fontHash: 'packaged-font' };

test('text metrics retain the real PDF measurements and embedded font provenance', () => {
  assert.deepEqual(comparePdfTextRun(actual, [expected], 1, 'title'), { actual, expected, maxDrift: 0 });
});
test('browser subpixel advance quantization is tolerated without changing image thresholds', () => {
  assert.equal(comparePdfTextRun({ ...actual, advance: 125.015625 }, [expected], 1, 'title').maxDrift, 0.015625);
});
test('position, baseline and advance changes cannot hide behind an aggregate image score', () => {
  for (const key of ['x', 'baseline', 'advance'])
    assert.throws(() => comparePdfTextRun({ ...actual, [key]: actual[key] + 0.2 }, [expected], 1, 'title'), /exceeds/);
  assert.throws(() => comparePdfTextRun({ ...actual, advance: NaN }, [expected], 1, 'title'), /exceeds/);
});
test('absent text and wrong-page matches fail closed', () => {
  assert.throws(() => comparePdfTextRun(actual, [], 1, 'title'), /Missing PDF/);
  assert.throws(() => comparePdfTextRun(actual, [expected], 2, 'title'), /Missing PDF/);
});
test('repeated text selects the closest position, not the first identical string', () => {
  const otherColumn = { ...expected, x: 250 };
  assert.equal(comparePdfTextRun(actual, [otherColumn, expected], 1, 'title').expected, expected);
});
