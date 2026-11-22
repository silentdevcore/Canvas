import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import { checkRuntimeBundles, loadRuntimeAssets, materializeRuntimeBundle, runtimeBundleFiles, runtimeRegistryPath } from './runtime-assets.mjs';

const hash = bytes => createHash('sha256').update(bytes).digest('hex');
function fixture(callback) {
  const root = mkdtempSync(join(tmpdir(), 'pvad-runtime-notices-'));
  const directory = join(root, runtimeRegistryPath);
  mkdirSync(join(directory, 'notices'), { recursive: true }); mkdirSync(join(root, 'fonts'));
  writeFileSync(join(root, 'fonts/font.ttf'), 'synthetic font');
  writeFileSync(join(directory, 'notices/LICENSE.txt'), 'Synthetic original license\r\n');
  const registry = { schemaVersion: 1, legalApprovalGranted: false, publicationReady: false,
    limitations: ['Synthetic test, not a redistribution approval.'],
    scanRoots: [{ path: 'fonts', extensions: ['.ttf'] }], groups: [{ id: 'synthetic-font', profile: 'fonts',
      provenance: 'source-matched', source: { url: 'https://example.org/archive', archiveSha256: 'a'.repeat(64) },
      reviewNote: 'Synthetic test source only; no legal approval.',
      notices: [{ file: 'notices/LICENSE.txt', sha256: hash('Synthetic original license\r\n') }],
      assets: [{ file: 'fonts/font.ttf', sha256: hash('synthetic font'), optionalLocalFile: false }] }] };
  const save = () => writeFileSync(join(directory, 'manifest.v1.json'), JSON.stringify(registry));
  save();
  try { callback({ root, directory, registry, save }); } finally { rmSync(root, { recursive: true, force: true }); }
}

test('real runtime registry covers 13 unchanged fonts, 20 models and 15 optional local binaries', () => {
  const evidence = loadRuntimeAssets();
  assert.equal(evidence.registry.groups.reduce((sum, group) => sum + group.assets.length, 0), 48);
  assert.equal(evidence.registry.groups.filter(group => group.profile === 'fonts').reduce((sum, group) => sum + group.assets.length, 0), 13);
  assert.equal(evidence.registry.groups.find(group => group.id === 'tessdata-fast').assets.length, 19);
  assert.equal(evidence.registry.groups.find(group => group.id === 'liberation-sans-1-07-4').source.archiveSha256,
    'efd381517f958b01969343634ffcbdd60056be7779af84c6f53a005090430204');
  assert.match(evidence.registry.groups.find(group => group.id === 'liberation-sans-1-07-4').declaredLicense, /GNU General Public License/);
  assert.deepEqual(checkRuntimeBundles().unresolvedGroups, ['ukrainian-ocr-model-unresolved', 'local-macos-native-binaries-unresolved']);
});

test('profile bundles preserve exact original notice bytes without mixing fonts into OCR', () => fixture(({ root }) => {
  const evidence = loadRuntimeAssets(root), fonts = runtimeBundleFiles(evidence, 'fonts'), ocr = runtimeBundleFiles(evidence, 'ocr');
  assert.equal(fonts.get('notices/LICENSE.txt').toString(), 'Synthetic original license\r\n');
  assert.equal(ocr.size, 1);
  assert.deepEqual(JSON.parse(ocr.get('inventory.json')).groups, []);
  assert.equal(JSON.parse(fonts.get('inventory.json')).legalApprovalGranted, false);
  assert.throws(() => runtimeBundleFiles(evidence, 'unknown'));
}));

test('changed font bytes fail instead of renewing an original hash', () => fixture(({ root }) => {
  writeFileSync(join(root, 'fonts/font.ttf'), 'changed');
  assert.throws(() => loadRuntimeAssets(root), /hash changed/);
}));
test('changed notice bytes fail and do not receive implicit approval', () => fixture(({ root, directory }) => {
  writeFileSync(join(directory, 'notices/LICENSE.txt'), 'changed');
  assert.throws(() => loadRuntimeAssets(root), /hash changed/);
}));
test('new unclassified runtime files fail coverage', () => fixture(({ root }) => {
  writeFileSync(join(root, 'fonts/new.ttf'), 'new');
  assert.throws(() => loadRuntimeAssets(root), /Unclassified/);
}));
test('versioned shared objects and uppercase suffixes are included in coverage', () => fixture(({ root, registry, save }) => {
  registry.scanRoots[0].extensions.push('.so', '.dll'); save();
  writeFileSync(join(root, 'fonts/libextra.so.3.1'), 'new');
  assert.throws(() => loadRuntimeAssets(root), /Unclassified/);
  rmSync(join(root, 'fonts/libextra.so.3.1')); writeFileSync(join(root, 'fonts/extra.DLL'), 'new');
  assert.throws(() => loadRuntimeAssets(root), /Unclassified/);
}));
test('missing required assets are not optional', () => fixture(({ root }) => {
  rmSync(join(root, 'fonts/font.ttf'));
  assert.throws(() => loadRuntimeAssets(root), /Missing runtime asset/);
}));
test('known local-only binaries may be absent without inventing provenance', () => fixture(({ root, registry, save }) => {
  registry.groups = [{ ...registry.groups[0], provenance: 'unresolved', source: null, notices: [],
    assets: [{ ...registry.groups[0].assets[0], optionalLocalFile: true }] }]; save();
  rmSync(join(root, 'fonts/font.ttf'));
  assert.deepEqual(loadRuntimeAssets(root).absentOptional, ['fonts/font.ttf']);
}));
test('unknown source cannot inherit an unrelated license', () => fixture(({ root, registry, save }) => {
  registry.groups[0].provenance = 'unresolved'; registry.groups[0].source = null; save();
  assert.throws(() => loadRuntimeAssets(root), /inherit an unrelated license/);
}));
test('Git blob proof is checked independently of a matching SHA256', () => fixture(({ root, registry, save }) => {
  registry.groups[0].assets[0].gitBlob = 'b'.repeat(40); save();
  assert.throws(() => loadRuntimeAssets(root));
}));
test('path traversal and duplicate assets fail', () => fixture(({ root, registry, save }) => {
  registry.groups[0].assets.push({ ...registry.groups[0].assets[0] }); save();
  assert.throws(() => loadRuntimeAssets(root), /Duplicate runtime asset/);
  registry.groups[0].assets.pop(); registry.groups[0].assets[0].file = '../escape.ttf'; save();
  assert.throws(() => loadRuntimeAssets(root), /Unsafe runtime asset path/);
}));
test('asset and dangling optional symlinks are rejected', () => fixture(({ root, registry, save }) => {
  const file = join(root, 'fonts/font.ttf'); rmSync(file); symlinkSync(join(root, 'absent.ttf'), file);
  registry.groups[0].provenance = 'unresolved'; registry.groups[0].source = null; registry.groups[0].notices = [];
  registry.groups[0].assets[0].optionalLocalFile = true; save();
  assert.throws(() => loadRuntimeAssets(root), /symlinks/);
}));
test('notice symlinks are rejected', () => fixture(({ root, directory }) => {
  const file = join(directory, 'notices/LICENSE.txt'); rmSync(file);
  symlinkSync(join(root, 'fonts/font.ttf'), file);
  assert.throws(() => loadRuntimeAssets(root), /symlinks/);
}));
test('generated bundles are deterministic and reject stale, missing or additional notices', () => fixture(({ root }) => {
  const evidence = loadRuntimeAssets(root), output = join(root, 'output');
  materializeRuntimeBundle(evidence, 'fonts', output);
  materializeRuntimeBundle(evidence, 'fonts', output, true);
  writeFileSync(join(output, 'inventory.json'), '{}');
  assert.throws(() => materializeRuntimeBundle(evidence, 'fonts', output, true), /drift/);
  materializeRuntimeBundle(evidence, 'fonts', output);
  writeFileSync(join(output, 'extra.txt'), 'extra');
  assert.throws(() => materializeRuntimeBundle(evidence, 'fonts', output), /Unexpected/);
  rmSync(join(output, 'extra.txt')); rmSync(join(output, 'notices/LICENSE.txt'));
  assert.throws(() => materializeRuntimeBundle(evidence, 'fonts', output, true), /Missing/);
}));
test('claimed legal or publication approval is rejected', () => fixture(({ root, registry, save }) => {
  registry.publicationReady = true; save(); assert.throws(() => loadRuntimeAssets(root));
  registry.publicationReady = false; registry.legalApprovalGranted = true; save(); assert.throws(() => loadRuntimeAssets(root));
}));

test('shipping projects copy profile-specific notices without changing font/model bytes', () => {
  const read = path => readFileSync(new URL('../../' + path, import.meta.url), 'utf8');
  const api = read('PVAD.WebApi/PVAD.WebApi.csproj'), ocr = read('src/Importing/PVAD.FileImporter.ImageOcr/PVAD.FileImporter.ImageOcr.csproj');
  assert.match(api, /runtime-assets\/bundles\/fonts\/\*\*\/\*/);
  assert.match(ocr, /runtime-assets\/bundles\/ocr\/\*\*\/\*/);
  assert.match(ocr, /PackageCopyToOutput="true"/);
  assert.match(ocr, /PackagePath="contentFiles\/any\/\$\(TargetFramework\)\/third-party\/runtime-assets\/ocr\/"/);
  assert.match(ocr, /PackageFlatten="false"/);
  assert.doesNotMatch(ocr, /PackagePath="[^"\n]*%\(RecursiveDir\)/);
  assert.match(ocr, /CopyToPublishDirectory="PreserveNewest"/);
});

test('protected rehearsal verifies actual NuGet entries, copy metadata and consumer/worker notice bytes', () => {
  const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
  const runner = read('./joint-package-rehearsal.mjs'), consumer = read('./GeneratorPackageProbe/Program.cs');
  for (const field of ['originalWorker', 'protectedWorker', 'independentConsumer'])
    assert.ok(runner.includes('runtimeAssetNotices.' + field));
  assert.match(runner, /documentationPublicationReady: false, runtimeAssetNotices/);
  for (const check of ['package-runtime-notice-path:', 'package-runtime-notice-bytes:', 'package-runtime-notice-copy-contract:',
    'consumer-runtime-notice-exists:', 'consumer-runtime-notice-bytes:']) assert.ok(consumer.includes(check));
  assert.match(consumer, /any\/net10\.0\/third-party\/runtime-assets\/ocr\//);
});
