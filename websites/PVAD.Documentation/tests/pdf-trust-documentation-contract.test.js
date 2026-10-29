import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const source = fs.readFileSync(path.join(root, 'websites/PVAD.Documentation/src/main.js'), 'utf8');

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
  assert.match(source, /POST \/api\/pvad\/v1\/pdf\/signatures\/timestamp/);
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
  assert.match(source, /POST \/api\/pvad\/v1\/pdf\/compliance\/validate/);
  assert.match(source, /POST \/api\/pvad\/v1\/pdf\/compliance\/preflight/);
  assert.match(source, /POST \/api\/pvad\/v1\/pdf\/compliance\/convert/);
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

test('PDF Trust documentation exposes separate PDF/A and PDF/UA learning paths', () => {
  assert.match(source, /title: 'PDF\/A compliance'/);
  assert.match(source, /title: 'PDF\/UA accessibility'/);
  assert.match(source, /Generate PDF\/A-2b/);
  assert.match(source, /Generate PDF\/A-3b with controlled attachments/);
  assert.match(source, /Correct PDF\/A findings after migration/);
  assert.match(source, /only when PdfA2B is explicitly selected/);
  assert.match(source, /Ordinary PDF output remains the default/);
  assert.match(source, /independent validator result/);
  assert.match(source, /title: 'Generate and validate PDF\/UA-1'/);
  assert.match(source, /title: 'Remediate before export'/);
  assert.match(source, /Designer Accessibility Inspector/);
  assert.match(source, /screen-reader review/);
  assert.match(source, /compliance endpoint only detects the PDF\/UA metadata claim/);
  assert.match(source, /it is not a PDF\/UA validator/);
  assert.match(source, /tools\/pdfua\/validate-pdfua\.sh/);
  assert.match(source, /Passing automated validation is evidence for the tested profile/);
  assert.match(source, /Download external PDF\/UA evidence/);
  assert.match(source, /manual evidence template/);
});

test('PDF Trust documentation exposes Generator, Viewer, Migration, API, and troubleshooting paths', async () => {
  assert.match(source, /title: 'PDF Trust'/);
  assert.match(source, /title: 'Generator path'/);
  assert.match(source, /title: 'Viewer path'/);
  assert.match(source, /title: 'Migration path'/);
  assert.match(source, /title: 'API path'/);
  assert.match(source, /title: 'Troubleshooting path'/);
  assert.match(source, /AES-256 revision 6/);
  assert.match(source, /A redaction mark is only an instruction/);
  assert.match(source, /Signature appearance is not evidence/);
  assert.match(source, /indeterminate or unsupported into success/);

  const imageDirectory = path.join(
    root,
    'websites/PVAD.Documentation/public/images/pdf-trust',
  );
  for (const name of [
    'viewer-trust-desktop.png',
    'viewer-trust-mobile.png',
    'viewer-signature-desktop.png',
    'viewer-signature-mobile.png',
    'viewer-compliance-desktop.png',
    'viewer-compliance-mobile.png',
    'viewer-redaction-review-desktop.png',
    'viewer-redaction-review-mobile.png',
  ]) {
    assert.ok(fs.existsSync(path.join(imageDirectory, name)), `${name} is missing`);
  }
});

test('existing PDF architecture documents exact editing boundaries', async () => {
  const boundaryPath = path.join(root, 'docs/architecture/existing-pdf-boundaries.md');
  const boundary = await fs.promises.readFile(boundaryPath, 'utf8');
  assert.match(boundary, /`PVAD\.Importer` owns reading and changing existing PDF object graphs/);
  assert.match(boundary, /`PVAD\.Pdf` owns new-document generation/);
  assert.match(boundary, /`PVAD\.Infrastructure\.Pdf` adapts application-level design documents/);
  assert.match(boundary, /Incremental update/);
  assert.match(boundary, /Full rewrite/);
  assert.match(boundary, /Regeneration/);
  assert.match(boundary, /No component may choose a weaker strategy/);
});

test('PDF/UA manual evidence has a non-passing structured template', async () => {
  const templatePath = path.join(root, 'tools/pdfua/matterhorn-evidence.template.json');
  const schemaPath = path.join(root, 'tools/pdfua/matterhorn-evidence.schema.json');
  const procedurePath = path.join(root, 'tools/pdfua/MATTERHORN-REVIEW.md');
  const publicTemplatePath = path.join(root, 'websites/PVAD.Documentation/public/downloads/pdf-trust/matterhorn-evidence.template.json');
  const template = JSON.parse(await fs.promises.readFile(templatePath, 'utf8'));
  const schema = JSON.parse(await fs.promises.readFile(schemaPath, 'utf8'));
  const publicTemplate = JSON.parse(await fs.promises.readFile(publicTemplatePath, 'utf8'));
  await fs.promises.access(procedurePath);

  assert.equal(schema.properties.schemaVersion.const, 1);
  assert.equal(template.schemaVersion, 1);
  assert.equal(template.review.status, 'not-run');
  assert.deepEqual(publicTemplate, template);
  assert.ok(template.checks.length >= 10);
  assert.ok(template.checks.every((check) => check.status === 'not-run'));
  assert.ok(template.checks.some((check) => check.id === 'screen-reader-reading-order'));
  assert.ok(template.checks.some((check) => check.id === 'table-navigation'));
  assert.ok(template.checks.some((check) => check.id === 'form-labels-and-values'));
});

test('PDF redaction documentation separates preflight from applied redaction', () => {
  assert.match(source, /Redaction preflight is separate from applying a redaction/);
  assert.match(source, /POST \/api\/pvad\/v1\/pdf\/redactions\/preflight never changes the uploaded document/);
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
  assert.match(source, /GET \/api\/pvad\/v1\/pdf\/redactions\/audits\/\{recordSha256\}/);
  assert.match(source, /append-only tenant evidence/);
  assert.match(source, /responses are never cached/);
  assert.match(source, /RFC 3161 authority is configured/);
  assert.match(source, /timestamp imprint, TSA signature, timestamping EKU/);
  assert.match(source, /original glyph bytes and positions cannot be resolved safely/);
  assert.match(source, /1-, 2-, 4-, 8-, and 16-bit images/);
  assert.match(source, /Attached mask streams are cloned and scrubbed in the same region/);
  assert.match(source, /Stencil, colour-key, soft-mask, and explicit-mask images/);
  assert.match(source, /Valid tiling and shading pattern uses are removed with the containing paint element/);
  assert.match(source, /Unused patterns and uses outside the region do not block/);
  assert.match(source, /bounded grayscale\/RGB JPX and embedded JBIG2 streams/i);
  assert.match(source, /validates dimensions, segment boundaries, and JBIG2Globals/);
  assert.match(source, /pdftotext, pdfimages, and strings/);
  assert.match(source, /independently maintained recovery corpus/);
  assert.match(source, /Chrome and LibreOffice PDFs by SHA-256/);
  assert.match(source, /no-recovery guarantee applies only to the documented supported corpus/);
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

test('PDF Trust documentation describes tenant-scoped inspection jobs and retention', () => {
  assert.match(source, /POST \/api\/pvad\/v1\/pdf\/trust\/inspect\/jobs/);
  assert.match(source, /GET \/api\/pvad\/v1\/jobs\/\{jobId\}\/result/);
  assert.match(source, /expires after one hour by default/);
  assert.match(source, /immutable audit correlation are tenant-scoped/);
  assert.match(source, /synchronous \/api\/pvad\/v1\/pdf\/trust\/inspect endpoint remains available for compatibility/);
});

test('PDF Trust documentation publishes all synthetic trust examples and expected reports', () => {
  assert.match(source, /Download synthetic PDF Trust corpus/);
  assert.match(source, /\/downloads\/pdf-trust\//);
  const directory = path.join(root, 'websites/PVAD.Documentation/public/downloads/pdf-trust');
  for (const id of ['clean', 'malformed', 'encrypted', 'signed', 'archived', 'accessible', 'redaction-risk']) {
    assert.ok(fs.existsSync(path.join(directory, `${id}.pdf`)), `${id}.pdf is missing`);
    assert.ok(fs.existsSync(path.join(directory, `${id}.expected.json`)), `${id}.expected.json is missing`);
  }
  assert.ok(fs.existsSync(path.join(directory, 'corpus.json')));
  assert.ok(fs.existsSync(path.join(directory, 'index.html')));
});

test('PDF Trust documentation publishes the external producer baseline', () => {
  assert.match(source, /Download external producer corpus/);
  assert.match(source, /\/downloads\/pdf-trust\/external\//);
  const directory = path.join(root, 'websites/PVAD.Documentation/public/downloads/pdf-trust/external');
  for (const id of [
    'chromium-skia-tagged',
    'libreoffice-tagged',
    'qpdf-aes256-unicode',
    'pyhanko-pades-bb',
    'libreoffice-pdfa2b',
    'libreoffice-pdfua1',
  ]) {
    assert.ok(fs.existsSync(path.join(directory, `${id}.pdf`)), `${id}.pdf is missing`);
    assert.ok(fs.existsSync(path.join(directory, `${id}.expected.json`)), `${id}.expected.json is missing`);
  }
  assert.ok(fs.existsSync(path.join(directory, 'external-corpus.json')));
  assert.ok(fs.existsSync(path.join(directory, 'index.html')));
  for (const evidence of [
    'qpdf-aes256-unicode-qpdf.txt',
    'pyhanko-pades-bb-validation.txt',
    'libreoffice-pdfa2b-2b-verapdf.xml',
    'libreoffice-pdfua1-ua1-verapdf.xml',
  ]) {
    assert.ok(fs.existsSync(path.join(directory, evidence)), `${evidence} is missing`);
  }
});

test('PDF Trust documentation publishes independently validated PAdES profiles', () => {
  assert.match(source, /Download independently validated PAdES corpus/);
  assert.match(source, /\/downloads\/pdf-trust\/pades\//);
  const directory = path.join(root, 'websites/PVAD.Documentation/public/downloads/pdf-trust/pades');
  for (const id of ['pxa-pades-b-t', 'pxa-pades-b-lt', 'pxa-pades-b-lta']) {
    assert.ok(fs.existsSync(path.join(directory, `${id}.pdf`)), `${id}.pdf is missing`);
    for (const report of ['simple', 'detailed', 'diagnostic']) {
      assert.ok(
        fs.existsSync(path.join(directory, `${id}-dss-${report}.xml`)),
        `${id} ${report} report is missing`,
      );
    }
  }
  assert.ok(fs.existsSync(path.join(directory, 'pades-corpus.json')));
  assert.ok(fs.existsSync(path.join(directory, 'root-trust-anchor.cer')));
  assert.ok(fs.existsSync(path.join(directory, 'root-current.crl')));
  assert.ok(fs.existsSync(path.join(directory, 'index.html')));
});

test('PDF Trust documentation publishes independently classified PAdES failure cases', () => {
  assert.match(source, /Download PAdES negative trust corpus/);
  assert.match(source, /\/downloads\/pdf-trust\/pades\/negative\//);
  const directory = path.join(root, 'websites/PVAD.Documentation/public/downloads/pdf-trust/pades/negative');
  for (const id of [
    'pxa-pades-expired',
    'pxa-pades-revocation-unavailable',
    'pxa-pades-revoked',
    'pxa-pades-tampered-lt',
    'pxa-pades-tampered-lta',
    'pxa-pades-untrusted',
  ]) {
    assert.ok(fs.existsSync(path.join(directory, `${id}.pdf`)), `${id}.pdf is missing`);
    for (const report of ['simple', 'detailed', 'diagnostic']) {
      assert.ok(
        fs.existsSync(path.join(directory, `${id}-dss-${report}.xml`)),
        `${id} ${report} report is missing`,
      );
    }
  }
  assert.ok(fs.existsSync(path.join(directory, 'negative-corpus.json')));
  assert.ok(fs.existsSync(path.join(directory, 'index.html')));
});

test('Redaction documentation states what is detected and what is sanitized', () => {
  assert.match(source, /narrows coverage by the clipping paths in force/);
  assert.match(source, /soft masks set in the graphics state, pattern resources, and page or form transparency groups/);
  assert.match(source, /form field values on every field rather than only overlapping widgets/);
  assert.match(source, /non-normal appearance streams such as down and rollover states/);
  assert.match(source, /bounded grayscale\/RGB JPX or embedded JBIG2 data/);
});

test('Redaction verification and audit signing are described with their limits', () => {
  assert.match(source, /each recovery route as its own named check/);
  assert.match(source, /never reports residue that is not there/);
  assert.match(source, /dedicated audit key configured per deployment/);
  assert.match(source, /PVAD-Redaction-Audit-Signed as false/);
});

test('Viewer redaction workflow is described as three separate steps', () => {
  assert.match(source, /The Viewer presents the same three steps separately/);
  assert.match(source, /apply stays disabled until preflight reports the marks as removable/);
  assert.match(source, /rather than as a message string/);
  assert.match(source, /localized in all six Designer languages/);
});
