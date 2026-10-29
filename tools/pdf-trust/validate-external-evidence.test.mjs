import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const root = path.resolve(import.meta.dirname, '../..');
const script = path.join(import.meta.dirname, 'validate-external-evidence.mjs');
const manifestPath = path.join(root, 'product-metadata/pdf-trust/external-evidence.json');

test('accepts pinned evidence and rejects a replaced file hash', () => {
  const valid = spawnSync(process.execPath, [script], { encoding: 'utf8' });
  assert.equal(valid.status, 0, valid.stderr);

  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  manifest.entries[0].inputSha256 = '0'.repeat(64);
  const temporary = path.join(os.tmpdir(), `pxa-evidence-${process.pid}.json`);
  try {
    fs.writeFileSync(temporary, JSON.stringify(manifest));
    const invalid = spawnSync(process.execPath, [script, temporary], { encoding: 'utf8' });
    assert.equal(invalid.status, 1);
    assert.match(invalid.stderr, /input hash does not match/);
  } finally {
    fs.rmSync(temporary, { force: true });
  }
});
