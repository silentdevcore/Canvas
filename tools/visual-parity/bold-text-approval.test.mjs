import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { boldTextPixelHash, matchesBoldTextApproval } from './bold-text-approval.mjs';
import { spawnImageMagick } from './image-tools.mjs';

const root = new URL('../../', import.meta.url);
const directory = new URL('test-assets/visual-parity/approvals/2026-11-13-bold-text/', root);
const approval = JSON.parse(readFileSync(new URL('approval.json', directory), 'utf8'));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

test('explicit owner approval retains the exact reviewed images, fixtures and packaged font', () => {
  assert.equal(approval.approvalSource, 'Explicit user confirmation after viewing both 200% comparisons.');
  assert.deepEqual(approval.cases.map(entry => entry.id).sort(), ['custom-title', 'text']);
  for (const entry of approval.cases) {
    assert.equal(hash(readFileSync(new URL(entry.fixture, root))), entry.fixtureSha256);
    assert.equal(hash(readFileSync(new URL(entry.comparisonImage, directory))), entry.comparisonSha256);
    assert.equal(boldTextPixelHash(fileURLToPath(new URL(entry.comparisonImage, directory))), entry.comparisonPixelSha256);
  }
  assert.equal(hash(readFileSync(new URL('assets/fonts/liberation/LiberationSans-Bold.ttf', root))), approval.fontHash);
  assert.equal(matchesBoldTextApproval(approval, approval), true);
});
test('a new browser, platform, scale, font or changed crop requires renewed approval', () => {
  for (const patch of [{ platform: 'linux' }, { browserVersion: 'new-version' }, { rasterPixelsPerPoint: 1 },
    { fontHash: 'different-font' }, { cases: [] }, { cases: [approval.cases[0], approval.cases[0]] },
    { cases: approval.cases.map(entry => ({ ...entry, comparisonPixelSha256: 'changed-pixels' })) },
    { cases: approval.cases.map(entry => ({ ...entry, fixtureSha256: 'changed-fixture' })) }])
    assert.equal(matchesBoldTextApproval(approval, { ...approval, ...patch }), false);
  assert.equal(matchesBoldTextApproval({ ...approval, status: 'pending' }, approval), false);
  assert.equal(matchesBoldTextApproval(null, approval), false);
});
test('PNG metadata does not revoke pixel-identical approval, but one changed pixel does', () => {
  const temporary = mkdtempSync(path.join(tmpdir(), 'pvad-bold-approval-'));
  try {
    const original = fileURLToPath(new URL('text.png', directory));
    const metadata = path.join(temporary, 'metadata.png');
    const changed = path.join(temporary, 'changed.png');
    assert.equal(spawnImageMagick([original, '-set', 'comment', 'different metadata only', metadata]).status, 0);
    assert.notEqual(hash(readFileSync(metadata)), hash(readFileSync(original)));
    assert.equal(boldTextPixelHash(metadata), boldTextPixelHash(original));
    assert.equal(spawnImageMagick([original, '-fill', '#ff0000', '-draw', 'point 0,0', changed]).status, 0);
    assert.notEqual(boldTextPixelHash(changed), boldTextPixelHash(original));
    const cases = approval.cases.map(entry => ({ ...entry, comparisonSha256: 'new-png-encoding' }));
    assert.equal(matchesBoldTextApproval(approval, { ...approval, cases }), true);
  } finally { rmSync(temporary, { recursive: true, force: true }); }
});
