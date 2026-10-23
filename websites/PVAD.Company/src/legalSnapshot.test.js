import test from 'node:test';
import assert from 'node:assert/strict';
import {
  loadPublishedLegalDocument,
  loadPublishedLegalHistory,
  loadPublishedLegalVersion,
  selectSnapshotDocument,
} from './legalSnapshot.js';

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

test('requests audience-specific public documents for DPA and withdrawal pages', async () => {
  const urls = [];
  const dpaDocument = { ...document, key: 'dpa' };
  const withdrawalDocument = { ...document, key: 'withdrawal' };
  const fetchImpl = async (url) => {
    urls.push(url);
    return response(url.includes('/dpa/') ? dpaDocument : withdrawalDocument);
  };

  await loadPublishedLegalDocument({ kind: 'dpa', fetchImpl });
  await loadPublishedLegalDocument({ kind: 'withdrawal', fetchImpl });

  assert.match(urls[0], /audience=Business/);
  assert.match(urls[1], /audience=Consumer/);
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

test('loads an explicitly selected published version without snapshot fallback', async () => {
  let requestedUrl = '';
  const result = await loadPublishedLegalVersion({
    kind: 'terms',
    version: '1.0 final',
    fetchImpl: async (url) => {
      requestedUrl = url;
      return response({ ...document, version: '1.0 final' });
    },
  });

  assert.match(requestedUrl, /\/versions\/1.0%20final\?/);
  assert.equal(result.archived, true);
  assert.equal(result.document.version, '1.0 final');
});

test('loads and validates public version history metadata', async () => {
  const history = await loadPublishedLegalHistory({
    kind: 'terms',
    fetchImpl: async () => response({
      key: 'terms',
      currentVersion: '1.1',
      versions: [
        {
          version: '1.1',
          contentHash: hash,
          effectiveAt: '2026-08-01T00:00:00Z',
          changeSummary: 'Clarified account responsibilities.',
        },
      ],
    }),
  });

  assert.equal(history.currentVersion, '1.1');
  assert.equal(history.versions[0].changeSummary, 'Clarified account responsibilities.');
});

test('rejects history entries without verified public metadata', async () => {
  await assert.rejects(
    loadPublishedLegalHistory({
      kind: 'terms',
      fetchImpl: async () => response({
        key: 'terms',
        versions: [{ version: 'draft', effectiveAt: null, contentHash: 'invalid' }],
      }),
    }),
    /history version "draft" is invalid/);
});
