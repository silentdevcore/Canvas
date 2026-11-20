import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { copyFileSync, cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { protect, checkRepository } from './pvad-native-licensing.mjs';
import { migrationProjects, regularOutputs, validateProjectClosure } from './joint-package-rehearsal.mjs';
import { createTestLicenses } from './with-test-license.mjs';
import { rehearseSupportRetention } from './support-retention-rehearsal.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const here = join(root, 'tools/native-licensing');
export const webApiProjects = Object.freeze({ ...migrationProjects,
  'PVAD.Infrastructure.Persistence': 'src/Infrastructure/PVAD.Infrastructure.Persistence/PVAD.Infrastructure.Persistence.csproj',
  'PVAD.WebApi': 'PVAD.WebApi/PVAD.WebApi.csproj' });
const sha = path => createHash('sha256').update(readFileSync(path)).digest('hex');
const json = path => JSON.parse(readFileSync(path, 'utf8'));
function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', timeout: 900_000, maxBuffer: 64 * 1024 * 1024, ...options });
  if (result.error || result.status !== 0) throw new Error(`${command} failed (${result.status}):\n${result.stdout ?? ''}\n${result.stderr ?? ''}`, { cause: result.error });
  for (const line of (result.stdout ?? '').split(/\r?\n/).filter(line => !/^[\[{]/.test(line.trim()) && /warning|warnung|NPOI:/i.test(line))) console.error(line);
  return result.stdout;
}
const result = output => JSON.parse(output.trim().split(/\r?\n/).at(-1));

export function main(args) {
  assert.equal(args.length, 0, 'No host, output, trust, preservation or publication overrides are accepted.');
  validateProjectClosure(webApiProjects, ['PVAD.WebApi']);
  const { version } = checkRepository();
  const trust = join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json');
  const beforeTrust = sha(trust), beforeBuild = regularOutputs(webApiProjects);
  const output = join(root, 'artifacts/native-webapi-protection', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  mkdirSync(output, { recursive: true });
  const work = join(output, 'private-work'); mkdirSync(work, { mode: 0o700 });
  const secret = mkdtempSync(join(tmpdir(), 'pvad-host-protection-'));
  let succeeded = false;
  try {
    const licenses = createTestLicenses(secret, Date.now(), false, false, false, true);
    const build = join(work, 'build');
    const common = ['--disable-build-servers', '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false',
      '-p:SkipOcrWorkerPackaging=true', '-p:SkipCodeWorkerPackaging=true', '-p:DebugType=None', '-p:DebugSymbols=false',
      '-p:EmbedAllSources=false', '-p:EnableSourceLink=false', '-p:EnableSourceControlManagerQueries=false'];
    console.error('Building the isolated complete WebApi and worker graph.');
    run('dotnet', ['build', join(here, 'WebApiProtectionProbe/WebApiProtectionProbe.csproj'), '-c', 'Release', '--artifacts-path', build,
      ...common, `-p:PvadLicenseTrustStorePath=${licenses.trustPath}`]);
    const original = join(build, 'bin/WebApiProtectionProbe/release');
    for (const name of ['PVAD.WebApi.deps.json', 'PVAD.WebApi.runtimeconfig.json'])
      copyFileSync(join(build, 'bin/PVAD.WebApi/release', name), join(original, name));
    const originalCode = join(build, 'bin/PVAD.CodeWorker/release'), originalOcr = join(build, 'bin/PVAD.FileImporter.ImageOcr.Worker/release');
    for (const [directory, name] of [[originalCode, 'PVAD.CodeWorker'], [originalOcr, 'PVAD.FileImporter.ImageOcr.Worker']])
      copyFileSync(join(directory, name + '.dll'), join(original, name + '.dll'));
    const invoke = (directory, worker, name, unlicensed = false) => result(run('dotnet', [join(directory, 'WebApiProtectionProbe.dll'), secret, worker, directory,
      join(work, name), licenses.revocations, ...(unlicensed ? ['unlicensed'] : [])]));
    console.error('Running baseline HTTP authorization, export and worker checks.');
    const baseline = invoke(original, originalCode, 'baseline-result');
    const baselineUnlicensed = invoke(original, originalCode, 'baseline-unlicensed', true);
    run('dotnet', ['tool', 'restore', '--tool-manifest', join(here, '.config/dotnet-tools.json')]);
    const protectedDirectory = join(work, 'protected');
    const modules = protect(original, protectedDirectory, Object.keys(webApiProjects).map(name => name + '.dll'), { preserveHostContracts: true });
    for (const [file, privateType] of [['PVAD.Licensing.dll', 'PvadLicenseAuthority'], ['PVAD.Pdf.dll', 'PdfWriter']]) {
      const types = directory => run('dotnet', ['tool', 'run', 'ilspycmd', '--', '--disable-updatecheck', '-l', 'c', join(directory, file)], { cwd: here });
      assert.ok(types(original).includes(privateType));
      assert.ok(!types(protectedDirectory).includes(privateType), 'Native implementation name retained: ' + privateType);
    }
    const runtime = join(work, 'host-runtime');
    cpSync(original, runtime, { recursive: true, filter: path => !/\.(pdb|cs|map)$/i.test(path) });
    for (const name of modules) copyFileSync(join(protectedDirectory, name), join(runtime, name));
    const workerBundles = {};
    const bundle = (source, name) => {
      const target = join(work, name); cpSync(source, target, { recursive: true, filter: path => !/\.(pdb|cs|map)$/i.test(path) });
      const nativeHashes = {};
      for (const file of readdirSync(target).filter(name => /^PVAD\..+\.dll$/.test(name))) {
        assert.ok(modules.includes(file)); copyFileSync(join(protectedDirectory, file), join(target, file));
        nativeHashes[file] = sha(join(target, file));
      }
      const vendors = readdirSync(target).filter(file => file.endsWith('.dll') && !modules.includes(file));
      for (const file of vendors) assert.equal(sha(join(target, file)), sha(join(source, file)), 'Worker vendor changed: ' + file);
      workerBundles[name] = { nativeHashes, vendorAssembliesUnchanged: vendors.length };
      return target;
    };
    const code = bundle(originalCode, 'protected-code-worker'), ocr = bundle(originalOcr, 'protected-ocr-worker');
    const surfaceBuild = join(work, 'surface-build');
    run('dotnet', ['build', join(here, 'ApiSurfaceProbe/ApiSurfaceProbe.csproj'), '-c', 'Release', '--artifacts-path', surfaceBuild, ...common]);
    const surface = path => result(run('dotnet', [join(surfaceBuild, 'bin/ApiSurfaceProbe/release/ApiSurfaceProbe.dll'), path]));
    const publicApi = {};
    for (const file of modules) {
      // Standalone workers keep their own Roslyn dependency bundle.
      const source = file === 'PVAD.CodeWorker.dll' ? originalCode : file === 'PVAD.FileImporter.ImageOcr.Worker.dll' ? originalOcr : original;
      const target = file === 'PVAD.CodeWorker.dll' ? code : file === 'PVAD.FileImporter.ImageOcr.Worker.dll' ? ocr : runtime;
      const before = surface(join(source, file));
      assert.deepEqual(surface(join(target, file)), before, 'Protected public API drift: ' + file);
      publicApi[file] = { types: before.length, protectedSha256: sha(join(target, file)) };
    }
    console.error('Running the same HTTP and process checks against jointly rewritten host references.');
    const after = invoke(runtime, code, 'protected-result');
    const afterUnlicensed = invoke(runtime, code, 'protected-unlicensed', true);
    assert.deepEqual(after, baseline, 'Protected HTTP/worker checks differ from baseline.');
    assert.deepEqual(afterUnlicensed, baselineUnlicensed, 'Unlicensed host behavior differs from baseline.');
    assert.equal(new Set(after.checks).size, after.checks.length);
    assert.equal(new Set(afterUnlicensed.checks).size, afterUnlicensed.checks.length);
    const vendorFiles = readdirSync(original).filter(name => name.endsWith('.dll') && !modules.includes(name));
    for (const file of vendorFiles) assert.equal(sha(join(runtime, file)), sha(join(original, file)));
    const pixels = (directory, file, count) => {
      run('pdftoppm', ['-r', '72', '-png', join(directory, file + '.pdf'), join(directory, file)]);
      const images = readdirSync(directory).filter(name => new RegExp(`^${file}-\\d+\\.png$`).test(name)).sort();
      assert.equal(images.length, count);
      return images.map(name => sha(join(directory, name)));
    };
    for (const [file, count] of [['export', 2], ['worker', 1]]) assert.deepEqual(pixels(join(work, 'protected-result'), file, count), pixels(join(work, 'baseline-result'), file, count));
    for (const [file, text] of [['export', 'Synthetic protected host'], ['worker', 'Synthetic protected worker']]) {
      const extract = directory => run('pdftotext', ['-layout', join(work, directory, file + '.pdf'), '-']);
      const originalText = extract('baseline-result'), protectedText = extract('protected-result');
      assert.ok(originalText.includes(text), 'Expected PDF content is missing: ' + file);
      assert.equal(protectedText, originalText, 'Protected PDF text changed: ' + file);
    }
    const supportRetention = rehearseSupportRetention({ original, protectedDirectory, modules, work, version });
    const report = { schemaVersion: 1, scope: 'joint-webapi-friend-reference-graph', version, syntheticOnly: true,
      platform: process.platform, architecture: after.actualArchitecture, modules, publicApi,
      baselineChecks: baseline.checks, protectedChecks: after.checks,
      baselineUnlicensedChecks: baselineUnlicensed.checks, protectedUnlicensedChecks: afterUnlicensed.checks, supportRetention,
      preservedHostContracts: ['PVAD.WebApi.dll', 'PVAD.Infrastructure.Persistence.dll'],
      nativeModulesUseDefaultProtection: true, privateNameSamplesRemoved: ['PvadLicenseAuthority', 'PdfWriter'],
      workerBundles, vendorFilesUnchanged: vendorFiles.length, pdfPixelsEqual: true, pdfTextEqual: true,
      authenticationTransport: 'test-only-synthetic-principal', databaseProvider: 'test-only-ef-inmemory',
      realBrowserSessionAcceptance: false, postgresAcceptance: false, crossPlatformAcceptance: false, productionStartupAcceptance: false,
      workerSandboxDeploymentAcceptance: false, productWideAcceptance: false, publicationPerformed: false };
    writeFileSync(join(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    succeeded = true;
  } finally {
    rmSync(secret, { recursive: true, force: true }); rmSync(work, { recursive: true, force: true });
    assert.ok(!existsSync(secret) && !existsSync(work)); assert.equal(sha(trust), beforeTrust);
    assert.deepEqual(regularOutputs(webApiProjects), beforeBuild, 'Regular product outputs changed.');
    writeFileSync(join(output, 'cleanup.json'), JSON.stringify({ succeeded, ephemeralArtifactsRemoved: true, sourceTrustUnchanged: true, regularProductBuildsUnchanged: true }, null, 2) + '\n');
  }
  console.log(`Protected WebApi rehearsal passed: ${output}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { main(process.argv.slice(2)); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
