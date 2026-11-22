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
  assert.deepEqual(evidence.observations.map(record => record.file).sort(), ['evidence/container-local-os.v1.json', 'evidence/macos-local-builds.v1.json']);
  for (const observation of evidence.observations) {
    assert.match(observation.file, /^evidence\/[a-z0-9.-]+\.json$/);
    assert.match(observation.sha256, /^[a-f0-9]{64}$/);
    const bytes = readObservation(observation.file);
    assert.equal(sha(bytes), observation.sha256, 'Recorded observation changed.');
    validateObservation(JSON.parse(bytes), assets);
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

export function verifyDownloads(directory) {
  const { evidence } = checkEvidence();
  for (const file of evidence.fonts.sourceFiles) {
    const bytes = regularBytes(join(directory, file.name));
    assert.equal(bytes.length, file.bytes); assert.equal(sha(bytes), file.sha256, 'Downloaded source archive changed.');
  }
  const comparison = compareModel(regularBytes(join(root, evidence.model.file)),
    regularBytes(join(directory, 'ukr-upstream.traineddata')), evidence.model.reference);
  assert.equal(comparison.relation, evidence.model.relation);
  return { comparison, downloadedReferencesRecomputed: true, publicationReady: false };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const args = process.argv.slice(2);
  if (args.length === 1 && args[0] === 'observe-macos') {
    const report = observeMacos(), output = join(root, 'artifacts/native-runtime-provenance', `${Date.now()}-${randomUUID().slice(0, 8)}`);
    mkdirSync(output, { recursive: true }); writeFileSync(join(output, 'macos.json'), JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
    console.log(JSON.stringify({ output, correlated: report.rows.filter(row => row.status === 'local-build-correlation').length, total: report.rows.length }));
  } else if (args.length === 2 && args[0] === 'verify-downloads') console.log(JSON.stringify(verifyDownloads(resolve(args[1])), null, 2));
  else if (args.length === 2 && args[0] === 'observe-container') console.log(JSON.stringify(observeContainer(args[1])));
  else { assert.equal(args.length, 0, 'Use observe-macos, observe-container <image-digest> or verify-downloads <download-directory>.'); console.log(JSON.stringify(checkEvidence(), null, 2)); }
}
