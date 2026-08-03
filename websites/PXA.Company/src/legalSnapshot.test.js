import test from 'node:test';
import assert from 'node:assert/strict';
import { loadPublishedLegalDocument, selectSnapshotDocument } from './legalSnapshot.js';

const hash = 'a'.repeat(64);
const document = {
  key: 'terms',
  version: '2026-07',
  contentHash: hash,
  renderedHtml: '<h1>Terms</h1>',
  effectiveAt: '2026-07-01T00:00:00Z',
  isAuthoritative: false,
};
const snapshot = {
  schemaVersion: 1,
  generatedAt: '2026-07-15T00:00:00Z',
  locale: 'en',
  audience: 'All',
  documents: [document],
};

function response(body, ok = true, status = ok ? 200 : 503) {
  return { ok, status, json: async () => body };
}

test('returns to the live API after recovering from a snapshot fallback', async () => {
  let online = false;
  const fetchImpl = async (url) => {
    if (url.startsWith('/api/'))
      return online
        ? response({ ...document, version: '2026-08' })
        : response(null, false);
    return response(snapshot);
  };
  const fallback = await loadPublishedLegalDocument({
    kind: 'terms',
    fetchImpl,
  });
  online = true;
  const recovered = await loadPublishedLegalDocument({ kind: 'terms', fetchImpl });

  assert.equal(fallback.source, 'snapshot');
  assert.equal(recovered.source, 'live');
  assert.equal(recovered.document.version, '2026-08');
});

test('uses a last-known-good snapshot when the API is unavailable', async () => {
  const result = await loadPublishedLegalDocument({
    kind: 'terms',
    now: new Date('2026-07-20T00:00:00Z'),
    fetchImpl: async (url) => url.startsWith('/api/')
      ? response(null, false)
      : response(snapshot),
  });

  assert.equal(result.source, 'snapshot');
  assert.equal(result.document.version, '2026-07');
  assert.equal(result.stale, false);
});

test('keeps an old snapshot readable but marks it stale', () => {
  const result = selectSnapshotDocument(
    snapshot,
    'terms',
    new Date('2026-09-01T00:00:00Z'));

  assert.equal(result.document.version, '2026-07');
  assert.equal(result.stale, true);
});

test('rejects an invalid snapshot instead of treating it as legal content', async () => {
  await assert.rejects(
    loadPublishedLegalDocument({
      kind: 'terms',
      fetchImpl: async (url) => url.startsWith('/api/')
        ? response(null, false)
        : response({ ...snapshot, documents: [] }),
    }),
    /Neither the Legal API nor its last-known-good snapshot/);
});
