import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { readFileSync } from 'node:fs';

test('isolated browser runner requires explicit opt-in and schema before starting services', () => {
  const script = fileURLToPath(new URL('./rename-authenticated-browser.mjs', import.meta.url));
  for (const overrides of [
    { PVAD_RUN_RENAME_BROWSER: '', PVAD_RENAME_SCHEMA: '' },
    { PVAD_RUN_RENAME_BROWSER: '1', PVAD_RENAME_SCHEMA: '' },
  ]) {
    const run = spawnSync(process.execPath, [script], {
      env: { ...process.env, ...overrides }, encoding: 'utf8', timeout: 10_000,
    });
    assert.equal(run.status, 1);
    assert.match(run.stderr, /Explicit opt-in and a current generated PVAD_RENAME_SCHEMA/);
    assert.equal(run.stdout, '');
  }
});

test('Nginx mode covers both production frontend builds and keeps runtime storage isolated', () => {
  const source = readFileSync(new URL('./rename-authenticated-browser.mjs', import.meta.url), 'utf8');
  assert.match(source, /if \(nginxProfile\) \{/);
  assert.doesNotMatch(source, /if \(nginxProfile && name === 'designer'\)/);
  assert.match(source, /PVAD_\$\{name\.toUpperCase\(\)\}_ROUTE_PATTERN=/);
  assert.match(source, /mount\(`\$\{directory\}\/dist`/);
  assert.match(source, /accountHosting: nginxProfile/);
  assert.match(source, /externalStagingAcceptance: false/);
  assert.match(source, /api\.child\.kill\('SIGTERM'\)/);
  assert.match(source, /assert\.deepEqual\(after, before/);
  assert.match(source, /recovery\.status !== 'failed'/);
  for (const key of ['PdfViewer__AnnotationStoragePath', 'PdfTrust__RevocationEvidence__CacheDirectory'])
    assert.match(source, new RegExp(`${key}: resolve\\(output,`));
});
