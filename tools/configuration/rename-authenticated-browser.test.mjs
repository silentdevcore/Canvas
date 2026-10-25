import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

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
