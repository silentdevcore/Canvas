import assert from 'node:assert/strict';
import { createHash, generateKeyPairSync, randomUUID } from 'node:crypto';
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { checkRepository, protect } from './pvad-native-licensing.mjs';
import { openSupportBundle, restoreSupportBundle, sealSupportBundle, writeSupportBundle } from './support-artifacts.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const here = join(root, 'tools/native-licensing');
const sha = path => createHash('sha256').update(readFileSync(path)).digest('hex');
const run = (command, args, options = {}) => {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', timeout: 300_000, maxBuffer: 16 * 1024 * 1024, ...options });
  assert.equal(result.status, 0, `${command} failed: ${result.error?.message ?? ''}\n${result.stdout}\n${result.stderr}`);
  for (const line of (result.stdout ?? '').split(/\r?\n/).filter(line => /warning|warnung|NPOI:/i.test(line))) console.error(line);
  return result.stdout.trim();
};

export function rehearseSupportRetention({ original, protectedDirectory, modules, work, version }) {
  const stage = join(work, 'support-plaintext-staging'); mkdirSync(stage, { mode: 0o700 });
  const files = [{ name: 'Mapping.txt', path: join(protectedDirectory, 'Mapping.txt') },
    ...modules.map(name => ({ name: name.replace(/\.dll$/, '.pdb'), path: join(original, name.replace(/\.dll$/, '.pdb')) })).filter(entry => existsSync(entry.path))];
  for (const entry of files) { copyFileSync(entry.path, join(stage, entry.name)); entry.path = join(stage, entry.name); }
  const expected = Object.fromEntries(files.map(entry => [entry.name, sha(entry.path)]));
  const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 3072,
    publicKeyEncoding: { type: 'spki', format: 'pem' }, privateKeyEncoding: { type: 'pkcs8', format: 'pem' } });
  const now = Date.now();
  const obfuscarVersion = JSON.parse(readFileSync(join(here, '.config/dotnet-tools.json'), 'utf8')).tools['obfuscar.globaltool'].version;
  const sealed = sealSupportBundle({ publicKeyPem: publicKey, files,
    metadata: { buildId: 'support-' + randomUUID(), version, commit: run('git', ['rev-parse', 'HEAD']),
      sourceDirty: Boolean(run('git', ['status', '--porcelain', '--untracked-files=no'])), obfuscarVersion,
      createdAt: new Date(now).toISOString(), deleteAfter: new Date(now + 86_400_000).toISOString() },
    assemblies: modules.map(name => ({ name, originalPath: join(original, name), protectedPath: join(protectedDirectory, name),
      originalSha256: sha(join(original, name)), protectedSha256: sha(join(protectedDirectory, name)) })) });
  const archive = join(work, 'encrypted-support.json'); writeSupportBundle(archive, sealed);
  rmSync(stage, { recursive: true }); assert.ok(!existsSync(stage));
  const recovered = join(work, 'recovered-support');
  const manifest = restoreSupportBundle(readFileSync(archive), privateKey, recovered, { now, expectedArchiveSha256: sha(archive) });
  for (const [name, expectedHash] of Object.entries(expected)) assert.equal(sha(join(recovered, name)), expectedHash);
  assert.equal(manifest.assemblies.length, modules.length);
  assert.throws(() => openSupportBundle(sealed, privateKey, { now: now + 86_400_000 }), /retention window/);
  const changed = JSON.parse(sealed), bytes = Buffer.from(changed.ciphertext, 'base64'); bytes[0] ^= 1;
  changed.ciphertext = bytes.toString('base64');
  assert.throws(() => openSupportBundle(Buffer.from(JSON.stringify(changed)), privateKey, { now }));
  return { schemaVersion: 1, syntheticOnly: true, encryptedRoundtripPassed: true,
    mappedAssemblies: manifest.assemblies.length, originalPortableSymbols: files.length - 1,
    plaintextStagingRemovedBeforeRestore: true, restoredHashesEqual: true,
    tamperingRejected: true, expiredRestoreRejected: true,
    productionKeyProvisioned: false, managedStorageConfigured: false };
}

function snapshot(directory) {
  const values = {};
  if (!existsSync(directory)) return values;
  for (const item of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, item.name);
    if (item.isDirectory()) Object.assign(values, snapshot(path));
    else if (item.isFile()) values[path] = sha(path);
  }
  return values;
}

export function main(args) {
  assert.equal(args.length, 0, 'No key, output, trust or publication overrides are accepted by the synthetic rehearsal.');
  const { version } = checkRepository();
  const trust = join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json');
  const trustHash = sha(trust);
  const directory = join(root, 'src/Licensing/PVAD.Licensing');
  const baseline = { bin: snapshot(join(directory, 'bin')), obj: snapshot(join(directory, 'obj')) };
  const output = join(root, 'artifacts/native-support-retention', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  mkdirSync(output, { recursive: true });
  const work = join(output, 'private-work'); mkdirSync(work, { mode: 0o700 });
  let succeeded = false;
  try {
    const build = join(work, 'build');
    run('dotnet', ['build', join(directory, 'PVAD.Licensing.csproj'), '-c', 'Release', '--artifacts-path', build,
      '--disable-build-servers', '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false',
      '-p:DebugType=portable', '-p:DebugSymbols=true', '-p:EmbedAllSources=false', '-p:EnableSourceLink=false']);
    run('dotnet', ['tool', 'restore', '--tool-manifest', join(here, '.config/dotnet-tools.json')]);
    const original = join(build, 'bin/PVAD.Licensing/release'), protectedDirectory = join(work, 'protected');
    const modules = protect(original, protectedDirectory, ['PVAD.Licensing.dll']);
    const report = rehearseSupportRetention({ original, protectedDirectory, modules, work, version });
    assert.equal(report.originalPortableSymbols, 1, 'The real-symbol rehearsal must retain a generated portable PDB.');
    const dotnetArchitecture = run('dotnet', ['--info'], { env: { ...process.env, DOTNET_CLI_UI_LANGUAGE: 'en-US' } }).match(/Architecture:\s+(\S+)/)?.[1];
    assert.ok(dotnetArchitecture, 'The actual .NET host architecture must be recorded.');
    writeFileSync(join(output, 'report.json'), JSON.stringify({ ...report, version, platform: process.platform, nodeArchitecture: process.arch, dotnetArchitecture }, null, 2) + '\n');
    succeeded = true;
  } finally {
    rmSync(work, { recursive: true, force: true });
    assert.ok(!existsSync(work)); assert.equal(sha(trust), trustHash);
    assert.deepEqual({ bin: snapshot(join(directory, 'bin')), obj: snapshot(join(directory, 'obj')) }, baseline);
    writeFileSync(join(output, 'cleanup.json'), JSON.stringify({ succeeded, ephemeralArtifactsRemoved: true, sourceTrustUnchanged: true, regularProductBuildsUnchanged: true }, null, 2) + '\n');
  }
  console.log(`Synthetic support retention rehearsal passed: ${output}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { main(process.argv.slice(2)); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
