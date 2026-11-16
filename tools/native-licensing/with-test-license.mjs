import assert from 'node:assert/strict';
import { createHash, generateKeyPairSync, randomUUID, sign } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

// Synthetic keys never enter the source trust store or regular bin/obj directories.
export function createTestLicenses(directory, now = Date.now()) {
  const { privateKey, publicKey } = generateKeyPairSync('ec', { namedCurve: 'prime256v1',
    publicKeyEncoding: { type: 'spki', format: 'pem' } });
  const keyId = 'ephemeral-native-engine-test';
  const trustPath = join(directory, 'trust.json');
  writeFileSync(trustPath, JSON.stringify({ schemaVersion: 1, keys: [{ keyId, publicKeyPem: publicKey }] }), { mode: 0o600 });
  const paths = { trustPath };
  for (const [name, products, start] of [
    ['pdf', ['pdf'], now - 60_000], ['word', ['word'], now - 60_000],
    ['spreadsheet', ['spreadsheet'], now - 60_000],
    ['importer', ['importer'], now - 60_000],
    ['integrated', ['pdf', 'word', 'spreadsheet', 'importer'], now - 60_000], ['expired', ['pdf'], now - 31 * 86400_000],
  ]) {
    const bytes = Buffer.from(JSON.stringify({ schemaVersion: 1, licenseId: randomUUID(), customerReference: 'synthetic-native-engine-test',
      model: 'trial', issuedAt: new Date(start).toISOString(), validFrom: new Date(start).toISOString(),
      validUntil: new Date(start + 30 * 86400_000).toISOString(), updatesUntil: null,
      redistributionAllowed: false, grants: products.map(product => ({ product, providers: [] })) }));
    const envelope = { schemaVersion: 1, keyId, algorithm: 'ECDSA_P256_SHA256_P1363', payload: bytes.toString('base64'),
      signature: sign('sha256', bytes, { key: privateKey, dsaEncoding: 'ieee-p1363' }).toString('base64') };
    paths[name] = join(directory, `${name}.json`);
    writeFileSync(paths[name], JSON.stringify(envelope), { mode: 0o600 });
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
  assert.ok(['pdf', 'word', 'spreadsheet', 'importer', 'image', 'test'].includes(args[0]), 'Usage: with-test-license.mjs pdf | word | spreadsheet | importer | image | test tests/<project>/<project>.csproj [dotnet test options]');
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
    const paths = createTestLicenses(secret);
    const env = { ...process.env, PVAD_NATIVE_LICENSE_PATH: paths.integrated, NativeLicensing__LicensePath: paths.integrated };
    const properties = [`-p:PvadLicenseTrustStorePath=${paths.trustPath}`];
    if (args[0] !== 'test') {
      const probe = { pdf: 'PdfEngineProbe', word: 'WordEngineProbe', spreadsheet: 'SpreadsheetEngineProbe', importer: 'ImporterEngineProbe', image: 'ImageEngineProbe' }[args[0]];
      const project = `tools/native-licensing/${probe}/${probe}.csproj`;
      run(['build', project, '--artifacts-path', build, '--disable-build-servers', '-m:1', ...properties,
        '-p:PvadNativeLicenseTestBootstrap=true'], env);
      const dll = join(build, `bin/${probe}/debug/${probe}.dll`);
      const licenses = [paths[args[0] === 'image' ? 'importer' : args[0]], args[0] === 'pdf' ? paths.word : paths.pdf];
      const extra = args[0] === 'image'
        ? [join(build, 'bin/PVAD.FileImporter.ImageOcr.Worker/debug/PVAD.FileImporter.ImageOcr.Worker.dll')] : [];
      const result = JSON.parse(run([dll, ...licenses, paths.expired, ...extra], env, true).trim());
      writeFileSync(join(output, `${args[0]}-admission.json`), JSON.stringify(result, null, 2) + '\n');
      console.log(`${args[0].toUpperCase()} admission: ${result.checks.length} checks passed.`);
    } else {
      run(['test', args[1], '--artifacts-path', build, '--disable-build-servers', '-m:1', ...properties,
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
