export function comparePdfTextRun(actual, pdfRuns, page, id) {
  const distance = run => Math.abs(run.baseline - actual.baseline) + Math.abs(run.x - actual.x);
  const expected = pdfRuns.filter(run => run.page === page && run.text === actual.text)
    .sort((a, b) => distance(a) - distance(b))[0];
  if (!expected) throw new Error(`Missing PDF text metrics for ${id} on page ${page}.`);
  const maxDrift = Math.max(...['x', 'baseline', 'advance'].map(key => Math.abs(actual[key] - expected[key])));
  if (!Number.isFinite(maxDrift) || maxDrift > 0.1)
    throw new Error(`Text layout drift exceeds 0.1pt for ${id}: ${maxDrift}.`);
  return { actual, expected, maxDrift };
}

// The enlarged Latin review also checks local glyph drift, which a total run width can hide.
export function comparePdfGlyphRun(actual, pdfRuns, page, id) {
  const result = comparePdfTextRun(actual, pdfRuns, page, id);
  const expected = result.expected;
  if (!expected.fontHash || actual.fontHash !== expected.fontHash)
    throw new Error(`Font bytes differ for ${id}.`);
  if (!Number.isFinite(expected.fontSize) || expected.fontSize <= 0
    || typeof expected.bold !== 'boolean' || typeof expected.italic !== 'boolean'
    || actual.fontSize !== expected.fontSize || actual.bold !== expected.bold || actual.italic !== expected.italic)
    throw new Error(`Font style differs for ${id}.`);
  if (!expected.glyphs?.length || actual.glyphs?.length !== expected.glyphs.length)
    throw new Error(`Missing or incompatible glyph metrics for ${id}.`);
  let maxGlyphDrift = 0;
  for (const [index, glyph] of actual.glyphs.entries()) {
    const pdf = expected.glyphs[index];
    if (glyph.text !== pdf.text) throw new Error(`Glyph content differs for ${id} at ${index}.`);
    const drift = Math.max(Math.abs(glyph.offset - pdf.offset), Math.abs(glyph.advance - pdf.advance));
    if (!Number.isFinite(drift) || drift > 0.1)
      throw new Error(`Glyph layout drift exceeds 0.1pt for ${id} at ${index}: ${drift}.`);
    maxGlyphDrift = Math.max(maxGlyphDrift, drift);
  }
  return { ...result, maxGlyphDrift };
}
