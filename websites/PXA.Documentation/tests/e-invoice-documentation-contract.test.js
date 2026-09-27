import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const source = fs.readFileSync(path.join(root, 'websites/PXA.Documentation/src/main.js'), 'utf8');
const guide = fs.readFileSync(path.join(root, 'docs/guides/PXA.EInvoicing.md'), 'utf8');
const coverage = fs.readFileSync(
  path.join(root, 'docs/guides/PXA.Unified-Migration-Provider-Coverage.md'),
  'utf8',
);

/**
 * Collapses whitespace so a prose assertion survives the document being reflowed. A sentence
 * that wraps across two lines is the same sentence.
 */
const prose = text => text.replace(/\s+/g, ' ');
const guideProse = prose(guide);
const coverageProse = prose(coverage);

test('electronic invoicing is a documented topic', () => {
  assert.match(source, /title: 'Electronic invoicing'/);
  assert.match(source, /docs\/guides\/PXA\.EInvoicing\.md/);
});

test('documentation keeps structural recognition, business validation and conformance apart', () => {
  for (const text of [source, guide]) {
    assert.match(text, /isStructurallyValid/);
    assert.match(text, /isConformanceValidated/);
    assert.match(text, /conformanceStatus/);
    assert.match(text, /validatorArtifactSetId/);
    assert.match(text, /validatorArtifactSetSha256/);
    assert.match(prose(text), /[Nn]ot a conformance claim/);
    assert.match(
      prose(text),
      /never reported as conformant on the strength of structural validation alone/,
    );
    assert.match(prose(text), /means no artifact set was configured/);
  }
});

test('documentation states the maintained versions and profiles', () => {
  for (const text of [source, guide]) {
    assert.match(text, /Factur-X/);
    assert.match(text, /1\.09\.2/);
    assert.match(text, /ZUGFeRD/);
    assert.match(text, /2\.5\.2/);
    assert.match(text, /XRechnung/);
    assert.match(text, /3\.0\.2/);
    assert.match(text, /factur-x\.xml/);
    assert.match(text, /xrechnung\.xml/);
    assert.match(text, /PXAEINV012/);
  }
  assert.match(guide, /EXTENDED-CTC-FR/);
  assert.match(guide, /UBL Invoice, UBL CreditNote, CII/);
});

test('documentation covers every operation', () => {
  for (const route of [
    '/api/pxa/v1/pdf/e-invoices/generate',
    '/api/pxa/v1/pdf/e-invoices/create',
    '/api/pxa/v1/pdf/e-invoices/attach',
    '/api/pxa/v1/pdf/e-invoices/attach/preflight',
    '/api/pxa/v1/pdf/e-invoices/inspect',
    '/api/pxa/v1/pdf/e-invoices/extract',
    '/api/pxa/v1/pdf/e-invoices/validations',
  ]) {
    assert.ok(guide.includes(route), `the guide must document ${route}`);
  }
  assert.match(guide, /POST \/api\/pxa\/v1\/pdf\/e-invoices\/validate/);
  assert.match(guide, /validations\/\{id\}\/revalidate/);
});

test('documentation states the attachment approvals and the preservation rules', () => {
  for (const text of [source, guide]) {
    assert.match(text, /allowArchiveConversion/);
    assert.match(text, /allowSignatureInvalidation/);
    assert.match(text, /replaceExistingInvoice/);
    assert.match(text, /allowInconsistentPdf/);
    assert.match(text, /preserveUnrecognized/);
  }
  assert.match(guideProse, /original bytes become the prefix of the result/);
  assert.match(guide, /PXAEINVAPI021/);
  assert.match(guide, /PXAEINVAPI022/);
  assert.match(guide, /PXAEINVAPI026/);
  assert.match(guideProse, /\/Unspecified` rather than `\/Alternative/);
  assert.match(guideProse, /re-extracted byte for byte/);
});

test('documentation never treats missing page text as agreement', () => {
  for (const text of [source, guide]) {
    assert.match(text, /notEvaluated/);
    assert.match(prose(text), /never treated as agreement/);
  }
  assert.match(guideProse, /scanned or otherwise text-inaccessible PDF stays at `notEvaluated`/);
});

test('documentation states the product boundary as an external integration', () => {
  for (const text of [source, guide]) {
    assert.match(text, /Peppol transport/);
    assert.match(text, /national tax submission/);
    assert.match(text, /accounting/);
    assert.match(text, /legal retention/);
    assert.match(prose(text), /external integrations, not PDF-engine capabilities/);
  }
  assert.match(guide, /## What PXA is not/);
  assert.match(guideProse, /deliberately has no API for any of them/);
});

test('the guide publishes a compatibility matrix and known limits', () => {
  assert.match(guide, /## Compatibility matrix/);
  assert.match(guide, /## Known limits/);
  assert.match(guide, /artifact set required/);
  assert.match(guideProse, /No official artifacts are shipped yet/);
  assert.match(guideProse, /Independent validation is not yet published/);
  assert.match(guideProse, /Encrypted sources cannot be attached to/);
  assert.match(guide, /PXAEINVATTACH001/);
  assert.match(guide, /PXAEINVATTACH016/);
});

test('the guide publishes C# and API examples', () => {
  assert.match(guide, /```csharp/);
  assert.match(guide, /```http/);
  assert.match(guide, /new PxaEInvoiceDocument/);
  assert.match(guide, /EInvoice\.GenerateXml\(invoice, options\)/);
  assert.match(guide, /EInvoice\.CreatePdf\(invoice, options, fontLoader\)/);
  assert.match(guide, /EInvoice\.CurrentFacturXVersion/);
  assert.match(guide, /EInvoice\.CurrentZugferdVersion/);
  assert.match(guide, /EInvoice\.CurrentXRechnungVersion/);
});

test('the guide documents permissions and the diagnostic ranges', () => {
  for (const permission of [
    'designer.pdf.e-invoices.generate',
    'designer.pdf.e-invoices.create',
    'designer.pdf.e-invoices.attach',
    'designer.pdf.e-invoices.validate',
    'designer.pdf.e-invoices.read',
  ]) {
    assert.ok(guide.includes(permission), `the guide must document ${permission}`);
  }
  assert.match(guideProse, /`generator` product entitlement/);
  assert.match(guide, /PXAEINVATTACH0xx/);
  assert.match(guide, /PXAEINVAPI0xx/);
  assert.match(guide, /Identifiers are stable/);
});

test('the guide documents the Designer foundation honestly and the migration mapping', () => {
  assert.match(guide, /## Designer integration status/);
  assert.match(guideProse, /development foundation, not a reachable product surface/);
  assert.match(guideProse, /React editor.*still required/);
  assert.match(guideProse, /canonical binding paths/);
  assert.match(guideProse, /same model to `generate` and `create`/);
  assert.match(guideProse, /totals-parity\.json/);
  assert.match(guide, /## Migration/);
  assert.match(guideProse, /`automatic`, `reviewRequired` or `unsupported`/);
  assert.match(guideProse, /can never become a silent TODO/);
  assert.match(guide, /PXA\.Unified-Migration-Provider-Coverage\.md/);
});

test('terminology is consistent between the guide and the provider coverage report', () => {
  for (const term of ['automatic', 'reviewRequired', 'unsupported']) {
    assert.ok(guide.includes(term), `the guide must use ${term}`);
    assert.ok(coverage.includes(term), `the coverage report must use ${term}`);
  }
  assert.match(coverage, /Electronic-invoice capabilities per PDF code provider/);
  assert.match(coverageProse, /Nothing in this table is a conformance claim/);
});

test('every relative link in the guide resolves to a file that exists', () => {
  const links = [...guide.matchAll(/\]\(([^)#]+\.md)(?:#[^)]*)?\)/g)].map(match => match[1]);
  assert.ok(links.length > 0, 'the guide should link to at least one other document');
  for (const link of links) {
    const target = path.resolve(root, 'docs/guides', link);
    assert.ok(fs.existsSync(target), `${link} does not exist`);
  }
});
