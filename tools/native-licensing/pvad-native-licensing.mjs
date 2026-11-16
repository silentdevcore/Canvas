import assert from 'node:assert/strict';
import { createHash, createPublicKey, generateKeyPairSync, randomUUID } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const library = 'src/Licensing/PVAD.Licensing/PVAD.Licensing.csproj';
const readJson = path => JSON.parse(readFileSync(path, 'utf8'));

export function validateRelease(version, releases, metadata) {
  const release = releases.releases.find(value => value.version === version);
  assert.ok(release, 'An exact product release entry is required.');
  assert.equal(metadata.version, version, 'Native release version drifted.');
  assert.equal(metadata.publishedAt, release.publishedAt, 'Native release date must match the curated manifest.');
  assert.match(metadata.publishedAt, /^\d{4}-\d{2}-\d{2}$/);
}

export function validateTrust(store, { requireKeys = false } = {}) {
  assert.equal(store.schemaVersion, 1);
  assert.deepEqual(Object.keys(store).sort(), ['keys', 'schemaVersion']);
  assert.ok(Array.isArray(store.keys) && store.keys.length <= 32);
  if (requireKeys) assert.ok(store.keys.length, 'Manufacturer trust-key provisioning is still open.');
  const ids = new Set();
  for (const key of store.keys) {
    assert.deepEqual(Object.keys(key).sort(), ['keyId', 'publicKeyPem']);
    assert.ok(typeof key.keyId === 'string' && key.keyId.trim() && key.keyId.length <= 100 && !ids.has(key.keyId));
    ids.add(key.keyId);
    assert.ok(!/PRIVATE/i.test(key.publicKeyPem), 'Private keys are forbidden in trust stores.');
    const parsed = createPublicKey(key.publicKeyPem);
    assert.equal(parsed.asymmetricKeyType, 'ec');
    assert.equal(parsed.asymmetricKeyDetails.namedCurve, 'prime256v1');
  }
}

export function checkRepository() {
  const version = readFileSync(join(root, 'VERSION'), 'utf8').trim();
  validateRelease(version, readJson(join(root, 'product-metadata/pvad-releases.json')),
    readJson(join(root, 'src/Licensing/PVAD.Licensing/LibraryRelease.json')));
  validateTrust(readJson(join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json')));
  const matrix = readJson(join(root, 'product-metadata/native-library-products.v1.json'));
  assert.equal(matrix.schemaVersion, 1);
  assert.equal(new Set(matrix.products.map(value => value.id)).size, matrix.products.length);
  for (const product of matrix.products) {
    assert.ok(product.entryPoints.length && typeof product.enforced === 'boolean');
    for (const directory of product.projectDirectories) {
      assert.ok(!isAbsolute(directory) && !directory.split('/').includes('..'));
      assert.ok(existsSync(join(root, directory)), `Missing integration directory: ${directory}`);
    }
  }
  return { version, products: matrix.products.length, enforcementStatus: matrix.enforcementStatus };
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', timeout: 180_000, maxBuffer: 16 * 1024 * 1024, ...options });
  if (result.error || result.status !== 0)
    throw new Error(`${command} failed (${result.status}):\n${result.stdout ?? ''}\n${result.stderr ?? ''}`, { cause: result.error });
  return result.stdout;
}

const xml = text => text.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');

function protect(input, output) {
  mkdirSync(output, { recursive: false });
  const modules = readdirSync(input).filter(file => /^PVAD\..+\.dll$/.test(file) && !/Tests|LicenseIssuer/.test(file));
  assert.ok(modules.length, 'No first-party libraries found.');
  const runtime = run('dotnet', ['--list-runtimes']).trim().split(/\r?\n/)
    .filter(line => line.startsWith('Microsoft.NETCore.App 10.'))
    .map(line => { const match = line.match(/^Microsoft.NETCore.App (\S+) \[(.+)\]$/); return join(match[2], match[1]); }).at(-1);
  assert.ok(runtime, 'A .NET 10 runtime is required.');
  const config = join(output, 'obfuscar.xml');
  writeFileSync(config, `<?xml version="1.0"?>\n<Obfuscator>
  <Var name="InPath" value="${xml(input)}" />
  <Var name="OutPath" value="${xml(output)}" />
  <Var name="KeepPublicApi" value="true" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="ReuseNames" value="false" />
  <Var name="UseUnicodeNames" value="false" />
  <Var name="HideStrings" value="false" />
  <AssemblySearchPath path="${xml(runtime)}" />
  ${modules.map(file => `<Module file="${xml(join(input, file))}" />`).join('\n  ')}
</Obfuscator>\n`);
  run('dotnet', ['tool', 'run', 'obfuscar.console', '--', config], { cwd: here });
  return modules;
}

function rehearse() {
  const { version } = checkRepository();
  const secret = mkdtempSync(join(tmpdir(), 'pvad-native-license-'));
  const output = join(root, 'artifacts/native-licensing', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  mkdirSync(output, { recursive: true });
  const input = join(output, 'input');
  const protectedDirectory = join(output, 'protected-support-only');
  const feed = join(output, 'local-feed-not-for-publication');
  let report;
  try {
    // This ephemeral key exists only for this isolated candidate and is never added to source trust.
    const { publicKey, privateKey } = generateKeyPairSync('ec', { namedCurve: 'prime256v1',
      publicKeyEncoding: { type: 'spki', format: 'pem' }, privateKeyEncoding: { type: 'pkcs8', format: 'pem' } });
    const keyPath = join(secret, 'key.pem');
    const trustPath = join(secret, 'trust.json');
    const payloadPath = join(secret, 'payload.json');
    const licensePath = join(secret, 'license.json');
    writeFileSync(keyPath, privateKey, { mode: 0o600 });
    const trust = { schemaVersion: 1, keys: [{ keyId: 'ephemeral-package-rehearsal', publicKeyPem: publicKey }] };
    validateTrust(trust, { requireKeys: true });
    writeFileSync(trustPath, JSON.stringify(trust));
    const start = new Date(Date.now() - 60_000);
    writeFileSync(payloadPath, JSON.stringify({ schemaVersion: 1, licenseId: randomUUID(), customerReference: 'synthetic-package-probe',
      model: 'trial', issuedAt: start.toISOString(), validFrom: start.toISOString(),
      validUntil: new Date(start.getTime() + 30 * 86400_000).toISOString(), updatesUntil: null,
      redistributionAllowed: false, grants: [{ product: 'pdf', providers: [] }] }));
    run('dotnet', ['run', '--project', 'tools/PVAD.LicenseIssuer', '--', '--payload', payloadPath,
      '--private-key', keyPath, '--key-id', 'ephemeral-package-rehearsal', '--output', licensePath]);
    console.error('Restoring pinned protection tools.');
    run('dotnet', ['tool', 'restore', '--tool-manifest', join(here, '.config/dotnet-tools.json')]);
    console.error('Building and protecting the isolated license library.');
    run('dotnet', ['build', library, '-c', 'Release', '-o', input,
      `-p:PvadLicenseTrustStorePath=${trustPath}`, '-p:DebugType=None']);
    protect(input, protectedDirectory);
    const protectedDll = join(protectedDirectory, 'PVAD.Licensing.dll');
    const before = run('dotnet', ['tool', 'run', 'ilspycmd', '--', '--disable-updatecheck', '-l', 'c', join(input, 'PVAD.Licensing.dll')], { cwd: here });
    const after = run('dotnet', ['tool', 'run', 'ilspycmd', '--', '--disable-updatecheck', '-l', 'c', protectedDll], { cwd: here });
    assert.ok(before.includes('PVAD.Licensing.PvadLicenseAuthority'));
    assert.ok(!after.includes('PVAD.Licensing.PvadLicenseAuthority'), 'Internal authority name was not obfuscated.');
    const apiProbe = join(here, 'ApiSurfaceProbe/ApiSurfaceProbe.csproj');
    const surface = path => JSON.parse(run('dotnet', ['run', '--project', apiProbe, '-c', 'Release', '--', path]).trim().split(/\r?\n/).at(-1));
    assert.deepEqual(surface(join(input, 'PVAD.Licensing.dll')), surface(protectedDll), 'Obfuscation changed the public API.');
    writeFileSync(join(output, 'original-types.txt'), before);
    writeFileSync(join(output, 'protected-types.txt'), after);
    console.error('Packing the protected DLL and running an independent local-feed consumer.');
    assert.throws(() => run('dotnet', ['pack', library, '--no-build', '-c', 'Release', '-o', join(output, 'rejected-pack'),
      '-p:DebugType=None', `-p:OutputPath=${input}/`, `-p:PvadLicenseTrustStorePath=${trustPath}`]),
    /Native license packages require an explicitly prepared protected assembly/);
    run('dotnet', ['pack', library, '--no-build', '-c', 'Release', '-o', feed,
      '-p:DebugType=None', `-p:OutputPath=${input}/`, `-p:PvadLicenseTrustStorePath=${trustPath}`, `-p:PvadProtectedAssemblyPath=${protectedDll}`]);
    const packagePath = join(feed, `PVAD.Licensing.${version}.nupkg`);
    const probe = join(here, 'PackageProbe/PackageProbe.csproj');
    const packages = join(secret, 'packages');
    run('dotnet', ['restore', probe, '--source', feed, `-p:RestorePackagesPath=${packages}`]);
    const probeOutput = run('dotnet', ['run', '--project', probe, '--no-restore', '-c', 'Release',
      `-p:RestorePackagesPath=${packages}`, '--', licensePath, packagePath, protectedDll]);
    const result = JSON.parse(probeOutput.trim().split(/\r?\n/).at(-1));
    assert.equal(result.protectedPackageVerified, true);
    report = { schemaVersion: 1, scope: 'licensing-core-only', platform: process.platform,
      nodeArchitecture: process.arch, architecture: result.dotnetProcessArchitecture,
      version, syntheticCandidateOnly: true, manufacturerTrustProvisioned: false, productEnforcementIntegrated: false,
      privateKeysRemovedOnExit: true, internalAuthorityRenamed: true, publicApiUnchanged: true,
      unprotectedPackagingRejected: true, ...result,
      protectedAssemblySha256: createHash('sha256').update(readFileSync(protectedDll)).digest('hex') };
  } finally {
    rmSync(secret, { recursive: true, force: true });
  }
  assert.ok(!existsSync(secret), 'Ephemeral signing material was not removed.');
  writeFileSync(join(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(`Local candidate verified: ${relative(root, join(output, 'report.json'))}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if (process.argv[2] === 'check' && process.argv.length === 3) console.log(JSON.stringify(checkRepository()));
    else if (process.argv[2] === 'rehearse' && process.argv.length === 3) rehearse();
    else throw new Error('Usage: node tools/native-licensing/pvad-native-licensing.mjs check|rehearse');
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
