const apiTypes = Object.freeze({
  terms: 'terms',
  privacy: 'privacy',
  license: 'license',
  'cookie-storage': 'cookie-storage',
  imprint: 'imprint',
  withdrawal: 'withdrawal',
  dpa: 'dpa',
});

function validTimestamp(value) {
  return typeof value === 'string' && Number.isFinite(Date.parse(value));
}

export function validatePublicLegalDocument(document, expectedKey) {
  if (!document || document.key !== expectedKey)
    throw new Error(`Legal document "${expectedKey}" is missing.`);
  if (typeof document.version !== 'string' || !document.version)
    throw new Error(`Legal document "${expectedKey}" has no version.`);
  if (!/^[a-f0-9]{64}$/.test(document.contentHash ?? ''))
    throw new Error(`Legal document "${expectedKey}" has an invalid content hash.`);
  if (typeof document.renderedHtml !== 'string' || !document.renderedHtml)
    throw new Error(`Legal document "${expectedKey}" has no rendered content.`);
  if (!validTimestamp(document.effectiveAt))
    throw new Error(`Legal document "${expectedKey}" has an invalid effective date.`);
  return document;
}

export function selectSnapshotDocument(snapshot, expectedKey, now = new Date()) {
  if (!snapshot || snapshot.schemaVersion !== 1 || !validTimestamp(snapshot.generatedAt))
    throw new Error('The legal snapshot metadata is invalid.');
  if (!Array.isArray(snapshot.documents))
    throw new Error('The legal snapshot has no document collection.');
  const document = validatePublicLegalDocument(
    snapshot.documents.find((value) => value?.key === expectedKey),
    expectedKey);
  const generatedAt = new Date(snapshot.generatedAt);
  return {
    document,
    generatedAt: generatedAt.toISOString(),
    stale: now.getTime() - generatedAt.getTime() > 30 * 24 * 60 * 60 * 1000,
  };
}

export async function loadPublishedLegalDocument({
  kind,
  locale = 'en',
  fetchImpl = fetch,
  now = new Date(),
}) {
  const expectedKey = apiTypes[kind];
  if (!expectedKey)
    throw new Error(`Unsupported legal document kind "${kind}".`);

  try {
    const liveResponse = await fetchImpl(
      `/api/pxa/v1/legal/documents/${expectedKey}/current?locale=${encodeURIComponent(locale)}`,
      { headers: { Accept: 'application/json' }, cache: 'no-store' });
    if (!liveResponse.ok)
      throw new Error(`Legal API returned HTTP ${liveResponse.status}.`);
    return {
      source: 'live',
      document: validatePublicLegalDocument(await liveResponse.json(), expectedKey),
      generatedAt: null,
      stale: false,
    };
  } catch (liveError) {
    try {
      const snapshotResponse = await fetchImpl(
        `/legal-snapshots/${encodeURIComponent(locale)}.json`,
        { headers: { Accept: 'application/json' }, cache: 'no-cache' });
      if (!snapshotResponse.ok)
        throw new Error(`Legal snapshot returned HTTP ${snapshotResponse.status}.`);
      return {
        source: 'snapshot',
        ...selectSnapshotDocument(await snapshotResponse.json(), expectedKey, now),
      };
    } catch (snapshotError) {
      throw new AggregateError(
        [liveError, snapshotError],
        `Neither the Legal API nor its last-known-good snapshot could provide "${expectedKey}".`);
    }
  }
}
