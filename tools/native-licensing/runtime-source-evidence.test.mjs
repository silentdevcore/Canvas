import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { loadRuntimeAssets } from './runtime-assets.mjs';
import { checkEvidence, compareModel, containerArguments, correlateNative, machoSections,
  parseDpkgPackages, trainedDataOffsets, validateEvidence, validateObservation, verifyDownloads } from './runtime-source-evidence.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const evidenceRoot = join(root, 'assets/licenses/runtime-assets');
const assets = loadRuntimeAssets(root);
const readAsset = file => readFileSync(join(root, file));
const readObservation = file => readFileSync(join(evidenceRoot, file));
const evidence = JSON.parse(readFileSync(join(evidenceRoot, 'source-evidence.v1.json')));
const mac = JSON.parse(readObservation('evidence/macos-local-builds.v1.json'));
const container = JSON.parse(readObservation('evidence/container-local-os.v1.json'));

function model() {
  const bytes = Buffer.alloc(80, 3);
  bytes.writeUInt32LE(2, 0); bytes.writeBigInt64LE(20n, 4); bytes.writeBigInt64LE(60n, 12);
  return bytes;
}
const pin = bytes => ({ bytes: bytes.length, sha256: sha(bytes),
  gitBlob: createHash('sha1').update(Buffer.from(`blob ${bytes.length}\0`)).update(bytes).digest('hex') });
function native() {
  const bytes = Buffer.alloc(64); bytes.writeUInt32LE(0xfeedfacf, 0); bytes.writeUInt32LE(0x01000007, 4);
  bytes.write('code', 32);
  const commands = `Load command 0
 uuid 12345678-1234-1234-1234-123456789ABC
Section
 sectname __text
 segname __TEXT
 size 0x4
 offset 32
 flags 0x80000400
Section
 sectname __bss
 segname __DATA
 size 0x100
 offset 0
 flags 0x1
`;
  return { bytes, commands };
}

test('model comparison distinguishes exact, truncated and unrelated bytes without granting usability', () => {
  const full = model(), local = full.subarray(0, 40);
  const result = compareModel(local, full, pin(full));
  assert.equal(result.relation, 'truncated-reference-prefix'); assert.equal(result.missingBytes, 40);
  assert.deepEqual(result.localDirectory.outOfBounds, [{ component: 1, offset: '60' }]);
  for (const flag of ['modelUsabilityCertified', 'legalApprovalGranted', 'publicationReady']) assert.equal(result[flag], false);
  assert.equal(compareModel(full, full, pin(full)).relation, 'exact-file-match');
  const unrelated = Buffer.from(full); unrelated[30] ^= 1;
  assert.equal(compareModel(unrelated, full, pin(full)).relation, 'different-bytes');
  assert.equal(compareModel(unrelated, full, pin(full)).missingBytes, null);
});
test('reference SHA256, size, Git blob and component bounds are independent checks', () => {
  const full = model();
  for (const patch of [{ sha256: '0'.repeat(64) }, { bytes: 81 }, { gitBlob: '0'.repeat(40) }])
    assert.throws(() => compareModel(full, full, { ...pin(full), ...patch }));
  const broken = model(); broken.writeBigInt64LE(80n, 12);
  assert.throws(() => compareModel(broken, broken, pin(broken)));
});
test('component-directory inspection rejects unsupported headers and reports invalid offsets', () => {
  for (const bytes of [Buffer.alloc(1), Buffer.alloc(12), Buffer.alloc(64 * 1024 * 1024 + 1)])
    assert.throws(() => trainedDataOffsets(bytes));
  const bytes = model(); bytes.writeUInt32LE(65, 0); assert.throws(() => trainedDataOffsets(bytes));
  bytes.writeUInt32LE(2, 0); bytes.writeBigInt64LE(-1n, 4); bytes.writeBigInt64LE(19n, 12);
  assert.deepEqual(trainedDataOffsets(bytes).outOfBounds, [{ component: 1, offset: '19' }]);
  bytes.writeBigInt64LE(-2n, 12); assert.equal(trainedDataOffsets(bytes).declaredOffsetsInBounds, false);
});
test('native correlation separates matching UUID/sections from whole-file identity', () => {
  const { bytes, commands } = native(), editedHeader = Buffer.from(bytes); editedHeader[12] = 7;
  const result = correlateNative(bytes, commands, editedHeader, commands);
  assert.equal(result.uuidMatches, true); assert.equal(result.allSectionBytesMatch, true);
  assert.equal(result.exactFileMatch, false); assert.equal(result.sectionCount, 2);
  for (const flag of ['publisherAuthenticated', 'reproducibleSourceBuildVerified', 'publicationReady']) assert.equal(result[flag], false);
  assert.equal(correlateNative(bytes, commands, bytes, commands).exactFileMatch, true);
  editedHeader[32] ^= 1;
  assert.equal(correlateNative(bytes, commands, editedHeader, commands).allSectionBytesMatch, false);
  assert.equal(correlateNative(bytes, commands, bytes, commands.replace('789ABC', '789ABD')).uuidMatches, false);
  assert.equal(correlateNative(bytes, commands, bytes, commands.replace('size 0x100', 'size 0x101')).allSectionBytesMatch, false);
});
test('native parser rejects incomplete, duplicate, out-of-bounds and unsupported section metadata', () => {
  const { bytes, commands } = native();
  for (const text of [commands.replace('offset 32', 'offset 63'), commands.replace('flags 0x1', 'flags NaN'),
    commands.replace('size 0x4', 'size 0x4garbage'), commands.replace('offset 32', 'offset -1'),
    commands.replace('sectname __text', 'sectname __other'), commands + commands,
    commands.replace('uuid 12345678-1234-1234-1234-123456789ABC', ''), commands + 'Section' + commands.split('Section')[1]])
    assert.throws(() => machoSections(bytes, text));
  for (const content of [Buffer.alloc(1), Buffer.alloc(64)]) assert.throws(() => machoSections(content, commands));
  const arm = Buffer.from(bytes); arm.writeUInt32LE(0x0100000c, 4); assert.throws(() => machoSections(arm, commands));
});
test('installed-package parser retains binary/source versions and rejects ambiguous rows', () => {
  const row = 'libsample1:arm64\t1:2.3-4~test\tsample\t1:2.3-4~test\tarm64\tii \n';
  assert.deepEqual(parseDpkgPackages(row), [{ name: 'libsample1:arm64', version: '1:2.3-4~test',
    source: 'sample', sourceVersion: '1:2.3-4~test', architecture: 'arm64' }]);
  for (const text of ['', row + row, row.replace('ii ', 'rc '), row.replace('arm64\tii', '../arm\tii'),
    row.replace('sample\t1:', '\t1:'), row.replace(/\tarm64/, '\tarm64\textra'), 'x'.repeat(4 * 1024 * 1024 + 1)])
    assert.throws(() => parseDpkgPackages(text));
});
test('container observations use an immutable local image and non-root, network-free ephemeral commands', () => {
  const image = 'sha256:' + 'a'.repeat(64), name = 'pvad-source-12345678-1234-1234-1234-123456789abc';
  const args = containerArguments(image, '/usr/bin/dpkg-query', ['-W'], name);
  for (const flag of ['--rm', '--pull=never', '--read-only', '--no-healthcheck']) assert.ok(args.includes(flag));
  for (const [flag, value] of [['--network', 'none'], ['--user', '65534:65534'], ['--cap-drop', 'ALL'],
    ['--security-opt', 'no-new-privileges'], ['--memory', '256m'], ['--pids-limit', '64'], ['--cpus', '1']])
    assert.equal(args[args.indexOf(flag) + 1], value);
  assert.ok(!args.some(arg => ['-p', '-v', '--privileged', '--env-file'].includes(arg)));
  assert.equal(args.at(-2), image); assert.equal(args.at(-1), '-W');
  assert.throws(() => containerArguments('pvad-webapi:latest', '/bin/cat', [], name));
  assert.throws(() => containerArguments(image, '/bin/sh', [], name));
  assert.throws(() => containerArguments(image, '/bin/cat', [], 'shared-container'));
});
test('recorded evidence binds the unchanged asset registry without claiming a fresh download or legal approval', () => {
  const result = checkEvidence();
  assert.equal(result.localAssetBindingsChecked, true); assert.equal(result.downloadedReferencesRecomputed, false);
  assert.equal(result.publicationReady, false); assert.equal(result.evidence.model.missingBytes, 933326);
  assert.equal(result.evidence.fonts.debianSignatureVerified, false);
  assert.equal(mac.rows.filter(row => row.status === 'local-build-correlation').length, 15);
  assert.equal(container.packages.length, 162); assert.equal(container.notices.length, 176);
  assert.deepEqual(container.packages.filter(row => !row.copyright?.bytes).map(row => row.name),
    ['libgcc-s1:arm64', 'libgomp1:arm64', 'libncursesw6:arm64', 'libstdc++6:arm64', 'openssl']);
});
test('evidence rejects changed registry, model, source identities, snapshots and invented approval', () => {
  for (const change of [e => { e.publicationReady = true; }, e => { e.legalApprovalGranted = true; },
    e => { e.runtimeRegistrySha256 = '0'.repeat(64); }, e => { e.model.localBytes++; },
    e => { e.model.localSha256 = '0'.repeat(64); }, e => { e.model.missingBytes--; },
    e => { e.model.modelUsabilityCertified = true; }, e => { e.model.reference.bytes = -1; },
    e => { e.model.reference.url = e.model.reference.url.replace(/[a-f0-9]{40}/, 'main'); },
    e => { e.model.file = '../private'; }, e => { e.fonts.version = '2.1.5'; },
    e => { e.fonts.binaryPackageSha256 = '0'.repeat(64); }, e => { e.fonts.debianSignatureVerified = true; },
    e => { e.fonts.sourceRebuildVerified = true; }, e => { e.fonts.sourceFiles[0].url = 'https://example.org/source'; },
    e => { e.fonts.sourceFiles.pop(); }, e => { e.fonts.sourceFiles[0].bytes = 0; },
    e => { e.observations[0].sha256 = '0'.repeat(64); }, e => { e.observations[0].file = '../private.json'; },
    e => { e.observations.pop(); }]) {
    const altered = structuredClone(evidence); change(altered);
    assert.throws(() => validateEvidence(altered, assets, readAsset, readObservation));
  }
});
test('local native evidence cannot claim publisher authentication, missing assets or a reproducible build', () => {
  for (const change of [r => { r.rows.pop(); }, r => { r.rows.push(r.rows[0]); }, r => { r.rows[0].status = 'source-matched'; },
    r => { r.rows[0].candidates[0].localSha256 = '0'.repeat(64); },
    r => { r.rows[0].candidates[0].publisherAuthenticated = true; },
    r => { r.rows[0].candidates[0].reproducibleSourceBuildVerified = true; },
    r => { for (const c of r.rows[0].candidates) c.allSectionBytesMatch = false; }]) {
    const altered = structuredClone(mac); change(altered); assert.throws(() => validateObservation(altered, assets));
  }
});
test('container snapshot never equates notice retention with attribution, source or release approval', () => {
  for (const change of [r => { r.imageToCurrentDockerfileBindingVerified = true; }, r => { r.networkEnabled = true; },
    r => { r.appStarted = true; }, r => { r.containersRemoved = false; }, r => { r.image.id = 'pvad:latest'; },
    r => { r.sourcePackagesAcquired = true; }, r => { r.managedRuntimeAndStaticComponentReviewComplete = true; },
    r => { r.notices[0].status = 'approved'; }, r => { r.notices[0].file = 'usr/share/doc/../copyright'; },
    r => { r.packages[0].copyright = null; }, r => { r.notices.push(r.notices[0]); }]) {
    const altered = structuredClone(container); change(altered); assert.throws(() => validateObservation(altered, assets));
  }
});
test('download verification rejects missing, substituted and symlinked evidence rather than downloading implicitly', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-source-downloads-'));
  const file = join(directory, evidence.fonts.sourceFiles[0].name);
  try {
    assert.throws(() => verifyDownloads(directory), /ENOENT/);
    writeFileSync(file, 'substituted source'); assert.throws(() => verifyDownloads(directory));
    rmSync(file); symlinkSync(join(evidenceRoot, 'source-evidence.v1.json'), file);
    assert.throws(() => verifyDownloads(directory), /Bounded regular/);
    rmSync(file); mkdirSync(file); assert.throws(() => verifyDownloads(directory), /Bounded regular/);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});
