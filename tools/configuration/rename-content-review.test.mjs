import assert from 'node:assert/strict';
import test from 'node:test';
import { compareContent, parseBatch } from './rename-content-review.mjs';

test('content triage distinguishes unchanged, identity-only, substantive and binary changes', () => {
  const compare = (before, after) => compareContent(Buffer.from(before), Buffer.from(after));
  assert.equal(compare('PXA', 'PXA'), 'byte-identical');
  assert.equal(compare('PXA.Pxa pxa', 'PVAD.Pvad pvad'), 'identity-substitution-only-review-required');
  assert.equal(compare('\uFEFFnamespace PXA.Core;', '\uFEFFnamespace PVAD.Core;'), 'identity-substitution-only-review-required');
  assert.equal(compare('\uFEFFnamespace PXA.Core;', 'namespace PVAD.Core;'), 'additional-content-review-required');
  assert.equal(compare('PXA', 'PVADE'), 'additional-content-review-required');
  assert.equal(compare('PXA\nreturn 1;', 'PVAD\nreturn 2;'), 'additional-content-review-required');
  assert.equal(compare('PXA\0', 'PVAD\0'), 'binary-content-review-required');
  assert.equal(compareContent(Buffer.from([255]), Buffer.from([254])), 'binary-content-review-required');
});

test('Git batch parsing uses byte lengths, not line splitting, for binary and Unicode payloads', () => {
  const oid = 'a'.repeat(40);
  const second = 'b'.repeat(40);
  const payload = Buffer.from('text\n\0é');
  const bytes = Buffer.concat([Buffer.from(`${oid} blob ${payload.length}\n`), payload,
    Buffer.from(`\n${second} blob 0\n\n`)]);
  assert.deepEqual(parseBatch(bytes).get(oid), payload);
  assert.equal(parseBatch(bytes).get(second).length, 0);
  assert.throws(() => parseBatch(bytes.subarray(0, bytes.length - 1)), /Truncated/);
  assert.throws(() => parseBatch(Buffer.from(`${oid} missing\n`)), /Unexpected/);
});
