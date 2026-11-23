import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { loadRuntimeAssets } from './runtime-assets.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const evidencePath = 'assets/licenses/runtime-assets/source-evidence.v1.json';
const regularBytes = (file, limit = 64 * 1024 * 1024) => {
  const stat = lstatSync(file);
  assert.ok(stat.isFile() && !stat.isSymbolicLink() && stat.size > 0 && stat.size <= limit, 'Bounded regular evidence file required.');
  return readFileSync(file);
};

export function trainedDataOffsets(bytes) {
  assert.ok(Buffer.isBuffer(bytes) && bytes.length >= 12 && bytes.length <= 64 * 1024 * 1024);
  const slots = bytes.readUInt32LE(0), headerBytes = 4 + slots * 8;
  assert.ok(slots >= 1 && slots <= 64 && bytes.length >= headerBytes, 'Unsupported or truncated component directory.');
  const offsets = Array.from({ length: slots }, (_, index) => bytes.readBigInt64LE(4 + index * 8));
  const outOfBounds = offsets.flatMap((offset, index) => offset === -1n ? [] :
    offset < BigInt(headerBytes) || offset >= BigInt(bytes.length) ? [{ component: index, offset: offset.toString() }] : []);
  return { slots, headerBytes, outOfBounds, declaredOffsetsInBounds: outOfBounds.length === 0 };
}

export function compareModel(local, upstream, pin) {
  const localDirectory = trainedDataOffsets(local), upstreamDirectory = trainedDataOffsets(upstream);
  assert.equal(sha(upstream), pin.sha256, 'Reference model changed.');
  assert.equal(upstream.length, pin.bytes);
  assert.equal(createHash('sha1').update(Buffer.from(`blob ${upstream.length}\0`)).update(upstream).digest('hex'), pin.gitBlob);
  assert.equal(upstreamDirectory.declaredOffsetsInBounds, true);
  const prefix = local.length < upstream.length && local.equals(upstream.subarray(0, local.length));
  return { localBytes: local.length, referenceBytes: upstream.length, localSha256: sha(local), referenceSha256: sha(upstream),
    relation: local.equals(upstream) ? 'exact-file-match' : prefix ? 'truncated-reference-prefix' : 'different-bytes',
    missingBytes: prefix ? upstream.length - local.length : null, localDirectory,
    modelUsabilityCertified: false, legalApprovalGranted: false, publicationReady: false };
}

export function machoSections(bytes, commands) {
  assert.ok(Buffer.isBuffer(bytes) && bytes.length >= 8 && bytes.length <= 64 * 1024 * 1024);
  assert.ok(typeof commands === 'string' && Buffer.byteLength(commands) <= 4 * 1024 * 1024);
  assert.equal(bytes.readUInt32LE(0), 0xfeedfacf, 'Only thin little-endian Mach-O 64 is reviewed.');
  assert.equal(bytes.readUInt32LE(4), 0x01000007, 'Only the existing x64 binaries are reviewed.');
  const uuids = [...commands.matchAll(/^\s*uuid ([a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12})\s*$/gim)];
  const uuid = uuids[0]?.[1]?.toUpperCase();
  assert.equal(uuids.length, 1, 'Exactly one native build UUID is required.');
  assert.ok(uuid, 'Missing native build UUID.');
  const sections = commands.split(/^Section\s*$/m).slice(1).map(block => {
    const field = name => {
      const value = block.match(new RegExp(`^\\s*${name} (\\S+)`, 'm'))?.[1];
      assert.ok(value, 'Incomplete native section metadata.'); return value;
    };
    const name = field('segname') + '/' + field('sectname');
    const sizeText = field('size'), offsetText = field('offset'), flagText = field('flags');
    assert.match(sizeText, /^0x[a-f0-9]+$/i); assert.match(offsetText, /^\d+$/); assert.match(flagText, /^0x[a-f0-9]{1,8}$/i);
    const size = Number.parseInt(sizeText, 16), offset = Number(offsetText);
    const kind = Number.parseInt(flagText, 16) & 0xff;
    assert.ok(Number.isSafeInteger(size) && size >= 0 && Number.isSafeInteger(offset) && offset >= 0);
    const zeroFill = [1, 12, 18].includes(kind);
    if (!zeroFill) assert.ok(offset + size <= bytes.length, 'Section exceeds its native file.');
    return { name, size, kind, zeroFill, sha256: zeroFill ? null : sha(bytes.subarray(offset, offset + size)) };
  }).sort((a, b) => a.name.localeCompare(b.name, 'en'));
  assert.ok(sections.length > 0 && sections.length <= 256);
  assert.equal(new Set(sections.map(section => section.name)).size, sections.length);
  assert.ok(sections.some(section => section.name === '__TEXT/__text' && section.size > 0));
  return { uuid, sections };
}

export function correlateNative(local, localCommands, candidate, candidateCommands) {
  const left = machoSections(local, localCommands), right = machoSections(candidate, candidateCommands);
  return { localSha256: sha(local), candidateSha256: sha(candidate), uuid: left.uuid,
    uuidMatches: left.uuid === right.uuid,
    allSectionBytesMatch: JSON.stringify(left.sections) === JSON.stringify(right.sections),
    sectionCount: left.sections.length, exactFileMatch: local.equals(candidate),
    publisherAuthenticated: false, reproducibleSourceBuildVerified: false, publicationReady: false };
}

function invoke(command, args) {
  const result = spawnSync(command, args, { encoding: 'utf8', timeout: 30_000, maxBuffer: 4 * 1024 * 1024 });
  assert.ok(!result.error && !result.signal && result.status === 0, 'Read-only native inspection failed.');
  return result.stdout;
}

export function observeMacos() {
  assert.equal(process.platform, 'darwin', 'Native correlation requires the actual macOS tools and local receipts.');
  const assets = loadRuntimeAssets(root), group = assets.registry.groups.find(value => value.id === 'local-macos-native-binaries-unresolved');
  const formulas = {
    'libarchive.13.dylib': ['libarchive', 'libarchive.13.dylib'], 'libb2.1.dylib': ['libb2', 'libb2.1.dylib'],
    'libgif.dylib': ['giflib', 'libgif.dylib'], 'libjpeg.8.dylib': ['jpeg-turbo', 'libjpeg.8.dylib'],
    'libleptonica-1.82.0.dylib': ['leptonica', 'libleptonica.dylib'], 'liblz4.1.dylib': ['lz4', 'liblz4.1.dylib'],
    'liblzma.5.dylib': ['xz', 'liblzma.5.dylib'], 'libopenjp2.7.dylib': ['openjpeg', 'libopenjp2.7.dylib'],
    'libpng16.16.dylib': ['libpng', 'libpng16.16.dylib'], 'libsharpyuv.0.dylib': ['webp', 'libsharpyuv.0.dylib'],
    'libtesseract50.dylib': ['tesseract', 'libtesseract.dylib'], 'libtiff.6.dylib': ['libtiff', 'libtiff.6.dylib'],
    'libwebp.7.dylib': ['webp', 'libwebp.7.dylib'], 'libwebpmux.3.dylib': ['webp', 'libwebpmux.3.dylib'],
    'libzstd.1.dylib': ['zstd', 'libzstd.1.dylib'],
  };
  const rows = [];
  for (const asset of group.assets) {
    const file = join(root, asset.file);
    if (!existsSync(file)) { rows.push({ file: asset.file, status: 'optional-local-file-absent' }); continue; }
    const local = regularBytes(file); assert.equal(sha(local), asset.sha256);
    const commands = invoke('otool', ['-l', file]);
    const [formula, basename] = formulas[asset.file.split('/').at(-1)];
    const directory = '/usr/local/Cellar/' + formula;
    const candidates = [];
    if (existsSync(directory)) for (const version of readdirSync(directory)) {
      assert.match(version, /^[A-Za-z0-9._+-]+$/);
      const install = join(directory, version), link = join(install, 'lib', basename), receiptFile = join(install, 'INSTALL_RECEIPT.json');
      if (!existsSync(link) || !existsSync(receiptFile)) continue;
      const candidateFile = realpathSync(link);
      assert.ok(candidateFile.startsWith(install + '/'));
      const candidate = regularBytes(candidateFile), receiptBytes = regularBytes(receiptFile, 1024 * 1024), receipt = JSON.parse(receiptBytes);
      candidates.push({ formula, installedVersion: version, candidateFile, receiptSha256: sha(receiptBytes),
        pouredFromBottle: receipt.poured_from_bottle === true, recipeVersion: receipt.source?.versions?.stable ?? null,
        ...correlateNative(local, commands, candidate, invoke('otool', ['-l', candidateFile])) });
    }
    rows.push({ file: asset.file, status: candidates.some(value => value.uuidMatches && value.allSectionBytesMatch)
      ? 'local-build-correlation' : 'no-matching-installed-build', candidates });
  }
  return { schemaVersion: 1, scope: 'local-macos-x64-build-correlation-not-source-or-distribution-approval',
    runtimeRegistrySha256: assets.registrySha256, rows, legalApprovalGranted: false, publicationReady: false };
}

const differenceScope = 'local-macos-x64-build-difference-explanation-not-source-or-distribution-approval';

function nativeMetadata(bytes) {
  assert.ok(bytes.length >= 32, 'Truncated Mach-O header.');
  const count = bytes.readUInt32LE(16), size = bytes.readUInt32LE(20), end = 32 + size;
  assert.ok(count > 0 && count <= 4096 && count <= size / 8 && size % 8 === 0 && end <= bytes.length,
    'Invalid Mach-O load-command bounds.');
  let offset = 32, signature = null, firstSection = Infinity;
  for (let index = 0; index < count; index++) {
    assert.ok(offset + 8 <= end, 'Truncated Mach-O load command.');
    const command = bytes.readUInt32LE(offset), length = bytes.readUInt32LE(offset + 4);
    assert.ok(length >= 8 && length % 8 === 0 && offset + length <= end, 'Invalid Mach-O command size.');
    if (command === 0x19) {
      assert.ok(length >= 72, 'Truncated Mach-O segment.');
      const sections = bytes.readUInt32LE(offset + 64);
      assert.equal(length, 72 + sections * 80, 'Invalid Mach-O section table size.');
      for (let item = 0; item < sections; item++) {
        const section = offset + 72 + item * 80, kind = bytes.readUInt32LE(section + 64) & 0xff;
        const sectionSize = bytes.readBigUInt64LE(section + 40), start = bytes.readUInt32LE(section + 48);
        if (sectionSize === 0n || [1, 12, 18].includes(kind)) continue;
        assert.ok(start >= end && BigInt(start) + sectionSize <= BigInt(bytes.length), 'Mach-O metadata overlaps file-backed sections.');
        firstSection = Math.min(firstSection, start);
      }
    } else if (command === 0x1d) {
      assert.equal(length, 16, 'Invalid Mach-O signature command.');
      assert.equal(signature, null, 'Duplicate Mach-O signature command.');
      const start = bytes.readUInt32LE(offset + 8), signatureSize = bytes.readUInt32LE(offset + 12);
      assert.ok(start >= end && signatureSize > 0 && start + signatureSize <= bytes.length, 'Invalid code signature bounds.');
      signature = [start, start + signatureSize];
    }
    offset += length;
  }
  assert.equal(offset, end, 'Mach-O command count and size disagree.');
  assert.ok(Number.isFinite(firstSection), 'No bounded file-backed Mach-O section.');
  return { end, signature, firstSection };
}

function dylibNames(commands) {
  return [...commands.matchAll(/^\s*cmd (LC_ID_DYLIB|LC_LOAD_DYLIB|LC_LOAD_WEAK_DYLIB|LC_REEXPORT_DYLIB)\s*$[\s\S]*?^\s*name (\S+) \(offset \d+\)\s*$/gm)]
    .map(match => ({ command: match[1], name: match[2] }));
}

// Explains a whole-file hash difference between UUID/section-equal builds by locating every differing byte.
export function explainNativeDifference(local, localCommands, candidate, candidateCommands) {
  const correlation = correlateNative(local, localCommands, candidate, candidateCommands);
  assert.ok(correlation.uuidMatches && correlation.allSectionBytesMatch, 'Only correlated builds are explained.');
  const left = nativeMetadata(local), right = nativeMetadata(candidate);
  const base = { localSha256: correlation.localSha256, candidateSha256: correlation.candidateSha256, uuid: correlation.uuid,
    localBytes: local.length, candidateBytes: candidate.length };
  if (local.length !== candidate.length) return { ...base, classification: 'different-length-not-explained', differingBytes: null, regions: null };
  const headerEnd = Math.max(left.end, right.end);
  assert.ok(headerEnd <= Math.min(left.firstSection, right.firstSection), 'Mach-O metadata regions overlap compared section data.');
  const signatures = [left.signature, right.signature].filter(Boolean);
  const regions = { headerAndLoadCommands: 0, codeSignature: 0, other: 0 };
  for (let index = 0; index < local.length; index++) {
    if (local[index] === candidate[index]) continue;
    if (index < headerEnd) regions.headerAndLoadCommands++;
    else if (signatures.some(([start, end]) => index >= start && index < end)) regions.codeSignature++;
    else regions.other++;
  }
  const before = dylibNames(candidateCommands), after = dylibNames(localCommands);
  return { ...base, classification: regions.other ? 'differences-outside-metadata' : 'differences-confined-to-load-commands-and-signature',
    differingBytes: regions.headerAndLoadCommands + regions.codeSignature + regions.other, regions,
    localSigned: !!left.signature, candidateSigned: !!right.signature,
    dylibNameChanges: after.flatMap((value, index) => before[index]?.command === value.command && before[index].name !== value.name
      ? [{ command: value.command, installed: before[index].name, local: value.name }] : []),
    dylibCommandCountsMatch: before.length === after.length,
    publisherAuthenticated: false, reproducibleSourceBuildVerified: false, publicationReady: false };
}

export function explainMacos() {
  const correlation = observeMacos(), assets = loadRuntimeAssets(root);
  const rows = correlation.rows.map(row => {
    if (row.status !== 'local-build-correlation') return { file: row.file, status: row.status };
    const candidate = row.candidates.find(value => value.uuidMatches && value.allSectionBytesMatch);
    const file = join(root, row.file);
    return { file: row.file, status: 'explained', formula: candidate.formula, installedVersion: candidate.installedVersion,
      receiptSha256: candidate.receiptSha256, ...explainNativeDifference(regularBytes(file), invoke('otool', ['-l', file]),
        regularBytes(candidate.candidateFile), invoke('otool', ['-l', candidate.candidateFile])) };
  });
  return { schemaVersion: 1, scope: differenceScope, runtimeRegistrySha256: assets.registrySha256, rows,
    legalApprovalGranted: false, publicationReady: false };
}

export function validateDifferences(record, assets) {
  assert.equal(record.schemaVersion, 1); assert.equal(record.scope, differenceScope);
  assert.equal(record.legalApprovalGranted, false); assert.equal(record.publicationReady, false);
  assert.equal(record.runtimeRegistrySha256, assets.registrySha256);
  const group = assets.registry.groups.find(value => value.id === 'local-macos-native-binaries-unresolved');
  assert.deepEqual(record.rows.map(row => row.file).sort(), group.assets.map(asset => asset.file).sort());
  const classes = ['different-length-not-explained', 'differences-outside-metadata', 'differences-confined-to-load-commands-and-signature'];
  for (const row of record.rows) {
    if (row.status !== 'explained') { assert.ok(['optional-local-file-absent', 'no-matching-installed-build'].includes(row.status)); continue; }
    assert.equal(row.localSha256, group.assets.find(asset => asset.file === row.file).sha256);
    assert.ok(classes.includes(row.classification)); assert.notEqual(row.localSha256, row.candidateSha256);
    for (const key of ['publisherAuthenticated', 'reproducibleSourceBuildVerified', 'publicationReady']) assert.equal(row[key], false);
    if (row.regions) {
      assert.equal(row.differingBytes, row.regions.headerAndLoadCommands + row.regions.codeSignature + row.regions.other);
      assert.equal(row.classification === 'differences-outside-metadata', row.regions.other > 0);
    }
  }
  return { explained: record.rows.filter(row => row.classification === 'differences-confined-to-load-commands-and-signature').length,
    total: record.rows.length };
}

export function validateObservation(record, assets) {
  assert.equal(record.schemaVersion, 1);
  assert.equal(record.publicationReady, false); assert.equal(record.legalApprovalGranted, false);
  if (record.scope === 'local-macos-x64-build-correlation-not-source-or-distribution-approval') {
    assert.equal(record.runtimeRegistrySha256, assets.registrySha256);
    const group = assets.registry.groups.find(value => value.id === 'local-macos-native-binaries-unresolved');
    assert.deepEqual(record.rows.map(row => row.file).sort(), group.assets.map(asset => asset.file).sort());
    for (const row of record.rows) {
      assert.ok(['optional-local-file-absent', 'local-build-correlation', 'no-matching-installed-build'].includes(row.status));
      if (row.status === 'optional-local-file-absent') continue;
      for (const candidate of row.candidates) {
        assert.equal(candidate.localSha256, group.assets.find(asset => asset.file === row.file).sha256);
        for (const key of ['publisherAuthenticated', 'reproducibleSourceBuildVerified', 'publicationReady']) assert.equal(candidate[key], false);
        assert.match(candidate.candidateSha256, /^[a-f0-9]{64}$/); assert.match(candidate.receiptSha256, /^[a-f0-9]{64}$/);
        assert.ok(Number.isSafeInteger(candidate.sectionCount) && candidate.sectionCount > 0 && candidate.sectionCount <= 256);
      }
      assert.equal(row.status === 'local-build-correlation', row.candidates.some(candidate => candidate.uuidMatches && candidate.allSectionBytesMatch));
    }
  } else {
    assert.equal(record.scope, 'existing-local-image-os-metadata-and-original-notices-not-release-approval');
    assert.match(record.image.id, /^sha256:[a-f0-9]{64}$/); assert.equal(record.image.os, 'linux');
    assert.ok(['amd64', 'arm64'].includes(record.image.architecture));
    for (const key of ['imageToCurrentDockerfileBindingVerified', 'appStarted', 'networkEnabled', 'sourcePackagesAcquired',
      'publisherAuthenticated', 'managedRuntimeAndStaticComponentReviewComplete']) assert.equal(record[key], false);
    assert.equal(record.containersRemoved, true);
    for (const key of ['currentDockerfileSha256', 'packageQuerySha256', 'osReleaseSha256', 'noticeArchiveSha256']) assert.match(record[key], /^[a-f0-9]{64}$/);
    parseDpkgPackages(record.packages.map(row => [row.name, row.version, row.source, row.sourceVersion, row.architecture, 'ii '].join('\t')).join('\n'));
    const notices = new Map();
    for (const notice of record.notices) {
      assert.match(notice.file, /^usr\/share\/(?:doc\/(?:[A-Za-z0-9+_.:-]+\/)+copyright|common-licenses\/[A-Za-z0-9+_.-]+)$/);
      assert.ok(!notice.file.split('/').some(part => part === '.' || part === '..'));
      assert.ok(!notices.has(notice.file)); notices.set(notice.file, notice);
      assert.ok(Number.isSafeInteger(notice.bytes) && notice.bytes >= 0 && notice.bytes <= 4 * 1024 * 1024);
      assert.equal(notice.status, notice.bytes ? 'original-text-retained-unreviewed' : 'link-or-empty-entry-needs-review');
      if (notice.bytes) assert.match(notice.sha256, /^[a-f0-9]{64}$/); else assert.equal(notice.sha256, null);
    }
    for (const row of record.packages) assert.deepEqual(row.copyright,
      notices.get('usr/share/doc/' + row.name.split(':')[0] + '/copyright') ?? null);
  }
}

const componentSourceScope = 'tesseract-5.2.0-component-source-candidates-not-verified-build-or-approval';
const componentSourceStates = ['candidate-version-association', 'candidate-version-inferred',
  'license-text-identical-across-candidates-version-ambiguous', 'unresolved-version-not-observed'];
const gitBlob = bytes => createHash('sha1').update(Buffer.from(`blob ${bytes.length}\0`)).update(bytes).digest('hex');
const payloadReferencePath = 'assets/licenses/runtime-assets/component-sources/tesseract-5.2.0/payload-reference.v1.json';

function relativeEvidencePath(file) {
  assert.ok(typeof file === 'string' && !file.startsWith('/') && !/[\\:\u0000-\u001f]/.test(file)
    && file.split('/').every(part => part && part !== '.' && part !== '..'), 'Unsafe evidence path.');
}

function sourceNoticeUrl(source, file) {
  relativeEvidencePath(file);
  assert.match(source.commit, /^[a-f0-9]{40}$/);
  const repository = new URL(source.repository);
  assert.ok(['github.com', 'gitlab.com'].includes(repository.hostname));
  assert.match(repository.pathname, /^\/[A-Za-z0-9._-]+\/[A-Za-z0-9._-]+$/);
  assert.equal(repository.href, 'https://' + repository.hostname + repository.pathname, 'Noncanonical source repository.');
  const member = file.split('/').map(part => encodeURIComponent(part)).join('/');
  return repository.hostname === 'github.com'
    ? 'https://raw.githubusercontent.com' + repository.pathname + '/' + source.commit + '/' + member
    : repository.href + '/-/raw/' + source.commit + '/' + member;
}

function validatePayloadReference(reference, scanned) {
  assert.equal(reference.schemaVersion, 1);
  assert.equal(reference.scope, 'retained-tesseract-payload-reference-not-build-authentication');
  for (const key of ['publisherAuthenticated', 'sourceBuildVerified', 'legalApprovalGranted', 'publicationReady']) assert.equal(reference[key], false);
  assert.equal(reference.package.key, scanned.key); assert.equal(reference.package.archiveSha256, scanned.archiveSha256);
  sourceNoticeUrl(reference, 'LICENSE.txt');
  assert.deepEqual(reference.payloads.map(payload => [payload.file, payload.sha256]).sort(),
    scanned.assets.map(asset => [asset.file, asset.sha256]).sort(), 'Payload reference differs from the scanned package.');
  for (const payload of reference.payloads) {
    relativeEvidencePath(payload.file);
    assert.match(payload.gitBlob, /^[a-f0-9]{40}$/);
    assert.ok(Number.isSafeInteger(payload.bytes) && payload.bytes > 0 && payload.bytes <= 128 * 1024 * 1024);
  }
}

export function verifyComponentPayloadBytes(payloads, readPayload) {
  assert.ok(Array.isArray(payloads) && payloads.length > 0 && payloads.length <= 32);
  const seen = new Set();
  for (const payload of payloads) {
    relativeEvidencePath(payload.file);
    assert.ok(!seen.has(payload.file), 'Duplicate payload.'); seen.add(payload.file);
    const bytes = readPayload(payload.file);
    assert.ok(Buffer.isBuffer(bytes) && bytes.length > 0 && bytes.length <= 128 * 1024 * 1024);
    assert.equal(bytes.length, payload.bytes, 'Payload size differs from the reference.');
    assert.equal(sha(bytes), payload.sha256, 'Payload SHA256 differs from the reference.');
    assert.equal(gitBlob(bytes), payload.gitBlob, 'Payload Git blob differs from the reference.');
  }
  return { payloads: payloads.length, localPayloadHashesRecomputed: true, upstreamFetched: false, publicationReady: false };
}

export function verifyComponentPayloads(directory, rootDirectory = root) {
  checkEvidence(rootDirectory);
  const reference = JSON.parse(regularBytes(join(rootDirectory, payloadReferencePath)));
  return verifyComponentPayloadBytes(reference.payloads, file => regularBytes(join(directory, file), 128 * 1024 * 1024));
}

// Candidate associations stay distinct from verified builds: every notice is reread and rehashed, every
// observed marker must exist in the recorded payload scan, and nothing grants source or legal approval.
export function validateComponentSources(record, components, readAsset, reference = JSON.parse(readAsset(payloadReferencePath))) {
  assert.equal(record.schemaVersion, 1); assert.equal(record.scope, componentSourceScope);
  for (const key of ['sourceBuildVerified', 'legalApprovalGranted', 'publicationReady']) assert.equal(record[key], false);
  const scanned = components.packages.find(entry => entry.key === record.package.key);
  assert.ok(scanned, 'Component sources name an unscanned package.');
  validatePayloadReference(reference, scanned);
  assert.equal(record.package.archiveSha256, scanned.archiveSha256);
  const association = record.artifactAssociation;
  assert.equal(association.relation, 'payloads-byte-identical-to-upstream-repository-blobs');
  assert.equal(association.buildRecipeInRepository, false);
  for (const field of ['repository', 'tag', 'commit']) assert.equal(association[field], reference[field], 'Payload origin differs from retained reference.');
  assert.deepEqual(association.payloads.map(payload => [payload.file, payload.sha256, payload.gitBlob]).sort(),
    reference.payloads.map(payload => [payload.file, payload.sha256, payload.gitBlob]).sort(), 'Associated payloads differ from the retained reference.');
  const checkNotice = (notice, sources) => {
    relativeEvidencePath(notice.file);
    assert.ok(notice.file.startsWith('assets/licenses/'));
    const bytes = readAsset(notice.file);
    assert.equal(bytes.length, notice.bytes, 'Stored original notice changed.'); assert.equal(sha(bytes), notice.sha256, 'Stored original notice changed.');
    assert.equal(gitBlob(bytes), notice.gitBlob, 'Stored notice is not the upstream blob.');
    assert.ok(sources.some(source => notice.url === sourceNoticeUrl(source, notice.upstreamPath)),
      'Notice URL does not match its repository, commit and member.');
  };
  relativeEvidencePath(record.wrapperLicense.repositoryCopy);
  checkNotice({ ...record.wrapperLicense, upstreamPath: 'LICENSE.txt', file: record.wrapperLicense.repositoryCopy,
    bytes: readAsset(record.wrapperLicense.repositoryCopy).length }, [reference]);
  const markers = new Map();
  for (const asset of scanned.assets) for (const marker of asset.markers)
    markers.set(marker.component, [...(markers.get(marker.component) ?? []), { asset: asset.file, versions: marker.versions }]);
  const names = record.components.map(value => value.component);
  assert.equal(new Set(names).size, names.length, 'Duplicate component source.');
  for (const component of markers.keys()) assert.ok(names.includes(component), `Observed component ${component} has no source record.`);
  for (const value of record.components) {
    assert.ok(componentSourceStates.includes(value.status));
    assert.ok(Object.hasOwn(reference.componentRepositories, value.component), 'Unreviewed component identity.');
    assert.ok(['identification-string', 'identification-string-and-file-name', 'bare-version-string',
      'bare-version-string-beside-component-messages'].includes(value.observed.basis), 'Unreviewed observation basis.');
    assert.ok(typeof value.ambiguity === 'string' && value.ambiguity.length > 20);
    assert.ok(value.observed.assets.length > 0 && new Set(value.observed.assets).size === value.observed.assets.length
      && value.observed.assets.every(asset => scanned.assets.some(item => item.file === asset)));
    const found = markers.get(value.component) ?? [];
    const sources = value.candidateSource ? [value.candidateSource] : value.candidateSources ?? [];
    if (value.observed.version === null) {
      assert.equal(value.status, 'unresolved-version-not-observed', 'Unknown versions must remain unresolved.');
      assert.equal(value.candidateSource, null); assert.equal(sources.length, 0); assert.deepEqual(value.notices, []);
    } else assert.match(value.observed.version, /^\d+(?:\.\d+)+$/);
    if (value.observed.basis === 'bare-version-string')
      assert.equal(value.status, 'license-text-identical-across-candidates-version-ambiguous', 'Bare version strings retain ambiguity.');
    for (const source of sources) {
      assert.equal(source.repository, reference.componentRepositories[value.component], 'Source belongs to another component.');
      sourceNoticeUrl(source, 'LICENSE');
    }
    if (value.observed.basis === 'bare-version-string-beside-component-messages') {
      assert.equal(value.status, 'candidate-version-inferred', 'An inferred version cannot become a direct association.');
      assert.equal(found.length, 0);
      const inferred = reference.inferredComponents.find(item => item.component === value.component);
      assert.ok(inferred, 'Unreviewed inferred component.');
      assert.equal(value.observed.version, inferred.version);
      assert.deepEqual([...value.observed.assets].sort(), [...inferred.assets].sort());
    } else {
      for (const asset of value.observed.assets) assert.ok(found.some(item => item.asset === asset), `${value.component} is not observed in ${asset}.`);
      if (value.observed.version !== null)
        for (const asset of value.observed.assets) assert.ok(found.some(item => item.asset === asset && item.versions.includes(value.observed.version)),
          `${value.component} version was not observed in ${asset}.`);
    }
    if (value.status === 'unresolved-version-not-observed') {
      assert.equal(value.observed.version, null); assert.equal(value.candidateSource, null); assert.deepEqual(value.notices, []);
      continue;
    }
    assert.ok(value.notices.length > 0, `${value.component} has no retained notice.`);
    if (value.status === 'license-text-identical-across-candidates-version-ambiguous') {
      assert.equal(value.candidateSource, null); assert.ok(sources.length >= 2);
      assert.equal(new Set(sources.map(source => source.tag)).size, sources.length, 'Duplicate ambiguous candidate tag.');
      assert.equal(new Set(sources.map(source => source.commit)).size, sources.length, 'Duplicate ambiguous candidate commit.');
      assert.ok(sources.some(source => source.tag.replace(/^v/, '') === value.observed.version), 'No candidate matches the observed version.');
      for (const notice of value.notices) assert.deepEqual([...notice.sameBytesAt].sort(), sources.map(source => source.tag).sort());
    } else {
      assert.ok(value.candidateSource && value.candidateSource.tag.replace(/^v/, '') === value.observed.version,
        'A candidate tag must equal the observed version, not a nearby release.');
    }
    for (const notice of value.notices) checkNotice(notice, sources);
  }
  return { components: record.components.length,
    withNotices: record.components.filter(value => value.notices.length).length,
    unresolved: record.components.filter(value => value.status === 'unresolved-version-not-observed').length };
}

const fontBuildScope = 'liberation-sans-candidate-rebuild-from-pinned-debian-source-not-reproduction-or-approval';

// A rebuild may only differ in FontForge's timestamp table, the cmap encoding with identical mappings and the
// resulting head checksum; anything else is not an equivalent candidate. It never authenticates the source.
function fontGlyphCount(bytes) {
  assert.ok(Buffer.isBuffer(bytes) && bytes.length >= 12 && bytes.length <= 16 * 1024 * 1024, 'Bounded TrueType font required.');
  assert.equal(bytes.readUInt32BE(0), 0x00010000, 'Only the pinned TrueType font format is supported.');
  const count = bytes.readUInt16BE(4), end = 12 + count * 16;
  assert.ok(count > 0 && count <= 256 && end <= bytes.length, 'Invalid font table directory.');
  const maxp = [];
  for (let index = 12; index < end; index += 16) if (bytes.toString('ascii', index, index + 4) === 'maxp') {
    const offset = bytes.readUInt32BE(index + 8), length = bytes.readUInt32BE(index + 12);
    assert.ok(offset >= end && length >= 6 && offset + length <= bytes.length, 'Invalid maxp table bounds.');
    maxp.push(bytes.readUInt16BE(offset + 4));
  }
  assert.equal(maxp.length, 1, 'Exactly one maxp table is required.');
  assert.ok(maxp[0] > 0, 'An empty font is not equivalent.');
  return maxp[0];
}

export function validateFontBuild(record, assets, evidence, readAsset) {
  assert.equal(record.schemaVersion, 1); assert.equal(record.scope, fontBuildScope);
  for (const key of ['shippedFontsReplaced', 'legalApprovalGranted', 'publicationReady']) assert.equal(record[key], false);
  assert.equal(record.source.version, evidence.fonts.version); assert.equal(record.source.signatureVerified, false);
  assert.equal(record.source.descriptorSha256, evidence.fonts.sourceFiles.find(file => file.name.endsWith('.dsc')).sha256);
  assert.match(record.environment.image, /^debian@sha256:[a-f0-9]{64}$/); assert.equal(record.environment.containersRemoved, true);
  assert.match(record.builtPackage.sha256, /^[a-f0-9]{64}$/);
  const group = assets.registry.groups.find(value => value.id === 'liberation-sans-1-07-4');
  assert.deepEqual(record.faces.map(face => face.file).sort(), group.assets.map(asset => asset.file).sort());
  for (const face of record.faces) {
    assert.equal(face.shippedSha256, group.assets.find(asset => asset.file === face.file).sha256, 'Shipped font hash changed.');
    assert.match(face.builtSha256, /^[a-f0-9]{64}$/);
    const shipped = readAsset(face.file);
    assert.equal(sha(shipped), face.shippedSha256, 'Shipped font bytes changed.');
    assert.ok(Number.isSafeInteger(face.glyphCount) && face.glyphCount > 0);
    assert.equal(face.glyphCount, fontGlyphCount(shipped), 'Glyph count differs from the shipped font.');
    assert.equal(face.glyphOutlinesEqual, true, 'Candidate glyph outlines differ.');
    assert.equal(face.cmapSubtablesEqual, true, 'Candidate cmap subtables differ.');
    assert.equal(face.cmapMappingsEqual, true, 'Candidate character mapping differs.');
    assert.ok(Array.isArray(face.differingTables) && new Set(face.differingTables).size === face.differingTables.length);
    assert.ok(face.differingTables.every(table => ['FFTM', 'cmap', 'head'].includes(table)), 'Candidate differs beyond build metadata.');
    assert.deepEqual(face.headFieldsDiffering, face.differingTables.includes('head') ? ['checkSumAdjustment'] : []);
    assert.equal(face.differingTables.length === 0, face.builtSha256 === face.shippedSha256);
  }
  assert.equal(record.byteReproduced, record.faces.every(face => face.builtSha256 === face.shippedSha256));
  return { faces: record.faces.length, byteReproduced: record.byteReproduced };
}

export function validateEvidence(evidence, assets, readAsset, readObservation) {
  assert.equal(evidence.schemaVersion, 1);
  assert.equal(evidence.publicationReady, false); assert.equal(evidence.legalApprovalGranted, false);
  assert.equal(evidence.runtimeRegistrySha256, assets.registrySha256);
  assert.equal(evidence.model.file, 'src/Importing/PVAD.FileImporter.ImageOcr/tessdata/ukr.traineddata');
  const model = readAsset(evidence.model.file);
  assert.equal(sha(model), evidence.model.localSha256);
  assert.equal(model.length, evidence.model.localBytes);
  assert.equal(trainedDataOffsets(model).declaredOffsetsInBounds, false);
  assert.equal(evidence.model.relation, 'truncated-reference-prefix');
  assert.equal(evidence.model.modelUsabilityCertified, false);
  assert.ok(Number.isSafeInteger(evidence.model.reference.bytes) && evidence.model.reference.bytes > model.length && evidence.model.reference.bytes <= 64 * 1024 * 1024);
  assert.equal(evidence.model.missingBytes, evidence.model.reference.bytes - model.length);
  assert.match(evidence.model.reference.sha256, /^[a-f0-9]{64}$/);
  assert.match(evidence.model.reference.gitBlob, /^[a-f0-9]{40}$/);
  assert.match(evidence.model.reference.url, /^https:\/\/raw\.githubusercontent\.com\/tesseract-ocr\/tessdata_fast\/[a-f0-9]{40}\/ukr\.traineddata$/);
  assert.equal(evidence.fonts.version, '1:1.07.4-11');
  assert.equal(evidence.fonts.binaryPackageSha256, assets.registry.groups.find(group => group.id === 'liberation-sans-1-07-4').source.archiveSha256);
  assert.deepEqual(evidence.fonts.sourceFiles.map(file => file.name).sort(), [
    'fonts-liberation_1.07.4-11.debian.tar.xz', 'fonts-liberation_1.07.4-11.dsc', 'fonts-liberation_1.07.4.orig.tar.gz']);
  for (const file of evidence.fonts.sourceFiles) {
    assert.match(file.name, /^fonts-liberation_[A-Za-z0-9._-]+\.(?:dsc|tar\.gz|tar\.xz)$/);
    assert.match(file.sha256, /^[a-f0-9]{64}$/);
    assert.ok(Number.isSafeInteger(file.bytes) && file.bytes > 0 && file.bytes < 16 * 1024 * 1024);
    assert.equal(file.url, 'https://deb.debian.org/debian/pool/main/f/fonts-liberation/' + file.name);
  }
  assert.equal(evidence.fonts.sourceRebuildVerified, false); assert.equal(evidence.fonts.debianSignatureVerified, false);
  const descriptor = evidence.fonts.descriptor;
  assert.deepEqual(descriptor.archives, ['fonts-liberation_1.07.4-11.debian.tar.xz', 'fonts-liberation_1.07.4.orig.tar.gz']);
  assert.deepEqual(descriptor.algorithms, ['sha256', 'sha1', 'md5']); assert.equal(descriptor.checksumsVerified, true);
  assert.equal(descriptor.signatureVerified, false); assert.equal(descriptor.signatureStatus, 'blocked-no-authenticated-keyring');
  assert.equal(descriptor.signer.trusted, false); assert.match(descriptor.signer.issuerFingerprint, /^[A-F0-9]{40}$/);
  assert.equal(descriptor.signer.issuerKeyId, descriptor.signer.issuerFingerprint.slice(24));
  assert.deepEqual(evidence.observations.map(record => record.file).sort(), ['evidence/container-local-os.v1.json',
    'evidence/container-notice-links.v1.json', 'evidence/liberation-sans-candidate-build.v1.json',
    'evidence/macos-build-differences.v1.json', 'evidence/macos-local-builds.v1.json', 'evidence/native-components.v1.json']);
  const records = new Map();
  for (const observation of evidence.observations) {
    assert.match(observation.file, /^evidence\/[a-z0-9.-]+\.json$/);
    assert.match(observation.sha256, /^[a-f0-9]{64}$/);
    const bytes = readObservation(observation.file);
    assert.equal(sha(bytes), observation.sha256, 'Recorded observation changed.');
    records.set(observation.file, { record: JSON.parse(bytes), sha256: observation.sha256 });
  }
  assert.deepEqual(evidence.componentSources.map(record => record.file), ['component-sources/tesseract-5.2.0/component-sources.v1.json']);
  for (const source of evidence.componentSources) {
    const bytes = readObservation(source.file);
    assert.equal(sha(bytes), source.sha256, 'Recorded component sources changed.');
    assert.equal(evidence.componentPayloadReference.file, payloadReferencePath);
    const referenceBytes = readAsset(payloadReferencePath);
    assert.equal(sha(referenceBytes), evidence.componentPayloadReference.sha256, 'Retained payload reference changed.');
    validateComponentSources(JSON.parse(bytes), records.get('evidence/native-components.v1.json').record, readAsset, JSON.parse(referenceBytes));
  }
  const historical = records.get('evidence/container-local-os.v1.json');
  for (const [file, { record }] of records) {
    if (file === 'evidence/container-notice-links.v1.json') validateNoticeLinks(record, historical.record, historical.sha256);
    else if (file === 'evidence/native-components.v1.json') validateNativeComponents(record);
    else if (file === 'evidence/macos-build-differences.v1.json') validateDifferences(record, assets);
    else if (file === 'evidence/liberation-sans-candidate-build.v1.json') validateFontBuild(record, assets, evidence, readAsset);
    else validateObservation(record, assets);
  }
  assert.ok(evidence.limitations.length >= 4 && evidence.limitations.every(value => typeof value === 'string' && value.length > 20));
  return { evidence, localAssetBindingsChecked: true, downloadedReferencesRecomputed: false, publicationReady: false };
}

export function checkEvidence(rootDirectory = root) {
  const evidence = JSON.parse(regularBytes(join(rootDirectory, evidencePath), 2 * 1024 * 1024));
  return validateEvidence(evidence, loadRuntimeAssets(rootDirectory),
    file => regularBytes(join(rootDirectory, file)),
    file => regularBytes(join(rootDirectory, 'assets/licenses/runtime-assets', file), 4 * 1024 * 1024));
}

export function parseDpkgPackages(text) {
  assert.ok(typeof text === 'string' && Buffer.byteLength(text) <= 4 * 1024 * 1024);
  const names = new Set();
  const packages = text.split('\n').filter(line => line.length).map(line => {
    const fields = line.split('\t');
    assert.equal(fields.length, 6, 'Unexpected package-query columns.');
    const [name, version, source, sourceVersion, architecture, status] = fields;
    for (const value of [name, source]) assert.match(value, /^[a-z0-9][a-z0-9+.:_-]{0,199}$/);
    for (const value of [version, sourceVersion]) assert.match(value, /^[A-Za-z0-9][A-Za-z0-9.+:~_-]{0,199}$/);
    assert.match(architecture, /^[a-z0-9_-]+$/); assert.match(status, /^ii *$/);
    assert.ok(!names.has(name), 'Duplicate installed package.'); names.add(name);
    return { name, version, source, sourceVersion, architecture };
  });
  assert.ok(packages.length > 0 && packages.length <= 10_000);
  return packages;
}

export function containerArguments(image, entrypoint, args, name) {
  assert.match(image, /^sha256:[a-f0-9]{64}$/);
  assert.match(name, /^pvad-source-[a-f0-9-]{36}$/);
  assert.ok(['/usr/bin/dpkg-query', '/bin/cat', '/bin/tar'].includes(entrypoint));
  return ['run', '--rm', '--pull=never', '--name', name, '--label', 'org.pvad.source-evidence=' + name,
    '--network', 'none', '--read-only', '--user', '65534:65534', '--workdir', '/',
    '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges', '--pids-limit', '64',
    '--memory', '256m', '--cpus', '1', '--no-healthcheck', '--entrypoint', entrypoint, image, ...args];
}

export function observeContainer(image) {
  assert.match(image, /^sha256:[a-f0-9]{64}$/);
  const identity = JSON.parse(invoke('docker', ['image', 'inspect', image, '--format',
    '{"id":{{json .Id}},"os":{{json .Os}},"architecture":{{json .Architecture}}}']));
  assert.equal(identity.id, image); assert.equal(identity.os, 'linux');
  assert.ok(['amd64', 'arm64'].includes(identity.architecture));
  const output = join(root, 'artifacts/native-runtime-provenance', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  mkdirSync(output, { recursive: true });
  function capture(entrypoint, args) {
    const name = 'pvad-source-' + randomUUID();
    try {
      const result = spawnSync('docker', containerArguments(image, entrypoint, args, name), { timeout: 60_000, maxBuffer: 64 * 1024 * 1024 });
      assert.ok(!result.error && !result.signal && result.status === 0, 'Read-only image observation failed.');
      return result.stdout;
    } finally {
      const owned = invoke('docker', ['ps', '-aq', '--filter', 'label=org.pvad.source-evidence=' + name]).trim();
      if (owned) {
        assert.match(owned, /^[a-f0-9]{12,64}$/);
        invoke('docker', ['rm', '-f', owned]);
      }
      assert.equal(invoke('docker', ['ps', '-aq', '--filter', 'label=org.pvad.source-evidence=' + name]).trim(), '');
    }
  }
  const query = capture('/usr/bin/dpkg-query', ['-W', '-f=${binary:Package}\t${Version}\t${source:Package}\t${source:Version}\t${Architecture}\t${db:Status-Abbrev}\n']);
  const packages = parseDpkgPackages(query.toString('utf8'));
  const osRelease = capture('/bin/cat', ['/etc/os-release']);
  const archive = capture('/bin/tar', ['-cf', '-', '-C', '/', 'usr/share/doc', 'usr/share/common-licenses']);
  writeFileSync(join(output, 'os-packages.tsv'), query, { flag: 'wx' });
  writeFileSync(join(output, 'os-release'), osRelease, { flag: 'wx' });
  writeFileSync(join(output, 'os-notices.tar'), archive, { flag: 'wx' });
  function tar(args, limit) {
    const result = spawnSync('tar', args, { input: archive, timeout: 10_000, maxBuffer: limit });
    assert.ok(!result.error && !result.signal && result.status === 0, 'Notice archive inspection failed.');
    return result.stdout;
  }
  const names = tar(['-tf', '-'], 4 * 1024 * 1024).toString('utf8').trim().split('\n');
  assert.ok(names.length <= 20_000);
  for (const name of names) assert.ok(!name.startsWith('/') && !name.split('/').includes('..') &&
    ['usr/share/doc/', 'usr/share/common-licenses/'].some(prefix => name.startsWith(prefix)), 'Unexpected notice archive path.');
  const notices = [];
  for (const name of names.filter(name => name.endsWith('/copyright') || /^usr\/share\/common-licenses\/[^/]+$/.test(name))) {
    const bytes = tar(['-xOf', '-', '--', name], 4 * 1024 * 1024);
    notices.push({ file: name, bytes: bytes.length, sha256: bytes.length ? sha(bytes) : null,
      status: bytes.length ? 'original-text-retained-unreviewed' : 'link-or-empty-entry-needs-review' });
  }
  const byName = new Map(notices.map(notice => [notice.file, notice]));
  const report = { schemaVersion: 1, scope: 'existing-local-image-os-metadata-and-original-notices-not-release-approval',
    image: identity, currentDockerfileSha256: sha(readFileSync(join(root, 'PVAD.WebApi/Dockerfile'))),
    imageToCurrentDockerfileBindingVerified: false, packages: packages.map(record => ({ ...record,
      copyright: byName.get('usr/share/doc/' + record.name.split(':')[0] + '/copyright') ?? null })), notices,
    packageQuerySha256: sha(query), osReleaseSha256: sha(osRelease), noticeArchiveSha256: sha(archive),
    containersRemoved: true, appStarted: false, networkEnabled: false, sourcePackagesAcquired: false,
    publisherAuthenticated: false, managedRuntimeAndStaticComponentReviewComplete: false,
    legalApprovalGranted: false, publicationReady: false };
  writeFileSync(join(output, 'container.json'), JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
  return { output, packages: packages.length, noticeEntries: notices.length,
    unresolvedCopyrightEntries: report.packages.filter(record => !record.copyright?.bytes).length };
}

const noticeRoots = ['usr/share/doc/', 'usr/share/common-licenses/'];
const linkScope = 'supplemental-notice-link-resolution-of-historical-archive-not-release-approval';
const insideNoticeRoots = path => noticeRoots.some(prefix => (path + '/').startsWith(prefix));
const tarText = (header, start, length) => {
  const raw = header.subarray(start, start + length), end = raw.indexOf(0);
  return raw.subarray(0, end === -1 ? length : end).toString('latin1');
};
const tarNumber = (header, start, length) => {
  assert.equal(header[start] & 0x80, 0, 'Unsupported binary tar number.');
  const text = tarText(header, start, length).trim();
  assert.match(text, /^[0-7]{0,12}$/, 'Invalid tar number.');
  return text ? Number.parseInt(text, 8) : 0;
};

// Reads only ustar/GNU header metadata from retained bytes; nothing is extracted to host paths.
export function parseNoticeArchive(bytes, { memberLimit = 4 * 1024 * 1024, entryLimit = 20_000 } = {}) {
  assert.ok(Buffer.isBuffer(bytes) && bytes.length >= 1024 && bytes.length % 512 === 0 && bytes.length <= 64 * 1024 * 1024,
    'Bounded tar archive required.');
  const entries = new Map();
  let offset = 0, ended = false;
  while (offset + 512 <= bytes.length) {
    const header = bytes.subarray(offset, offset + 512);
    if (header.every(value => value === 0)) {
      assert.ok(offset + 1024 <= bytes.length && bytes.subarray(offset, offset + 1024).every(value => value === 0),
        'Incomplete tar end marker.');
      assert.ok(bytes.subarray(offset + 1024).every(value => value === 0), 'Unexpected trailing tar data.');
      ended = true; break;
    }
    let sum = 0;
    for (let index = 0; index < 512; index++) sum += index >= 148 && index < 156 ? 32 : header[index];
    assert.equal(sum, tarNumber(header, 148, 8), 'Tar header checksum mismatch.');
    const magic = header.subarray(257, 265).toString('latin1');
    assert.ok(magic === 'ustar  \0' || magic === 'ustar\u000000', 'Unsupported tar format.');
    const prefix = magic === 'ustar\u000000' ? tarText(header, 345, 155) : '';
    const name = ((prefix ? prefix + '/' : '') + tarText(header, 0, 100)).replace(/\/$/, '');
    const flag = header[156] === 0 ? '0' : String.fromCharCode(header[156]);
    const type = { 0: 'file', 2: 'symlink', 5: 'directory' }[flag];
    assert.ok(type, 'Unsupported tar entry type.');
    assert.ok(name && !name.startsWith('/') && !/[\\\u0000-\u001f]/.test(name), 'Unsafe archive path.');
    assert.ok(name.split('/').every(part => part && part !== '.' && part !== '..'), 'Unsafe archive path.');
    assert.ok(insideNoticeRoots(name) || 'usr/share/'.startsWith(name + '/'), 'Unexpected notice archive path.');
    assert.ok(!entries.has(name), 'Duplicate archive member.');
    const size = tarNumber(header, 124, 12), target = tarText(header, 157, 100);
    if (type === 'file') {
      assert.ok(size <= memberLimit, 'Oversized archive member.');
      assert.ok(offset + 512 + size <= bytes.length, 'Truncated archive member.');
    } else assert.equal(size, 0, 'Links and directories cannot carry data.');
    if (type === 'symlink') assert.ok(target.length > 0 && !/[\u0000-\u001f]/.test(target), 'Invalid link target.');
    entries.set(name, { name, type, size, target: type === 'symlink' ? target : null,
      data: type === 'file' ? bytes.subarray(offset + 512, offset + 512 + size) : null });
    assert.ok(entries.size <= entryLimit, 'Too many archive members.');
    offset += 512 + Math.ceil(size / 512) * 512;
  }
  assert.ok(ended, 'Missing tar end marker.');
  return entries;
}

// Resolves archive-internal links only. Absolute, escaping and cyclic links are rejected, never followed.
function safeNoticePath(path) {
  assert.ok(typeof path === 'string' && insideNoticeRoots(path) && !/[\\\u0000-\u001f]/.test(path)
    && path.split('/').every(part => part && part !== '.' && part !== '..'), 'Unsafe notice path.');
}

function walkNoticeMember(entries, requested, hopLimit = 8) {
  safeNoticePath(requested);
  // Ancestors of the two three-level notice roots are implicit: the archive was created below them.
  const rootDepth = 2;
  let parts = requested.split('/'), index = rootDepth;
  const chain = [], seen = new Set();
  const result = (status, entry = null) => ({ status, entry, chain });
  while (index < parts.length) {
    const current = parts.slice(0, index + 1).join('/'), entry = entries.get(current);
    if (!entry) return result('missing-notice');
    if (entry.type === 'symlink') {
      assert.ok(typeof entry.target === 'string' && entry.target.length > 0 && !/[\\\u0000-\u001f]/.test(entry.target), 'Invalid notice link target.');
      assert.ok(!entry.target.startsWith('/'), 'Absolute notice link rejected.');
      assert.ok(!seen.has(current), 'Notice link cycle rejected.');
      assert.ok(chain.length < hopLimit, 'Notice link chain too long.');
      seen.add(current);
      const resolved = [];
      for (const part of [...parts.slice(0, index), ...entry.target.split('/')]) {
        if (part === '' || part === '.') continue;
        if (part === '..') { assert.ok(resolved.length > 0, 'Escaping notice link rejected.'); resolved.pop(); }
        else resolved.push(part);
      }
      assert.ok(insideNoticeRoots(resolved.join('/')), 'Notice link leaves the notice roots.');
      chain.push({ link: current, target: entry.target, kind: index === parts.length - 1 ? 'file-link' : 'directory-link' });
      parts = [...resolved, ...parts.slice(index + 1)];
      index = rootDepth;
      continue;
    }
    if (index < parts.length - 1) {
      if (entry.type !== 'directory') return result('missing-notice');
      index++;
      continue;
    }
    if (entry.type === 'directory') return result('directory-not-notice', entry);
    return result(entry.size === 0 ? 'empty-member' : chain.length ? 'original-text-through-link' : 'original-text', entry);
  }
  return result('missing-notice');
}

export function resolveNoticeMember(entries, requested, hopLimit = 8) {
  const { status, entry, chain } = walkNoticeMember(entries, requested, hopLimit);
  return { status, member: entry?.name ?? null, chain, bytes: entry?.type === 'file' ? entry.size : null,
    sha256: entry?.type === 'file' && entry.size ? sha(entry.data) : null };
}

function packageOfMember(member, packages) {
  const owner = member?.startsWith('usr/share/doc/') ? packages.get(member.split('/')[3]) : null;
  return owner ? { name: owner.name, version: owner.version, source: owner.source, sourceVersion: owner.sourceVersion } : null;
}

export function deriveNoticeLinks(archive, observation, observationSha256) {
  assert.equal(sha(archive), observation.noticeArchiveSha256, 'Notice archive does not match the historical observation.');
  const entries = parseNoticeArchive(archive);
  const recorded = new Map(observation.notices.map(notice => [notice.file, notice]));
  const packages = new Map(observation.packages.map(row => [row.name.split(':')[0], row]));
  const resolveRecorded = requested => {
    const result = resolveNoticeMember(entries, requested);
    if (result.sha256) {
      const original = recorded.get(result.member);
      assert.ok(original && original.sha256 === result.sha256 && original.bytes === result.bytes,
        'Resolved member differs from the historical notice record.');
    }
    return result;
  };
  const packageMappings = observation.packages.filter(row => !row.copyright?.bytes).map(row => {
    const requested = 'usr/share/doc/' + row.name.split(':')[0] + '/copyright', result = resolveRecorded(requested);
    const memberPackage = packageOfMember(result.member, packages);
    return { package: row.name, version: row.version, source: row.source, sourceVersion: row.sourceVersion, requested, ...result,
      memberPackage, sameSourceAndVersion: !!memberPackage && memberPackage.source === row.source && memberPackage.sourceVersion === row.sourceVersion,
      agreementInferred: false };
  });
  const licenseLinks = observation.notices.filter(notice => !notice.bytes && notice.file.startsWith('usr/share/common-licenses/'))
    .map(notice => ({ requested: notice.file, ...resolveRecorded(notice.file) }));
  const resolved = value => value.status === 'original-text-through-link' || value.status === 'original-text';
  return { schemaVersion: 1, scope: linkScope, historicalObservation: 'evidence/container-local-os.v1.json',
    historicalObservationSha256: observationSha256, image: observation.image, noticeArchiveSha256: observation.noticeArchiveSha256,
    archiveMembers: entries.size, packageMappings, licenseLinks,
    counts: { packages: observation.packages.length, unresolvedBefore: packageMappings.length,
      resolvedByLink: packageMappings.filter(resolved).length, unresolvedAfter: packageMappings.filter(value => !resolved(value)).length,
      licenseLinksResolved: licenseLinks.filter(resolved).length, licenseLinksUnresolved: licenseLinks.filter(value => !resolved(value)).length },
    archiveExtracted: false, hostLinksFollowed: false, historicalObservationChanged: false,
    legalApprovalGranted: false, publicationReady: false };
}

export function validateNoticeLinks(record, historical, historicalSha256) {
  assert.equal(record.schemaVersion, 1); assert.equal(record.scope, linkScope);
  for (const key of ['archiveExtracted', 'hostLinksFollowed', 'historicalObservationChanged', 'legalApprovalGranted', 'publicationReady'])
    assert.equal(record[key], false);
  assert.equal(record.historicalObservation, 'evidence/container-local-os.v1.json');
  assert.equal(record.historicalObservationSha256, historicalSha256, 'Notice links are bound to another historical observation.');
  assert.deepEqual(record.image, historical.image); assert.equal(record.noticeArchiveSha256, historical.noticeArchiveSha256);
  const notices = new Map(historical.notices.map(notice => [notice.file, notice]));
  const packages = new Map(historical.packages.map(row => [row.name.split(':')[0], row]));
  const statuses = ['original-text', 'original-text-through-link', 'empty-member', 'missing-notice', 'directory-not-notice'];
  const checkResult = value => {
    assert.ok(statuses.includes(value.status));
    assert.ok(Array.isArray(value.chain) && value.chain.length <= 8);
    // Replay the recorded chain with the same resolver used for original archives.
    // This checks consistency; only verifyContainerLinks rechecks actual archive bytes.
    const entries = new Map();
    const parents = path => {
      safeNoticePath(path);
      const parts = path.split('/');
      for (let depth = 3; depth < parts.length; depth++) {
        const name = parts.slice(0, depth).join('/');
        if (!entries.has(name)) entries.set(name, { name, type: 'directory', size: 0 });
      }
    };
    parents(value.requested);
    if (value.member) parents(value.member);
    for (const hop of value.chain) {
      parents(hop.link);
      assert.ok(['file-link', 'directory-link'].includes(hop.kind));
      assert.ok(entries.get(hop.link)?.type !== 'symlink', 'Duplicate recorded link.');
      entries.set(hop.link, { name: hop.link, type: 'symlink', target: hop.target });
    }
    if (value.status.startsWith('original-text')) {
      const original = notices.get(value.member);
      assert.ok(original && original.bytes === value.bytes && original.sha256 === value.sha256, 'Mapping is not a retained original notice.');
      assert.equal(value.status === 'original-text-through-link', value.chain.length > 0);
    } else assert.equal(value.sha256, null);
    if (value.member) {
      assert.ok(!entries.has(value.member), 'Recorded terminal member conflicts with its link path.');
      entries.set(value.member, { name: value.member, type: value.status === 'directory-not-notice' ? 'directory' : 'file', size: value.bytes });
    }
    const replayed = walkNoticeMember(entries, value.requested);
    assert.deepEqual({ status: replayed.status, member: replayed.entry?.name ?? null, chain: replayed.chain },
      { status: value.status, member: value.member, chain: value.chain }, 'Recorded notice chain does not reach its declared member.');
  };
  const unresolved = historical.packages.filter(row => !row.copyright?.bytes).map(row => row.name).sort();
  assert.deepEqual(record.packageMappings.map(value => value.package).sort(), unresolved);
  for (const value of record.packageMappings) {
    const row = historical.packages.find(item => item.name === value.package);
    assert.deepEqual([value.version, value.source, value.sourceVersion], [row.version, row.source, row.sourceVersion]);
    assert.equal(value.requested, 'usr/share/doc/' + row.name.split(':')[0] + '/copyright');
    assert.equal(value.agreementInferred, false);
    checkResult(value);
    assert.deepEqual(value.memberPackage, packageOfMember(value.member, packages));
    assert.equal(value.sameSourceAndVersion, !!value.memberPackage && value.memberPackage.source === row.source && value.memberPackage.sourceVersion === row.sourceVersion);
  }
  const links = historical.notices.filter(notice => !notice.bytes && notice.file.startsWith('usr/share/common-licenses/')).map(notice => notice.file).sort();
  assert.deepEqual(record.licenseLinks.map(value => value.requested).sort(), links);
  record.licenseLinks.forEach(checkResult);
  const resolved = value => value.status.startsWith('original-text');
  assert.deepEqual(record.counts, { packages: historical.packages.length, unresolvedBefore: record.packageMappings.length,
    resolvedByLink: record.packageMappings.filter(resolved).length, unresolvedAfter: record.packageMappings.filter(value => !resolved(value)).length,
    licenseLinksResolved: record.licenseLinks.filter(resolved).length, licenseLinksUnresolved: record.licenseLinks.filter(value => !resolved(value)).length });
  return record.counts;
}

export function verifyContainerLinks(archivePath, rootDirectory = root) {
  const directory = join(rootDirectory, 'assets/licenses/runtime-assets');
  const historicalBytes = regularBytes(join(directory, 'evidence/container-local-os.v1.json'), 4 * 1024 * 1024);
  const derived = deriveNoticeLinks(regularBytes(archivePath), JSON.parse(historicalBytes), sha(historicalBytes));
  const recorded = JSON.parse(regularBytes(join(directory, 'evidence/container-notice-links.v1.json'), 4 * 1024 * 1024));
  assert.deepEqual(derived, recorded, 'Recorded notice links differ from the retained archive.');
  return { counts: derived.counts, archiveRecomputed: true, publicationReady: false };
}

// Fixed identification strings of commonly embedded libraries. A marker is a hint that a component's
// code or text is present; its absence does not prove the component is absent.
const componentMarkers = [
  ['zlib', /(?:in|de)flate ([0-9]+(?:\.[0-9]+)+) Copyright/g],
  ['libpng', /libpng version ([0-9]+(?:\.[0-9]+)+)/g],
  ['libjpeg-turbo', /libjpeg-turbo version ([0-9]+(?:\.[0-9]+)+)/g],
  ['expat', /expat_([0-9]+(?:\.[0-9]+)+)/g],
  ['libtiff', /LIBTIFF, Version ([0-9]+(?:\.[0-9]+)+)/g],
  ['leptonica', /leptonica-([0-9]+(?:\.[0-9]+)+)/g],
  ['tesseract', /tesseract ([0-9]+\.[0-9]+(?:\.[0-9]+)?)/g],
  ['liblzma', /\b[Ll]iblzma\b()/g],
];
const componentScope = 'local-nuget-cache-native-component-identification-not-attribution-approval';
const coverageStates = ['named-in-retained-notice', 'not-named-in-retained-notice', 'no-retained-notice-file'];

export function componentMarkersIn(bytes) {
  assert.ok(Buffer.isBuffer(bytes) && bytes.length > 0 && bytes.length <= 128 * 1024 * 1024);
  const text = bytes.toString('latin1');
  return componentMarkers.flatMap(([component, pattern]) => {
    const versions = [...new Set([...text.matchAll(pattern)].map(match => match[1] || null))];
    return versions.length ? [{ component, versions: versions.filter(Boolean).sort() }] : [];
  });
}

export function noticeComponents(text) {
  assert.ok(typeof text === 'string' && text.length <= 8 * 1024 * 1024);
  return [...new Set([...text.matchAll(/^# END: (.+?)\s*$/gm)].map(match => match[1]))].sort((a, b) => a.localeCompare(b, 'en'));
}

// Retained notices are the package-supplied files plus the repository's pinned upstream copies.
export function coverageFor(component, notices) {
  if (!notices.length) return 'no-retained-notice-file';
  return notices.some(notice => notice.components.some(name => name.toLowerCase() === component)) ? 'named-in-retained-notice' : 'not-named-in-retained-notice';
}

function noticeRecords(directory, origin) {
  if (!existsSync(directory)) return [];
  return readdirSync(directory).filter(name => /^(?:THIRD-PARTY-NOTICES|LICENSE|NOTICE|COPYING)[A-Za-z0-9.-]*$/i.test(name)).sort()
    .map(name => {
      const bytes = regularBytes(join(directory, name), 8 * 1024 * 1024);
      return { origin, file: name, bytes: bytes.length, sha256: sha(bytes), components: noticeComponents(bytes.toString('utf8')) };
    });
}

export function observeNativeComponents(restoreReviewPath, nugetRoot, rootDirectory = root) {
  const reviewBytes = regularBytes(restoreReviewPath, 8 * 1024 * 1024), review = JSON.parse(reviewBytes);
  assert.equal(review.scope, 'existing-local-api-restore-snapshot-not-fresh-release-graph');
  const packages = review.packages.map(entry => {
    const [id, version] = entry.key.split('/');
    assert.match(id, /^[A-Za-z0-9.]+$/); assert.match(version, /^[0-9][0-9.]*$/);
    const directory = join(nugetRoot, id.toLowerCase(), version);
    const notices = [...noticeRecords(directory, 'package'),
      ...noticeRecords(join(rootDirectory, 'assets/licenses/native-packages', id, version), 'repository-upstream-copy')];
    const assets = entry.assets.map(asset => {
      assert.ok(!asset.file.split('/').includes('..') && !asset.file.startsWith('/'));
      const bytes = regularBytes(join(directory, asset.file), 128 * 1024 * 1024);
      assert.equal(sha(bytes), asset.sha256, 'Cached native payload differs from the restore review.');
      return { file: asset.file, sha256: asset.sha256, markers: componentMarkersIn(bytes) };
    });
    const found = new Map();
    for (const asset of assets) for (const marker of asset.markers) {
      const value = found.get(marker.component) ?? { component: marker.component, versions: new Set(), assets: 0 };
      marker.versions.forEach(item => value.versions.add(item)); value.assets++; found.set(marker.component, value);
    }
    const components = [...found.values()].sort((a, b) => a.component.localeCompare(b.component, 'en'))
      .map(value => ({ component: value.component, versions: [...value.versions].sort(), assets: value.assets,
        noticeCoverage: coverageFor(value.component, notices) }));
    return { key: entry.key, archiveSha256: entry.archiveSha256, notices, assets, components };
  });
  const all = packages.flatMap(entry => entry.components);
  return { schemaVersion: 1, scope: componentScope, restoreReviewSha256: sha(reviewBytes), packages,
    counts: { packages: packages.length, assets: packages.reduce((sum, entry) => sum + entry.assets.length, 0),
      componentObservations: all.length, ...Object.fromEntries(coverageStates.map(state => [state, all.filter(value => value.noticeCoverage === state).length])) },
    markerAbsenceProvesAbsence: false, componentRightsVerified: false, publisherAuthenticated: false,
    legalApprovalGranted: false, publicationReady: false };
}

export function validateNativeComponents(record) {
  assert.equal(record.schemaVersion, 1); assert.equal(record.scope, componentScope);
  for (const key of ['markerAbsenceProvesAbsence', 'componentRightsVerified', 'publisherAuthenticated', 'legalApprovalGranted', 'publicationReady'])
    assert.equal(record[key], false);
  assert.match(record.restoreReviewSha256, /^[a-f0-9]{64}$/);
  const known = new Set(componentMarkers.map(([component]) => component));
  const packages = new Set();
  for (const entry of record.packages) {
    assert.match(entry.key, /^[A-Za-z0-9.]+\/[0-9][0-9.]*$/); assert.match(entry.archiveSha256, /^[a-f0-9]{64}$/);
    assert.ok(!packages.has(entry.key), 'Duplicate native component package.'); packages.add(entry.key);
    for (const notice of entry.notices) {
      assert.ok(['package', 'repository-upstream-copy'].includes(notice.origin));
      assert.match(notice.file, /^(?:THIRD-PARTY-NOTICES|LICENSE|NOTICE|COPYING)[A-Za-z0-9.-]*$/i); assert.match(notice.sha256, /^[a-f0-9]{64}$/);
      assert.ok(Array.isArray(notice.components));
    }
    const found = new Map(), paths = new Set();
    for (const asset of entry.assets) {
      assert.ok(typeof asset.file === 'string' && !asset.file.startsWith('/') && !/[\\:\u0000-\u001f]/.test(asset.file)
        && asset.file.split('/').every(part => part && part !== '.' && part !== '..'), 'Unsafe native component asset path.');
      assert.ok(!paths.has(asset.file), 'Duplicate native component asset.'); paths.add(asset.file);
      assert.match(asset.sha256, /^[a-f0-9]{64}$/);
      const markers = new Set();
      for (const marker of asset.markers) {
        assert.ok(known.has(marker.component) && !markers.has(marker.component), 'Unknown or duplicate component marker.');
        markers.add(marker.component);
        assert.ok(Array.isArray(marker.versions) && marker.versions.every(version => typeof version === 'string' && /^\d+(?:\.\d+)+$/.test(version)));
        assert.deepEqual(marker.versions, [...new Set(marker.versions)].sort(), 'Noncanonical marker versions.');
        const value = found.get(marker.component) ?? { component: marker.component, versions: new Set(), assets: 0 };
        marker.versions.forEach(version => value.versions.add(version)); value.assets++; found.set(marker.component, value);
      }
    }
    const expected = [...found.values()].sort((a, b) => a.component.localeCompare(b.component, 'en'))
      .map(value => ({ component: value.component, versions: [...value.versions].sort(), assets: value.assets,
        noticeCoverage: coverageFor(value.component, entry.notices) }));
    assert.deepEqual(entry.components, expected, 'Component summaries differ from their payload markers.');
  }
  const all = record.packages.flatMap(entry => entry.components);
  assert.deepEqual(record.counts, { packages: record.packages.length, assets: record.packages.reduce((sum, entry) => sum + entry.assets.length, 0),
    componentObservations: all.length, ...Object.fromEntries(coverageStates.map(state => [state, all.filter(value => value.noticeCoverage === state).length])) });
  return record.counts;
}

// Reads an OpenPGP signature packet only to report who claims to have signed; it is never trusted.
export function signatureIssuer(armored) {
  const body = armored.match(/-----BEGIN PGP SIGNATURE-----\r?\n(?:[A-Za-z-]+: .*\r?\n)*\r?\n([A-Za-z0-9+/=\r\n]+?)\r?\n=[A-Za-z0-9+/]{4}\r?\n-----END PGP SIGNATURE-----/)?.[1];
  assert.ok(body, 'Missing armored signature.');
  const packet = Buffer.from(body.replace(/\s+/g, ''), 'base64');
  assert.ok(packet.length >= 8 && packet.length <= 4096);
  let offset = 1, length;
  if (packet[0] & 0x40) {
    assert.equal(packet[0] & 0x3f, 2, 'Not a signature packet.');
    const first = packet[1];
    if (first < 192) { length = first; offset = 2; }
    else if (first < 224) { length = ((first - 192) << 8) + packet[2] + 192; offset = 3; }
    else { assert.equal(first, 255, 'Partial packet lengths are unsupported.'); length = packet.readUInt32BE(2); offset = 6; }
  } else {
    assert.equal((packet[0] >> 2) & 0x0f, 2, 'Not a signature packet.');
    const bytes = [1, 2, 4][packet[0] & 3]; assert.ok(bytes, 'Indeterminate packet length.');
    length = packet.readUIntBE(1, bytes); offset = 1 + bytes;
  }
  assert.ok(offset + length <= packet.length, 'Truncated signature packet.');
  const signature = packet.subarray(offset, offset + length);
  assert.equal(signature[0], 4, 'Only version 4 signatures are inspected.');
  const hashedLength = signature.readUInt16BE(4), hashed = signature.subarray(6, 6 + hashedLength);
  const unhashedLength = signature.readUInt16BE(6 + hashedLength), unhashed = signature.subarray(8 + hashedLength, 8 + hashedLength + unhashedLength);
  const subpackets = area => {
    const values = [];
    for (let index = 0; index < area.length;) {
      let size = area[index], header = 1;
      if (size >= 192 && size < 255) { size = ((size - 192) << 8) + area[index + 1] + 192; header = 2; }
      else if (size === 255) { size = area.readUInt32BE(index + 1); header = 5; }
      assert.ok(size >= 1 && index + header + size <= area.length, 'Invalid signature subpacket.');
      values.push({ type: area[index + header] & 0x7f, data: area.subarray(index + header + 1, index + header + size) });
      index += header + size;
    }
    return values;
  };
  const all = [...subpackets(hashed), ...subpackets(unhashed)];
  const fingerprint = all.find(value => value.type === 33)?.data;
  const keyId = all.find(value => value.type === 16)?.data;
  return { publicKeyAlgorithm: signature[2], hashAlgorithm: signature[3],
    issuerFingerprint: fingerprint?.length === 21 ? fingerprint.subarray(1).toString('hex').toUpperCase() : null,
    issuerKeyId: keyId?.length === 8 ? keyId.toString('hex').toUpperCase() : fingerprint?.length === 21 ? fingerprint.subarray(13).toString('hex').toUpperCase() : null,
    trusted: false };
}

// Recomputes every checksum the descriptor declares; the clearsigned signature itself stays unverified.
export function checkSourceDescriptor(descriptor, archives) {
  const text = descriptor.toString('utf8');
  assert.ok(text.startsWith('-----BEGIN PGP SIGNED MESSAGE-----\n'), 'Descriptor is not clearsigned.');
  const body = text.slice(text.indexOf('\n\n') + 2, text.indexOf('\n-----BEGIN PGP SIGNATURE-----'));
  const block = name => {
    const matches = [...body.matchAll(new RegExp(`^${name}:\\n((?: \\S+ \\d+ \\S+\\n?)+)`, 'gm'))];
    assert.ok(matches.length > 0, `Missing ${name} block.`);
    assert.equal(matches.length, 1, `Duplicate ${name} block.`);
    return matches[0][1].trim().split('\n').map(line => { const [digest, size, file] = line.trim().split(' '); return { digest, size: Number(size), file }; });
  };
  const algorithms = [['Checksums-Sha256', 'sha256'], ['Checksums-Sha1', 'sha1'], ['Files', 'md5']];
  assert.ok(archives instanceof Map && archives.size > 0, 'Source archives are required.');
  for (const [name, algorithm] of algorithms) {
    const entries = block(name), listed = new Set(entries.map(entry => entry.file));
    assert.equal(listed.size, entries.length, `Duplicate archive in ${name}.`);
    assert.deepEqual([...listed].sort(), [...archives.keys()].sort(), `Supplied archives differ from the descriptor ${name} list.`);
    for (const entry of entries) {
      const bytes = archives.get(entry.file);
      assert.ok(Buffer.isBuffer(bytes), `Descriptor lists an archive that was not supplied: ${entry.file}.`);
      assert.ok(Number.isSafeInteger(entry.size) && entry.size >= 0, 'Invalid descriptor archive size.');
      assert.equal(bytes.length, entry.size, `Descriptor size differs for ${entry.file}.`);
      assert.equal(createHash(algorithm).update(bytes).digest('hex'), entry.digest, `Descriptor ${algorithm} differs for ${entry.file}.`);
    }
  }
  return { archives: [...archives.keys()].sort(), algorithms: algorithms.map(([, algorithm]) => algorithm), checksumsVerified: true,
    signer: signatureIssuer(text), signatureVerified: false, signatureStatus: 'blocked-no-authenticated-keyring' };
}

export function verifyDownloads(directory) {
  const { evidence } = checkEvidence();
  for (const file of evidence.fonts.sourceFiles) {
    const bytes = regularBytes(join(directory, file.name));
    assert.equal(bytes.length, file.bytes); assert.equal(sha(bytes), file.sha256, 'Downloaded source archive changed.');
  }
  const archives = new Map(evidence.fonts.sourceFiles.filter(file => !file.name.endsWith('.dsc'))
    .map(file => [file.name, regularBytes(join(directory, file.name))]));
  const descriptor = checkSourceDescriptor(regularBytes(join(directory, 'fonts-liberation_1.07.4-11.dsc')), archives);
  assert.deepEqual(descriptor, evidence.fonts.descriptor, 'Recorded descriptor check differs from the downloaded sources.');
  const comparison = compareModel(regularBytes(join(root, evidence.model.file)),
    regularBytes(join(directory, 'ukr-upstream.traineddata')), evidence.model.reference);
  assert.equal(comparison.relation, evidence.model.relation);
  return { comparison, descriptor, downloadedReferencesRecomputed: true, publicationReady: false };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const args = process.argv.slice(2);
  if (args.length === 1 && args[0] === 'observe-macos') {
    const report = observeMacos(), output = join(root, 'artifacts/native-runtime-provenance', `${Date.now()}-${randomUUID().slice(0, 8)}`);
    mkdirSync(output, { recursive: true }); writeFileSync(join(output, 'macos.json'), JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
    console.log(JSON.stringify({ output, correlated: report.rows.filter(row => row.status === 'local-build-correlation').length, total: report.rows.length }));
  } else if (args.length === 2 && args[0] === 'verify-downloads') console.log(JSON.stringify(verifyDownloads(resolve(args[1])), null, 2));
  else if (args.length === 2 && args[0] === 'observe-container') console.log(JSON.stringify(observeContainer(args[1])));
  else if (args.length === 2 && args[0] === 'resolve-container-links') {
    const directory = join(root, 'assets/licenses/runtime-assets'), historicalBytes = regularBytes(join(directory, 'evidence/container-local-os.v1.json'), 4 * 1024 * 1024);
    const record = deriveNoticeLinks(regularBytes(resolve(args[1])), JSON.parse(historicalBytes), sha(historicalBytes));
    const output = join(root, 'artifacts/native-runtime-provenance', `${Date.now()}-${randomUUID().slice(0, 8)}`);
    mkdirSync(output, { recursive: true }); writeFileSync(join(output, 'container-notice-links.json'), JSON.stringify(record, null, 2) + '\n', { flag: 'wx' });
    console.log(JSON.stringify({ output, counts: record.counts }));
  } else if (args.length === 1 && args[0] === 'explain-macos') {
    const report = explainMacos(), output = join(root, 'artifacts/native-runtime-provenance', `${Date.now()}-${randomUUID().slice(0, 8)}`);
    mkdirSync(output, { recursive: true }); writeFileSync(join(output, 'macos-differences.json'), JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
    console.log(JSON.stringify({ output, ...validateDifferences(report, loadRuntimeAssets(root)) }));
  } else if (args.length === 3 && args[0] === 'observe-native-components') {
    const record = observeNativeComponents(resolve(args[1]), resolve(args[2]));
    const output = join(root, 'artifacts/native-runtime-provenance', `${Date.now()}-${randomUUID().slice(0, 8)}`);
    mkdirSync(output, { recursive: true }); writeFileSync(join(output, 'native-components.json'), JSON.stringify(record, null, 2) + '\n', { flag: 'wx' });
    console.log(JSON.stringify({ output, counts: record.counts }));
  } else if (args.length === 2 && args[0] === 'verify-container-links') console.log(JSON.stringify(verifyContainerLinks(resolve(args[1])), null, 2));
  else if (args.length === 2 && args[0] === 'verify-component-payloads') console.log(JSON.stringify(verifyComponentPayloads(resolve(args[1])), null, 2));
  else { assert.equal(args.length, 0, 'Use observe-macos, explain-macos, observe-container <image-digest>, resolve-container-links <archive>, verify-container-links <archive>, observe-native-components <restore-review> <nuget-root>, verify-component-payloads <package-directory> or verify-downloads <download-directory>.'); console.log(JSON.stringify(checkEvidence(), null, 2)); }
}
