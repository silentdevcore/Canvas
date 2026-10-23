import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = resolve(root, `artifacts/rename/viewer-captures/${randomUUID()}`);
mkdirSync(output, { recursive: true });
const result = spawnSync('npx', ['--no-install', 'playwright', 'test', '--config', 'playwright.viewer-capture.config.ts'], {
  cwd: resolve(root, 'pvad-designer'), encoding: 'utf8', timeout: 300_000,
  env: { ...process.env, PVAD_TEST_DESIGNER_URL: 'http://127.0.0.1:16176',
    PVAD_CAPTURE_PDF_TRUST_DOCS: '1', PVAD_VIEWER_CAPTURE_DIRECTORY: output },
});
writeFileSync(resolve(output, 'test.log'), `${result.stdout ?? ''}\n${result.stderr ?? ''}`);
assert.equal(result.error, undefined, result.error?.message);
assert.equal(result.status, 0, `Viewer capture tests failed; inspect ${resolve(output, 'test.log')}`);
const captures = [];
for (const name of ['viewer-trust', 'viewer-compliance', 'viewer-signature', 'viewer-redaction-review']) {
  for (const viewport of ['desktop', 'mobile']) {
    const filename = `${name}-${viewport}.png`;
    const bytes = readFileSync(resolve(output, filename));
    assert.ok(bytes.length > 1000, filename);
    captures.push({ filename, sha256: createHash('sha256').update(bytes).digest('hex') });
  }
}
const editorCaptures = ['element-library-favorites.png', 'content-library-preview.png'].map(filename => {
  const bytes = readFileSync(resolve(output, filename));
  assert.ok(bytes.length > 1000, filename);
  return { filename, sha256: createHash('sha256').update(bytes).digest('hex') };
});
// Publish only after both projects and the complete set of images have passed.
for (const { filename } of captures) copyFileSync(resolve(output, filename),
  resolve(root, 'websites/PVAD.Documentation/public/images/pdf-trust', filename));
for (const { filename } of editorCaptures) copyFileSync(resolve(output, filename),
  resolve(root, 'websites/PVAD.Documentation/public/images/designer', filename));
writeFileSync(resolve(output, 'capture-evidence.json'), `${JSON.stringify({ schemaVersion: 1,
  source: 'current-designer-and-viewer-ui-with-synthetic-api-fixtures', backendCryptographicEvidence: false, captures, editorCaptures }, null, 2)}\n`);
console.log(`Captured ${captures.length + editorCaptures.length} UI images. Synthetic API fixtures are not backend cryptographic evidence.`);
