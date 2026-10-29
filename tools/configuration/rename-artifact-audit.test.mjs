import assert from 'node:assert/strict';
import test from 'node:test';
import { inspectArtifact, inspectOldNames } from './rename-artifact-audit.mjs';

test('old-name audit records locations and hashes, never source lines or matching suffix values', () => {
  const secret = 'PXA_hiddenSensitiveValue';
  const findings = inspectOldNames('synthetic.cs', `var value = "${secret}";\ncanvas.draw();`);
  assert.equal(findings.length, 2);
  assert.equal(findings[0].line, 1);
  assert.match(findings[0].lineHash, /^[a-f0-9]{64}$/);
  assert.ok(!JSON.stringify(findings).includes('hiddenSensitiveValue'));
  assert.ok(findings.every(item => item.classification === 'review-required'));
});

test('binary inspection hashes bytes without claiming pixel or cryptographic approval', () => {
  const bytes = Buffer.alloc(24);
  Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]).copy(bytes);
  bytes.writeUInt32BE(800, 16); bytes.writeUInt32BE(600, 20);
  const result = inspectArtifact('PVAD.WebApi/AdminDocumentation/images/synthetic.png', bytes);
  assert.equal(result.kind, 'png');
  assert.equal(result.width, 800); assert.equal(result.height, 600);
  assert.equal(result.visibility, 'protected-admin');
  assert.equal(result.review, 'required');
  assert.match(result.sha256, /^[a-f0-9]{64}$/);
});
