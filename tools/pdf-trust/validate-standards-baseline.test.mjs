import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const script = new URL('./validate-standards-baseline.mjs', import.meta.url);

test('standards baseline is structurally valid but not release-approved', () => {
  const structural = spawnSync(process.execPath, [script.pathname], { encoding: 'utf8' });
  assert.equal(structural.status, 0, structural.stderr);

  const release = spawnSync(process.execPath, [script.pathname, '--release'], { encoding: 'utf8' });
  assert.equal(release.status, 1);
  assert.match(release.stderr, /not approved|not externally approved/);
});
