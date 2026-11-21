import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { closeSync, cpSync, existsSync, lstatSync, openSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { nativeTestAssemblyPath, withNativeTestSession } from './test-session.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const require = createRequire(join(root, 'pvad-designer/package.json'));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
export const projects = [
  'PVAD.Licensing.Tests', 'PVAD.Generator.Tests', 'PVAD.Infrastructure.Pdf.Tests',
  'PVAD.Importer.Tests', 'PVAD.FileImporter.Tests', 'PVAD.FileImporter.ImageAnalysis.Tests',
  'PVAD.FileImporter.ImageOcr.Tests', 'PVAD.Infrastructure.Converters.Tests',
  'PVAD.Infrastructure.Word.Tests', 'PVAD.Infrastructure.Spreadsheet.Tests',
];
export const apiClasses = [
  'PvadNativePdfLicenseTests', 'PvadNativeWordLicenseTests', 'PvadNativeSpreadsheetLicenseTests',
  'PvadNativeConverterLicenseTests', 'PvadNativeImporterLicenseTests', 'PvadNativeTrustLicenseTests',
  'PvadNativeCodeMigrationLicenseTests', 'PvadNativeViewerMigrationLicenseTests', 'PvadNativeReportMigrationLicenseTests',
  'ExportControllerTests', 'SpreadsheetControllerTests', 'DocumentOpsControllerTests',
  'MigrationGeneratedCodeExecutionTests', 'CodeWorkspaceConversionTests', 'CodeWorkspaceApplyTests',
  'PdfTrust', 'PdfSignature', 'PdfRedaction', 'PdfViewerRedaction', 'PdfViewerAnnotationsControllerTests',
  'PdfEInvoice', 'PvadPdfTrust', 'PvadEInvoice', 'PvadRedaction', 'ImageConversionControllerTests',
];

export function auditRegressionTrx(xml) {
  const { JSDOM } = require('jsdom');
  const dom = new JSDOM(xml, { contentType: 'text/xml' });
  try {
    const document = dom.window.document;
    const summary = document.querySelector('ResultSummary');
    assert.equal(summary?.getAttribute('outcome'), 'Completed');
    const counter = summary.querySelector('Counters');
    assert.ok(counter);
    const counters = Object.fromEntries([...counter.attributes].map(value => [value.name, Number(value.value)]));
    const rows = [...document.querySelectorAll('UnitTestResult')];
    assert.ok(rows.length > 0, 'An empty test selection is not acceptance.');
    for (const key of ['total', 'executed', 'passed']) assert.equal(counters[key], rows.length, key);
    for (const [key, count] of Object.entries(counters)) {
      assert.ok(Number.isSafeInteger(count) && count >= 0);
      if (!['total', 'executed', 'passed'].includes(key)) assert.equal(count, 0, key);
    }
    const ids = rows.map(row => row.getAttribute('executionId'));
    assert.ok(ids.every(Boolean) && new Set(ids).size === ids.length, 'Duplicate/missing execution IDs.');
    for (const row of rows) {
      assert.equal(row.getAttribute('outcome'), 'Passed');
      assert.ok(row.getAttribute('testName'));
      const start = Date.parse(row.getAttribute('startTime')), end = Date.parse(row.getAttribute('endTime'));
      assert.ok(Number.isFinite(start) && Number.isFinite(end) && end >= start);
    }
    assert.equal(document.querySelectorAll('RunInfo[outcome="Error"], RunInfo[outcome="Aborted"]').length, 0);
    return { passed: rows.length, counters, testNames: rows.map(row => row.getAttribute('testName')) };
  } finally { dom.window.close(); }
}

export function regressionEnvironment(environment, output) {
  const result = { ...environment };
  // Ignore caller rendering overrides; every visual run must generate fresh artifacts.
  for (const key of Object.keys(result))
    if (/^(?:PVAD_VISUAL_PARITY_|PVAD_PDF_TRUST_(?:OPERATION_|VISUAL_))/.test(key)) delete result[key];
  return { ...result, MSBUILDNOINPROCNODE: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0',
    MSBUILDDISABLENODEREUSE: '1', UseSharedCompilation: 'false', BuildInParallel: 'false', VITE_ENABLE_VISUAL_PARITY: 'false', PVAD_RUN_POSTGRES_TESTS: '1',
    TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE: result.TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE ?? '/var/run/docker.sock',
    TESTCONTAINERS_RYUK_DISABLED: 'false',
    ConnectionStrings__PvadDatabase: 'Host=127.0.0.1;Port=1;Database=unused_regression;Username=unused;Password=unused;Timeout=1',
    DataProtection__KeysDirectory: join(output, 'runtime/keys'), Storage__RootPath: join(output, 'runtime/objects'),
    PdfViewer__AnnotationStoragePath: join(output, 'runtime/viewer'),
    PdfTrust__RevocationEvidence__CacheDirectory: join(output, 'runtime/cache'),
    Licensing__PrivateKeyPath: join(output, 'runtime/licensing/private.pem'),
    Licensing__PublicKeyPath: join(output, 'runtime/licensing/public.pem'),
  };
}

export async function runRegressions() {
  assert.notEqual(process.platform, 'win32', 'This local regression launcher uses POSIX process groups; platform CI stays separate.');
  return await withNativeTestSession(async () => {
    const output = dirname(process.env.PVAD_NATIVE_TEST_BUILD_ROOT);
    const env = regressionEnvironment(process.env, output);
    env.DOCKER_HOST ??= execFileSync('docker', ['context', 'inspect', '--format', '{{.Endpoints.docker.Host}}'], { encoding: 'utf8' }).trim();
    assert.ok(env.DOCKER_HOST.startsWith('unix://'), 'This profile only creates isolated database fixtures through a local Docker socket.');
    const report = { scope: 'local-native-source-regressions-not-full-api-or-platform-acceptance',
      status: 'running', syntheticOnly: true, steps: [], startedAt: new Date().toISOString() };
    const save = () => writeFileSync(join(output, 'regression-report.json'), JSON.stringify(report, null, 2) + '\n');
    const inputs = [...new Set(execFileSync('git', ['ls-files', '-co', '--exclude-standard', '--',
      'src', 'tests', 'PVAD.WebApi', 'pvad-designer', 'tools/native-licensing', 'tools/visual-parity',
      'test-assets', 'product-metadata', 'Directory.Build.props', 'Directory.Build.targets', 'VERSION'],
    { cwd: root, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 }).trim().split('\n'))]
      .filter(file => file && lstatSync(join(root, file)).isFile()).sort();
    const hashes = Object.fromEntries(inputs.map(file => [file, hash(readFileSync(join(root, file)))]));
    writeFileSync(join(output, 'regression-source-hashes.json'), JSON.stringify(hashes, null, 2));
    const snapshots = [
      ['artifacts/visual-parity', 'visual-parity'], ['artifacts/pdf-trust-visual', 'pdf-trust-visual'],
      ['artifacts/pdf-trust-operations', 'pdf-trust-operations'],
      [`artifacts/platform-parity/${process.platform}-${process.arch}`, 'platform-parity'],
    ];
    for (const [path, name] of snapshots) if (existsSync(join(root, path)))
      cpSync(join(root, path), join(output, 'previous-artifacts', name), { recursive: true });
    console.log('Regression evidence: ' + relative(root, output));
    save();
    async function run(name, command, args, extraEnv = {}, minutes = 20, cwd = root) {
      console.log('RUN ' + name);
      const step = { name, status: 'running', startedAt: new Date().toISOString() };
      report.steps.push(step); save();
      const fd = openSync(join(output, name + '.log'), 'w', 0o600);
      const child = spawn(command, args, { cwd, env: { ...env, ...extraEnv }, detached: true, stdio: ['ignore', fd, fd] });
      let timedOut = false, killTimer;
      const stop = signal => { if (child.pid) try { process.kill(-child.pid, signal); } catch (error) { if (error.code !== 'ESRCH') throw error; } };
      const timer = setTimeout(() => { timedOut = true; stop('SIGTERM'); killTimer = setTimeout(() => stop('SIGKILL'), 5000); }, minutes * 60_000);
      try {
        const code = await new Promise((yes, no) => { child.once('error', no); child.once('close', yes); });
        step.exitCode = code; step.timedOut = timedOut;
        assert.ok(!timedOut && code === 0, name + ': inspect the private step log');
        step.status = 'passed'; console.log('PROCESS COMPLETE ' + name);
      } catch (error) { step.status = 'failed'; throw error; }
      finally { clearTimeout(timer); clearTimeout(killTimer); closeSync(fd); step.completedAt = new Date().toISOString(); save(); }
      return step;
    }
    try {
      for (const name of [...projects, 'PVAD.Api.Tests']) {
        const project = `tests/${name}/${name}.csproj`;
        const args = ['test', project, '--disable-build-servers', '-m:1', '-nr:false',
          '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', '--results-directory', output,
          '--logger', `trx;LogFileName=regression-${name}.trx`, '--verbosity', 'minimal'];
        if (name === 'PVAD.Api.Tests') args.push('--filter', apiClasses.map(value => 'FullyQualifiedName~' + value).join('|'));
        const step = await run(name, 'dotnet', args);
        let audit;
        try { audit = auditRegressionTrx(readFileSync(join(output, `regression-${name}.trx`), 'utf8')); }
        catch (error) { step.status = 'failed'; save(); throw error; }
        step.passed = audit.passed; step.filtered = name === 'PVAD.Api.Tests';
        console.log(`AUDITED ${name}: ${audit.passed} passed, none skipped.`);
        if (step.filtered) for (const required of ['CodeWorkspaceConversionTests', 'MigrationGeneratedCodeExecutionTests',
          'PdfTrustControllerTests', 'PdfSignaturesControllerTests', 'PdfRedactionsControllerTests', 'PvadNativePdfLicenseTests'])
          assert.ok(audit.testNames.some(value => value.includes(required + '.')), 'Missing API regression group: ' + required);
        const assembly = nativeTestAssemblyPath(project);
        step.assemblySha256 = hash(readFileSync(assembly)); save();
      }
      await run('designer-typecheck', process.execPath, ['pvad-designer/node_modules/typescript/bin/tsc', '--noEmit', '-p', 'pvad-designer/tsconfig.json']);
      await run('designer-jest', process.execPath, [join(root, 'pvad-designer/node_modules/jest/bin/jest.js'),
        '--runInBand', '--json', '--outputFile', join(output, 'designer-jest.json')], {}, 20, join(root, 'pvad-designer'));
      const jest = JSON.parse(readFileSync(join(output, 'designer-jest.json')));
      assert.ok(jest.success && jest.numTotalTests > 0 && jest.numPassedTests === jest.numTotalTests && jest.numPendingTests === 0 && jest.numFailedTests === 0);
      report.designerTests = jest.numTotalTests;
      await run('designer-build', process.execPath, [join(root, 'pvad-designer/node_modules/vite/bin/vite.js'), 'build'], {}, 20, join(root, 'pvad-designer'));
      await run('trust-golden', process.execPath, ['tools/visual-parity/pdf-trust-golden.mjs'], { PVAD_PDF_TRUST_VISUAL_ENFORCE: 'true' });
      cpSync(join(root, 'artifacts/pdf-trust-visual'), join(output, 'trust-golden'), { recursive: true });
      await run('trust-operations', process.execPath, ['tools/visual-parity/pdf-trust-operations.mjs'], { PVAD_PDF_TRUST_OPERATION_ENFORCE: 'true' });
      cpSync(join(root, 'artifacts/pdf-trust-operations'), join(output, 'trust-operations'), { recursive: true });
      await run('platform-parity', process.execPath, ['tools/visual-parity/platforms.mjs'], {}, 40);
      cpSync(join(root, `artifacts/platform-parity/${process.platform}-${process.arch}`), join(output, 'platform-parity'), { recursive: true });
      for (const fixture of ['mixed-layout', 'image-fit', 'shared-renderer', 'styled-tables']) {
        const path = fixture === 'mixed-layout' ? 'test-assets/page-layout/mixed-layout.json' : `test-assets/visual-parity/${fixture}.json`;
        await run('parity-' + fixture, process.execPath, ['tools/visual-parity/run.mjs', '--bundled-browser'],
          { PVAD_VISUAL_PARITY_FIXTURE: path, PVAD_VISUAL_PARITY_ENFORCE: 'true', PVAD_VISUAL_PARITY_REUSE_ARTIFACTS: 'false' });
        cpSync(join(root, 'artifacts/visual-parity'), join(output, 'parity-' + fixture), { recursive: true });
      }
      for (const [file, expected] of Object.entries(hashes)) assert.equal(hash(readFileSync(join(root, file))), expected, 'Regression input changed: ' + file);
      report.sourceInputsUnchanged = inputs.length;
      report.status = 'passed';
    } catch (error) { report.status = 'failed'; report.error = error.message; throw error; }
    finally {
      rmSync(join(output, 'runtime'), { recursive: true, force: true });
      report.temporaryRuntimeRemoved = !existsSync(join(output, 'runtime'));
      report.completedAt = new Date().toISOString(); save();
    }
    return { output, ...report };
  });
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  assert.equal(process.argv.length, 2, 'No external paths or test filters are accepted.');
  await runRegressions();
}
