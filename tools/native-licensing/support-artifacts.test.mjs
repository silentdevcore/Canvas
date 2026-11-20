import assert from 'node:assert/strict';
import { createHash, generateKeyPairSync } from 'node:crypto';
import { existsSync, mkdtempSync, readFileSync, realpathSync, rmSync, statSync, symlinkSync, truncateSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import { test } from 'node:test';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { openSupportBundle, restoreSupportBundle, sealSupportBundle, writeSupportBundle } from './support-artifacts.mjs';

const pair = () => generateKeyPairSync('rsa', { modulusLength: 3072,
  publicKeyEncoding: { format: 'pem', type: 'spki' }, privateKeyEncoding: { format: 'pem', type: 'pkcs8' } });
const recipient = pair();
const now = Date.parse('2026-11-20T12:00:00.000Z');
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
function fixture(run) {
  const directory = mkdtempSync(join(realpathSync(tmpdir()), 'pvad-support-test-'));
  try {
    const map = 'private-original-method -> renamed-method\n';
    const pdb = Buffer.from('BSJBsynthetic-portable-symbol-fixture');
    const original = Buffer.from('original-dll-fixture'), protectedBytes = Buffer.from('protected-dll-fixture');
    for (const [name, bytes] of Object.entries({ 'Mapping.txt': map, 'PVAD.Test.pdb': pdb, 'original.dll': original, 'protected.dll': protectedBytes })) writeFileSync(join(directory, name), bytes);
    const request = { metadata: { buildId: 'unit-test', version: '1.1.0', commit: 'a'.repeat(40), sourceDirty: true, obfuscarVersion: '2.2.50',
      createdAt: new Date(now).toISOString(), deleteAfter: new Date(now + 86_400_000).toISOString() },
    assemblies: [{ name: 'PVAD.Test.dll', originalPath: join(directory, 'original.dll'), protectedPath: join(directory, 'protected.dll'),
      originalSha256: sha(original), protectedSha256: sha(protectedBytes) }],
    files: [{ name: 'Mapping.txt', path: join(directory, 'Mapping.txt') }, { name: 'PVAD.Test.pdb', path: join(directory, 'PVAD.Test.pdb') }],
    publicKeyPem: recipient.publicKey };
    run({ directory, request, map, pdb });
  } finally { rmSync(directory, { recursive: true, force: true }); }
}

test('sealed archives contain no plaintext names or symbols and restore exactly with bound DLL hashes', () => fixture(({ directory, request, map, pdb }) => {
  const sealed = sealSupportBundle(request), next = sealSupportBundle(request);
  assert.notDeepEqual(sealed, next, 'Encryption must use fresh keys and nonces.');
  for (const text of [map, 'PVAD.Test.pdb', 'PVAD.Test.dll', 'unit-test', 'PRIVATE KEY']) assert.ok(!sealed.toString().includes(text));
  const restored = openSupportBundle(sealed, recipient.privateKey, { now });
  assert.equal(restored.files[0].data.toString(), map); assert.deepEqual(restored.files[1].data, pdb);
  assert.equal(restored.manifest.assemblies[0].protectedSha256, request.assemblies[0].protectedSha256);
  const output = join(directory, 'restored');
  restoreSupportBundle(sealed, recipient.privateKey, output, { now, expectedArchiveSha256: sha(sealed) });
  assert.equal(readFileSync(join(output, 'Mapping.txt'), 'utf8'), map);
  if (process.platform !== 'win32') {
    assert.equal(statSync(output).mode & 0o777, 0o700);
    assert.equal(statSync(join(output, 'PVAD.Test.pdb')).mode & 0o777, 0o600);
  }
  assert.throws(() => restoreSupportBundle(sealed, recipient.privateKey, output, { now, expectedArchiveSha256: sha(sealed) }));
  assert.equal(readFileSync(join(output, 'Mapping.txt'), 'utf8'), map);
}));

test('wrong recipients, modified envelopes, truncation and invalid encodings fail before extraction', () => fixture(({ directory, request }) => {
  const sealed = sealSupportBundle(request);
  assert.throws(() => openSupportBundle(sealed, pair().privateKey, { now }), /Wrong support recipient/);
  for (const field of ['ciphertext', 'tag', 'nonce', 'wrappedKey']) {
    const changed = JSON.parse(sealed), bytes = Buffer.from(changed[field], 'base64'); bytes[0] ^= 1;
    changed[field] = bytes.toString('base64');
    const output = join(directory, field);
    assert.throws(() => restoreSupportBundle(Buffer.from(JSON.stringify(changed)), recipient.privateKey, output, { now, expectedArchiveSha256: sha(sealed) }));
    assert.ok(!existsSync(output));
  }
  for (const field of ['format', 'algorithm', 'recipientSha256']) {
    const changed = JSON.parse(sealed); changed[field] += 'changed';
    assert.throws(() => openSupportBundle(Buffer.from(JSON.stringify(changed)), recipient.privateKey, { now }));
  }
  const changed = JSON.parse(sealed); changed.ciphertext += '\n';
  assert.throws(() => openSupportBundle(Buffer.from(JSON.stringify(changed)), recipient.privateKey, { now }), /base64/);
  assert.throws(() => openSupportBundle(sealed.subarray(0, 20), recipient.privateKey, { now }));
}));

test('retention windows and metadata are enforced without claiming automated storage deletion', () => fixture(({ request }) => {
  const sealed = sealSupportBundle(request);
  for (const time of [now - 1, now + 86_400_000, Number.NaN]) assert.throws(() => openSupportBundle(sealed, recipient.privateKey, { now: time }), /retention window/);
  for (const [key, value] of [['commit', 'unknown'], ['deleteAfter', request.metadata.createdAt], ['sourceDirty', 'false'], ['buildId', '../unsafe'], ['version', 'not-a-version']])
    assert.throws(() => sealSupportBundle({ ...request, metadata: { ...request.metadata, [key]: value } }));
}));

test('only reviewed support types and assemblies are archived; unknown, duplicate and unbound inputs fail', () => fixture(({ request }) => {
  for (const name of ['../Mapping.txt', '/Mapping.txt', 'key.pem', 'source.cs', 'license.json', 'module.dll', 'PVAD.Unknown.pdb'])
    assert.throws(() => sealSupportBundle({ ...request, files: [{ ...request.files[0], name }] }));
  assert.throws(() => sealSupportBundle({ ...request, files: [request.files[1]] }), /mapping is required/);
  assert.throws(() => sealSupportBundle({ ...request, files: [...request.files, request.files[0]] }), /Duplicate/);
  assert.throws(() => sealSupportBundle({ ...request, assemblies: [...request.assemblies, request.assemblies[0]] }), /Duplicate/);
  assert.throws(() => sealSupportBundle({ ...request, assemblies: [{ ...request.assemblies[0], protectedSha256: '0'.repeat(64) }] }), /checksum mismatch/);
  assert.throws(() => sealSupportBundle({ ...request, publicKeyPem: recipient.privateKey }), /only a public/);
  const weak = generateKeyPairSync('rsa', { modulusLength: 2048, publicKeyEncoding: { format: 'pem', type: 'spki' } });
  assert.throws(() => sealSupportBundle({ ...request, publicKeyPem: weak.publicKey }), /3072/);
  assert.throws(() => sealSupportBundle({ ...request, assemblies: Array(129).fill(request.assemblies[0]) }));
  assert.throws(() => sealSupportBundle({ ...request, files: Array(130).fill(request.files[0]) }));
}));

test('symlinks and existing output files are rejected without overwriting caller files', () => fixture(({ directory, request }) => {
  const output = join(directory, 'sealed.json'), bytes = sealSupportBundle(request);
  writeSupportBundle(output, bytes);
  assert.throws(() => writeSupportBundle(output, Buffer.from('overwrite')));
  assert.deepEqual(readFileSync(output), bytes);
  if (process.platform !== 'win32') {
    const link = join(directory, 'linked-map'); symlinkSync(request.files[0].path, link);
    assert.throws(() => sealSupportBundle({ ...request, files: [{ name: 'Mapping.txt', path: link }] }), /regular support/);
    const linkedDirectory = join(directory, 'linked-directory'); symlinkSync(directory, linkedDirectory);
    assert.throws(() => writeSupportBundle(join(linkedDirectory, 'unexpected.json'), bytes), /Symlinked/);
    assert.ok(!existsSync(join(directory, 'unexpected.json')));
  }
}));

test('support CLI logs no input, key, token or filesystem error details on failure', () => {
  const result = spawnSync(process.execPath, [fileURLToPath(new URL('./support-artifacts.mjs', import.meta.url)), 'restore', '/missing-private-name', '/missing-secret-key', '/missing-output'], { encoding: 'utf8' });
  assert.equal(result.status, 1);
  assert.equal(result.stderr.trim(), 'Support archive operation failed; no secret details are logged.');
  assert.equal(result.stdout, '');
});

test('oversized support inputs and invalid symbol headers fail before encryption or extraction', () => fixture(({ directory, request }) => {
  const oversized = join(directory, 'oversized'); writeFileSync(oversized, ''); truncateSync(oversized, 64 * 1024 * 1024 + 1);
  assert.throws(() => sealSupportBundle({ ...request, files: [{ name: 'Mapping.txt', path: oversized }] }), /size limit/);
  writeFileSync(request.files[1].path, 'not-a-portable-PDB');
  assert.throws(() => sealSupportBundle(request), /portable original symbols/);
}));

test('CLI sealing and restoration roundtrip using explicit operator files without replacing an archive', () => fixture(({ directory, request, map }) => {
  const script = fileURLToPath(new URL('./support-artifacts.mjs', import.meta.url));
  const publicPath = join(directory, 'public.pem'), privatePath = join(directory, 'private.pem');
  writeFileSync(publicPath, recipient.publicKey); writeFileSync(privatePath, recipient.privateKey, { mode: 0o600 });
  const { publicKeyPem, ...parameters } = request;
  parameters.metadata.createdAt = new Date(Date.now() - 60_000).toISOString();
  parameters.metadata.deleteAfter = new Date(Date.now() + 86_400_000).toISOString();
  const input = join(directory, 'request.json'), archive = join(directory, 'archive.json'), output = join(directory, 'restore');
  writeFileSync(input, JSON.stringify(parameters));
  const call = args => spawnSync(process.execPath, [script, ...args], { encoding: 'utf8' });
  assert.equal(call(['seal', input, publicPath, archive]).status, 0);
  const original = readFileSync(archive);
  assert.equal(call(['seal', input, publicPath, archive]).status, 1);
  assert.deepEqual(readFileSync(archive), original);
  assert.equal(call(['restore', archive, privatePath, output, sha(original)]).status, 0);
  assert.equal(readFileSync(join(output, 'Mapping.txt'), 'utf8'), map);
}));

test('restoration requires an independent archive checksum, not only public-key encryption', () => fixture(({ directory, request }) => {
  const archive = sealSupportBundle(request), replacement = sealSupportBundle(request);
  const target = join(directory, 'rejected');
  assert.throws(() => restoreSupportBundle(archive, recipient.privateKey, target, { now }));
  assert.throws(() => restoreSupportBundle(replacement, recipient.privateKey, target, { now, expectedArchiveSha256: sha(archive) }), /independently recorded/);
  assert.ok(!existsSync(target));
}));

test('CI runs synthetic retention and uploads only its reports, never support data or recipient keys', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(readFileSync(new URL('../../.github/workflows/native-library-licensing.yml', import.meta.url), 'utf8'));
  const steps = workflow.jobs.licensing.steps;
  assert.ok(steps.some(step => step.run === 'node tools/native-licensing/support-retention-rehearsal.mjs'));
  const uploads = Object.values(workflow.jobs).flatMap(job => job.steps.filter(step => step.uses?.startsWith('actions/upload-artifact@')));
  const paths = uploads.flatMap(step => step.with.path.trim().split('\n').map(value => value.trim()));
  assert.ok(paths.includes('artifacts/native-support-retention/*/report.json'));
  assert.ok(paths.includes('artifacts/native-support-retention/*/cleanup.json'));
  for (const path of paths) assert.match(path, /\/(?:[^/]+\.trx|report[^/]*\.json|cleanup\.json|run\.json|[^/]+-admission\.json|surface-validation\.json)$/);
  const runner = readFileSync(new URL('./joint-package-rehearsal.mjs', import.meta.url), 'utf8');
  assert.ok(runner.includes('rehearseSupportRetention({ original, protectedDirectory, modules, work, version })'));
});
