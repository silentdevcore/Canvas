import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { chmodSync, mkdtempSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { inspectIndex } from './rename-index-review.mjs';

test('actual index review detects unstaged content, unsafe new files, missing rename targets and modes', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-index-review-'));
  const git = (...args) => execFileSync('git', args, { cwd: root, stdio: 'pipe' });
  try {
    git('init', '-q');
    writeFileSync(join(root, 'PXA.txt'), 'synthetic baseline');
    git('add', '.');
    git('-c', 'user.name=Index Test', '-c', 'user.email=index@example.test', 'commit', '-qm', 'fixture');
    renameSync(join(root, 'PXA.txt'), join(root, 'PVAD.txt'));
    git('add', '-A');
    assert.deepEqual(inspectIndex(root).problems, []);
    writeFileSync(join(root, 'PVAD.txt'), 'unstaged');
    assert.ok(inspectIndex(root).problems.some(item => item.reason === 'unstaged-content'));
    git('add', '-A');
    writeFileSync(join(root, '.env'), 'SYNTHETIC=not-a-secret');
    assert.ok(inspectIndex(root).problems.some(item => item.reason === 'untracked-file'));
    git('add', '.env');
    assert.ok(inspectIndex(root).problems.some(item => item.reason === 'new-runtime-or-secret-path'));
    git('rm', '-f', '.env');
    chmodSync(join(root, 'PVAD.txt'), 0o755);
    git('add', 'PVAD.txt');
    assert.deepEqual(inspectIndex(root).modeChanges, [{ path: 'PVAD.txt', before: '100644', after: '100755' }]);
    git('rm', '-f', 'PVAD.txt');
    assert.ok(inspectIndex(root).problems.some(item => item.reason === 'unpaired-index-deletion'));
  } finally { rmSync(root, { recursive: true, force: true }); }
});
