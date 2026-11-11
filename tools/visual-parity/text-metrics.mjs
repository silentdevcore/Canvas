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
