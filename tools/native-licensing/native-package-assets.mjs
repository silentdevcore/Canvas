import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { lstatSync, readFileSync, realpathSync } from 'node:fs';
import { isAbsolute, join } from 'node:path';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
export function nativePayload(path) {
  assert.equal(typeof path, 'string');
  assert.ok(path && !isAbsolute(path) && !/[\\:\x00-\x1f\x7f]/.test(path) &&
    path.split('/').every(part => part && part !== '.' && part !== '..'), 'Unsafe native payload path.');
  const runtime = /^runtimes\/([^/]+)\/native\//i.exec(path);
  const legacy = /^(x86|x64|arm64)\/.*\.dll$/i.exec(path);
  const isNative = path.split('/').some(part => part.toLowerCase() === 'native') ||
    /\.(?:so(?:\.\d+)*|dylib|a|lib|wasm)$/i.test(path) || !!legacy;
  if (!isNative) return null;
  if (runtime) assert.match(runtime[1], /^[A-Za-z0-9][A-Za-z0-9_.-]*$/, 'Invalid declared native RID.');
  return { file: path, runtimeIdentifier: runtime?.[1] ?? null, declaredArchitecture: legacy?.[1].toLowerCase() ?? null,
    binding: runtime ? 'runtime-native-directory' : legacy ? 'legacy-architecture-directory' : 'unassigned-native-payload',
    architectureVerified: false, deploymentSelection: 'not-evaluated' };
}

function bytes(directory, path, limit) {
  assert.ok(!lstatSync(directory).isSymbolicLink(), 'Native package roots cannot be symlinks.');
  let current = realpathSync(directory);
  for (const part of path.split('/')) {
    assert.ok(part && part !== '.' && part !== '..' && !part.includes('\\') && !part.includes(':'));
    current = join(current, part);
    assert.ok(!lstatSync(current).isSymbolicLink(), 'Native package symlinks are not accepted.');
  }
  const info = lstatSync(current);
  assert.ok(info.isFile() && info.size <= limit, 'Invalid native package file size.');
  return readFileSync(current);
}

export function createNativePackageInspector(inspectArchive) {
  assert.equal(typeof inspectArchive, 'function');
  const inspected = new Map();
  return ({ packageDirectory, archiveFile, files }) => {
    assert.ok(Array.isArray(files));
    const candidates = files.map(nativePayload).filter(Boolean).sort((a, b) => a.file < b.file ? -1 : a.file > b.file ? 1 : 0);
    assert.equal(new Set(candidates.map(asset => asset.file.toLowerCase())).size, candidates.length, 'Duplicate declared native payload.');
    if (!candidates.length) return { status: 'not-inspected-no-native-payload-in-restore-manifest', assets: [] };
    assert.ok(candidates.length <= 512);
    assert.match(archiveFile, /^[A-Za-z0-9_.+-]+\.nupkg$/);
    const archive = bytes(packageDirectory, archiveFile, 256 * 1024 * 1024);
    const digest = bytes(packageDirectory, archiveFile + '.sha512', 1024).toString('utf8').trim();
    assert.match(digest, /^[A-Za-z0-9+/]{86}==$/, 'Invalid native archive cache digest.');
    assert.equal(createHash('sha512').update(archive).digest('base64'), digest, 'Native archive cache digest changed.');
    const archiveSha256 = sha256(archive);
    const key = JSON.stringify([realpathSync(packageDirectory), archiveSha256, candidates.map(asset => asset.file)]);
    let evidence = inspected.get(key);
    if (!evidence) {
      evidence = inspectArchive({ archive: join(packageDirectory, archiveFile), packageDirectory, files: candidates.map(asset => asset.file) });
      assert.equal(evidence.schemaVersion, 1);
      assert.equal(evidence.archiveSha256, archiveSha256);
      assert.equal(evidence.archiveSha512, digest);
      assert.equal(evidence.cacheDigestVerified, true);
      assert.equal(evidence.archiveAuthenticity, 'not-verified');
      assert.deepEqual(evidence.assets.map(asset => asset.file), candidates.map(asset => asset.file), 'Native inspector coverage differs.');
      inspected.set(key, structuredClone(evidence));
    }
    const assets = candidates.map((candidate, index) => {
      const proof = evidence.assets[index];
      assert.match(proof.sha256, /^[a-f0-9]{64}$/);
      assert.ok(Number.isSafeInteger(proof.bytes) && proof.bytes >= 0 && proof.bytes <= 128 * 1024 * 1024);
      const cached = bytes(packageDirectory, candidate.file, 128 * 1024 * 1024);
      assert.equal(cached.length, proof.bytes);
      assert.equal(sha256(cached), proof.sha256, 'Native cache payload changed after inspection.');
      return { ...candidate, bytes: proof.bytes, sha256: proof.sha256, componentAttribution: 'review-required' };
    });
    return { status: 'archive-and-cache-verified', archiveSha256, archiveSha512: digest,
      cacheDigestVerified: true, archiveAuthenticity: 'not-verified', nativeComponentsReviewed: false, assets };
  };
}
