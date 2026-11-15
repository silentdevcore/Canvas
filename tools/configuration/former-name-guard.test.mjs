import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { candidateFiles, hasFormerName, validateAllowlist } from './former-name-guard.mjs';

const sha = 'a'.repeat(64);

test('former short and expanded names are detected in any casing', () => {
  for (const value of ['PXA', 'pxa-designer', 'Pxa.Core', 'Power Dox', 'POWERDOX']) assert.ok(hasFormerName(value), value);
  for (const value of ['PVAD', 'PVADE', 'canvas', 'pvad-designer']) assert.equal(hasFormerName(value), false, value);
});

test('only exact reviewed paths with known reasons pass and pinned bytes must match', () => {
  const allowlist = { schemaVersion: 2, entries: [
    { path: 'evidence.pdf', reason: 'published-evidence', sha256: sha },
    { path: 'tests/Rejects.cs', reason: 'rejection-or-absence-test' },
  ] };
  assert.deepEqual(validateAllowlist([{ path: 'evidence.pdf', sha256: sha }, { path: 'tests/Rejects.cs', sha256: 'b'.repeat(64) }], allowlist), []);
  assert.deepEqual(validateAllowlist([{ path: 'evidence.pdf', sha256: 'c'.repeat(64) }, { path: 'tests/Rejects.cs', sha256: sha }], allowlist),
    ['changed pinned file: evidence.pdf']);
  assert.deepEqual(validateAllowlist([{ path: 'src/New.cs', sha256: sha }, { path: 'evidence.pdf', sha256: sha }, { path: 'tests/Rejects.cs', sha256: sha }], allowlist),
    ['unreviewed former name: src/New.cs']);
  assert.deepEqual(validateAllowlist([{ path: 'evidence.pdf', sha256: sha }], allowlist), ['stale allowlist entry: tests/Rejects.cs']);
  for (const entry of [{ path: 'x', reason: 'unknown' }, { path: 'x', reason: 'published-evidence' }])
    assert.ok(validateAllowlist([], { schemaVersion: 2, entries: [entry] }).some(failure => failure.startsWith('invalid entry')));
  assert.throws(() => validateAllowlist([], { schemaVersion: 1, entries: [] }), /Invalid/);
});

test('scanning covers tracked and untracked files but not ignored, private or linked files', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-former-name-'));
  try {
    execFileSync('git', ['init', '--quiet'], { cwd: root });
    writeFileSync(join(root, '.gitignore'), 'ignored.txt\n');
    writeFileSync(join(root, 'tracked.txt'), 'PXA');
    writeFileSync(join(root, 'untracked.txt'), 'power dox');
    writeFileSync(join(root, 'ignored.txt'), 'PXA');
    writeFileSync(join(root, 'clean.txt'), 'PVAD');
    symlinkSync('tracked.txt', join(root, 'link.txt'));
    execFileSync('git', ['add', 'tracked.txt', 'clean.txt'], { cwd: root });
    assert.deepEqual(candidateFiles(root).map(file => file.path), ['tracked.txt', 'untracked.txt']);
  } finally { rmSync(root, { recursive: true, force: true }); }
});
