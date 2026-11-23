import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { loadRuntimeAssets } from './runtime-assets.mjs';
import { checkEvidence, compareModel, componentMarkersIn, containerArguments, correlateNative, coverageFor, deriveNoticeLinks, machoSections,
  checkSourceDescriptor, explainNativeDifference, noticeComponents, signatureIssuer, validateDifferences, validateNativeComponents,
  parseDpkgPackages, parseNoticeArchive, resolveNoticeMember, trainedDataOffsets, validateEvidence, validateNoticeLinks,
  validateComponentSources, validateFontBuild, validateObservation, verifyComponentPayloadBytes, verifyDownloads } from './runtime-source-evidence.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const evidenceRoot = join(root, 'assets/licenses/runtime-assets');
const assets = loadRuntimeAssets(root);
const readAsset = file => readFileSync(join(root, file));
const readObservation = file => readFileSync(join(evidenceRoot, file));
const evidence = JSON.parse(readFileSync(join(evidenceRoot, 'source-evidence.v1.json')));
const mac = JSON.parse(readObservation('evidence/macos-local-builds.v1.json'));
const container = JSON.parse(readObservation('evidence/container-local-os.v1.json'));
const containerSha = sha(readObservation('evidence/container-local-os.v1.json'));
const links = JSON.parse(readObservation('evidence/container-notice-links.v1.json'));
const components = JSON.parse(readObservation('evidence/native-components.v1.json'));
const differences = JSON.parse(readObservation('evidence/macos-build-differences.v1.json'));
const componentSources = JSON.parse(readObservation('component-sources/tesseract-5.2.0/component-sources.v1.json'));
const fontBuild = JSON.parse(readObservation('evidence/liberation-sans-candidate-build.v1.json'));

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

// Builds a GNU-format tar from [name, type, data/target] rows, as the historical archive uses.
function tar(rows, { magic = 'ustar  \0', corrupt = false } = {}) {
  const blocks = [];
  for (const [name, type, value = ''] of rows) {
    const header = Buffer.alloc(512), data = type === '0' ? Buffer.from(value) : Buffer.alloc(0);
    header.write(name, 0, 100, 'latin1'); header.write('0000644\0', 100); header.write('0000000\0', 108); header.write('0000000\0', 116);
    header.write(data.length.toString(8).padStart(11, '0') + '\0', 124); header.write('00000000000\0', 136);
    header.write(type, 156, 1, 'latin1'); if (type === '2' || type === '1') header.write(value, 157, 100, 'latin1');
    header.write(magic, 257, 8, 'latin1'); header.fill(32, 148, 156);
    let sum = 0; for (const byte of header) sum += byte;
    header.write((sum + (corrupt ? 1 : 0)).toString(8).padStart(6, '0') + '\0 ', 148, 8, 'latin1');
    blocks.push(header, data, Buffer.alloc((512 - data.length % 512) % 512));
  }
  return Buffer.concat([...blocks, Buffer.alloc(1024)]);
}
const doc = 'usr/share/doc/';
const baseRows = [[doc, '5'], [doc + 'base/', '5'], [doc + 'base/copyright', '0', 'Original text'],
  [doc + 'dirlink', '2', 'base'], [doc + 'file/', '5'], [doc + 'file/copyright', '2', '../base/copyright'],
  [doc + 'empty/', '5'], [doc + 'empty/copyright', '0', ''], [doc + 'folder/', '5'], [doc + 'folder/copyright/', '5'],
  ['usr/share/common-licenses/', '5'], ['usr/share/common-licenses/GPL-3', '0', 'GPL text'], ['usr/share/common-licenses/GPL', '2', 'GPL-3']];

test('notice links distinguish original text, file and directory links, empty members, missing notices and directories', () => {
  const entries = parseNoticeArchive(tar(baseRows));
  const original = resolveNoticeMember(entries, doc + 'base/copyright');
  assert.equal(original.status, 'original-text'); assert.equal(original.sha256, sha(Buffer.from('Original text')));
  const directory = resolveNoticeMember(entries, doc + 'dirlink/copyright');
  assert.equal(directory.status, 'original-text-through-link'); assert.equal(directory.member, doc + 'base/copyright');
  assert.deepEqual(directory.chain, [{ link: doc + 'dirlink', target: 'base', kind: 'directory-link' }]);
  const file = resolveNoticeMember(entries, doc + 'file/copyright');
  assert.equal(file.member, doc + 'base/copyright'); assert.equal(file.chain[0].kind, 'file-link');
  assert.equal(resolveNoticeMember(entries, doc + 'empty/copyright').status, 'empty-member');
  assert.equal(resolveNoticeMember(entries, doc + 'empty/copyright').sha256, null);
  assert.equal(resolveNoticeMember(entries, doc + 'absent/copyright').status, 'missing-notice');
  assert.equal(resolveNoticeMember(entries, doc + 'folder/copyright').status, 'directory-not-notice');
  assert.equal(resolveNoticeMember(entries, 'usr/share/common-licenses/GPL').member, 'usr/share/common-licenses/GPL-3');
});

test('notice archive parsing rejects traversal, unsupported entries, duplicates, oversized members and damaged headers', () => {
  const rejected = [
    [[doc + '../etc/passwd', '0', 'x']],                         // traversal
    [['/usr/share/doc/x/copyright', '0', 'x']],                  // absolute
    [['etc/passwd', '0', 'x']],                                  // outside the notice roots
    [[doc + 'hard', '1', 'usr/share/doc/base/copyright']],       // hard link
    [[doc + 'device', '3']],                                     // device
    [[doc + 'x', '0', 'a'], [doc + 'x', '0', 'b']],              // duplicate file
    [[doc + 'dir', '0', 'x'], [doc + 'dir/', '5']],              // duplicate as file and directory
  ];
  for (const rows of rejected) assert.throws(() => parseNoticeArchive(tar(rows)), /Unsafe|Unexpected|Unsupported|Duplicate/);
  assert.throws(() => parseNoticeArchive(tar([[doc + 'big', '0', 'abcdef']]), { memberLimit: 4 }), /Oversized/);
  assert.throws(() => parseNoticeArchive(tar(baseRows, { corrupt: true })), /checksum/);
  assert.throws(() => parseNoticeArchive(tar(baseRows, { magic: 'notatar\0' })), /format/);
  assert.throws(() => parseNoticeArchive(tar(baseRows).subarray(0, tar(baseRows).length - 1024)), /end marker/);
});

test('notice archives require two end blocks and reject appended or hidden trailing data', () => {
  const archive = tar(baseRows);
  assert.throws(() => parseNoticeArchive(archive.subarray(0, archive.length - 512)), /end marker/);
  const interrupted = Buffer.from(archive); interrupted[interrupted.length - 512] = 1;
  assert.throws(() => parseNoticeArchive(interrupted), /end marker|trailing/);
  assert.throws(() => parseNoticeArchive(Buffer.concat([archive, tar([[doc + 'hidden', '0', 'data']])])), /trailing/);
  assert.equal(parseNoticeArchive(Buffer.concat([archive, Buffer.alloc(1024)])).size, baseRows.length);
});

test('notice link resolution rejects absolute, escaping, root-leaving and cyclic links instead of following them', () => {
  const cases = [[[doc + 'a', '2', '/etc']], [[doc + 'a', '2', '../../../../etc']], [[doc + 'a', '2', '../../../tmp']],
    [[doc + 'a', '2', 'b'], [doc + 'b', '2', 'a']], [[doc + 'a', '2', 'a']]];
  for (const rows of cases) assert.throws(() => resolveNoticeMember(parseNoticeArchive(tar([[doc, '5'], ...rows])), doc + 'a/copyright'),
    /Absolute|Escaping|leaves the notice roots|cycle|too long/);
});

test('derived mappings require the historical archive and the historical notice hashes', () => {
  const observation = { ...structuredClone(container), notices: [{ file: doc + 'base/copyright', bytes: 13, sha256: sha(Buffer.from('Original text')),
    status: 'original-text-retained-unreviewed' }, { file: doc + 'file/copyright', bytes: 0, sha256: null, status: 'link-or-empty-entry-needs-review' }],
    packages: [{ name: 'base:arm64', version: '1', source: 'src', sourceVersion: '1', architecture: 'arm64', copyright: null },
      { name: 'file:arm64', version: '1', source: 'src', sourceVersion: '1', architecture: 'arm64', copyright: null }] };
  const archive = tar(baseRows);
  assert.throws(() => deriveNoticeLinks(archive, observation, 'a'.repeat(64)), /historical observation/);
  observation.noticeArchiveSha256 = sha(archive);
  const record = deriveNoticeLinks(archive, observation, 'a'.repeat(64));
  const file = record.packageMappings.find(value => value.package === 'file:arm64');
  assert.equal(file.memberPackage.name, 'base:arm64'); assert.equal(file.sameSourceAndVersion, true); assert.equal(file.agreementInferred, false);
  observation.notices[0].sha256 = 'b'.repeat(64);
  assert.throws(() => deriveNoticeLinks(archive, observation, 'a'.repeat(64)), /differs from the historical notice record/);
});

test('recorded notice links resolve all five ARM64 packages to same-source originals without approval or a changed snapshot', () => {
  assert.deepEqual(validateNoticeLinks(links, container, containerSha), { packages: 162, unresolvedBefore: 5, resolvedByLink: 5,
    unresolvedAfter: 0, licenseLinksResolved: 3, licenseLinksUnresolved: 0 });
  assert.deepEqual(links.packageMappings.map(value => value.package).sort(),
    ['libgcc-s1:arm64', 'libgomp1:arm64', 'libncursesw6:arm64', 'libstdc++6:arm64', 'openssl']);
  assert.ok(links.packageMappings.every(value => value.sameSourceAndVersion && value.chain.length === 1));
  const mutations = [record => { record.packageMappings[0].sha256 = 'c'.repeat(64); }, record => { record.packageMappings[0].agreementInferred = true; },
    record => { record.packageMappings.pop(); }, record => { record.counts.unresolvedAfter = 1; }, record => { record.legalApprovalGranted = true; },
    record => { record.historicalObservationSha256 = 'd'.repeat(64); }, record => { record.packageMappings[0].chain[0].target = '/etc'; },
    record => { record.packageMappings[0].sameSourceAndVersion = false; }, record => { record.licenseLinks[0].member = doc + 'base/copyright'; }];
  for (const mutate of mutations) {
    const record = structuredClone(links); mutate(record);
    assert.throws(() => validateNoticeLinks(record, container, containerSha));
  }
  assert.throws(() => validateNoticeLinks(links, container, 'e'.repeat(64)), /another historical observation/);
});

test('recorded notice chains must replay exactly to their declared member and status', () => {
  const mutations = [record => { record.packageMappings[0].chain[0].target = '../../../../etc'; },
    record => { record.packageMappings[0].chain[0].target = 'libssl3t64'; },
    record => { record.packageMappings[0].chain[0].link = doc + 'unrelated'; },
    record => { record.packageMappings[0].chain[0].kind = 'file-link'; },
    record => { record.packageMappings[0].chain.push(record.packageMappings[0].chain[0]); },
    record => { record.licenseLinks[0].chain[0].target = 'GPL-3'; }];
  for (const mutate of mutations) {
    const record = structuredClone(links); mutate(record);
    assert.throws(() => validateNoticeLinks(record, container, containerSha));
  }
  const multiHop = structuredClone(links), first = multiHop.packageMappings[0];
  const target = first.chain[0].target;
  first.chain[0].target = 'alias';
  first.chain.push({ link: doc + 'alias', target, kind: 'directory-link' });
  assert.deepEqual(validateNoticeLinks(multiHop, container, containerSha), links.counts);
});

test('component markers report versions from fixed identification strings and do not treat absence as proof', () => {
  const bytes = Buffer.from('\0libpng version 1.6.44\0inflate 1.3.0.1 Copyright\0LIBTIFF, Version 4.4.0\0leptonica-1.82.0.\0Liblzma\0');
  assert.deepEqual(componentMarkersIn(bytes), [
    { component: 'zlib', versions: ['1.3.0.1'] }, { component: 'libpng', versions: ['1.6.44'] },
    { component: 'libtiff', versions: ['4.4.0'] }, { component: 'leptonica', versions: ['1.82.0'] }, { component: 'liblzma', versions: [] }]);
  assert.deepEqual(componentMarkersIn(Buffer.from('no recognizable component text')), []);
  assert.deepEqual(noticeComponents('# libpng\ntext\n# END: libpng\n# zlib\n# END: zlib\n# END: zlib\n'), ['libpng', 'zlib']);
  const notices = [{ components: ['libpng', 'zlib'] }];
  assert.equal(coverageFor('zlib', notices), 'named-in-retained-notice');
  assert.equal(coverageFor('libtiff', notices), 'not-named-in-retained-notice');
  assert.equal(coverageFor('zlib', []), 'no-retained-notice-file');
});

test('recorded native components keep the Tesseract component gap visible without approval', () => {
  const counts = validateNativeComponents(components);
  assert.equal(counts.assets, 45); assert.equal(counts.packages, 8);
  const tesseract = components.packages.find(entry => entry.key === 'Tesseract/5.2.0');
  assert.ok(tesseract.components.length > 0 && tesseract.components.every(value => value.noticeCoverage !== 'named-in-retained-notice'));
  const mutations = [record => { record.componentRightsVerified = true; }, record => { record.markerAbsenceProvesAbsence = true; },
    record => { record.packages.find(entry => entry.key === 'Tesseract/5.2.0').components[0].noticeCoverage = 'named-in-retained-notice'; },
    record => { record.counts['not-named-in-retained-notice'] = 0; }, record => { record.packages[0].assets[0].sha256 = 'not-a-hash'; },
    record => { record.packages[4].assets[0].markers.push({ component: 'invented', versions: [] }); }];
  for (const mutate of mutations) {
    const record = structuredClone(components); mutate(record);
    assert.throws(() => validateNativeComponents(record));
  }
});

test('native component versions and unique safe asset paths are independently reconciled', () => {
  const mutations = [record => { record.packages.find(entry => entry.components.length).components[0].versions = ['9999.0']; },
    record => { record.packages[0].assets[0].file = '../../private'; },
    record => { record.packages[0].assets[0].file = 'C:\\private'; },
    record => { record.packages[0].assets[0].file = '/private'; },
    record => { record.packages[0].assets[1].file = record.packages[0].assets[0].file; },
    record => { const asset = record.packages.flatMap(entry => entry.assets).find(value => value.markers.length);
      asset.markers.push(structuredClone(asset.markers[0])); }];
  for (const mutate of mutations) {
    const record = structuredClone(components); mutate(record); assert.throws(() => validateNativeComponents(record));
  }
});

function explainedNative(signed = false) {
  const bytes = Buffer.alloc(384);
  bytes.writeUInt32LE(0xfeedfacf, 0); bytes.writeUInt32LE(0x01000007, 4);
  bytes.writeUInt32LE(signed ? 3 : 2, 16); bytes.writeUInt32LE(signed ? 192 : 176, 20);
  bytes.writeUInt32LE(0x19, 32); bytes.writeUInt32LE(152, 36); bytes.writeUInt32LE(1, 96);
  bytes.write('__text', 104); bytes.write('__TEXT', 120); bytes.writeBigUInt64LE(4n, 144); bytes.writeUInt32LE(256, 152);
  bytes.writeUInt32LE(0x1b, 184); bytes.writeUInt32LE(24, 188);
  Buffer.from('12345678123412341234123456789abc', 'hex').copy(bytes, 192);
  if (signed) {
    bytes.writeUInt32LE(0x1d, 208); bytes.writeUInt32LE(16, 212);
    bytes.writeUInt32LE(320, 216); bytes.writeUInt32LE(8, 220);
  }
  bytes.write('code', 256);
  return { bytes, commands: native().commands.replace('offset 32', 'offset 256') };
}

test('build differences are explained only when every differing byte lies in load commands or the code signature', () => {
  const { bytes, commands: signed } = explainedNative(true);
  const header = Buffer.from(bytes); header[12] = 7;
  const confined = explainNativeDifference(bytes, signed, header, signed);
  assert.equal(confined.classification, 'differences-confined-to-load-commands-and-signature');
  assert.deepEqual(confined.regions, { headerAndLoadCommands: 1, codeSignature: 0, other: 0 });
  const signature = Buffer.from(bytes); signature[322] = 9;
  assert.deepEqual(explainNativeDifference(bytes, signed, signature, signed).regions, { headerAndLoadCommands: 0, codeSignature: 1, other: 0 });
  const { bytes: unsigned, commands } = explainedNative();
  const outside = Buffer.from(unsigned); outside[280] = 1;
  assert.equal(explainNativeDifference(unsigned, commands, outside, commands).classification, 'differences-outside-metadata');
  assert.equal(explainNativeDifference(unsigned, commands, Buffer.concat([unsigned, Buffer.alloc(1)]), commands).classification, 'different-length-not-explained');
  const code = Buffer.from(unsigned); code[256] ^= 1;
  assert.throws(() => explainNativeDifference(unsigned, commands, code, commands), /Only correlated builds/);
  assert.equal(confined.publisherAuthenticated, false); assert.equal(confined.reproducibleSourceBuildVerified, false);
});

test('Mach-O metadata bounds must agree with the actual commands, sections and signature payload', () => {
  const { bytes, commands } = explainedNative();
  for (const mutate of [data => data.writeUInt32LE(0xffffffff, 20), data => data.writeUInt32LE(184, 20),
    data => data.writeUInt32LE(3, 16), data => data.writeUInt32LE(0, 36),
    data => data.writeUInt32LE(160, 36), data => data.writeUInt32LE(200, 152)]) {
    const changed = Buffer.from(bytes); mutate(changed); changed[280] = 7;
    assert.throws(() => explainNativeDifference(bytes, commands, changed, commands), /Mach-O/);
  }
  const signed = explainedNative(true);
  for (const mutate of [data => data.writeUInt32LE(380, 216), data => data.writeUInt32LE(0, 220), data => data.writeUInt32LE(1, 216)]) {
    const changed = Buffer.from(signed.bytes); mutate(changed);
    assert.throws(() => explainNativeDifference(signed.bytes, signed.commands, changed, signed.commands), /signature/);
  }
  const moved = Buffer.from(bytes);
  moved.writeUInt32LE(3, 16); moved.writeUInt32LE(240, 20);
  moved.writeUInt32LE(0x2, 208); moved.writeUInt32LE(64, 212); moved.writeUInt32LE(320, 152);
  moved.fill(0, 256, 260); moved.write('code', 320);
  assert.throws(() => explainNativeDifference(bytes, commands, moved, commands.replace('offset 256', 'offset 320')), /overlap/);
});

test('recorded macOS differences explain all fifteen local libraries without authenticating them', () => {
  assert.deepEqual(validateDifferences(differences, assets), { explained: 15, total: 15 });
  for (const mutate of [record => { record.rows[0].publisherAuthenticated = true; }, record => { record.rows[0].regions.other = 1; },
    record => { record.rows.pop(); }, record => { record.rows[0].localSha256 = 'f'.repeat(64); }, record => { record.legalApprovalGranted = true; }]) {
    const record = structuredClone(differences); mutate(record);
    assert.throws(() => validateDifferences(record, assets));
  }
});

function armoredSignature() {
  const fingerprint = Buffer.from('B8BF54137B09D35CF026FE9D091AB856069AAA1C', 'hex');
  const hashed = Buffer.concat([Buffer.from([22, 33, 4]), fingerprint]);
  const unhashed = Buffer.concat([Buffer.from([9, 16]), fingerprint.subarray(12)]);
  const body = Buffer.concat([Buffer.from([4, 1, 1, 10, 0, hashed.length]), hashed, Buffer.from([0, unhashed.length]), unhashed, Buffer.from([0x7f, 0xb2, 0, 1, 1])]);
  const packet = Buffer.concat([Buffer.from([0xc2, body.length]), body]);
  return `-----BEGIN PGP SIGNATURE-----\n\n${packet.toString('base64')}\n=AAAA\n-----END PGP SIGNATURE-----\n`;
}
function descriptor(archives, change = text => text) {
  const lines = algorithm => [...archives].map(([name, bytes]) => ` ${createHash(algorithm).update(bytes).digest('hex')} ${bytes.length} ${name}`).join('\n');
  return Buffer.from(change(`-----BEGIN PGP SIGNED MESSAGE-----\nHash: SHA512\n\nFormat: 3.0 (quilt)\nSource: synthetic\nChecksums-Sha1:\n${lines('sha1')}\nChecksums-Sha256:\n${lines('sha256')}\nFiles:\n${lines('md5')}\n\n${armoredSignature()}`));
}

test('the source descriptor check recomputes every declared checksum and reports, but never trusts, the signer', () => {
  const archives = new Map([['synthetic.orig.tar.gz', Buffer.from('original')], ['synthetic.debian.tar.xz', Buffer.from('debian')]]);
  const result = checkSourceDescriptor(descriptor(archives), archives);
  assert.equal(result.checksumsVerified, true); assert.equal(result.signatureVerified, false);
  assert.equal(result.signatureStatus, 'blocked-no-authenticated-keyring');
  assert.deepEqual(result.signer, { publicKeyAlgorithm: 1, hashAlgorithm: 10, issuerFingerprint: 'B8BF54137B09D35CF026FE9D091AB856069AAA1C',
    issuerKeyId: '091AB856069AAA1C', trusted: false });
  const changed = new Map(archives); changed.set('synthetic.orig.tar.gz', Buffer.from('replaced'));
  assert.throws(() => checkSourceDescriptor(descriptor(archives), changed), /differs/);
  assert.throws(() => checkSourceDescriptor(descriptor(archives), new Map([...archives, ['extra.tar.gz', Buffer.from('x')]])), /differ from the descriptor/);
  assert.throws(() => checkSourceDescriptor(descriptor(archives, text => text.replace('-----BEGIN PGP SIGNED MESSAGE-----\n', '')), archives), /clearsigned/);
  assert.throws(() => checkSourceDescriptor(descriptor(archives, text => text.replace(/Files:\n[^]*?\n\n/, '\n')), archives), /Missing Files/);
  assert.throws(() => signatureIssuer('no signature'), /Missing armored signature/);
});

test('every descriptor algorithm must cover every supplied archive once with no duplicate blocks', () => {
  const archives = new Map([['synthetic.orig.tar.gz', Buffer.from('original')], ['synthetic.debian.tar.xz', Buffer.from('debian')]]);
  for (const field of ['Checksums-Sha256', 'Checksums-Sha1', 'Files']) {
    assert.throws(() => checkSourceDescriptor(descriptor(archives, text => text.replace(new RegExp(`(${field}:\\n)[^\\n]*\\n`), '$1')), archives));
    assert.throws(() => checkSourceDescriptor(descriptor(archives, text => text.replace(new RegExp(`(${field}:\\n)([^\\n]*\\n)`), '$1$2$2')), archives));
    assert.throws(() => checkSourceDescriptor(descriptor(archives, text => text.replace(new RegExp(`(${field}:\\n(?: [^\\n]*\\n)+)`), '$1$1')), archives));
  }
});

test('recorded font evidence keeps the Debian signature blocked while binding the descriptor checksums', () => {
  assert.equal(evidence.fonts.descriptor.signatureStatus, 'blocked-no-authenticated-keyring');
  for (const mutate of [value => { value.fonts.descriptor.signatureVerified = true; }, value => { value.fonts.descriptor.signer.trusted = true; },
    value => { value.fonts.descriptor.checksumsVerified = false; }, value => { value.fonts.descriptor.signer.issuerKeyId = 'ABCDEF0123456789'; }]) {
    const changed = structuredClone(evidence); mutate(changed);
    assert.throws(() => validateEvidence(changed, assets, readAsset, readObservation));
  }
});

test('Tesseract component sources keep candidate tags, inferred versions and the unresolved liblzma gap distinct', () => {
  assert.deepEqual(validateComponentSources(componentSources, components, readAsset), { components: 7, withNotices: 6, unresolved: 1 });
  const byName = Object.fromEntries(componentSources.components.map(value => [value.component, value]));
  assert.equal(byName.libpng.status, 'candidate-version-inferred');
  assert.equal(byName.liblzma.status, 'unresolved-version-not-observed');
  assert.equal(byName.tesseract.status, 'license-text-identical-across-candidates-version-ambiguous');
  const mutations = [
    record => { record.legalApprovalGranted = true; },
    record => { record.sourceBuildVerified = true; },
    record => { record.components.find(value => value.component === 'zlib').notices[0].sha256 = '0'.repeat(64); },
    record => { record.components.find(value => value.component === 'libpng').status = 'candidate-version-association'; },
    record => { record.components.find(value => value.component === 'libtiff').candidateSource.tag = 'v4.4.1'; },
    record => { record.components.find(value => value.component === 'libtiff').observed.version = '4.5.0'; },
    record => { record.components = record.components.filter(value => value.component !== 'liblzma'); },
    record => { record.components.find(value => value.component === 'liblzma').notices = structuredClone(byName.zlib.notices); },
    record => { record.components.find(value => value.component === 'zlib').notices[0].url = byName.zlib.notices[0].url.replace(byName.zlib.candidateSource.commit, 'a'.repeat(40)); },
    record => { record.artifactAssociation.payloads[0].sha256 = '1'.repeat(64); },
    record => { record.artifactAssociation.buildRecipeInRepository = true; },
    record => { record.components.push(structuredClone(record.components[0])); },
    record => { record.components.find(value => value.component === 'tesseract').notices[0].sameBytesAt = ['5.0.0']; },
  ];
  for (const mutate of mutations) {
    const record = structuredClone(componentSources); mutate(record);
    assert.throws(() => validateComponentSources(record, components, readAsset));
  }
  const changedNotice = file => file.endsWith('leptonica-license.txt') ? Buffer.concat([readAsset(file), Buffer.from(' ')]) : readAsset(file);
  assert.throws(() => validateComponentSources(componentSources, components, changedNotice), /changed|differ/i);
});

test('unknown component versions cannot borrow an unrelated ambiguous license', () => {
  const record = structuredClone(componentSources);
  const unknown = record.components.find(value => value.component === 'liblzma');
  const tesseract = record.components.find(value => value.component === 'tesseract');
  unknown.status = tesseract.status;
  unknown.candidateSources = structuredClone(tesseract.candidateSources);
  unknown.notices = structuredClone(tesseract.notices);
  assert.throws(() => validateComponentSources(record, components, readAsset));
});

test('notice origins and paths must match the exact component repository and immutable commit', () => {
  const zlib = componentSources.components.find(value => value.component === 'zlib');
  for (const url of [
    `http://unrelated.invalid/${zlib.candidateSource.commit}/LICENSE`,
    `https://unrelated.invalid/${zlib.candidateSource.commit}/LICENSE`,
    zlib.notices[0].url + '?redirect=other', zlib.notices[0].url + '#unrelated',
    zlib.notices[0].url.replace('/madler/zlib/', '/other/zlib/'),
    zlib.notices[0].url.replace('/LICENSE', '/OTHER'),
  ]) {
    const record = structuredClone(componentSources);
    record.components.find(value => value.component === 'zlib').notices[0].url = url;
    assert.throws(() => validateComponentSources(record, components, readAsset));
  }
  const record = structuredClone(componentSources);
  record.artifactAssociation.payloads[0].gitBlob = '0'.repeat(40);
  assert.throws(() => validateComponentSources(record, components, readAsset));
  const borrowed = structuredClone(componentSources);
  const source = borrowed.components.find(value => value.component === 'zlib');
  source.candidateSource.repository = borrowed.components.find(value => value.component === 'tesseract').candidateSources[0].repository;
  assert.throws(() => validateComponentSources(borrowed, components, readAsset));
});

test('the Liberation Sans candidate rebuild is equivalent only within build metadata and never claims reproduction', () => {
  assert.deepEqual(validateFontBuild(fontBuild, assets, evidence, readAsset), { faces: 4, byteReproduced: false });
  const mutations = [
    record => { record.byteReproduced = true; },
    record => { record.faces[0].glyphOutlinesEqual = false; },
    record => { record.faces[0].differingTables.push('glyf'); },
    record => { record.faces[0].cmapMappingsEqual = false; },
    record => { record.faces[0].headFieldsDiffering.push('fontRevision'); },
    record => { record.faces[0].shippedSha256 = '2'.repeat(64); },
    record => { record.source.signatureVerified = true; },
    record => { record.shippedFontsReplaced = true; },
    record => { record.faces.pop(); },
    record => { record.faces[0].builtSha256 = record.faces[0].shippedSha256; },
  ];
  for (const mutate of mutations) {
    const record = structuredClone(fontBuild); mutate(record);
    assert.throws(() => validateFontBuild(record, assets, evidence, readAsset));
  }
});

test('font equivalence requires true booleans and a glyph count matching the shipped font', () => {
  for (const field of ['glyphOutlinesEqual', 'cmapSubtablesEqual', 'cmapMappingsEqual']) {
    for (const value of ['false', 'true', 0, 1, null, {}, []]) {
      const record = structuredClone(fontBuild); record.faces[0][field] = value;
      assert.throws(() => validateFontBuild(record, assets, evidence, readAsset));
    }
  }
  for (const count of [0, -1, 1.5, '681', 682]) {
    const record = structuredClone(fontBuild); record.faces[0].glyphCount = count;
    assert.throws(() => validateFontBuild(record, assets, evidence, readAsset));
  }
});

test('payload verification recomputes SHA256, byte lengths and Git blob IDs from actual bytes', () => {
  const bytes = Buffer.from('synthetic DLL payload, never executable');
  const record = { file: 'x64/synthetic.dll', bytes: bytes.length, sha256: sha(bytes),
    gitBlob: createHash('sha1').update(Buffer.from(`blob ${bytes.length}\0`)).update(bytes).digest('hex') };
  assert.deepEqual(verifyComponentPayloadBytes([record], () => bytes),
    { payloads: 1, localPayloadHashesRecomputed: true, upstreamFetched: false, publicationReady: false });
  for (const change of [{ bytes: record.bytes + 1 }, { sha256: '0'.repeat(64) }, { gitBlob: '0'.repeat(40) },
    { file: '../private' }, { file: 'C:\\private' }])
    assert.throws(() => verifyComponentPayloadBytes([{ ...record, ...change }], () => bytes));
  assert.throws(() => verifyComponentPayloadBytes([record, record], () => bytes));
  assert.throws(() => verifyComponentPayloadBytes([record], () => Buffer.from('changed')));
});

test('the payload reference is hash-pinned by the offline evidence index', () => {
  for (const mutate of [record => { record.componentPayloadReference.sha256 = '0'.repeat(64); },
    record => { record.componentPayloadReference.file = '../private'; }]) {
    const record = structuredClone(evidence); mutate(record);
    assert.throws(() => validateEvidence(record, assets, readAsset, readObservation));
  }
});

test('font glyph counts are read from a bounded TrueType maxp table', () => {
  const font = Buffer.alloc(34);
  font.writeUInt32BE(0x00010000, 0); font.writeUInt16BE(1, 4); font.write('maxp', 12);
  font.writeUInt32BE(28, 20); font.writeUInt32BE(6, 24);
  font.writeUInt32BE(0x00010000, 28); font.writeUInt16BE(681, 32);
  const check = bytes => {
    const record = structuredClone(fontBuild), registry = structuredClone(assets.registry);
    const first = record.faces[0]; first.shippedSha256 = sha(bytes);
    registry.groups.find(group => group.id === 'liberation-sans-1-07-4').assets.find(asset => asset.file === first.file).sha256 = sha(bytes);
    return validateFontBuild(record, { registry }, evidence, file => file === first.file ? bytes : readAsset(file));
  };
  assert.equal(check(font).faces, 4);
  for (const mutate of [bytes => bytes.writeUInt16BE(2, 4), bytes => bytes.writeUInt32BE(30, 20),
    bytes => bytes.writeUInt32BE(5, 24), bytes => bytes.writeUInt16BE(0, 32)]) {
    const changed = Buffer.from(font); mutate(changed); assert.throws(() => check(changed));
  }
  assert.throws(() => check(font.subarray(0, 6)));
});
