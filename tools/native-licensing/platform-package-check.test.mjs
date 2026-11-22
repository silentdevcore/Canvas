import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { test } from 'node:test';
import { runtimeLibraries, validatePlatformResult, main } from './platform-package-check.mjs';
import { migrationProjects } from './joint-package-rehearsal.mjs';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
function sample() {
  return { schemaVersion: 1, platform: 'linux', architecture: 'Arm64',
    scope: 'native-products-and-migrations-fifty-four-assembly-graph',
    modules: Object.keys(migrationProjects).map(name => name + '.dll'), independentPackageReferences: 52,
    singleProductProducts: ['converter', 'importer', 'pdf', 'spreadsheet', 'word'], singleProductChecks: 67,
    unlicensedMetadataAssemblies: 54, unlicensedCoreDtoTypes: ['PVAD.Core.Contracts.DesignExportDto'], unlicensedContractChecks: 2,
    syntheticOnly: true, pdfPixelsEqual: true, jsonWordSpreadsheetEqual: true, nativeOcrExecuted: true,
    unprotectedRepeatVerified: true, testJitPolicy: 'inherited-runtime-settings',
    codeWorkerExecuted: true, codeWorkerPdfPixelsEqual: true, migrationProviderComparisonPassed: true,
    publicationPerformed: false, productionTrustProvisioned: false, productWideAcceptance: false,
    converterCallCases: 62, codeWorkerRequestCases: 12,
    migrationConsumerResults: { 'migration-code': {}, 'migration-report': {}, 'migration-viewer': {} } };
}

test('signed admission jobs install Unix OCR dependencies before executing native probes', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(read('../../.github/workflows/native-library-licensing.yml'));
  const job = workflow.jobs.licensing;
  assert.deepEqual(job.strategy.matrix.os, ['ubuntu-24.04', 'windows-2025', 'macos-15-intel']);
  assert.deepEqual(job.strategy.matrix.os, workflow.jobs['platform-package-protection'].strategy.matrix.os);
  const firstProbe = job.steps.findIndex(step => step.run === 'node tools/native-licensing/with-test-license.mjs pdf');
  const inventory = job.steps.findIndex(step => step.run === 'node --test tools/native-licensing/*.test.mjs');
  for (const [os, library] of [['Linux', 'liblept.so.5'], ['macOS', 'libleptonica.dylib']]) {
    const index = job.steps.findIndex(step => step.if === `runner.os == '${os}'` && step.run?.includes(library));
    assert.ok(index > inventory && inventory >= 0 && index < firstProbe, os);
    assert.ok(job.steps[index].run.includes('ln -s'));
    assert.ok(!job.steps[index].run.includes('ln -sf'), 'Existing runtime files must not be overwritten.');
  }
  const probe = read('./ImporterEngineProbe/ImporterEngineProbe.csproj');
  assert.ok(probe.includes('SkiaSharp.NativeAssets.Linux.NoDependencies" Version="3.119.2"'));
  assert.ok(probe.includes("IsOSPlatform('Linux')"));
});

test('platform acceptance requires the complete graph and actual OCR, code execution and document parity', () => {
  validatePlatformResult(sample(), 'linux');
  for (const property of ['nativeOcrExecuted', 'codeWorkerExecuted', 'codeWorkerPdfPixelsEqual', 'pdfPixelsEqual', 'jsonWordSpreadsheetEqual', 'migrationProviderComparisonPassed', 'unprotectedRepeatVerified']) {
    const result = sample(); result[property] = false;
    assert.throws(() => validatePlatformResult(result, 'linux'), undefined, property);
  }
  for (const change of [r => r.modules.splice(0, 1), r => r.modules[0] = 'Other.dll',
    r => r.independentPackageReferences--, r => r.converterCallCases--, r => r.codeWorkerRequestCases--,
    r => r.singleProductProducts.pop(), r => r.singleProductChecks--,
    r => r.unlicensedMetadataAssemblies--, r => r.unlicensedCoreDtoTypes = [],
    r => r.unlicensedCoreDtoTypes.push(r.unlicensedCoreDtoTypes[0]),
    r => r.unlicensedCoreDtoTypes[0] = 'Unreviewed.Engine', r => r.unlicensedContractChecks = 0,
    r => delete r.migrationConsumerResults['migration-report'], r => r.publicationPerformed = true,
    r => r.productionTrustProvisioned = true, r => r.productWideAcceptance = true,
    r => r.platform = 'darwin', r => r.architecture = 'Unknown', r => r.testJitPolicy = 'adaptive']) {
    const result = sample(); change(result); assert.throws(() => validatePlatformResult(result, 'linux'));
  }
  assert.throws(() => validatePlatformResult({ ...sample(), platform: 'win32' }, 'win32'));
});

test('runtime aliases retain the reviewed Linux architecture mapping and Windows uses its package runtime', () => {
  assert.ok(runtimeLibraries('linux', 'arm64').every(([, path]) => path.startsWith('/usr/lib/aarch64-linux-gnu/')));
  assert.ok(runtimeLibraries('linux', 'x64').every(([, path]) => path.startsWith('/usr/lib/x86_64-linux-gnu/')));
  assert.equal(runtimeLibraries('linux', 'x64').length, 3);
  assert.deepEqual(runtimeLibraries('linux', 'arm64'), [
    ['x64/libleptonica-1.82.0.so', '/usr/lib/aarch64-linux-gnu/liblept.so.5'],
    ['x64/libtesseract50.so', '/usr/lib/aarch64-linux-gnu/libtesseract.so.5'],
    ['libdl.so', '/usr/lib/aarch64-linux-gnu/libdl.so.2'],
  ]);
  assert.deepEqual(runtimeLibraries('win32', 'x64'), []);
  assert.throws(() => runtimeLibraries('win32', 'arm64'));
  assert.throws(() => runtimeLibraries('linux', 'ia32'));
  assert.throws(() => runtimeLibraries('other', 'x64'));
  const mac = runtimeLibraries('darwin', 'x64', name => '/opt/' + name);
  assert.equal(mac.length, 2); assert.ok(mac.every(([path]) => path.startsWith('x64/')));
  assert.deepEqual(mac.map(([, path]) => path), ['/opt/leptonica/lib/libleptonica.dylib', '/opt/tesseract/lib/libtesseract.dylib']);
});

test('the platform wrapper owns only temporary native aliases and never edits source trust or production paths', () => {
  assert.throws(() => main(['publish']), /No runtime, output, trust or publication overrides/);
  const source = read('./platform-package-check.mjs');
  for (const value of ["rehearse(['--migrations'])", "PVAD_PACKAGE_REQUIRE_NATIVE_OCR = '1'", 'rmSync(native',
    'jointCleanup: cleanup', 'Object.entries(previous)', 'publicationPerformed: false']) assert.ok(source.includes(value));
  assert.ok(!source.includes('AcceptNPOIOSMFLicense'));
  const probe = read('./GeneratorPackageProbe/ImporterPackageChecks.cs');
  assert.ok(probe.includes('PVAD_PACKAGE_REQUIRE_NATIVE_OCR'));
  assert.ok(probe.includes('real-protected-worker-native-ocr-text'));
  assert.ok(read('./joint-package-rehearsal.mjs').includes('skipping is not platform acceptance'));
});

test('each reviewed OS runs the full protected graph and uploads no binaries, keys or maps', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(read('../../.github/workflows/native-library-licensing.yml'));
  const licensingSteps = workflow.jobs.licensing.steps;
  const preserve = licensingSteps.findIndex(step => step.run === 'git config --global core.autocrlf false');
  const checkout = licensingSteps.findIndex(step => step.uses?.startsWith('actions/checkout@'));
  assert.ok(preserve >= 0 && preserve < checkout, 'Byte preservation must precede checkout in the licensing matrix too.');
  const job = workflow.jobs['platform-package-protection'];
  assert.deepEqual(job.strategy.matrix.os, ['ubuntu-24.04', 'windows-2025', 'macos-15-intel']);
  assert.equal(job['runs-on'], '${{ matrix.os }}');
  assert.equal(job.strategy['fail-fast'], false);
  assert.deepEqual(workflow.permissions, { contents: 'read' });
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/platform-package-check.mjs'));
  assert.ok(job.steps.some(step => step.run?.includes('core.autocrlf false')));
  for (const os of ['Linux', 'Windows', 'macOS']) assert.ok(job.steps.some(step => step.if === `runner.os == '${os}'`));
  const upload = job.steps.find(step => step.uses?.startsWith('actions/upload-artifact@'));
  assert.equal(upload.if, 'always()');
  assert.deepEqual(upload.with.path.trim().split('\n').map(path => path.trim()), [
    'artifacts/native-platform-packages/*/report.json', 'artifacts/native-platform-packages/*/cleanup.json',
    'artifacts/native-joint-packages/*/report.json', 'artifacts/native-joint-packages/*/cleanup.json']);
  assert.ok(!JSON.stringify(job).includes('nuget push'));
});
