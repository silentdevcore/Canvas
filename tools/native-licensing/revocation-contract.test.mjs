import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { test } from 'node:test';
const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');
const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));

test('worker revocations are read only from host configuration before native activation', () => {
  for (const path of ['src/Execution/PVAD.CodeWorker/Program.cs', 'src/Importing/PVAD.FileImporter.ImageOcr.Worker/Program.cs']) {
    const source = read(path);
    assert.ok(source.indexOf('ImportRevocationsFromFile') < source.indexOf('PvadLicense.LoadFromFile'));
    assert.ok(source.includes('Environment.GetEnvironmentVariable("PVAD_NATIVE_REVOCATIONS_PATH")'));
  }
  const ocr = read('src/Importing/PVAD.FileImporter.ImageOcr/ProcessIsolatedTesseractOcrEngine.cs');
  assert.ok(ocr.includes('CaptureImporterWorkerConfiguration()'));
  assert.ok(ocr.includes('Environment.Remove("PVAD_NATIVE_REVOCATIONS_PATH")'));
  assert.ok(ocr.includes('File.Delete(revocationPath)'));
  const host = read('PVAD.WebApi/Services/Licensing/PvadNativeLicenseBootstrap.cs');
  assert.ok(host.indexOf('ImportRevocationsFromFile') < host.indexOf('PvadLicense.LoadFromFile'));
});

test('CI rehearses signed revocations without uploading keys, lists or isolated binaries', () => {
  const job = require('js-yaml').load(read('.github/workflows/native-library-licensing.yml')).jobs.licensing;
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/with-test-license.mjs revocation'));
  const upload = job.steps.find(step => step.uses?.startsWith('actions/upload-artifact@')).with.path;
  assert.ok(upload.includes('artifacts/native-engine-tests/*/revocation-admission.json'));
  assert.ok(!upload.includes('isolated-build') && !upload.includes('revocations.json'));
});

test('documentation states restart, freshness and manufacturer-tool boundaries without promising remote revocation', () => {
  const guide = read('docs/native-library-licensing.md');
  for (const text of ['PVADLIC014', 'PVADLIC015', 'at most 512', 'after process restart', 'Stale', 'durable'])
    assert.ok(guide.toLowerCase().includes(text.toLowerCase()), text);
  const operator = read('operator-docs/PVAD.Native-Library-License-Issuance.md');
  assert.ok(operator.includes('does not add an Admin revoke button'));
  assert.ok(operator.includes('automated durable') || operator.includes('Automated durable'));
});
