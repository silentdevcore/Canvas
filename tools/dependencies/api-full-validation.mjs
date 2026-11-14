import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { createWriteStream } from 'node:fs';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const require = createRequire(resolve(root, 'pvad-designer/package.json'));
const theory = 'PVAD.Api.Tests.PvadApplicationLifetimeTests.Other_failures_are_not_converted_to_a_retention_exit';
const exceptionTypes = ['System.InvalidOperationException', 'System.IO.IOException', 'System.OperationCanceledException', 'System.AggregateException'];
const hash = value => createHash('sha256').update(value).digest('hex');

export function apiValidationEnvironment(environment, temporary) {
  const allow = ['PATH', 'HOME', 'TMPDIR', 'DOTNET_ROOT', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES', 'LANG', 'LC_ALL'];
  return {
    ...Object.fromEntries(allow.filter(key => environment[key] !== undefined).map(key => [key, environment[key]])),
    PVAD_RUN_POSTGRES_TESTS: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1',
    DataProtection__KeysDirectory: resolve(temporary, 'keys'), Storage__RootPath: resolve(temporary, 'objects'),
    PdfViewer__AnnotationStoragePath: resolve(temporary, 'viewer'),
    PdfTrust__RevocationEvidence__CacheDirectory: resolve(temporary, 'cache'),
    Licensing__PrivateKeyPath: resolve(temporary, 'licensing/private.pem'),
    Licensing__PublicKeyPath: resolve(temporary, 'licensing/public.pem'),
  };
}

export function auditApiResults(catalog, xml) {
  const { JSDOM } = require('jsdom');
  const dom = new JSDOM(xml, { contentType: 'text/xml' });
  try {
    const document = dom.window.document;
    const counter = document.querySelector('ResultSummary Counters');
    assert.ok(counter, 'TRX counters are required.');
    const counters = Object.fromEntries([...counter.attributes].map(value => [value.name, Number(value.value)]));
    const results = [...document.querySelectorAll('UnitTestResult')].map(node => ({
      name: node.getAttribute('testName'), outcome: node.getAttribute('outcome'), executionId: node.getAttribute('executionId'),
      duration: node.getAttribute('duration'), startTime: node.getAttribute('startTime'), endTime: node.getAttribute('endTime'),
    }));
    assert.ok(catalog.length > 0 && catalog.every(name => typeof name === 'string' && name.length > 0));
    assert.equal(new Set(catalog).size, catalog.length, 'Discovery names must be unique.');
    assert.equal(catalog.filter(name => name === theory).length, 1, 'Expected late-enumerated theory must be discovered once.');
    const expanded = results.filter(result => result.name?.startsWith(theory + '('));
    assert.equal(expanded.length, exceptionTypes.length, 'Only the four reviewed exception variants may expand.');
    for (const type of exceptionTypes)
      assert.equal(expanded.filter(result => result.name.startsWith(theory + '(failure: ' + type + ':')).length, 1);
    const concrete = results.filter(result => !expanded.includes(result));
    const expected = new Set(catalog.filter(name => name !== theory));
    assert.equal(concrete.length, expected.size, 'Every other discovered case must execute exactly once.');
    for (const result of concrete) assert.ok(expected.has(result.name), 'Unexpected test result.');
    assert.equal(new Set(results.map(result => result.name)).size, results.length, 'Duplicate result names.');
    assert.ok(results.every(result => result.executionId), 'Execution IDs are required.');
    assert.equal(new Set(results.map(result => result.executionId)).size, results.length, 'Duplicate execution IDs.');
    assert.ok(results.every(result => result.outcome === 'Passed'), 'Failed, skipped or aborted cases are not passing.');
    assert.ok(results.every(result => Number.isFinite(Date.parse(result.startTime)) && Number.isFinite(Date.parse(result.endTime))
      && Date.parse(result.endTime) >= Date.parse(result.startTime)), 'Valid case start/end times are required.');
    for (const key of ['total', 'executed', 'passed']) assert.equal(counters[key], results.length);
    for (const [key, count] of Object.entries(counters))
      if (!['total', 'executed', 'passed'].includes(key)) assert.equal(count, 0, 'Non-passing TRX counter: ' + key);
    return { status: 'passed', discoveryEntries: catalog.length, concreteTestCases: results.length, counters,
      lateEnumeration: { method: theory, exceptionTypes }, results };
  } finally { dom.window.close(); }
}

export async function runFullApiValidation() {
  const output = resolve(root, 'artifacts/api-full-validation', new Date().toISOString().replaceAll(':', '-') + '-' + randomUUID());
  await mkdir(output, { recursive: true, mode: 0o700 });
  const temporary = await mkdtemp(resolve(tmpdir(), 'pvad-api-full-'));
  const environment = apiValidationEnvironment(process.env, temporary);
  const assembly = resolve(root, 'tests/PVAD.Api.Tests/bin/Debug/net10.0/PVAD.Api.Tests.dll');
  const settings = 'tools/dependencies/api-serial.runsettings';
  const summary = { sourceCommit: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
    sourceDiffSha256: hash(execFileSync('git', ['diff', '--binary', 'HEAD', '--', '.', ':(exclude).vscode/**'], { cwd: root, maxBuffer: 32 * 1024 * 1024 })),
    settingsSha256: hash(await readFile(resolve(root, settings))), startedAt: new Date().toISOString(),
    status: 'running', syntheticOnly: true, postgresEnabled: true, commands: [] };
  const save = () => writeFile(resolve(output, 'summary.json'), JSON.stringify(summary, null, 2) + '\n', { mode: 0o600 });
  const run = async (name, args, minutes) => {
    console.log('Full API validation: ' + name + '; private evidence: ' + output);
    const command = { name, arguments: args, startedAt: new Date().toISOString() };
    summary.commands.push(command);
    await save();
    const log = createWriteStream(resolve(output, name + '.log'), { mode: 0o600 });
    const child = spawn('dotnet', args, { cwd: root, env: environment, detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', 'pipe'] });
    let stdout = '', timedOut = false;
    const signal = name => {
      if (!child.pid) return;
      try { if (process.platform === 'win32') child.kill(name); else process.kill(-child.pid, name); }
      catch (error) { if (error.code !== 'ESRCH') throw error; }
    };
    let killTimer;
    const timer = setTimeout(() => {
      timedOut = true; signal('SIGTERM'); killTimer = setTimeout(() => signal('SIGKILL'), 5000);
    }, minutes * 60_000);
    child.stdout.on('data', bytes => { if (name === 'discovery') stdout += bytes; });
    child.stdout.pipe(log, { end: false }); child.stderr.pipe(log, { end: false });
    try {
      const exitCode = await new Promise((yes, no) => { child.once('error', no); child.once('close', code => yes(code)); });
      command.exitCode = exitCode; command.timedOut = timedOut;
      assert.equal(timedOut, false, 'Owned command exceeded its time budget.');
      assert.equal(exitCode, 0, name + ' failed; inspect its private log.');
      return stdout;
    } finally {
      clearTimeout(timer); clearTimeout(killTimer);
      await new Promise(yes => log.end(yes));
      command.completedAt = new Date().toISOString(); await save();
    }
  };
  try {
    const project = 'tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj';
    await run('build', ['build', project, '--no-restore', '--disable-build-servers', '-m:1', '-t:Rebuild',
      '-p:UseSharedCompilation=false', '-p:SkipOcrWorkerPackaging=true', '-p:SkipCodeWorkerPackaging=true', '--nologo', '-v:q'], 15);
    summary.assemblySha256 = hash(await readFile(assembly));
    summary.runnerSha256 = hash(await readFile(fileURLToPath(import.meta.url)));
    const common = ['test', project, '--no-build', '--settings', settings];
    const discovery = await run('discovery', [...common, '--list-tests'], 5);
    const catalog = discovery.split(/\r?\n/).filter(line => /^\s+PVAD\.Api\.Tests\./.test(line)).map(line => line.trim());
    assert.ok(catalog.length > 1000, 'A complete unfiltered API catalog is required.');
    await writeFile(resolve(output, 'catalog.json'), JSON.stringify(catalog, null, 2) + '\n', { mode: 0o600 });
    await run('full', [...common, '--results-directory', output, '--logger', 'trx;LogFileName=api-full.trx',
      '--logger', 'console;verbosity=detailed', '--diag', resolve(output, 'vstest-diag.log'),
      '--blame-hang', '--blame-hang-timeout', '180s', '--blame-hang-dump-type', 'mini'], 42);
    assert.equal(hash(await readFile(assembly)), summary.assemblySha256, 'Assembly changed during execution.');
    const audit = auditApiResults(catalog, await readFile(resolve(output, 'api-full.trx'), 'utf8'));
    await writeFile(resolve(output, 'verified-results.json'), JSON.stringify(audit, null, 2) + '\n', { mode: 0o600 });
    summary.status = 'passed'; summary.passed = audit.concreteTestCases;
  } catch (error) {
    summary.status = 'failed'; summary.error = error.message;
    throw error;
  } finally {
    summary.completedAt = new Date().toISOString();
    await rm(temporary, { recursive: true, force: true });
    summary.temporaryStorageRemoved = true; await save();
  }
  return { output, ...summary };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  if (process.argv.length !== 2 || process.platform === 'win32') {
    console.error('Run without arguments on the documented local POSIX host; no external resource names are accepted.');
    process.exitCode = 1;
  } else {
    try { const result = await runFullApiValidation(); console.log(JSON.stringify({ output: result.output, status: result.status, passed: result.passed })); }
    catch (error) { console.error(error.message); process.exitCode = 1; }
  }
}
