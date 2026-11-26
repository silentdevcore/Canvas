import assert from 'node:assert/strict';
import { createHash, createPublicKey, generateKeyPairSync, randomUUID } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { validateInventory } from './importer-surface.mjs';

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
  assert.equal(matrix.dependencyInventory, 'product-metadata/native-library-dependencies.v1.json');
  const dependencies = readJson(join(root, matrix.dependencyInventory));
  validateDependencies(dependencies, matrix.products.map(product => product.id));
  validateIntegrationCoverage(matrix, dependencies);
  assert.equal(matrix.codeMigrationEntries, 'product-metadata/native-code-migration-entries.v1.json');
  validateCodeMigrationEntries(readJson(join(root, matrix.codeMigrationEntries)));
  assert.equal(matrix.reportMigrationEntries, 'product-metadata/native-report-migration-entries.v1.json');
  validateReportMigrationEntries(readJson(join(root, matrix.reportMigrationEntries)));
  assert.equal(matrix.fileImporterEntries, 'product-metadata/native-file-importer-entries.v1.json');
  validateFileImporterEntries(readJson(join(root, matrix.fileImporterEntries)));
  assert.equal(matrix.importerPublicSurface, 'product-metadata/native-importer-public-surface.v1.json');
  validateInventory(readJson(join(root, matrix.importerPublicSurface)));
  return { version, products: matrix.products.length, enforcementStatus: matrix.enforcementStatus };
}

export function validateIntegrationCoverage(matrix, dependencies) {
  assert.equal(matrix.enforcementStatus, 'all-native-entry-points');
  assert.equal(matrix.products.length, 10);
  for (const product of matrix.products) {
    assert.equal(product.enforced, true, 'A current product integration is unfinished.');
    if (product.remainingEntryPoints) assert.deepEqual(product.remainingEntryPoints, [], 'A public processing entry remains unreviewed.');
  }
  assert.equal(matrix.continuationCoverage.status, 'reviewed-current-built-in-pipelines');
  assert.deepEqual(matrix.continuationCoverage.remaining, []);
  assert.equal(matrix.continuationCoverage.publicAdmission, 'fresh-current-license');
  assert.equal(matrix.continuationCoverage.authorityBinding, 'originating-process-verifier');
  assert.equal(matrix.continuationCoverage.workerAdmission, 'fresh-at-dispatch-and-worker-start');
  for (const rule of dependencies.rules) assert.equal(rule.status, 'implemented', 'A declared dependency integration is unfinished.');
}

export function validateFileImporterEntries(inventory) {
  assert.deepEqual(Object.keys(inventory).sort(), ['entries', 'schemaVersion', 'scope']);
  assert.equal(inventory.schemaVersion, 1);
  assert.equal(inventory.scope, 'registry-file-importer-processing-entry-points');
  assert.equal(inventory.entries.length, 8);
  const directory = join(root, 'src/Importing/PVAD.FileImporter');
  const registry = readFileSync(join(directory, 'FileImporterRegistry.cs'), 'utf8');
  const types = new Set(readdirSync(directory).filter(name => name.endsWith('.cs')).flatMap(name =>
    [...readFileSync(join(directory, name), 'utf8').matchAll(/public sealed class (\w+) : IFileImporter\b/g)].map(match => match[1])));
  const keys = new Set();
  for (const entry of inventory.entries) {
    assert.deepEqual(Object.keys(entry).sort(), ['key', 'publicProcessingOverloads', 'type']);
    assert.match(entry.key, /^[a-z]+$/);
    assert.ok(!keys.has(entry.key)); keys.add(entry.key);
    assert.ok(types.delete(entry.type), 'Missing or duplicated file importer type.');
    const source = readFileSync(join(directory, entry.type + '.cs'), 'utf8');
    assert.ok(registry.includes(`static () => new ${entry.type}(`));
    assert.equal(entry.publicProcessingOverloads, ['MarkdownFileImporter', 'PdfFileImporter'].includes(entry.type) ? 4 : 2);
    assert.ok(source.includes(entry.key === 'pdf'
      ? 'PvadLicense.BeginOperation(PvadNativeOperation.DesignerPdfImport)'
      : `PvadLicense.RequireCapability(PvadLicenseProducts.Importer, "import.${entry.key}");`));
  }
  assert.equal(types.size, 0, 'A public importer lacks inventory coverage.');
  assert.deepEqual([...keys].sort(), ['doc', 'docx', 'image', 'md', 'odt', 'pdf', 'pptx', 'svg']);
}

export function validateCodeMigrationEntries(inventory) {
  assert.deepEqual(Object.keys(inventory).sort(), ['entries', 'schemaVersion', 'scope']);
  assert.equal(inventory.schemaVersion, 1);
  assert.equal(inventory.scope, 'provider-entry-points-only');
  assert.equal(inventory.entries.length, 24);
  const ids = new Set();
  for (const entry of inventory.entries) {
    assert.deepEqual(Object.keys(entry).sort(), ['product', 'provider', 'source', 'type']);
    assert.ok(['migration.pdf.code', 'migration.spreadsheet.code'].includes(entry.product));
    assert.match(entry.provider, /^[a-z][a-z0-9-]*$/); assert.notEqual(entry.provider, 'auto');
    assert.ok(!ids.has(entry.product + ':' + entry.provider)); ids.add(entry.product + ':' + entry.provider);
    assert.ok(!isAbsolute(entry.source) && !entry.source.split('/').includes('..') && existsSync(join(root, entry.source)));
    assert.match(entry.type, /^[A-Za-z][A-Za-z0-9]*$/);
    const constant = entry.product === 'migration.pdf.code' ? 'PdfCodeMigration' : 'SpreadsheetCodeMigration';
    const source = readFileSync(join(root, entry.source), 'utf8');
    assert.ok(source.includes(`CodeMigrationAdmission.Begin(PvadLicenseProducts.${constant}, "${entry.provider}")`));
    assert.ok(source.includes(`CodeMigrationAdmission.Demand(PvadLicenseProducts.${constant}, "${entry.provider}");`));
    assert.ok(source.includes('MigrationResult IAdmittedCodeMigration.MigrateAdmitted(string sourceCode)'));
    assert.ok(source.includes(`public sealed class ${entry.type} : CSharpSourceMigration`));
  }
  assert.equal(inventory.entries.filter(entry => entry.product === 'migration.pdf.code').length, 16);
  assert.equal(inventory.entries.filter(entry => entry.product === 'migration.spreadsheet.code').length, 8);
}

export function validateReportMigrationEntries(inventory) {
  assert.deepEqual(Object.keys(inventory).sort(), ['entries', 'schemaVersion', 'scope']);
  assert.equal(inventory.schemaVersion, 1);
  assert.equal(inventory.scope, 'report-converter-and-wrapper-entry-points');
  assert.equal(inventory.entries.length, 8);
  const providers = new Set();
  for (const entry of inventory.entries) {
    assert.deepEqual(Object.keys(entry).sort(), ['product', 'provider', 'publicConverterOverloads', 'source', 'type', 'wrapper', 'wrapperSource']);
    assert.equal(entry.product, 'migration.report.designer');
    assert.match(entry.provider, /^[a-z][a-z0-9-]*$/);
    assert.notEqual(entry.provider, 'auto');
    assert.ok(!providers.has(entry.provider)); providers.add(entry.provider);
    assert.equal(entry.publicConverterOverloads, entry.provider === 'devexpress-report' ? 5 : 2);
    for (const path of [entry.source, entry.wrapperSource]) {
      assert.ok(!isAbsolute(path) && !path.split('/').includes('..') && existsSync(join(root, path)));
      const source = readFileSync(join(root, path), 'utf8');
      assert.ok(source.includes(`BeginOperation(PvadNativeOperation.ReportDesignerMigration, "${entry.provider}")`));
      assert.ok(source.includes(`DemandMigrationContinuation(PvadLicenseProducts.ReportDesignerMigration, "${entry.provider}")`));
    }
    assert.match(entry.type, /^[A-Za-z][A-Za-z0-9]*$/);
    assert.match(entry.wrapper, /^[A-Za-z][A-Za-z0-9]*$/);
  }
}

export function validateDependencies(inventory, products) {
  assert.deepEqual(Object.keys(inventory).sort(), ['notes', 'policySource', 'rules', 'schemaVersion']);
  assert.equal(inventory.schemaVersion, 1);
  assert.equal(inventory.policySource, 'src/Licensing/PVAD.Licensing/PvadLicenseExecution.cs');
  assert.ok(Array.isArray(inventory.rules) && inventory.rules.length > 0);
  const ids = new Set();
  for (const rule of inventory.rules) {
    assert.deepEqual(Object.keys(rule).sort(), ['additionalGrants', 'boundary', 'id', 'privateDependency', 'product', 'source', 'status']);
    assert.match(rule.id, /^[a-z0-9]+(?:-[a-z0-9]+)*$/);
    assert.ok(!ids.has(rule.id), 'Duplicate dependency rule.'); ids.add(rule.id);
    assert.ok(products.includes(rule.product));
    assert.ok(['fresh-public', 'internal-continuation', 'private-byte-dependency', 'fresh-public-callback', 'fresh-provider'].includes(rule.boundary));
    assert.ok(['implemented', 'planned'].includes(rule.status));
    assert.ok(!isAbsolute(rule.source) && !rule.source.split('/').includes('..') && existsSync(join(root, rule.source)), 'Invalid dependency source.');
    assert.ok(Array.isArray(rule.additionalGrants) && new Set(rule.additionalGrants).size === rule.additionalGrants.length);
    assert.ok(rule.additionalGrants.every(product => products.includes(product)));
    if (rule.boundary === 'private-byte-dependency') {
      assert.equal(rule.product, 'importer');
      assert.ok(['RegenerationPdf', 'BlankPagePdf', 'OcrResultPdf'].includes(rule.privateDependency));
    } else assert.equal(rule.privateDependency, null);
  }
  assert.ok(Array.isArray(inventory.notes) && inventory.notes.every(note => typeof note === 'string' && note.trim()));
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', timeout: 180_000, maxBuffer: 16 * 1024 * 1024, ...options });
  if (result.error || result.status !== 0)
    throw new Error(`${command} failed (${result.status}):\n${result.stdout ?? ''}\n${result.stderr ?? ''}`, { cause: result.error });
  return result.stdout;
}

const xml = text => text.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');

export function protect(input, output, expectedModules, { preserveHostContracts = false } = {}) {
  mkdirSync(output, { recursive: false });
  const modules = readdirSync(input).filter(file => /^PVAD\..+\.dll$/.test(file) && !/Tests|LicenseIssuer/.test(file));
  assert.ok(modules.length, 'No first-party libraries found.');
  if (expectedModules) assert.deepEqual(modules.sort(), [...expectedModules].sort(), 'The joint protection graph differs from the reviewed profile.');
  if (preserveHostContracts) {
    assert.equal(modules.length, 56, 'Host preservation requires the reviewed complete WebApi graph.');
    for (const name of ['PVAD.WebApi.dll', 'PVAD.Infrastructure.Persistence.dll']) assert.ok(modules.includes(name));
  }
  const runtimes = run('dotnet', ['--list-runtimes']).trim().split(/\r?\n/);
  const runtime = runtimes
    .filter(line => line.startsWith('Microsoft.NETCore.App 10.'))
    .map(line => { const match = line.match(/^Microsoft.NETCore.App (\S+) \[(.+)\]$/); return join(match[2], match[1]); }).at(-1);
  assert.ok(runtime, 'A .NET 10 runtime is required.');
  const aspnet = runtimes.filter(line => line.startsWith('Microsoft.AspNetCore.App 10.'))
    .map(line => { const match = line.match(/^Microsoft.AspNetCore.App (\S+) \[(.+)\]$/); return join(match[2], match[1]); }).at(-1);
  if (preserveHostContracts) assert.ok(aspnet, 'The protected WebApi requires an ASP.NET Core 10 runtime.');
  const config = join(output, 'obfuscar.xml');
  writeFileSync(config, `<?xml version="1.0"?>\n<Obfuscator>
  <Var name="InPath" value="${xml(input)}" />
  <Var name="OutPath" value="${xml(output)}" />
  <Var name="KeepPublicApi" value="true" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="ReuseNames" value="false" />
  <Var name="UseUnicodeNames" value="false" />
  <Var name="HideStrings" value="false" />
  <Var name="SkipGenerated" value="true" />
  <AssemblySearchPath path="${xml(runtime)}" />
  ${preserveHostContracts ? `<AssemblySearchPath path="${xml(aspnet)}" />` : ''}
  ${modules.map(file => `<Module file="${xml(join(input, file))}">
    ${preserveHostContracts && ['PVAD.WebApi.dll', 'PVAD.Infrastructure.Persistence.dll'].includes(file)
      ? '<SkipType name="*" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" />' : ''}
    <SkipType name="*" decorator="System.Runtime.CompilerServices.CompilerGeneratedAttribute" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" />
    <SkipType name="*&lt;*" skipMethods="true" skipFields="true" skipProperties="true" skipEvents="true" />
  </Module>`).join('\n  ')}
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
