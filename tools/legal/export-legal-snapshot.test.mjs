import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  exportLegalSnapshot,
  validateLegalSnapshot,
} from './export-legal-snapshot.mjs';

const hash = 'b'.repeat(64);
const validSnapshot = {
  schemaVersion: 1,
  generatedAt: '2026-07-30T12:00:00Z',
  locale: 'en',
  audience: 'All',
  documents: [
    {
      key: 'terms',
      version: '2026-07',
      contentHash: hash,
      renderedHtml: '<h1>Terms</h1>',
      effectiveAt: '2026-07-01T00:00:00Z',
    },
  ],
};

test('rejects empty and internally inconsistent deployment snapshots', () => {
  assert.throws(
    () => validateLegalSnapshot({ ...validSnapshot, documents: [] }),
    /at least one published document/);
  assert.throws(
    () => validateLegalSnapshot({
      ...validSnapshot,
      documents: [{ ...validSnapshot.documents[0], contentHash: 'invalid' }],
    }),
    /invalid content hash/);
  assert.throws(
    () => validateLegalSnapshot({
      ...validSnapshot,
      documents: [{
        ...validSnapshot.documents[0],
        effectiveAt: '2026-08-01T00:00:00Z',
      }],
    }),
    /invalid effective date/);
});

test('writes a validated snapshot atomically for the Company deployment', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'pxa-legal-snapshot-'));
  const output = join(directory, 'en.json');
  try {
    const result = await exportLegalSnapshot({
      apiBase: 'https://api.pxa.test',
      locale: 'en',
      audience: 'All',
      output,
      fetchImpl: async () => ({
        ok: true,
        json: async () => structuredClone(validSnapshot),
      }),
    });

    assert.equal(result.documents, 1);
    assert.deepEqual(JSON.parse(await readFile(output, 'utf8')), validSnapshot);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
