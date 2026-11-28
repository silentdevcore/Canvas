import assert from 'node:assert/strict';
import { constants, createCipheriv, createDecipheriv, createHash, createPublicKey, privateDecrypt, publicEncrypt, randomBytes } from 'node:crypto';
import { lstatSync, mkdirSync, openSync, readFileSync, realpathSync, rmSync, closeSync, fsyncSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const format = 'pvad-native-support-v1';
const algorithm = 'RSA-OAEP-SHA256+A256GCM';
const maxBytes = 64 * 1024 * 1024;
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const digest = value => assert.match(value, /^[a-f0-9]{64}$/);
const date = value => { assert.equal(new Date(value).toISOString(), value); return Date.parse(value); };
const name = value => assert.match(value, /^(?:Mapping\.txt|PVAD\.[A-Za-z0-9.]+\.pdb)$/);
const exact = (object, keys) => assert.deepEqual(Object.keys(object).sort(), [...keys].sort());

function regularFile(path, limit = maxBytes) {
  assert.ok(lstatSync(path).isFile(), 'Only regular support input files are allowed.');
  assert.equal(realpathSync(path), resolve(path), 'Symlinked support input paths are not allowed.');
  assert.ok(lstatSync(path).size <= limit, 'Support input exceeds its size limit.');
  return readFileSync(path);
}

function validateManifest(manifest) {
  exact(manifest, ['schemaVersion', 'buildId', 'version', 'commit', 'sourceDirty', 'obfuscarVersion', 'createdAt', 'deleteAfter', 'assemblies', 'files']);
  assert.equal(manifest.schemaVersion, 1);
  assert.match(manifest.buildId, /^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$/);
  assert.match(manifest.version, /^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)$/);
  assert.match(manifest.commit, /^(?:[a-f0-9]{40}|[a-f0-9]{64})$/);
  assert.equal(typeof manifest.sourceDirty, 'boolean');
  assert.match(manifest.obfuscarVersion, /^\d+\.\d+\.\d+$/);
  assert.ok(date(manifest.deleteAfter) > date(manifest.createdAt), 'A future retention deadline is required.');
  assert.ok(Array.isArray(manifest.assemblies) && manifest.assemblies.length > 0 && manifest.assemblies.length <= 128);
  const assemblies = new Map();
  for (const entry of manifest.assemblies) {
    exact(entry, ['name', 'originalSha256', 'protectedSha256']);
    assert.match(entry.name, /^PVAD\.[A-Za-z0-9.]+\.dll$/);
    assert.ok(!assemblies.has(entry.name), 'Duplicate assembly.');
    digest(entry.originalSha256); digest(entry.protectedSha256);
    assemblies.set(entry.name, entry);
  }
  assert.ok(Array.isArray(manifest.files) && manifest.files.length > 0 && manifest.files.length <= 129);
  const names = new Set();
  let bytes = 0;
  for (const entry of manifest.files) {
    exact(entry, ['name', 'kind', 'sha256', 'size', 'assembly']);
    name(entry.name); digest(entry.sha256);
    assert.ok(!names.has(entry.name), 'Duplicate support filename.'); names.add(entry.name);
    assert.ok(Number.isSafeInteger(entry.size) && entry.size > 0); bytes += entry.size;
    if (entry.name === 'Mapping.txt') {
      assert.equal(entry.kind, 'obfuscar-map'); assert.equal(entry.assembly, null);
    } else {
      assert.equal(entry.kind, 'original-portable-pdb');
      assert.equal(entry.assembly, entry.name.replace(/\.pdb$/, '.dll'));
      assert.ok(assemblies.has(entry.assembly), 'Symbols need their original and protected assembly hashes.');
    }
  }
  assert.ok(names.has('Mapping.txt'), 'The complete joint Obfuscar mapping is required.');
  assert.ok(bytes <= maxBytes, 'Support payload exceeds its size limit.');
}

// Only public recipient material is needed on a build runner. The private key stays with support.
export function sealSupportBundle({ metadata, assemblies, files, publicKeyPem }) {
  assert.ok(!String(publicKeyPem).includes('PRIVATE KEY'), 'Build runners must receive only a public recipient key.');
  const key = createPublicKey(publicKeyPem);
  assert.equal(key.asymmetricKeyType, 'rsa');
  assert.ok(key.asymmetricKeyDetails.modulusLength >= 3072, 'Support recipient requires RSA 3072 or stronger.');
  assert.ok(Array.isArray(assemblies) && assemblies.length > 0 && assemblies.length <= 128);
  const manifest = { ...metadata, schemaVersion: 1, assemblies: assemblies.map(entry => {
    const original = regularFile(entry.originalPath), protectedBytes = regularFile(entry.protectedPath);
    assert.equal(hash(original), entry.originalSha256, 'Original assembly checksum mismatch.');
    assert.equal(hash(protectedBytes), entry.protectedSha256, 'Protected assembly checksum mismatch.');
    return { name: entry.name, originalSha256: entry.originalSha256, protectedSha256: entry.protectedSha256 };
  }), files: [] };
  assert.ok(Array.isArray(files) && files.length > 0 && files.length <= 129);
  let totalBytes = 0;
  const contents = files.map(entry => {
    name(entry.name);
    totalBytes += lstatSync(entry.path).size;
    assert.ok(totalBytes <= maxBytes, 'Support payload exceeds its size limit.');
    const data = regularFile(entry.path);
    if (entry.name.endsWith('.pdb')) assert.equal(data.subarray(0, 4).toString('ascii'), 'BSJB', 'Only portable original symbols are supported.');
    manifest.files.push({ name: entry.name, kind: entry.name === 'Mapping.txt' ? 'obfuscar-map' : 'original-portable-pdb',
      sha256: hash(data), size: data.length, assembly: entry.name === 'Mapping.txt' ? null : entry.name.replace(/\.pdb$/, '.dll') });
    return data.toString('base64');
  });
  validateManifest(manifest);
  const plaintext = Buffer.from(JSON.stringify({ manifest, contents }));
  assert.ok(plaintext.length <= maxBytes * 2);
  const fingerprint = hash(key.export({ format: 'der', type: 'spki' }));
  const header = { format, algorithm, recipientSha256: fingerprint };
  const aesKey = randomBytes(32), nonce = randomBytes(12);
  try {
    const cipher = createCipheriv('aes-256-gcm', aesKey, nonce);
    cipher.setAAD(Buffer.from(JSON.stringify(header)));
    const ciphertext = Buffer.concat([cipher.update(plaintext), cipher.final()]);
    return Buffer.from(JSON.stringify({ ...header,
      wrappedKey: publicEncrypt({ key, padding: constants.RSA_PKCS1_OAEP_PADDING, oaepHash: 'sha256' }, aesKey).toString('base64'),
      nonce: nonce.toString('base64'), tag: cipher.getAuthTag().toString('base64'), ciphertext: ciphertext.toString('base64') }) + '\n');
  } finally { aesKey.fill(0); plaintext.fill(0); }
}

const base64 = value => {
  assert.equal(typeof value, 'string');
  const bytes = Buffer.from(value, 'base64');
  assert.equal(bytes.toString('base64'), value, 'Noncanonical base64.');
  return bytes;
};

export function openSupportBundle(bytes, privateKeyPem, { now = Date.now() } = {}) {
  assert.ok(bytes.length <= maxBytes * 3, 'Encrypted support archive exceeds its size limit.');
  const envelope = JSON.parse(bytes.toString('utf8'));
  exact(envelope, ['format', 'algorithm', 'recipientSha256', 'wrappedKey', 'nonce', 'tag', 'ciphertext']);
  assert.equal(envelope.format, format); assert.equal(envelope.algorithm, algorithm);
  const key = createPublicKey(privateKeyPem);
  assert.equal(key.asymmetricKeyType, 'rsa');
  assert.equal(hash(key.export({ format: 'der', type: 'spki' })), envelope.recipientSha256, 'Wrong support recipient.');
  const aesKey = privateDecrypt({ key: privateKeyPem, padding: constants.RSA_PKCS1_OAEP_PADDING, oaepHash: 'sha256' }, base64(envelope.wrappedKey));
  let plaintext;
  try {
    assert.equal(aesKey.length, 32);
    const nonce = base64(envelope.nonce), tag = base64(envelope.tag);
    assert.equal(nonce.length, 12); assert.equal(tag.length, 16);
    const decipher = createDecipheriv('aes-256-gcm', aesKey, nonce);
    decipher.setAAD(Buffer.from(JSON.stringify({ format, algorithm, recipientSha256: envelope.recipientSha256 })));
    decipher.setAuthTag(tag);
    plaintext = Buffer.concat([decipher.update(base64(envelope.ciphertext)), decipher.final()]);
    assert.ok(plaintext.length <= maxBytes * 2);
    const payload = JSON.parse(plaintext.toString('utf8'));
    exact(payload, ['manifest', 'contents']); validateManifest(payload.manifest);
    assert.ok(Number.isFinite(now) && now >= date(payload.manifest.createdAt) && now < date(payload.manifest.deleteAfter), 'Archive is outside its retention window.');
    assert.ok(Array.isArray(payload.contents));
    assert.equal(payload.contents.length, payload.manifest.files.length);
    const files = payload.contents.map((value, index) => {
      const data = base64(value), entry = payload.manifest.files[index];
      assert.equal(data.length, entry.size); assert.equal(hash(data), entry.sha256, 'Support payload checksum mismatch.');
      if (entry.name.endsWith('.pdb')) assert.equal(data.subarray(0, 4).toString('ascii'), 'BSJB');
      return { name: entry.name, data };
    });
    return { manifest: payload.manifest, files };
  } finally { aesKey.fill(0); plaintext?.fill(0); }
}

export function writeSupportBundle(path, bytes) {
  const parent = dirname(resolve(path));
  assert.equal(realpathSync(parent), parent, 'Symlinked support output directories are not allowed.');
  const fd = openSync(path, 'wx', 0o600);
  try { writeFileSync(fd, bytes); fsyncSync(fd); } finally { closeSync(fd); }
}

export function restoreSupportBundle(bytes, privateKeyPem, directory, options) {
  digest(options?.expectedArchiveSha256);
  assert.equal(hash(bytes), options.expectedArchiveSha256, 'Archive differs from its independently recorded checksum.');
  const payload = openSupportBundle(bytes, privateKeyPem, options);
  const parent = dirname(resolve(directory));
  assert.equal(realpathSync(parent), parent, 'Symlinked restore directories are not allowed.');
  mkdirSync(directory, { mode: 0o700 });
  try {
    for (const file of payload.files) writeSupportBundle(join(directory, file.name), file.data);
    writeSupportBundle(join(directory, 'manifest.json'), Buffer.from(JSON.stringify(payload.manifest, null, 2) + '\n'));
  } catch (error) { rmSync(directory, { recursive: true, force: true }); throw error; }
  return payload.manifest;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const [action, input, keyPath, output, expectedArchiveSha256, ...extra] = process.argv.slice(2);
    assert.ok(['seal', 'restore'].includes(action) && input && keyPath && output && extra.length === 0 &&
      (action === 'seal' ? expectedArchiveSha256 === undefined : Boolean(expectedArchiveSha256)),
      'Usage: support-artifacts.mjs seal request.json public.pem archive.json | restore archive.json private.pem new-directory expected-sha256');
    if (action === 'seal') {
      const request = JSON.parse(regularFile(resolve(input)));
      exact(request, ['metadata', 'assemblies', 'files']);
      for (const entry of [...request.assemblies.flatMap(item => [item.originalPath, item.protectedPath]), ...request.files.map(item => item.path)])
        assert.ok(isAbsolute(entry), 'CLI support input paths must be absolute.');
      writeSupportBundle(output, sealSupportBundle({ ...request, publicKeyPem: regularFile(resolve(keyPath)) }));
    } else restoreSupportBundle(regularFile(resolve(input), maxBytes * 3), regularFile(resolve(keyPath)), output, { expectedArchiveSha256 });
    console.log('Support archive operation completed.');
  } catch { console.error('Support archive operation failed; no secret details are logged.'); process.exitCode = 1; }
}
