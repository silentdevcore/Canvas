import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const source = fs.readFileSync(path.join(root, 'websites/PXA.Documentation/src/main.js'), 'utf8');

test('PDF Trust documentation separates integrity, trust, and revocation', () => {
  assert.match(source, /PAdES B-B and B-T/);
  assert.match(source, /CustomTrustAnchorPaths/);
  assert.match(source, /IntermediateCertificatePaths/);
  assert.match(source, /RevocationMode/);
  assert.match(source, /AllowCertificateDownloads/);
  assert.match(source, /Valid integrity proves that signed bytes were not changed/);
  assert.match(source, /Indeterminate revocation must never be presented as valid/);
  assert.match(source, /certificate-declared HTTPS CRL and OCSP endpoints/);
  assert.match(source, /reports B-LT only when a valid VRI key/);
  assert.match(source, /Creating new B-LT signatures remains a separate roadmap item/);
  assert.match(source, /embeddedValidationMaterial/);
});

test('PDF Trust documentation keeps trust policy server-owned', () => {
  assert.match(source, /Custom anchors are server configuration, never request input/);
  assert.doesNotMatch(source, /"trustAnchor":\s*"-----BEGIN CERTIFICATE-----/);
  assert.doesNotMatch(source, /"ocspUrl":/);
  assert.doesNotMatch(source, /"crlUrl":/);
});
