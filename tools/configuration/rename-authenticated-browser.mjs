import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { createHash, randomBytes } from 'node:crypto';
import { createWriteStream, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createServer } from 'node:net';
import { setTimeout as delay } from 'node:timers/promises';

if (process.env.PVAD_RUN_RENAME_BROWSER !== '1' || !process.env.PVAD_RENAME_SCHEMA)
  throw new Error('Explicit opt-in and a current generated PVAD_RENAME_SCHEMA SQL file are required.');

const root = fileURLToPath(new URL('../../', import.meta.url));
const runId = randomBytes(6).toString('hex');
const output = resolve(root, 'artifacts/rename/authenticated-final', runId);
mkdirSync(output, { recursive: true });
writeFileSync(resolve(output, '../latest.json'), `${JSON.stringify({ runId, output: `artifacts/rename/authenticated-final/${runId}` })}\n`);
const schema = readFileSync(resolve(process.env.PVAD_RENAME_SCHEMA));
const nginxProfile = process.env.PVAD_RENAME_NGINX === '1';
let nginxImage;
const hostingChecks = [];
const containers = [], children = [], logs = [];
const docker = (args, input) => execFileSync('docker', args, { input, encoding: 'utf8', timeout: 60_000 }).trim();

async function run(command, args, cwd, env, logName) {
  const log = createWriteStream(resolve(output, logName)); logs.push(log);
  const child = spawn(command, args, { cwd: resolve(root, cwd), env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
  const exited = new Promise(yes => {
    child.once('error', error => yes({ code: 1, signal: null, errorCode: error.code }));
    child.once('exit', (code, signal) => yes({ code, signal }));
  });
  child.stdout.pipe(log); child.stderr.pipe(log);
  children.push({ child, exited });
  return { child, exited };
}
async function waitFor(url, process) {
  for (let attempt = 0; attempt < 120; attempt++) {
    if (process) assert.equal(process.child.exitCode, null, 'A test service exited; inspect its local log.');
    let ready = false;
    try { ready = (await fetch(url, { signal: AbortSignal.timeout(1500) })).ok; } catch { }
    if (ready) {
      await delay(250);
      if (process) {
        assert.equal(process.child.exitCode, null, 'The test service lost its port during startup.');
        assert.equal(process.child.signalCode, null, 'The test service was terminated during startup.');
      }
      return;
    }
    await delay(500);
  }
  throw new Error('Isolated browser service readiness timed out; inspect its local log.');
}
async function freePort(port) {
  for (const host of ['127.0.0.1', '::1'])
    await new Promise((yes, no) => {
      const server = createServer(); server.once('error', no);
      server.listen(port, host, () => server.close(yes));
    });
}

try {
  // Refuse occupied ports rather than reusing applications or customer databases.
  for (const port of [16096, 16176, 16178]) await freePort(port);
  const databasePassword = randomBytes(24).toString('hex');
  const database = docker(['run', '--rm', '-d', '--name', `pvad-browser-db-${runId}`,
    '--label', 'pvade.scope=disposable-final-browser', '-p', '127.0.0.1::5432',
    '-e', `POSTGRES_PASSWORD=${databasePassword}`, 'postgres:17-alpine']);
  containers.push(database);
  let ready = false;
  for (let attempt = 0; attempt < 90; attempt++) {
    try { docker(['exec', database, 'pg_isready', '--host', '127.0.0.1', '-U', 'postgres']); ready = true; break; } catch { await delay(500); }
  }
  assert.ok(ready, 'Isolated PostgreSQL did not become ready.');
  docker(['exec', '-i', database, 'psql', '-U', 'postgres', '-v', 'ON_ERROR_STOP=1'], schema);
  const port = container => JSON.parse(docker(['inspect', container]))[0].NetworkSettings.Ports;
  const databasePort = port(database)['5432/tcp'][0].HostPort;
  const mail = docker(['run', '--rm', '-d', '--name', `pvad-browser-mail-${runId}`,
    '--label', 'pvade.scope=disposable-final-browser', '-p', '127.0.0.1::1025', '-p', '127.0.0.1::8025', 'axllent/mailpit:v1.30.0']);
  containers.push(mail);
  const mailPorts = port(mail);
  const apiEnvironment = {
    ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:16096',
    ConnectionStrings__PvadDatabase: `Host=127.0.0.1;Port=${databasePort};Database=postgres;Username=postgres;Password=${databasePassword}`,
    PVAD_BOOTSTRAP_ADMIN_EMAIL: 'rename-bootstrap@example.test', PVAD_BOOTSTRAP_ADMIN_PASSWORD: `Aa1!${randomBytes(24).toString('hex')}`,
    Observability__Enabled: 'false', Mail__Transport: 'Smtp', Mail__SmtpHost: '127.0.0.1', Mail__SmtpPort: mailPorts['1025/tcp'][0].HostPort,
    Mail__AccountBaseUrl: 'http://localhost:16178', Mail__DesignerBaseUrl: 'http://localhost:16176',
    Registration__AllowedReturnOrigins__20: 'http://localhost:16176', Registration__AllowedReturnOrigins__21: 'http://localhost:16178',
    DesignerAuthentication__AllowedOrigins__20: 'http://localhost:16176',
    CodeWorker__Enabled: 'true', CodeWorker__WorkerPath: resolve(root, 'src/Execution/PVAD.CodeWorker/bin/Debug/net10.0'),
    DataProtection__KeysDirectory: resolve(output, `keys-${runId}`), Storage__RootPath: resolve(output, `objects-${runId}`),
    PdfViewer__AnnotationStoragePath: resolve(output, `viewer-${runId}`),
    PdfTrust__RevocationEvidence__CacheDirectory: resolve(output, `revocation-cache-${runId}`),
    Licensing__PrivateKeyPath: resolve(output, `license-${runId}/private.pem`), Licensing__PublicKeyPath: resolve(output, `license-${runId}/public.pem`),
  };
  const api = await run('dotnet', ['bin/Debug/net10.0/PVAD.WebApi.dll'], 'PVAD.WebApi', apiEnvironment, 'api.log');
  await waitFor('http://127.0.0.1:16096/health/ready', api);
  const env = { PVAD_DEV_API_URL: 'http://127.0.0.1:16096',
    VITE_PVAD_COMPANY_URL: 'http://localhost:5173/', VITE_PVAD_DOCUMENTATION_URL: 'http://localhost:5174/',
    VITE_PVAD_DEMO_URL: 'http://localhost:5175/', VITE_PVAD_DESIGNER_URL: 'http://localhost:16176/', VITE_PVAD_ACCOUNT_URL: 'http://localhost:16178/' };
  for (const [directory, uiPort, name] of [['pvad-designer', 16176, 'designer'], ['websites/PVAD.Account', 16178, 'account']]) {
    const build = await run('npm', ['run', 'build'], directory, env, `${name}-build.log`);
    assert.equal((await build.exited).code, 0, `${name} build failed.`);
    if (nginxProfile) {
      nginxImage = docker(['image', 'inspect', '--format', '{{.Id}}', 'nginx:1.29-alpine']);
      assert.match(nginxImage, /^sha256:[a-f0-9]{64}$/);
      const key = `PVAD_${name.toUpperCase()}_ROUTE_PATTERN=`;
      const pattern = readFileSync(resolve(root, 'deploy/websites/route-patterns.env.example'), 'utf8')
        .split('\n').find(line => line.startsWith(key))?.slice(key.length);
      assert.ok(pattern?.startsWith('^/'));
      const mount = (source, target) => ['--mount', `type=bind,src=${resolve(root, source)},dst=${target},readonly`];
      const proxy = docker(['run', '--rm', '-d', '--name', `pvad-browser-${name}-proxy-${runId}`,
        '--label', 'pvade.scope=disposable-final-browser', '-p', `127.0.0.1:${uiPort}:8080`,
        '-e', 'PVAD_API_UPSTREAM=http://host.docker.internal:16096', '-e', `PVAD_SPA_ROUTE_PATTERN=${pattern}`,
        ...mount(`${directory}/dist`, '/usr/share/nginx/html'),
        ...mount('deploy/websites/nginx.conf.template', '/etc/nginx/templates/default.conf.template'),
        ...(name === 'designer' ? mount('deploy/websites/designer.headers.conf', '/etc/nginx/pvad/designer.headers.conf') : []),
        ...mount('deploy/websites/15-pvad-environment.envsh', '/docker-entrypoint.d/15-pvad-environment.envsh'), nginxImage]);
      containers.push(proxy);
      await waitFor(`http://localhost:${uiPort}/`);
      const entry = name === 'designer' ? '/pdf/create' : '/login';
      const response = await fetch(`http://localhost:${uiPort}${entry}`);
      hostingChecks.push({ application: name, path: entry, status: response.status });
      assert.equal(response.status, 200);
      if (name === 'designer') {
        const headers = readFileSync(resolve(root, 'deploy/websites/designer.headers.conf'), 'utf8');
        for (const header of ['Content-Security-Policy', 'Content-Security-Policy-Report-Only'])
          assert.equal(response.headers.get(header), headers.match(new RegExp(`add_header ${header} "([^"]+)" always;`))?.[1]);
      }
      const apiProbe = await fetch(`http://localhost:${uiPort}/api/pvad/v1/version`);
      assert.equal(apiProbe.status, 200, 'The isolated Nginx proxy must reach the real local API.');
      if (name === 'account') {
        for (const path of ['/registration-pending', '/accept-invitation', '/confirm-email', '/forgot-password', '/reset-password', '/legal-updates', '/legal-review', '/not-an-account-route']) {
          const status = (await fetch(`http://localhost:${uiPort}${path}`)).status;
          hostingChecks.push({ application: name, path, status });
          assert.equal(status, path === '/not-an-account-route' ? 404 : 200, `Account direct route ${path} must preserve its HTTP status.`);
        }
      } else {
        const workers = readdirSync(resolve(root, `${directory}/dist/assets`)).filter(file => file.endsWith('.mjs'));
        assert.ok(workers.length > 0, 'The built PDF module worker must exist.');
        for (const worker of workers) {
          const response = await fetch(`http://localhost:${uiPort}/assets/${worker}`);
          assert.equal(response.status, 200);
          assert.match(response.headers.get('content-type') ?? '', /(?:application|text)\/javascript/);
        }
      }
      continue;
    }
    const ui = await run(process.execPath, [resolve(root, 'pvad-designer/node_modules/vite/bin/vite.js'), 'preview', '--host', 'localhost', '--port', String(uiPort), '--strictPort'], directory, env, `${name}.log`);
    await waitFor(`http://localhost:${uiPort}/`, ui);
  }
  const test = await run(process.execPath, ['node_modules/@playwright/test/cli.js', 'test',
    'e2e/rename-account-designer.spec.ts', 'e2e/account-designer-flow.spec.ts', '--project=chrome-desktop',
    '--reporter=line', `--output=../artifacts/rename/authenticated-final/${runId}/playwright`], 'pvad-designer', {
    PVAD_SMOKE_MAILPIT_URL: `http://127.0.0.1:${mailPorts['8025/tcp'][0].HostPort}`,
    PVAD_SMOKE_DESIGNER_URL: 'http://localhost:16176', PVAD_SMOKE_ACCOUNT_URL: 'http://localhost:16178',
    PVAD_SMOKE_CHROMIUM_CHANNEL: 'chromium',
  }, 'browser.log');
  const result = await test.exited;
  const recovery = { status: 'not-run', scope: 'Local API stop/start only; not a release rollback.', checks: [] };
  if (result.code === 0 && nginxProfile) {
    try {
      const snapshot = () => Object.fromEntries(['designer_templates', 'designer_template_versions'].map(table => [table,
        docker(['exec', database, 'psql', '-U', 'postgres', '-Atc',
          `SELECT COUNT(*)::text || ':' || COALESCE(MD5(string_agg(row_to_json(t)::text, '' ORDER BY t."Id")), MD5('')) FROM designer.${table} AS t;`])]));
      const before = snapshot();
      assert.ok(Number(before.designer_templates.split(':')[0]) > 0, 'Recovery must check real saved templates, not an empty database.');
      for (const uiPort of [16176, 16178]) {
        const denied = await fetch(`http://localhost:${uiPort}/api/pvad/v1/auth/me`, { headers: { 'X-PVAD-Application': 'designer' } });
        assert.equal(denied.status, 401);
        const cors = await fetch(`http://localhost:${uiPort}/api/pvad/v1/auth/me`, { method: 'OPTIONS',
          headers: { Origin: 'https://untrusted.example.invalid', 'Access-Control-Request-Method': 'POST' } });
        assert.equal(cors.headers.get('access-control-allow-origin'), null);
        recovery.checks.push({ port: uiPort, anonymousSessionStatus: denied.status, untrustedCorsGranted: false });
      }
      // Only this run's API is stopped; its database, keyring and proxies stay intact.
      api.child.kill('SIGTERM');
      await Promise.race([api.exited, delay(5000)]);
      if (api.child.exitCode === null && api.child.signalCode === null) api.child.kill('SIGKILL');
      await api.exited;
      for (const uiPort of [16176, 16178]) {
        const unavailable = await fetch(`http://localhost:${uiPort}/api/pvad/v1/version`);
        assert.equal(unavailable.status, 503);
        assert.match(unavailable.headers.get('content-type') ?? '', /application\/problem\+json/);
        assert.equal((await unavailable.json()).canonicalCode, 'PVADAPI018');
        recovery.checks.push({ port: uiPort, apiStoppedStatus: 503, problemDiagnostic: 'PVADAPI018' });
      }
      const restarted = await run('dotnet', ['bin/Debug/net10.0/PVAD.WebApi.dll'], 'PVAD.WebApi', apiEnvironment, 'api-restarted.log');
      await waitFor('http://127.0.0.1:16096/health/ready', restarted);
      for (const uiPort of [16176, 16178]) {
        for (const path of ['/api/pvad/v1/version', '/api/pxa/v1/version'])
          assert.equal((await fetch(`http://localhost:${uiPort}${path}`)).status, 200);
        recovery.checks.push({ port: uiPort, canonicalAndLegacyRecovered: true });
      }
      const after = snapshot();
      assert.deepEqual(after, before, 'Saved templates and immutable versions must survive an API restart unchanged.');
      recovery.templateSnapshots = { before, after };
      recovery.status = 'passed';
    } catch (error) { recovery.status = 'failed'; recovery.failure = error.message; }
  }
  writeFileSync(resolve(output, 'report.json'), `${JSON.stringify({ sourceCommit: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
    uncommittedTrackedChanges: execFileSync('git', ['diff', '--name-only', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim().length > 0,
    syntheticDataOnly: true, designerHosting: nginxProfile ? 'exact-repository-nginx-csp-profile' : 'vite-preview',
    accountHosting: nginxProfile ? 'exact-repository-nginx-template' : 'vite-preview', nginxImage,
    hostingChecks,
    recovery,
    sourceFiles: Object.fromEntries(['tools/configuration/rename-authenticated-browser.mjs', 'deploy/websites/route-patterns.env.example', 'deploy/websites/nginx.conf.template']
      .map(path => [path, createHash('sha256').update(readFileSync(resolve(root, path))).digest('hex')])),
    externalStagingAcceptance: false,
    scope: 'Three real authenticated API/PostgreSQL/SMTP cases including local fonts/images, binding preservation, PDF code/report migrations, preview, artifact downloads and Viewer handoff; two separate mocked denial/layout cases. Not public-host, binding substitution, every-provider or native OS print certification.',
    status: result.code === 0 && recovery.status !== 'failed' ? 'passed' : 'failed', exitCode: result.code }, null, 2)}\n`);
  assert.equal(result.code, 0, 'Authenticated browser tests failed; inspect browser.log.');
  assert.notEqual(recovery.status, 'failed', 'Local API recovery failed; inspect report.json.');
  console.log('Isolated authenticated browser tests passed.');
} finally {
  for (const { child, exited } of children.reverse()) {
    if (child.exitCode !== null || child.signalCode !== null) continue;
    child.kill('SIGTERM');
    await Promise.race([exited, delay(5000)]);
    if (child.exitCode === null && child.signalCode === null) { child.kill('SIGKILL'); await exited; }
  }
  for (const log of logs) log.end();
  for (const container of containers.reverse()) docker(['rm', '-f', '-v', container]);
}
