import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, renameSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { findFormerShortNameMatches, inspectArtifact, inspectOldNames } from './rename-artifact-audit.mjs';
import { scanRepositoryNames } from './repository-naming-inventory.mjs';

test('old-name audit records locations and hashes, never source lines or matching suffix values', () => {
  const secret = 'PXA_hiddenSensitiveValue';
  const findings = inspectOldNames('synthetic.cs', `var value = "${secret}";\ncanvas.draw();`);
  assert.equal(findings.length, 2);
  assert.equal(findings[0].line, 1);
  assert.match(findings[0].lineHash, /^[a-f0-9]{64}$/);
  assert.ok(!JSON.stringify(findings).includes('hiddenSensitiveValue'));
  assert.ok(findings.every(item => item.classification === 'review-required'));
});

test('binary inspection hashes bytes without claiming pixel or cryptographic approval', () => {
  const bytes = Buffer.alloc(24);
  Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]).copy(bytes);
  bytes.writeUInt32BE(800, 16); bytes.writeUInt32BE(600, 20);
  const result = inspectArtifact('PVAD.WebApi/AdminDocumentation/images/synthetic.png', bytes);
  assert.equal(result.kind, 'png');
  assert.equal(result.width, 800); assert.equal(result.height, 600);
  assert.equal(result.visibility, 'protected-admin');
  assert.equal(result.review, 'required');
  assert.match(result.sha256, /^[a-f0-9]{64}$/);
});

test('short-name detection includes mixed case and embedded identifiers without copying suffixes', () => {
  assert.deepEqual(findFormerShortNameMatches('pXa PXA pxa'), [
    { offset: 0, token: 'pXa' }, { offset: 4, token: 'PXA' }, { offset: 8, token: 'pxa' },
  ]);
  assert.deepEqual(findFormerShortNameMatches('prefixPxaSensitiveSuffix'), [{ offset: 6, token: 'Pxa' }]);
  assert.deepEqual(findFormerShortNameMatches('PVAD PVADE'), []);
  const findings = inspectOldNames('source.ts', 'const value = "pXa_hiddenSensitiveValue";');
  assert.equal(findings.length, 1);
  assert.deepEqual(findings[0].tokens, ['pXa']);
  assert.ok(!JSON.stringify(findings).includes('hiddenSensitiveValue'));
});

test('consolidated inventory scans tracked text, null-byte source, filenames and binary offsets separately', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-naming-inventory-'));
  const git = (...args) => execFileSync('git', args, { cwd: root, stdio: 'pipe' });
  try {
    git('init', '--quiet');
    for (const directory of ['.vscode', '.aws', '.codex', 'nested/.vscode', 'output'])
      mkdirSync(join(root, directory), { recursive: true });
    const files = {
      '.gitignore': 'ignored.js\noutput/\n',
      'Source.CS': 'pXa_secretValue\0PXA_otherSecret',
      'pXa-file.XML': '<current>PVAD</current>',
      'clean.md': 'PVAD',
      'asset.bin': Buffer.from([0, 80, 120, 65, 0]),
      'invalid.txt': Buffer.from([255, 80, 88, 65]),
      'sample.pdf': '%PDF-1.7\nPXA',
      '.vscode/private.ts': 'PXA', '.aws/private.ts': 'PXA', '.codex/private.ts': 'PXA',
      'nested/.vscode/private.ts': 'PXA', 'output/generated.js': 'PXA', 'ignored.js': 'PXA',
    };
    for (const [path, bytes] of Object.entries(files)) writeFileSync(join(root, path), bytes);
    writeFileSync(join(root, 'untracked.ts'), 'PXA_privateUntracked');
    symlinkSync('Source.CS', join(root, 'linked.ts'));
    git('add', '--force', '--', ...Object.keys(files), 'linked.ts');
    const status = git('status', '--porcelain').toString();
    const before = readFileSync(join(root, 'Source.CS'));
    const result = scanRepositoryNames(root);
    assert.deepEqual(result.files.map(file => file.path), [
      'Source.CS', 'asset.bin', 'invalid.txt', 'pXa-file.XML', 'sample.pdf',
    ]);
    assert.deepEqual(result.excluded, { private: 4, ignored: 2, nonRegularOrMissing: 1 });
    const source = result.files.find(file => file.path === 'Source.CS');
    assert.equal(source.kind, 'text');
    assert.equal(source.containsNullBytes, true);
    assert.equal(source.occurrences, 2);
    assert.deepEqual(source.records[0].matches, [
      { column: 1, token: 'pXa' }, { column: 17, token: 'PXA' },
    ]);
    assert.match(source.records[0].lineSha256, /^[a-f0-9]{64}$/);
    assert.equal(result.files.find(file => file.path === 'pXa-file.XML').pathMatches.length, 1);
    assert.deepEqual(result.files.find(file => file.path === 'asset.bin').records,
      [{ offset: 1, token: 'PxA' }]);
    assert.equal(result.files.find(file => file.path === 'invalid.txt').kind, 'binary');
    assert.equal(result.files.find(file => file.path === 'sample.pdf').kind, 'binary');
    assert.ok(!JSON.stringify(result).includes('secretValue'));
    assert.ok(!JSON.stringify(result).includes('otherSecret'));
    assert.ok(!JSON.stringify(result).includes('privateUntracked'));
    assert.equal(git('status', '--porcelain').toString(), status);
    assert.deepEqual(readFileSync(join(root, 'Source.CS')), before);
    assert.deepEqual(scanRepositoryNames(root), result);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('inventory does not follow a directory replaced by a symlink', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-inventory-link-'));
  const outside = mkdtempSync(join(tmpdir(), 'pvad-inventory-outside-'));
  try {
    execFileSync('git', ['init', '--quiet'], { cwd: root });
    mkdirSync(join(root, 'nested'));
    writeFileSync(join(root, 'nested/source.ts'), 'PVAD');
    execFileSync('git', ['add', '--', 'nested/source.ts'], { cwd: root });
    writeFileSync(join(outside, 'source.ts'), 'PXA_privateExternal');
    renameSync(join(root, 'nested'), join(root, 'original'));
    symlinkSync(outside, join(root, 'nested'));
    const result = scanRepositoryNames(root);
    assert.equal(result.counts.scannedFiles, 0);
    assert.equal(result.excluded.nonRegularOrMissing, 1);
    assert.deepEqual(result.files, []);
  } finally {
    rmSync(root, { recursive: true, force: true });
    rmSync(outside, { recursive: true, force: true });
  }
});
