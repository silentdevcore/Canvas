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
  assert.match(source, /Signature \/ push button/);
  assert.match(source, /Saved signatures, biometric data, credentials/);
  assert.match(source, /Uploaded source is parsed, never executed/);
  assert.match(source, /licenseKey \/ jwt \/ authPayload \/ serverUrl/);
  assert.match(source, /signature material and the unsupported attachment are absent/);
  assert.match(source, /unmatched form values.*versioned design metadata/i);
  assert.doesNotMatch(source, /licenseKey:\s*['"][^'"]+['"]/);
});
