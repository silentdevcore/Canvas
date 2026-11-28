import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { randomUUID, randomBytes, createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, readFileSync, readdirSync, existsSync, writeFileSync, rmSync, createWriteStream } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { createServer } from 'node:net';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createTestLicenses } from './with-test-license.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const project = 'tools/account-workflow-browser/PVAD.AccountWorkflowBrowser.csproj';
export function connectedEnvironment(environment) {
  return Object.fromEntries(['PATH', 'HOME', 'TMPDIR', 'DOTNET_ROOT', 'DOTNET_HOST_PATH', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES',
    'DOCKER_HOST', 'TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE', 'LANG', 'LC_ALL'].filter(key => environment[key] !== undefined).map(key => [key, environment[key]]));
}
const digest = path => createHash('sha256').update(readFileSync(path)).digest('hex');
function regularSnapshot() {
  const result = {};
  function visit(path) {
    if (!existsSync(path)) return;
    for (const entry of readdirSync(path, { withFileTypes: true })) {
      const file = join(path, entry.name);
      if (entry.isDirectory()) visit(file); else if (entry.isFile()) result[file] = digest(file);
    }
  }
  for (const name of ['bin', 'obj']) visit(join(root, 'src/Licensing/PVAD.Licensing', name));
  return result;
}
async function freePort() {
  return new Promise((resolvePort, reject) => {
    const server = createServer(); server.once('error', reject);
    server.listen(0, '127.0.0.1', () => { const port = server.address().port; server.close(error => error ? reject(error) : resolvePort(port)); });
  });
}
async function waitSite(url, child) {
  for (let i = 0; i < 120; i++) {
    if (child.exitCode !== null) throw new Error('An isolated website exited before readiness.');
    try { if ((await fetch(url)).ok) return; } catch {}
    await new Promise(yes => setTimeout(yes, 250));
  }
  throw new Error('Isolated website readiness timed out.');
}

export async function runConnectedAccountBrowser() {
  assert.equal(process.argv.length, 2, 'This synthetic workflow accepts no deployment inputs.');
  const id = randomUUID().replaceAll('-', '');
  const output = join(root, 'artifacts/connected-account-browser', `${Date.now()}-${id.slice(0, 8)}`);
  const build = join(output, 'isolated-build-not-for-publication');
  const work = mkdtempSync(join(tmpdir(), 'pvad-account-live-'));
  mkdirSync(output, { recursive: true }); writeFileSync(join(work, 'owner.txt'), id, { mode: 0o600 });
  const trust = join(root, 'src/Licensing/PVAD.Licensing/TrustedKeys.json');
  const originalTrust = digest(trust), originalRegular = regularSnapshot();
  const environment = connectedEnvironment(process.env);
  const children = [], streams = [], pending = new Map();
  let host, browser, succeeded = false, proof;
  const step = message => console.log('connected-account: ' + message);
  function command(name, executable, args, cwd = root, extra = {}) {
    const child = spawn(executable, args, { cwd, env: { ...environment, ...extra }, stdio: ['pipe', 'pipe', 'pipe'], detached: process.platform !== 'win32' });
    const exited = new Promise(yes => { child.once('error', () => yes(1)); child.once('exit', code => yes(code ?? 1)); });
    const log = createWriteStream(join(output, name + '.log')); streams.push(log);
    if (name !== 'host') child.stdout.pipe(log); // Control replies contain synthetic mail tokens; never persist them.
    child.stderr.pipe(log); children.push({ child, exited });
    return { child, exited };
  }
  async function finish(handle, phase) { assert.equal(await handle.exited, 0, `${phase} failed; see its isolated log.`); }
  async function stop(handle) {
    if (handle.child.exitCode !== null || handle.child.signalCode !== null) return;
    try { if (process.platform !== 'win32') process.kill(-handle.child.pid, 'SIGTERM'); else handle.child.kill(); } catch {}
    const exited = await Promise.race([handle.exited.then(() => true), new Promise(yes => setTimeout(() => yes(false), 10_000))]);
    if (!exited) { try { if (process.platform !== 'win32') process.kill(-handle.child.pid, 'SIGKILL'); else handle.child.kill('SIGKILL'); } catch {} await handle.exited; }
  }
  const control = (action, data = {}) => new Promise((yes, no) => {
    const requestId = randomUUID();
    const timer = setTimeout(() => { pending.delete(requestId); no(new Error('Isolated control timed out: ' + action)); }, 60_000);
    pending.set(requestId, result => { clearTimeout(timer); yes(result); });
    host.child.stdin.write(JSON.stringify({ id: requestId, action, ...data }) + '\n');
  });
  try {
    const paths = createTestLicenses(work, Date.now(), false, false, true);
    const native = { PVAD_NATIVE_LICENSE_PATH: paths.integrated, NativeLicensing__LicensePath: paths.integrated,
      PVAD_TEST_NATIVE_ISSUER_PRIVATE_KEY: paths.issuerPrivateKey, PVAD_REQUIRE_NATIVE_ISSUER: '1' };
    const props = [`-p:PvadLicenseTrustStorePath=${paths.trustPath}`, '-p:PvadNativeLicenseTestBootstrap=true'];
    step('restore'); await finish(command('restore', 'dotnet', ['restore', project, '--artifacts-path', build, '--disable-parallel', ...props], root, native), 'Restore');
    step('build'); await finish(command('build', 'dotnet', ['build', project, '--no-restore', '--artifacts-path', build,
      '--disable-build-servers', '-m:1', '-nr:false', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false', ...props,
      '--verbosity', 'minimal'], root, native), 'Build');
    const accountPort = await freePort(), designerPort = await freePort();
    const urls = { account: `http://localhost:${accountPort}`, designer: `http://localhost:${designerPort}` };
    step('real-backend');
    host = command('host', 'dotnet', [join(build, 'bin/PVAD.AccountWorkflowBrowser/debug/PVAD.AccountWorkflowBrowser.dll')], root, native);
    let buffer = '';
    const booted = new Promise((yes, no) => {
      const timer = setTimeout(() => no(new Error('Real backend startup timed out.')), 90_000);
      pending.set('boot', result => { clearTimeout(timer); yes(result); });
      host.exited.then(() => { clearTimeout(timer); no(new Error('Real backend exited.')); });
    });
    host.child.stdout.on('data', data => {
      buffer += data.toString();
      while (buffer.includes('\n')) {
        const index = buffer.indexOf('\n'), line = buffer.slice(0, index); buffer = buffer.slice(index + 1);
        if (!line.startsWith('PVAD_ACCOUNT_LIVE ')) continue;
        const reply = JSON.parse(line.slice('PVAD_ACCOUNT_LIVE '.length));
        const receive = pending.get(reply.id); pending.delete(reply.id); receive?.(reply.result);
      }
    });
    host.child.stdin.write(JSON.stringify({ runId: id, workDirectory: work, accountUrl: urls.account, designerUrl: urls.designer,
      privateKeyPath: paths.issuerPrivateKey, workerPath: join(build, 'bin/PVAD.CodeWorker/debug') }) + '\n');
    const started = await booted;
    assert.ok(started.realKestrel && started.realPostgres && started.realSigner);
    assert.ok(new URL(started.apiUrl).hostname === '127.0.0.1' || new URL(started.apiUrl).hostname === 'localhost');
    const sites = { PVAD_DEV_API_URL: started.apiUrl, VITE_PVAD_ACCOUNT_URL: urls.account + '/', VITE_PVAD_DESIGNER_URL: urls.designer + '/' };
    step('websites');
    const vite = join(root, 'pvad-designer/node_modules/vite/bin/vite.js');
    const account = command('account', process.execPath, [vite, '--host', '127.0.0.1', '--port', String(accountPort), '--strictPort'], join(root, 'websites/PVAD.Account'), sites);
    const designer = command('designer', process.execPath, [vite, '--host', '127.0.0.1', '--port', String(designerPort), '--strictPort'], join(root, 'pvad-designer'), sites);
    await Promise.all([waitSite(urls.account + '/login', account.child), waitSite(urls.designer, designer.child)]);
    const { createRequire } = await import('node:module');
    const { chromium } = createRequire(join(root, 'pvad-designer/package.json'))('@playwright/test');
    browser = await chromium.launch();
    const { exerciseConnectedAccounts } = await import('./connected-account-scenarios.mjs');
    proof = await exerciseConnectedAccounts({ browser, urls, control, work, output, step,
      password: 'Pvad-Local-' + randomBytes(24).toString('hex') + '!' });
    const summary = await control('summary');
    assert.equal(summary.customers, 2); assert.equal(summary.templates, 2); assert.equal(summary.published, 2);
    assert.ok(summary.versions >= 2 && summary.codeWorkspaces === 2);
    assert.equal(summary.authoredCodeWorkspaces, 2);
    assert.equal(summary.licenses, 4); assert.equal(summary.renewals, 2); assert.equal(summary.completedRegistrations, 2);
    writeFileSync(join(output, 'report.json'), JSON.stringify({ schemaVersion: 1, syntheticOnly: true, connectedBrowserAndBackend: true,
      realKestrel: true, realPostgres: true, noApiResponseMocks: true, developmentMailTransport: true,
      productionIsolationVerified: false, productionKeysProvisioned: false, cases: proof, database: summary }, null, 2) + '\n');
    succeeded = true;
  } finally {
    await browser?.close();
    if (host?.child.exitCode === null && host.child.signalCode === null) {
      try { await control('stop'); await Promise.race([host.exited, new Promise(yes => setTimeout(yes, 15_000))]); } catch {}
    }
    for (const child of [...children].reverse()) await stop(child);
    for (const stream of streams) await new Promise(yes => stream.end(yes));
    const containers = execFileSync('docker', ['ps', '-aq', '--filter', `label=pvade.account-live=${id}`], { env: environment, encoding: 'utf8' }).trim().split(/\s+/).filter(Boolean);
    for (const container of containers) {
      assert.equal(execFileSync('docker', ['inspect', '--format', '{{index .Config.Labels "pvade.account-live"}}', container], { env: environment, encoding: 'utf8' }).trim(), id);
      execFileSync('docker', ['rm', '-f', '-v', container], { env: environment, stdio: 'pipe' });
    }
    rmSync(work, { recursive: true, force: true }); rmSync(build, { recursive: true, force: true });
    const cleanup = { succeeded, temporaryKeysRemoved: !existsSync(work), isolatedBuildRemoved: !existsSync(build),
      sourceTrustUnchanged: digest(trust) === originalTrust, regularLicensingBuildUnchanged: JSON.stringify(regularSnapshot()) === JSON.stringify(originalRegular),
      ownedContainersRemoved: execFileSync('docker', ['ps', '-aq', '--filter', `label=pvade.account-live=${id}`], { env: environment, encoding: 'utf8' }).trim() === '' };
    writeFileSync(join(output, 'cleanup.json'), JSON.stringify(cleanup, null, 2) + '\n');
    assert.ok(Object.entries(cleanup).filter(([name]) => name !== 'succeeded').every(([, value]) => value === true));
  }
  step('completed: ' + output);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href)
  runConnectedAccountBrowser().catch(error => { console.error(error.message); process.exitCode = 1; });
