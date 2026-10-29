import assert from 'node:assert/strict';
import test from 'node:test';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { canonicalPath, inspectDeletedPairs } from './rename-file-pairs.mjs';

test('owned rename paths map consistently without changing neutral canvas terms', () => {
  assert.equal(canonicalPath('PXA.WebApi/PXA.WebApi.http'), 'PVAD.WebApi/PVAD.WebApi.http');
  assert.equal(canonicalPath('pxa-designer/src/PxaStore.ts'), 'pvad-designer/src/PvadStore.ts');
  assert.equal(canonicalPath('src/PXA.Core/CanvasRenderer.cs'), 'src/PVAD.Core/CanvasRenderer.cs');
});

test('file pairing remains identical before and after staging a rename', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-rename-pairs-'));
  const git = (...args) => execFileSync('git', args, { cwd: directory, stdio: 'pipe' });
  try {
    git('init', '--quiet');
    writeFileSync(join(directory, 'PXA.txt'), 'Synthetic retained bytes\n');
    git('add', 'PXA.txt');
    git('-c', 'user.name=Rename fixture', '-c', 'user.email=fixture@example.invalid',
      '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Synthetic baseline');
    renameSync(join(directory, 'PXA.txt'), join(directory, 'PVAD.txt'));
    const unstaged = inspectDeletedPairs(directory);
    assert.equal(unstaged.length, 1);
    assert.equal(unstaged[0].target, 'PVAD.txt');
    assert.equal(unstaged[0].sourceSha256, unstaged[0].targetSha256);
    git('add', '-A');
    assert.deepEqual(inspectDeletedPairs(directory), unstaged);
    git('-c', 'user.name=Rename fixture', '-c', 'user.email=fixture@example.invalid',
      '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Synthetic rename');
    assert.deepEqual(inspectDeletedPairs(directory), []);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
