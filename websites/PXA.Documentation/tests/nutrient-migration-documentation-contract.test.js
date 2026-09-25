import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const source = fs.readFileSync(path.join(root, 'websites/PXA.Documentation/src/main.js'), 'utf8');

test('Nutrient Viewer migration is discoverable and documents the public contract', () => {
  assert.match(source, /title: 'Nutrient Viewer to Designer Migration'/);
  assert.match(source, /id="nutrient-viewer-migration"/);
  assert.match(source, /POST \/api\/pxa\/v1\/migrations\/pdf\/designer\/nutrient-viewer\/convert/);
  assert.match(source, /GET \/api\/pxa\/v1\/migrations\/pdf\/designer\/nutrient-viewer\/examples/);
  assert.match(source, /original PDF remains mandatory/i);
  assert.match(source, /one <code>design<\/code> artifact/);
  assert.match(source, /Open in Designer/);
  assert.match(source, /TypeScript Compiler API/);
  assert.match(source, /PXA\.Migration\.Pdf\.Viewer\.<Provider>/);
  assert.match(source, /PXA\.Migration\.Pdf\.Viewer\.Nutrient/);
  assert.match(source, /CANMIGNUTWEB002-006/);
  assert.match(source, /CANMIGNUTJSON040-048/);
});

test('Nutrient documentation describes mappings and security omissions', () => {
  assert.match(source, /Nutrient \.NET \(formerly GdPicture\.NET\)/);
  assert.match(source, /AddTextFormField \/ AddCheckBoxFormField/);
  assert.match(source, /AddStickyNoteAnnotation/);
  assert.match(source, /dynamic and forward selections remain review items/i);
  assert.match(source, /file\/stream existing-PDF load/i);
  assert.match(source, /PdfEditableDocument/);
  assert.match(source, /PdfPageComposer/);
  assert.match(source, /cross-document cloning/i);
  assert.match(source, /refuses to delete the last page/i);
  assert.match(source, /CropBox read\/set\/reset/i);
  assert.match(source, /one-based page-range extraction\/split/i);
  assert.match(source, /CANMIGNUTNET070/);
  assert.match(source, /CANMIGNUTNET071/);
  assert.match(source, /CropBox operations are limited to non-encrypted input/i);
  assert.match(source, /retains existing destination pages/i);
  assert.match(source, /CANMIGNUTNET069/);
  assert.match(source, /Signature \/ push button/);
  assert.match(source, /Saved signatures, biometric data, credentials/);
  assert.match(source, /Uploaded source is parsed, never executed/);
  assert.match(source, /licenseKey \/ jwt \/ authPayload \/ serverUrl/);
  assert.match(source, /signature material and the unsupported attachment are absent/);
  assert.match(source, /unmatched form values.*versioned design metadata/i);
  assert.match(source, /Aliases, fields, and parameters/);
  assert.match(source, /Symbol-aware Roslyn evidence/);
  assert.match(source, /Trust review evidence/);
  assert.match(source, /source location/);
  assert.match(source, /does not execute irreversible redaction/);
  assert.match(source, /does not sign, validate certificates/);
  assert.match(source, /PFX paths or passwords/);
  assert.doesNotMatch(source, /PFX path:\s*['"][^'"]+['"]/i);
  assert.doesNotMatch(source, /licenseKey:\s*['"][^'"]+['"]/);
});

test('Nutrient architecture and roadmap preserve the Trust execution boundary', () => {
  const checklist = fs.readFileSync(path.join(root, 'checklists/PXA.Migration-Nutrient-Code-And-Viewer.md'), 'utf8');
  const boundaries = fs.readFileSync(path.join(root, 'docs/architecture/existing-pdf-boundaries.md'), 'utf8');
  const fragment = fs.readFileSync(path.join(root, 'product-metadata/release-fragments/nutrient-existing-pdf-page-editing.json'), 'utf8');

  assert.match(checklist, /source locations to Trust evidence/i);
  assert.match(checklist, /supported alias, field, or method parameter/i);
  assert.match(checklist, /does not execute signing\s+or\s+irreversible redaction/i);
  assert.match(checklist, /does not copy PFX paths, passwords, private keys/i);
  assert.match(boundaries, /Migration evidence and Trust handoff/);
  assert.match(boundaries, /Roslyn syntax and semantic information/);
  assert.match(boundaries, /call is reached through a supported alias, field, or method parameter/);
  assert.match(boundaries, /PFX paths, passwords, private keys/);
  assert.match(boundaries, /does not execute signing/);
  assert.match(fragment, /symbol-aware Roslyn detection/i);
  assert.match(fragment, /Trust review evidence/);
  assert.match(fragment, /without executing Trust operations/);
});
