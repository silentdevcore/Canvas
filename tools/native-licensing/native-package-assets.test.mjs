import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { createNativePackageInspector, nativePayload } from './native-package-assets.mjs';
import { collectNugetNotices } from './package-documents.mjs';

const sha = (value, algorithm = 'sha256', encoding = 'hex') => createHash(algorithm).update(value).digest(encoding);
const payload = 'runtimes/linux-x64/native/libsynthetic.so';
function fixture(callback) {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-native-dependency-'));
  const root = join(directory, 'cache'); mkdirSync(dirname(join(root, payload)), { recursive: true });
  const archiveFile = 'synthetic.library.1.2.3.nupkg', archive = Buffer.from('Synthetic original archive');
  writeFileSync(join(root, archiveFile), archive);
  const digest = sha(archive, 'sha512', 'base64');
  writeFileSync(join(root, archiveFile + '.sha512'), digest);
  writeFileSync(join(root, payload), 'Synthetic payload');
  const proof = { schemaVersion: 1, archiveSha256: sha(archive), archiveSha512: digest,
    cacheDigestVerified: true, archiveAuthenticity: 'not-verified',
    assets: [{ file: payload, bytes: 17, sha256: sha('Synthetic payload') }] };
  const request = { packageDirectory: root, archiveFile, files: [payload, 'lib/net10.0/Managed.dll'] };
  try { callback({ directory, root, request, proof }); }
  finally { rmSync(directory, { recursive: true, force: true }); }
}

test('native declaration classification distinguishes declared RIDs, legacy folders and unassigned payloads', () => {
  assert.deepEqual(nativePayload(payload), { file: payload, runtimeIdentifier: 'linux-x64', declaredArchitecture: null,
    binding: 'runtime-native-directory', architectureVerified: false, deploymentSelection: 'not-evaluated' });
  assert.equal(nativePayload('x86/tesseract.dll').declaredArchitecture, 'x86');
  assert.equal(nativePayload('x64/leptonica.dll').runtimeIdentifier, null);
  for (const file of ['build/libextra.so.2.1', 'native/descriptor.json', 'assets/file.wasm', 'lib/libsomething.a', 'libs/extra.LIB'])
    assert.equal(nativePayload(file).binding, 'unassigned-native-payload');
  for (const file of ['lib/net10.0/Managed.dll', 'runtimes/win-x64/lib/net10.0/Managed.dll', 'README.md']) assert.equal(nativePayload(file), null);
});

test('unsafe native paths and malformed declared RIDs fail before filesystem access', () => {
  for (const path of ['../native/escape.so', '/native/absolute.so', 'C:/native/escape.dll', 'native\\escape.dll',
    'native/./escape.so', 'native//escape.so', 'native/escape\0.so', 'runtimes/bad@rid/native/file.so'])
    assert.throws(() => nativePayload(path));
});

test('archive/cache evidence retains original hashes without certifying component rights or deployment selection', () => fixture(({ request, proof }) => {
  let calls = 0;
  const inspect = createNativePackageInspector(input => {
    assert.deepEqual(input.files, [payload]); calls++; return structuredClone(proof);
  });
  const result = inspect(request);
  assert.equal(result.status, 'archive-and-cache-verified');
  assert.equal(result.nativeComponentsReviewed, false);
  assert.equal(result.archiveAuthenticity, 'not-verified');
  assert.equal(result.assets[0].componentAttribution, 'review-required');
  assert.equal(result.assets[0].deploymentSelection, 'not-evaluated');
  result.assets[0].sha256 = 'modified caller object';
  assert.equal(inspect(request).assets[0].sha256, proof.assets[0].sha256);
  assert.equal(calls, 1);
}));

test('a managed-only manifest makes no native-absence inspection claim', () => {
  const inspect = createNativePackageInspector(() => assert.fail('No native archive requested.'));
  assert.deepEqual(inspect({ files: ['lib/net10.0/Managed.dll'] }), {
    status: 'not-inspected-no-native-payload-in-restore-manifest', assets: [] });
});

test('changed cache bytes are rejected even after memoized archive inspection', () => fixture(({ root, request, proof }) => {
  const inspect = createNativePackageInspector(() => structuredClone(proof)); inspect(request);
  writeFileSync(join(root, payload), 'Synthetic payloae');
  assert.throws(() => inspect(request), /payload changed/);
}));

test('archive drift or a malformed cache digest fail before the archive inspector', () => {
  for (const change of ['archive', 'digest']) fixture(({ root, request }) => {
    if (change === 'archive') writeFileSync(join(root, request.archiveFile), 'Different archive');
    else writeFileSync(join(root, request.archiveFile + '.sha512'), 'malformed');
    assert.throws(() => createNativePackageInspector(() => assert.fail('Must fail before inspection.'))(request), /digest/);
  });
});

test('incomplete coverage, wrong archive hashes and approval-like inspector claims cannot be accepted', () => {
  for (const change of ['coverage', 'archive', 'digest', 'authenticity', 'cache']) fixture(({ request, proof }) => {
    if (change === 'coverage') proof.assets = [];
    if (change === 'archive') proof.archiveSha256 = 'a'.repeat(64);
    if (change === 'digest') proof.archiveSha512 = 'different';
    if (change === 'authenticity') proof.archiveAuthenticity = 'trusted';
    if (change === 'cache') proof.cacheDigestVerified = false;
    assert.throws(() => createNativePackageInspector(() => proof)(request));
  });
});

test('duplicate paths and invalid file sizes or payload hashes fail', () => {
  for (const change of ['duplicate', 'size', 'hash']) fixture(({ request, proof }) => {
    if (change === 'duplicate') request.files.push(payload);
    if (change === 'size') proof.assets[0].bytes = -1;
    if (change === 'hash') proof.assets[0].sha256 = 'not-a-hash';
    assert.throws(() => createNativePackageInspector(() => proof)(request));
  });
});

test('symlinked native payloads or archive digests are rejected', () => {
  for (const target of ['payload', 'digest']) fixture(({ root, request, proof }) => {
    const file = join(root, target === 'payload' ? payload : request.archiveFile + '.sha512');
    const original = readFileSync(file), replacement = join(root, 'replacement');
    writeFileSync(replacement, original); rmSync(file); symlinkSync(replacement, file);
    assert.throws(() => createNativePackageInspector(() => proof)(request), /symlinks/);
  });
});

test('notice collection requires native verification and retains distinct package licenses and original notices', () => fixture(({ root, request, proof, directory }) => {
  const nuspec = '<package><metadata><id>Synthetic.Library</id><version>1.2.3</version><license type="expression">MIT</license></metadata></package>';
  writeFileSync(join(root, 'synthetic.library.nuspec'), nuspec);
  writeFileSync(join(root, 'LICENSE.txt'), 'Original library license');
  const assets = { packageFolders: { [directory]: {} }, libraries: { 'Synthetic.Library/1.2.3': {
    type: 'package', path: 'cache', files: [...request.files, 'LICENSE.txt'] } } };
  assert.throws(() => collectNugetNotices(assets, join(directory, 'missing-inspector')), /inspector is required/);
  const [record] = collectNugetNotices(assets, join(directory, 'verified'), undefined, createNativePackageInspector(() => proof));
  assert.equal(record.license.value, 'MIT');
  assert.equal(record.legalApproval, 'not-granted');
  assert.equal(record.nativeAssets.nativeComponentsReviewed, false);
  assert.equal(readFileSync(join(directory, 'verified', record.notices[0].file), 'utf8'), 'Original library license');
}));

test('packaging uses the standard .NET ZIP inspector and includes native evidence in its protected report', () => {
  const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
  const runner = read('./joint-package-rehearsal.mjs'), probe = read('./PackageDocumentationProbe/NativePackageInspection.cs');
  assert.match(probe, /new ZipArchive\(stream, ZipArchiveMode.Read\)/);
  assert.match(probe, /CryptographicOperations.FixedTimeEquals/);
  assert.match(runner, /createNativePackageInspector/);
  assert.match(runner, /nativePackageInventory/);
});
