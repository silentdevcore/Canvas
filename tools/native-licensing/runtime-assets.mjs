import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = fileURLToPath(new URL('../../', import.meta.url));
export const runtimeRegistryPath = 'assets/licenses/runtime-assets';
const profiles = ['fonts', 'ocr'];
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const encoded = value => Buffer.from(JSON.stringify(value, null, 2) + '\n');

function safePath(root, path, optional = false) {
  assert.ok(!lstatSync(root).isSymbolicLink(), 'Runtime roots cannot be symlinks.');
  assert.equal(typeof path, 'string');
  assert.ok(path && !isAbsolute(path) && !path.includes('\\') && !path.includes(':') &&
    path.split('/').every(part => part && part !== '.' && part !== '..'), 'Unsafe runtime asset path.');
  let current = resolve(root);
  for (const part of path.split('/')) {
    current = join(current, part);
    if (!existsSync(current)) {
      // lstat also detects dangling symlinks, which existsSync follows.
      let info;
      try { info = lstatSync(current); } catch (error) { if (error.code !== 'ENOENT') throw error; }
      assert.ok(!info, 'Runtime asset symlinks are not accepted.');
      if (optional) return null;
      assert.fail('Missing runtime asset: ' + path);
    }
    assert.ok(!lstatSync(current).isSymbolicLink(), 'Runtime asset symlinks are not accepted.');
  }
  assert.ok(lstatSync(current).isFile(), 'Runtime asset is not a regular file.');
  return current;
}

function pinnedBytes(root, record, limit, optional = false) {
  assert.match(record.sha256, /^[a-f0-9]{64}$/);
  const path = safePath(root, record.file, optional);
  if (!path) return null;
  const size = lstatSync(path).size;
  assert.ok(size > 0 && size <= limit, 'Invalid runtime asset size.');
  const bytes = readFileSync(path);
  assert.equal(sha256(bytes), record.sha256, 'Runtime asset hash changed: ' + record.file);
  return bytes;
}

function walk(root, path, files = []) {
  const absolute = join(root, path);
  if (!existsSync(absolute)) return files;
  assert.ok(!lstatSync(absolute).isSymbolicLink(), 'Runtime directories cannot be symlinks.');
  for (const entry of readdirSync(absolute, { withFileTypes: true })) {
    const child = path + '/' + entry.name;
    assert.ok(!entry.isSymbolicLink(), 'Runtime asset symlinks are not accepted.');
    if (entry.isDirectory()) walk(root, child, files);
    else { assert.ok(entry.isFile()); files.push(child); }
  }
  return files.sort();
}

export function loadRuntimeAssets(root = repository) {
  const directory = join(root, runtimeRegistryPath);
  const registryBytes = readFileSync(safePath(root, runtimeRegistryPath + '/manifest.v1.json'));
  const registry = JSON.parse(registryBytes);
  assert.equal(registry.schemaVersion, 1);
  assert.equal(registry.legalApprovalGranted, false);
  assert.equal(registry.publicationReady, false);
  assert.ok(Array.isArray(registry.groups) && registry.groups.length > 0);
  assert.ok(Array.isArray(registry.scanRoots) && registry.scanRoots.length > 0);
  const assetPaths = new Set(), groupIds = new Set(), noticeFiles = new Map(), present = [], absentOptional = [];
  for (const group of registry.groups) {
    assert.match(group.id, /^[a-z0-9]+(?:-[a-z0-9]+)*$/);
    assert.ok(!groupIds.has(group.id), 'Duplicate runtime group.'); groupIds.add(group.id);
    assert.ok(profiles.includes(group.profile));
    assert.ok(['source-matched', 'unresolved'].includes(group.provenance));
    assert.ok(group.reviewNote?.length > 15);
    assert.ok(Array.isArray(group.notices));
    assert.ok(Array.isArray(group.assets) && group.assets.length > 0);
    if (group.provenance === 'source-matched') {
      assert.ok(group.notices.length > 0);
      assert.match(group.source.url, /^https:\/\/[^\s]+$/);
      assert.match(group.source.archiveSha256 ?? group.source.commit, /^(?:[a-f0-9]{64}|[a-f0-9]{40})$/);
    } else {
      assert.equal(group.source, null, 'Unknown provenance must not invent an upstream source.');
      assert.deepEqual(group.notices, [], 'Unresolved assets must not inherit an unrelated license.');
    }
    for (const notice of group.notices) {
      const bytes = pinnedBytes(directory, notice, 2 * 1024 * 1024);
      const text = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
      assert.ok(!text.includes('\0') && !/-----BEGIN (?:EC |RSA |ENCRYPTED )?PRIVATE KEY-----/.test(text));
      const prior = noticeFiles.get(notice.file);
      if (prior) assert.ok(prior.equals(bytes), 'Conflicting runtime notice.');
      noticeFiles.set(notice.file, bytes);
    }
    for (const asset of group.assets) {
      assert.equal(typeof asset.optionalLocalFile, 'boolean');
      assert.ok(!asset.optionalLocalFile || group.provenance === 'unresolved');
      assert.ok(!assetPaths.has(asset.file.toLowerCase()), 'Duplicate runtime asset.');
      assetPaths.add(asset.file.toLowerCase());
      const bytes = pinnedBytes(root, asset, 512 * 1024 * 1024, asset.optionalLocalFile);
      if (asset.gitBlob) {
        assert.equal(group.provenance, 'source-matched');
        assert.match(asset.gitBlob, /^[a-f0-9]{40}$/);
        assert.equal(createHash('sha1').update(Buffer.from(`blob ${bytes.length}\0`)).update(bytes).digest('hex'), asset.gitBlob);
      }
      (bytes ? present : absentOptional).push(asset.file);
    }
  }
  const scanned = new Set();
  for (const scan of registry.scanRoots) {
    // Validate even an absent scan root without following a symlink.
    safePath(root, scan.path + '/.inventory-path-probe', true);
    assert.ok(Array.isArray(scan.extensions) && scan.extensions.length > 0);
    for (const extension of scan.extensions) assert.match(extension, /^\.[a-z0-9]+$/);
    for (const file of walk(root, scan.path)) {
      if (!scan.extensions.some(extension => file.toLowerCase().endsWith(extension) ||
        (extension === '.so' && /\.so(?:\.\d+)+$/i.test(file)))) continue;
      assert.ok(assetPaths.has(file.toLowerCase()), 'Unclassified runtime asset: ' + file);
      scanned.add(file);
    }
  }
  for (const file of present) assert.ok(scanned.has(file), 'Runtime asset is outside inventory roots: ' + file);
  return { registry, registrySha256: sha256(registryBytes), noticeFiles, present, absentOptional };
}

export function runtimeBundleFiles(evidence, profile) {
  assert.ok(profiles.includes(profile), 'Unknown runtime notice profile.');
  const groups = evidence.registry.groups.filter(group => group.profile === profile);
  const files = new Map();
  for (const group of groups) for (const notice of group.notices)
    files.set(notice.file, evidence.noticeFiles.get(notice.file));
  files.set('inventory.json', encoded({ schemaVersion: 1, profile,
    scope: 'catalog-of-application-owned-assets-not-proof-of-selected-deployment-or-native-transitive-licenses',
    sourceRegistrySha256: evidence.registrySha256, publicationReady: false, legalApprovalGranted: false,
    groups, unresolvedGroups: groups.filter(group => group.provenance === 'unresolved').map(group => group.id),
    limitations: evidence.registry.limitations }));
  return files;
}

export function materializeRuntimeBundle(evidence, profile, destination, check = false) {
  const files = runtimeBundleFiles(evidence, profile);
  const actual = walk(dirname(destination), destination.split(sep).at(-1))
    .map(path => path.slice(destination.split(sep).at(-1).length + 1));
  for (const file of actual) assert.ok(files.has(file), 'Unexpected generated runtime notice: ' + file);
  for (const [file, bytes] of files) {
    const path = join(destination, file);
    if (check) assert.ok(readFileSync(safePath(destination, file)).equals(bytes), 'Generated runtime notice drift: ' + path);
    else {
      assert.ok(!existsSync(destination) || !lstatSync(destination).isSymbolicLink());
      mkdirSync(dirname(path), { recursive: true }); writeFileSync(path, bytes);
    }
  }
  return { profile, files: files.size, assetCount: evidence.registry.groups.filter(group => group.profile === profile)
    .reduce((sum, group) => sum + group.assets.length, 0) };
}

export function checkRuntimeBundles(root = repository, write = false) {
  const evidence = loadRuntimeAssets(root), results = [];
  for (const profile of profiles) results.push(materializeRuntimeBundle(evidence, profile,
    join(root, runtimeRegistryPath, 'bundles', profile), !write));
  results.push(materializeRuntimeBundle(evidence, 'fonts', join(root, 'pvad-designer/public/licenses/runtime-assets'), !write));
  return { publicationReady: false, presentAssets: evidence.present.length,
    absentOptionalAssets: evidence.absentOptional.length, unresolvedGroups: evidence.registry.groups
      .filter(group => group.provenance === 'unresolved').map(group => group.id), bundles: results };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  assert.ok(process.argv.length === 2 || (process.argv.length === 3 && process.argv[2] === '--write'));
  console.log(JSON.stringify(checkRuntimeBundles(repository, process.argv[2] === '--write'), null, 2));
}
