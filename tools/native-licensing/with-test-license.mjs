import assert from 'node:assert/strict';
import { createHash, generateKeyPairSync, randomUUID, sign } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { validateAdmissionMatrix } from './admission-matrix.mjs';
import { validateMigrationAdmission } from './migration-admission.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export function migrationCodeTestGrants() {
  const inventory = JSON.parse(readFileSync(join(root, 'product-metadata/native-code-migration-entries.v1.json')));
  assert.equal(inventory.schemaVersion, 1);
  assert.equal(inventory.scope, 'provider-entry-points-only');
  assert.equal(inventory.entries.length, 24);
  return ['migration.pdf.code', 'migration.spreadsheet.code'].map(product => {
    const providers = inventory.entries.filter(entry => entry.product === product).map(entry => entry.provider);
    assert.ok(providers.length && new Set(providers).size === providers.length);
    assert.ok(providers.every(provider => /^[a-z][a-z0-9-]*$/.test(provider) && provider !== 'auto'));
    return { product, providers };
  });
}

export function migrationReportTestGrant() {
  const inventory = JSON.parse(readFileSync(join(root, 'product-metadata/native-report-migration-entries.v1.json')));
  assert.equal(inventory.schemaVersion, 1);
  assert.equal(inventory.scope, 'report-converter-and-wrapper-entry-points');
  const providers = inventory.entries.map(entry => entry.provider);
  assert.equal(providers.length, 8);
  assert.equal(new Set(providers).size, 8);
  assert.ok(providers.every(provider => /^[a-z][a-z0-9-]*$/.test(provider) && provider !== 'auto'));
  return { product: 'migration.report.designer', providers };
}

// Synthetic keys never enter the source trust store or regular bin/obj directories.
export function createTestLicenses(directory, now = Date.now(), includeCodeProviders = false, includeReportProviders = false, includeIssuer = false, includeRevocations = false, includePackageProducts = false) {
  const { privateKey, publicKey } = generateKeyPairSync('ec', { namedCurve: 'prime256v1',
    publicKeyEncoding: { type: 'spki', format: 'pem' } });
  const keyId = 'ephemeral-native-engine-test';
  const trustPath = join(directory, 'trust.json');
  writeFileSync(trustPath, JSON.stringify({ schemaVersion: 1, keys: [{ keyId, publicKeyPem: publicKey }] }), { mode: 0o600 });
  const paths = { trustPath };
  if (includeIssuer) {
    paths.issuerPrivateKey = join(directory, 'issuer-private.pem');
    writeFileSync(paths.issuerPrivateKey, privateKey.export({ type: 'pkcs8', format: 'pem' }), { mode: 0o600 });
  }
  const profiles = [
    ['pdf', ['pdf'], now - 60_000], ['word', ['word'], now - 60_000],
    ['spreadsheet', ['spreadsheet'], now - 60_000],
    ['importer', ['importer'], now - 60_000],
    ['converter', ['converter'], now - 60_000],
    ['integrated', ['pdf', 'word', 'spreadsheet', 'importer', 'converter'], now - 60_000], ['expired', ['pdf'], now - 31 * 86400_000],
  ].map(([name, products, start]) => [name, products.map(product => ({ product, providers: [] })), start]);
  if (includePackageProducts)
    for (const product of ['pdf', 'importer'])
      profiles.push(['package-' + product, [{ product, providers: [] }], now - 60_000]);
  const migrationGrants = migrationCodeTestGrants();
  profiles.find(([name]) => name === 'integrated')[1].push(...migrationGrants);
  profiles.push(['migration-code', migrationGrants, now - 60_000]);
  for (const kind of ['viewer', 'designer']) {
    const grants = [{ product: `migration.pdf.${kind}`, providers: ['nutrient-viewer'] }];
    profiles.find(([name]) => name === 'integrated')[1].push(...grants);
    profiles.push([`migration-${kind}`, grants, now - 60_000]);
  }
  const reportGrant = migrationReportTestGrant();
  profiles.find(([name]) => name === 'integrated')[1].push(reportGrant);
  profiles.push(['migration-report', [reportGrant], now - 60_000]);
  if (includeReportProviders)
    for (const provider of reportGrant.providers)
      profiles.push([`migration.report.designer-${provider}`, [{ product: reportGrant.product, providers: [provider] }], now - 60_000]);
  if (includeCodeProviders) {
    for (const { product, providers } of migrationGrants)
      for (const provider of providers)
        profiles.push([`${product}-${provider}`, [{ product, providers: [provider] }], now - 60_000]);
  }
  for (const [name, grants, start] of profiles) {
    const bytes = Buffer.from(JSON.stringify({ schemaVersion: 1, licenseId: randomUUID(), customerReference: 'synthetic-native-engine-test',
      model: 'trial', issuedAt: new Date(start).toISOString(), validFrom: new Date(start).toISOString(),
      validUntil: new Date(start + 30 * 86400_000).toISOString(), updatesUntil: null,
      redistributionAllowed: false, grants }));
    const envelope = { schemaVersion: 1, keyId, algorithm: 'ECDSA_P256_SHA256_P1363', payload: bytes.toString('base64'),
      signature: sign('sha256', bytes, { key: privateKey, dsaEncoding: 'ieee-p1363' }).toString('base64') };
    paths[name] = join(directory, `${name}.json`);
    writeFileSync(paths[name], JSON.stringify(envelope), { mode: 0o600 });
  }
  if (includeRevocations) {
    const ids = ['pdf', 'importer'].map(name => JSON.parse(Buffer.from(JSON.parse(readFileSync(paths[name])).payload, 'base64')).licenseId);
    const bytes = Buffer.from(JSON.stringify({ schemaVersion: 1, purpose: 'PVAD.NativeLicenseRevocations', sequence: 1,
      issuedAt: new Date(now - 60_000).toISOString(), nextUpdate: new Date(now + 86400_000).toISOString(), revokedLicenseIds: ids }));
    paths.revocations = join(directory, 'revocations.json');
    writeFileSync(paths.revocations, JSON.stringify({ schemaVersion: 1, kind: 'PVAD.NativeLicenseRevocations', keyId,
      algorithm: 'ECDSA_P256_SHA256_P1363', payload: bytes.toString('base64'),
      signature: sign('sha256', bytes, { key: privateKey, dsaEncoding: 'ieee-p1363' }).toString('base64') }), { mode: 0o600 });
  }
  return paths;
}

function run(args, env, capture = false) {
  const result = spawnSync('dotnet', args, { cwd: root, env, encoding: 'utf8',
    stdio: capture ? 'pipe' : 'inherit', timeout: 1_200_000, maxBuffer: 16 * 1024 * 1024 });
  if (result.error || result.status !== 0)
    throw new Error(`Isolated dotnet command failed (${result.status}).${capture ? `\n${result.stdout ?? ''}\n${result.stderr ?? ''}` : ''}`, { cause: result.error });
  return result.stdout;
}

function regularLicenseBuildSnapshot() {
  const result = {};
  function visit(path) {
    if (!existsSync(path)) return;
    for (const entry of readdirSync(path, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
      const file = join(path, entry.name);
      if (entry.isDirectory()) visit(file);
      else if (entry.isFile()) result[relative(root, file)] = createHash('sha256').update(readFileSync(file)).digest('hex');
    }
  }
  for (const directory of ['bin', 'obj']) visit(join(root, 'src/Licensing/PVAD.Licensing', directory));
  return result;
}

export function main(args) {
  assert.ok(['pdf', 'word', 'spreadsheet', 'importer', 'image', 'converter', 'migration-code', 'migration-viewer', 'migration-report', 'native-issuer', 'revocation', 'test'].includes(args[0]), 'Usage: with-test-license.mjs pdf | word | spreadsheet | importer | image | converter | migration-code | migration-viewer | migration-report | native-issuer | revocation | test tests/<project>/<project>.csproj [dotnet test options]');
  if (args[0] === 'revocation') assert.equal(args.length, 1);
  if (args[0] === 'native-issuer') {
    assert.equal(args.length, 1);
    assert.equal(process.env.PVAD_RUN_POSTGRES_TESTS, '1', 'native-issuer requires real PostgreSQL tests; skipped database checks are not acceptance.');
  }
  if (args[0] === 'test') {
    assert.ok(args[1] && /^tests\/.+\.csproj$/.test(args[1]) && !args[1].split('/').includes('..') && existsSync(join(root, args[1])), 'Expected an existing repository test project.');
    assert.ok(!args.slice(2).some(arg => /^(--no-build|--no-restore|--artifacts-path|-p:|\/p:)/i.test(arg)), 'The runner owns isolated builds and their trust configuration.');
  }
  const secret = mkdtempSync(join(tmpdir(), 'pvad-native-engine-test-'));
  const output = join(root, 'artifacts/native-engine-tests', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  const build = join(output, 'isolated-build-not-for-publication');
  mkdirSync(output, { recursive: true });
  const originalTrust = readFileSync(join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json'));
  const originalLicenseBuild = regularLicenseBuildSnapshot();
  let succeeded = false;
  try {
    const paths = createTestLicenses(secret, Date.now(), args[0] === 'migration-code', args[0] === 'migration-report', args[0] === 'native-issuer', args[0] === 'revocation');
    const env = { ...process.env, PVAD_NATIVE_LICENSE_PATH: paths.integrated, NativeLicensing__LicensePath: paths.integrated };
    const properties = [`-p:PvadLicenseTrustStorePath=${paths.trustPath}`];
    if (args[0] === 'native-issuer') {
      env.PVAD_TEST_NATIVE_ISSUER_PRIVATE_KEY = paths.issuerPrivateKey;
      env.PVAD_REQUIRE_NATIVE_ISSUER = '1';
      run(['restore', 'tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj', '--artifacts-path', build, '--disable-parallel', ...properties,
        '-p:PvadNativeLicenseTestBootstrap=true'], env);
      run(['build', 'tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj', '--no-restore', '--artifacts-path', build, '--disable-build-servers', '-m:1', '-nr:false',
        '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', ...properties, '-p:PvadNativeLicenseTestBootstrap=true',
        '--verbosity', 'minimal'], env);
      const filter = 'FullyQualifiedName~PvadNativeLicenseIssuanceTests|FullyQualifiedName~PvadLicensingSeparationTests|FullyQualifiedName~PvadLicenseReplacementTests|FullyQualifiedName~AdminMutationContractTests|FullyQualifiedName~AdminSystemControllerTests|FullyQualifiedName~PvadCanonicalApiRouteTests|FullyQualifiedName~OpenApiNamingSnapshotTests';
      run(['vstest', join(build, 'bin/PVAD.Api.Tests/debug/PVAD.Api.Tests.dll'), `/TestCaseFilter:${filter}`,
        `/ResultsDirectory:${output}`, '/Logger:trx;LogFileName=licensed-tests.trx'], env);
    } else if (args[0] === 'revocation') {
      const project = 'tools/native-licensing/RevocationEngineProbe/RevocationEngineProbe.csproj';
      run(['restore', project, '--artifacts-path', build, '--disable-parallel', ...properties, '-p:PvadNativeLicenseTestBootstrap=true'], env);
      run(['build', project, '--no-restore', '--artifacts-path', build, '--disable-build-servers', '-m:1', '-nr:false',
        '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', ...properties, '-p:PvadNativeLicenseTestBootstrap=true'], env);
      const result = JSON.parse(run([join(build, 'bin/RevocationEngineProbe/debug/RevocationEngineProbe.dll'),
        paths.integrated, paths.pdf, paths.importer, paths.revocations, join(build, 'bin/PVAD.CodeWorker/debug'),
        join(build, 'bin/PVAD.FileImporter.ImageOcr.Worker/debug/PVAD.FileImporter.ImageOcr.Worker.dll')], env, true));
      writeFileSync(join(output, 'revocation-admission.json'), JSON.stringify(result, null, 2) + '\n');
      console.log(`REVOCATION admission: ${result.checks.length} checks passed.`);
    } else if (args[0] !== 'test') {
      const probe = { pdf: 'PdfEngineProbe', word: 'WordEngineProbe', spreadsheet: 'SpreadsheetEngineProbe', importer: 'ImporterEngineProbe', image: 'ImageEngineProbe', converter: 'ConverterEngineProbe', 'migration-code': 'MigrationCodeEngineProbe', 'migration-viewer': 'MigrationViewerEngineProbe', 'migration-report': 'MigrationReportEngineProbe' }[args[0]];
      const project = `tools/native-licensing/${probe}/${probe}.csproj`;
      run(['build', project, '--artifacts-path', build, '--disable-build-servers', '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', ...properties,
        '-p:PvadNativeLicenseTestBootstrap=true'], env);
      const dll = join(build, `bin/${probe}/debug/${probe}.dll`);
      const licenses = [paths[args[0] === 'image' ? 'importer' : args[0]], args[0] === 'pdf' ? paths.word : paths.pdf];
      const extra = args[0] === 'image'
        ? [join(build, 'bin/PVAD.FileImporter.ImageOcr.Worker/debug/PVAD.FileImporter.ImageOcr.Worker.dll')] : [];
      const result = JSON.parse(run([dll, ...licenses, paths.expired, ...extra], env, true).trim());
      if (['pdf', 'word', 'spreadsheet', 'converter'].includes(args[0])) result.admissionCoverage = validateAdmissionMatrix(result, args[0]);
      if (['migration-code', 'migration-report', 'migration-viewer'].includes(args[0]))
        result.admissionCoverage = validateMigrationAdmission(result, args[0]);
      writeFileSync(join(output, `${args[0]}-admission.json`), JSON.stringify(result, null, 2) + '\n');
      console.log(`${args[0].toUpperCase()} admission: ${result.checks.length} checks passed.`);
    } else {
      run(['test', args[1], '--artifacts-path', build, '--disable-build-servers', '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', ...properties,
        '-p:PvadNativeLicenseTestBootstrap=true', '--results-directory', output,
        '--logger', 'trx;LogFileName=licensed-tests.trx', ...args.slice(2)], env);
    }
    succeeded = true;
  } finally {
    rmSync(secret, { recursive: true, force: true });
    rmSync(build, { recursive: true, force: true });
    assert.ok(!existsSync(secret) && !existsSync(build), 'Ephemeral keys, licenses, or synthetic-trust binaries remain.');
    assert.deepEqual(readFileSync(join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json')), originalTrust, 'Source trust was modified.');
    assert.deepEqual(regularLicenseBuildSnapshot(), originalLicenseBuild, 'Regular licensing build outputs were modified.');
    writeFileSync(join(output, 'run.json'), JSON.stringify({ succeeded, syntheticOnly: true,
      ephemeralFilesRemoved: true, sourceTrustUnchanged: true, regularLicenseBuildUnchanged: true,
      usesIsolatedBuildDirectories: true }, null, 2) + '\n');
  }
  console.log(`Evidence: ${relative(root, output)}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { main(process.argv.slice(2)); }
  catch (error) { console.error(error.message); process.exitCode = 1; }
}
