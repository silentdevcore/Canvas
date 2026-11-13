import test from 'node:test';
import assert from 'node:assert/strict';
import { comparePdfGlyphRun, comparePdfTextRun } from './text-metrics.mjs';

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

const glyphRun = { text: 'AB', x: 20, baseline: 32, advance: 14, fontSize: 12,
  fontHash: 'bold-font-bytes', bold: true, italic: false,
  glyphs: [{ text: 'A', offset: 0, advance: 7 }, { text: 'B', offset: 7, advance: 7 }] };
const pdfGlyphRun = { ...glyphRun, page: 1 };

test('enlarged review verifies exact font bytes, style and individual glyph positions', () => {
  const actual = { ...glyphRun, glyphs: [glyphRun.glyphs[0], { ...glyphRun.glyphs[1], offset: 7.02 }] };
  assert.ok(comparePdfGlyphRun(actual, [pdfGlyphRun], 1, 'bold').maxGlyphDrift < 0.021);
});
test('glyph drift cannot cancel out behind an unchanged total line width', () => {
  const actual = { ...glyphRun, glyphs: [{ ...glyphRun.glyphs[0], advance: 7.2 }, glyphRun.glyphs[1]] };
  assert.throws(() => comparePdfGlyphRun(actual, [pdfGlyphRun], 1, 'bold'), /Glyph layout drift/);
});
test('enlarged review rejects substituted fonts, styles and incomplete or reordered glyphs', () => {
  for (const actual of [
    { ...glyphRun, fontHash: 'another-font' }, { ...glyphRun, fontSize: 13 },
    { ...glyphRun, bold: false }, { ...glyphRun, italic: true }, { ...glyphRun, glyphs: [] },
    { ...glyphRun, glyphs: [...glyphRun.glyphs].reverse() },
    { ...glyphRun, glyphs: [{ ...glyphRun.glyphs[0], offset: NaN }, glyphRun.glyphs[1]] },
  ]) assert.throws(() => comparePdfGlyphRun(actual, [pdfGlyphRun], 1, 'bold'));
  assert.throws(() => comparePdfGlyphRun({ ...glyphRun, bold: undefined }, [{ ...pdfGlyphRun, bold: undefined }], 1, 'bold'));
});
