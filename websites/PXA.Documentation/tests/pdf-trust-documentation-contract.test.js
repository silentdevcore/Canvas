import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const source = fs.readFileSync(path.join(root, 'websites/PXA.Documentation/src/main.js'), 'utf8');

test('PDF Trust documentation separates integrity, trust, and revocation', () => {
  assert.match(source, /PAdES B-B, B-T, B-LT, and B-LTA/);
  assert.match(source, /CustomTrustAnchorPaths/);
  assert.match(source, /IntermediateCertificatePaths/);
  assert.match(source, /RevocationMode/);
  assert.match(source, /AllowCertificateDownloads/);
  assert.match(source, /Valid integrity proves that signed bytes were not changed/);
  assert.match(source, /Indeterminate revocation must never be presented as valid/);
  assert.match(source, /rejects malformed or overlapping byte ranges/);
  assert.match(source, /non-whitespace bytes after the final complete revision/);
  assert.match(source, /later revisions that redefine a signature object/);
  assert.match(source, /wrap an older signature in a newly introduced field/);
  assert.match(source, /certificate-declared HTTPS CRL and OCSP endpoints/);
  assert.match(source, /reports or creates B-LT only when a valid VRI key/);
  assert.match(source, /B-LTA additionally requires a later trusted RFC 3161 document timestamp/);
  assert.match(source, /POST \/api\/pxa\/v1\/pdf\/signatures\/timestamp/);
  assert.match(source, /Long-term signing uses the server trust policy and fails closed/);
  assert.match(source, /embeddedValidationMaterial/);
  assert.match(source, /archiveTimestampStatus/);
});

test('PDF Trust documentation keeps trust policy server-owned', () => {
  assert.match(source, /Custom anchors are server configuration, never request input/);
  assert.doesNotMatch(source, /"trustAnchor":\s*"-----BEGIN CERTIFICATE-----/);
  assert.doesNotMatch(source, /"ocspUrl":/);
  assert.doesNotMatch(source, /"crlUrl":/);
});
