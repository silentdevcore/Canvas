import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, posix, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { main as rehearse, migrationProjects } from './joint-package-rehearsal.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const controlledEnvironment = ['PVAD_PACKAGE_REQUIRE_NATIVE_OCR', 'PVAD_PACKAGE_OCR_NATIVE_PATH', 'PVAD_PACKAGE_OCR_TESSDATA_PATH', 'LD_LIBRARY_PATH'];

export function runtimeLibraries(platform, architecture, brewPrefix) {
  assert.ok(['x64', 'arm64'].includes(architecture), 'Unreviewed native runtime architecture.');
  if (platform === 'win32') {
    assert.equal(architecture, 'x64', 'Windows ARM64 OCR is not a reviewed runtime.');
    return [];
  }
  if (platform === 'linux') {
    const directory = architecture === 'arm64' ? '/usr/lib/aarch64-linux-gnu' : '/usr/lib/x86_64-linux-gnu';
    return [['x64/libleptonica-1.82.0.so', posix.join(directory, 'liblept.so.5')],
      ['x64/libtesseract50.so', posix.join(directory, 'libtesseract.so.5')], ['libdl.so', posix.join(directory, 'libdl.so.2')]];
  }
  assert.equal(platform, 'darwin', 'Unreviewed operating system.');
  return [['x64/libleptonica-1.82.0.dylib', posix.join(brewPrefix('leptonica'), 'lib/libleptonica.dylib')],
    ['x64/libtesseract50.dylib', posix.join(brewPrefix('tesseract'), 'lib/libtesseract.dylib')]];
}

export function validatePlatformResult(report, platform) {
  assert.equal(report.schemaVersion, 1);
  assert.equal(report.platform, platform);
  assert.equal(report.scope, 'native-products-and-migrations-fifty-four-assembly-graph');
  assert.equal(report.modules.length, 54);
  assert.deepEqual([...report.modules].sort(), Object.keys(migrationProjects).map(name => name + '.dll').sort());
  assert.equal(report.independentPackageReferences, 52);
  assert.deepEqual(report.singleProductProducts, ['converter', 'importer', 'pdf', 'spreadsheet', 'word']);
  assert.equal(report.singleProductChecks, 67);
  assert.equal(report.unlicensedMetadataAssemblies, 54);
  assert.ok(Array.isArray(report.unlicensedCoreDtoTypes) && report.unlicensedCoreDtoTypes.length > 0);
  assert.equal(new Set(report.unlicensedCoreDtoTypes).size, report.unlicensedCoreDtoTypes.length);
  assert.ok(report.unlicensedCoreDtoTypes.every(type => type.startsWith('PVAD.Core.Contracts.')));
  assert.ok(report.unlicensedContractChecks > report.unlicensedCoreDtoTypes.length);
  for (const name of ['syntheticOnly', 'pdfPixelsEqual', 'jsonWordSpreadsheetEqual', 'nativeOcrExecuted',
    'codeWorkerExecuted', 'codeWorkerPdfPixelsEqual', 'migrationProviderComparisonPassed', 'unprotectedRepeatVerified']) assert.equal(report[name], true, name);
  assert.equal(report.testJitPolicy, 'inherited-runtime-settings');
  for (const name of ['publicationPerformed', 'productionTrustProvisioned', 'productWideAcceptance']) assert.equal(report[name], false, name);
  assert.equal(report.converterCallCases, 62);
  assert.equal(report.codeWorkerRequestCases, 12);
  assert.deepEqual(Object.keys(report.migrationConsumerResults).sort(), ['migration-code', 'migration-report', 'migration-viewer']);
  assert.ok(['X64', 'Arm64'].includes(report.architecture));
  if (platform === 'win32') assert.equal(report.architecture, 'X64');
}

export function main(args) {
  assert.equal(args.length, 0, 'No runtime, output, trust or publication overrides are accepted.');
  const output = join(root, 'artifacts/native-platform-packages', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  mkdirSync(output, { recursive: true });
  const native = mkdtempSync(join(tmpdir(), 'pvad-platform-native-'));
  const previous = Object.fromEntries(controlledEnvironment.map(key => [key, process.env[key]]));
  let succeeded = false;
  try {
    const prefix = name => {
      const result = spawnSync('brew', ['--prefix', name], { encoding: 'utf8', timeout: 30_000 });
      assert.equal(result.status, 0, 'The reviewed OCR runtime must be installed.');
      return result.stdout.trim();
    };
    const libraries = runtimeLibraries(process.platform, process.arch, prefix);
    for (const [name, source] of libraries) {
      assert.ok(existsSync(source), 'A required native OCR runtime is unavailable.');
      const target = join(native, name); mkdirSync(dirname(target), { recursive: true }); symlinkSync(source, target);
    }
    process.env.PVAD_PACKAGE_REQUIRE_NATIVE_OCR = '1';
    process.env.PVAD_PACKAGE_OCR_TESSDATA_PATH = join(root, 'src/Importing/PVAD.FileImporter.ImageOcr/tessdata');
    if (process.platform === 'win32') delete process.env.PVAD_PACKAGE_OCR_NATIVE_PATH;
    else process.env.PVAD_PACKAGE_OCR_NATIVE_PATH = native;
    if (process.platform === 'linux') process.env.LD_LIBRARY_PATH = [native, previous.LD_LIBRARY_PATH].filter(Boolean).join(':');
    const { output: jointOutput, report } = rehearse(['--migrations']);
    validatePlatformResult(report, process.platform);
    const cleanup = JSON.parse(readFileSync(join(jointOutput, 'cleanup.json')));
    for (const value of Object.values(cleanup)) assert.equal(value, true);
    writeFileSync(join(output, 'report.json'), JSON.stringify({ schemaVersion: 1, platform: process.platform,
      nodeArchitecture: process.arch, nativeRuntimeLibraries: libraries.map(([name]) => name),
      jointReport: report, jointCleanup: cleanup, publicationPerformed: false }, null, 2) + '\n');
    succeeded = true;
  } finally {
    for (const [key, value] of Object.entries(previous)) { if (value === undefined) delete process.env[key]; else process.env[key] = value; }
    rmSync(native, { recursive: true, force: true });
    assert.ok(!existsSync(native));
    writeFileSync(join(output, 'cleanup.json'), JSON.stringify({ succeeded, temporaryNativeLinksRemoved: true }, null, 2) + '\n');
  }
  console.log(`Native platform package evidence: ${output}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { main(process.argv.slice(2)); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
