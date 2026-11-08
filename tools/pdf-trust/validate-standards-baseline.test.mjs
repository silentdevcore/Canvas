import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';

const script = new URL('./validate-standards-baseline.mjs', import.meta.url);

test('policy mismatch uses neutral output while the recorded policy identity stays required', () => {
  const source = new URL('../../product-metadata/pdf-trust/standards-baseline.json', import.meta.url);
  const original = readFileSync(source);
  const baseline = JSON.parse(original);
  const directory = mkdtempSync(join(tmpdir(), 'pvad-standards-cli-'));
  try {
    const tools = join(directory, 'tools/pdf-trust');
    const metadata = join(directory, 'product-metadata/pdf-trust');
    mkdirSync(tools, { recursive: true });
    mkdirSync(metadata, { recursive: true });
    const probe = join(tools, 'validate-standards-baseline.mjs');
    copyFileSync(script, probe);
    for (const policyId of [baseline.policyId, `${baseline.policyId}-invalid`, 'pvad-pdf-trust']) {
      writeFileSync(join(metadata, 'standards-baseline.json'), JSON.stringify({ ...baseline, policyId }));
      const result = spawnSync(process.execPath, [probe], { encoding: 'utf8' });
      assert.ifError(result.error);
      assert.equal(result.status, policyId === baseline.policyId ? 0 : 1, result.stderr);
      if (result.status !== 0) {
        assert.equal(result.stderr.trim(), 'PDF Trust standards: policyId does not match the recorded PDF Trust policy.');
        assert.ok(!result.stderr.includes(baseline.policyId));
      }
    }
    assert.deepEqual(readFileSync(source), original);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('standards baseline is structurally valid but not release-approved', () => {
  const structural = spawnSync(process.execPath, [script.pathname], { encoding: 'utf8' });
  assert.equal(structural.status, 0, structural.stderr);

  const release = spawnSync(process.execPath, [script.pathname, '--release'], { encoding: 'utf8' });
  assert.equal(release.status, 1);
  assert.match(release.stderr, /not approved|not externally approved/);
});
