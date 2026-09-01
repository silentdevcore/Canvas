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
  assert.match(source, /certificationStatus/);
  assert.match(source, /must cover the first signed revision/);
  assert.match(source, /level 1 allows no changes/);
  assert.match(source, /FieldMDP locks are enforced separately/);
  assert.match(source, /reads the trailer \/XRefStm of hybrid-reference files/);
  assert.match(source, /object definitions that no cross-reference revision references/);
  assert.match(source, /Parse limits bound nesting/);
  assert.match(source, /Saving is an explicit choice between two strategies/);
  assert.match(source, /objects reachable from the trailer and removes orphaned payloads/);
  assert.match(source, /Malformed, duplicate, mismatched, or late DocMDP declarations invalidate certification/);
  assert.match(source, /CCITT dimensions/);
  assert.match(source, /refuses to run on a signed document unless the caller accepts/);
});

test('PDF/A documentation states the exact generated profile and validation boundary', () => {
  assert.match(source, /ComplianceProfile = PdfComplianceProfile\.PdfA2B/);
  assert.match(source, /POST \/api\/pxa\/v1\/pdf\/compliance\/validate/);
  assert.match(source, /POST \/api\/pxa\/v1\/pdf\/compliance\/preflight/);
  assert.match(source, /POST \/api\/pxa\/v1\/pdf\/compliance\/convert/);
  assert.match(source, /Generation fails closed for encryption, unembedded text fonts/);
  assert.match(source, /Preflight returns ready, reviewRequired, or unsupported/);
  assert.match(source, /original remains unchanged/);
  assert.match(source, /full rewrite invalidates and removes those signatures/);
  assert.match(source, /Visible signature appearances remain unsupported/);
  assert.match(source, /tools\/pdfa\/validate-pdfa\.sh/);
  assert.match(source, /machine-readable report from the pinned official veraPDF container/);
  assert.match(source, /second standards-based or commercial validator/);
  assert.match(source, /merely opening a PDF in a reader is not validation evidence/);
  assert.match(source, /metadata string outside the catalog is only an unverified claim/);
  assert.match(source, /bounded 8-bit samples in losslessly decodable image XObjects/);
  assert.match(source, /JPEG, JPX, colour-key masks, oversized images, unsupported bit depths/);
  assert.match(source, /Colored tiling Pattern streams are traversed/);
  assert.match(source, /uncolored tiling patterns retain their identity/);
  assert.match(source, /Embedded Gray, RGB, and CMYK ICC source profiles use LittleCMS/);
  assert.match(source, /mesh, missing, and malformed shadings remain blocked/);
  assert.match(source, /resource declaration are converted atomically/);
  assert.match(source, /transformed through LittleCMS/);
  assert.match(source, /checks annotation normal appearances, static AcroForm behavior/);
  assert.match(source, /transparency is preserved rather than incorrectly prohibited/);
  assert.match(source, /standards-level release evidence still comes from the pinned veraPDF profile/i);
});

test('PDF redaction documentation separates preflight from applied redaction', () => {
  assert.match(source, /Redaction preflight is separate from applying a redaction/);
  assert.match(source, /POST \/api\/pxa\/v1\/pdf\/redactions\/preflight never changes the uploaded document/);
  assert.match(source, /removable, reviewRequired, or unsupported/);
  assert.match(source, /covered text is never echoed back/);
  assert.match(source, /a removable result is not a claim that a document has been redacted/);
  assert.match(source, /any covered image whose reusable pixels cannot yet be purged/);
  assert.match(source, /geometric ambiguity, images, layers, and unsupported structures block/);
  assert.match(source, /Apply is a bounded edit, not a regeneration/);
  assert.match(source, /a failed verification returns an error instead of the file/);
  assert.match(source, /the removed content never appears in it/);
  assert.match(source, /The Viewer uses the same gated path/);
  assert.match(source, /maps marks through each page CropBox and rotation/);
  assert.match(source, /fails closed with 409 and the preflight report/);
  assert.match(source, /rather than written into the redacted document/);
});

test('PDF encryption documentation states the implemented revision and its limits', () => {
  assert.match(source, /typed existing-PDF reader/);
  assert.match(source, /selected AESV3 crypt filters/);
  assert.match(source, /Designer, and the WebApi defaults to the PDF 2\.0 Standard Security Handler/);
  assert.match(source, /RC4-128 remains available only through the explicit Rc4_128 legacy option/);
  assert.match(source, /AES-128 is available as an interoperability bridge/);
  assert.match(source, /X\.509 recipient encryption uses Adobe\.PubSec/);
  assert.match(source, /opens legacy RC4 and AES-128 documents/);
  assert.match(source, /decrypts object streams before expanding compressed objects/);
  assert.match(source, /\/Perms integrity mismatch rejects the document/);
  assert.match(source, /restricted permissions require a distinct owner password/);
  assert.match(source, /rejected without indicating which password check was closer/);
  assert.match(source, /RFC 4013 SASLprep mappings/);
  assert.match(source, /evaluates password-strength guidance locally/);
  assert.match(source, /owner password and replacement encryption settings/);
  assert.match(source, /refuses missing output encryption rather than returning plaintext/);
  assert.match(source, /encrypted incremental updates remain unsupported/);
  assert.match(source, /required signature-value exemption is not implemented/);
});

test('PDF Trust documentation keeps trust policy server-owned', () => {
  assert.match(source, /Custom anchors are server configuration, never request input/);
  assert.doesNotMatch(source, /"trustAnchor":\s*"-----BEGIN CERTIFICATE-----/);
  assert.doesNotMatch(source, /"ocspUrl":/);
  assert.doesNotMatch(source, /"crlUrl":/);
});
