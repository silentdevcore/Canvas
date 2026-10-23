import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { randomBytes, createHash } from 'node:crypto';
import { copyFileSync, createWriteStream, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { chromium } from '../../pvad-designer/node_modules/playwright/index.mjs';

if (process.env.PVAD_RUN_ADMIN_CAPTURE !== '1') throw new Error('Set PVAD_RUN_ADMIN_CAPTURE=1 to capture the isolated Admin application.');
const root = fileURLToPath(new URL('../../', import.meta.url));
const output = resolve(root, 'artifacts/rename/admin-capture');
mkdirSync(output, { recursive: true });
const schema = readFileSync(resolve(root, process.env.PVAD_ADMIN_CAPTURE_SCHEMA ?? 'artifacts/rename/readiness-schema.sql'));
const name = `pvad-admin-capture-${randomBytes(5).toString('hex')}`;
const databasePassword = randomBytes(24).toString('hex');
const password = `Aa1!${randomBytes(24).toString('hex')}`;
const email = 'admin-capture@example.test';
const docker = (args, input) => execFileSync('docker', args, { encoding: 'utf8', input, stdio: ['pipe', 'pipe', 'pipe'] }).trim();
const children = [], logs = [];
let database, browser;
function start(command, args, cwd, env, logName) {
  const log = createWriteStream(resolve(output, logName)); logs.push(log);
  const child = spawn(command, args, { cwd: resolve(root, cwd), env: { ...process.env, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
  child.stdout.pipe(log); child.stderr.pipe(log); children.push(child);
  return child;
}
async function waitFor(url, child) {
  for (let i = 0; i < 120; i++) {
    if (child.exitCode !== null) throw new Error('Capture application exited; inspect its local diagnostic log.');
    try { if ((await fetch(url, { signal: AbortSignal.timeout(2000) })).ok) return; } catch { }
    await delay(500);
  }
  throw new Error(`Capture application readiness timed out: ${url}`);
}
try {
  // All ports must be unused; never reuse or shut down an existing application.
  const net = await import('node:net');
  for (const port of [16086, 16177]) await new Promise((yes, no) => {
    const server = net.createServer(); server.once('error', no);
    server.listen(port, '127.0.0.1', () => server.close(yes));
  });
  database = docker(['run', '--detach', '--rm', '--name', name, '-p', '127.0.0.1::5432',
    '-e', `POSTGRES_PASSWORD=${databasePassword}`, 'postgres:17-alpine']);
  let ready = false;
  for (let i = 0; i < 90; i++) {
    try { docker(['exec', database, 'pg_isready', '--host', '127.0.0.1', '-U', 'postgres']); ready = true; break; } catch { await delay(500); }
  }
  assert.ok(ready, 'Isolated PostgreSQL did not become ready.');
  docker(['exec', '-i', database, 'psql', '-U', 'postgres', '-v', 'ON_ERROR_STOP=1'], schema);
  const inspection = JSON.parse(docker(['inspect', database]))[0];
  const port = inspection.NetworkSettings.Ports['5432/tcp'][0].HostPort;
  const api = start('dotnet', ['bin/Debug/net10.0/PVAD.WebApi.dll'], 'PVAD.WebApi', {
    ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:16086',
    ConnectionStrings__PvadDatabase: `Host=127.0.0.1;Port=${port};Database=postgres;Username=postgres;Password=${databasePassword}`,
    PVAD_BOOTSTRAP_ADMIN_EMAIL: email, PVAD_BOOTSTRAP_ADMIN_PASSWORD: password,
    OAuth__Enabled: 'false', Mail__Transport: 'Disabled', Observability__Enabled: 'false',
    DataProtection__KeysDirectory: resolve(output, 'keys'),
    Licensing__PrivateKeyPath: resolve(output, 'license/private.pem'), Licensing__PublicKeyPath: resolve(output, 'license/public.pem'),
    Storage__RootPath: resolve(output, 'objects'),
  }, 'api.log');
  await waitFor('http://127.0.0.1:16086/health/ready', api);
  const ui = start(process.execPath, ['node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', '16177', '--strictPort'],
    'websites/PVAD.Admin', { PVAD_DEV_API_URL: 'http://127.0.0.1:16086' }, 'admin.log');
  await waitFor('http://127.0.0.1:16177/login', ui);
  browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1440, height: 980 }, locale: 'en-US', timezoneId: 'UTC' });
  const page = await context.newPage();
  const failedApiRequests = [];
  page.on('response', response => {
    if (new URL(response.url()).pathname.startsWith('/api/') && response.status() >= 400)
      failedApiRequests.push({ path: new URL(response.url()).pathname, status: response.status() });
  });
  await page.goto('http://127.0.0.1:16177/login');
  await page.locator('[name="identifier"]').fill(email);
  await page.locator('[name="password"]').fill(password);
  await page.locator('#login-form button[type="submit"]').click();
  await page.waitForURL('**/dashboard');
  assert.ok(failedApiRequests.every(item => item.path === '/api/pvad/v1/auth/me' && item.status === 401),
    'Only the expected anonymous session-bootstrap denial is allowed before login.');
  failedApiRequests.length = 0;
  const captures = [];
  const directory = resolve(root, 'PVAD.WebApi/AdminDocumentation/images');
  const stagingDirectory = resolve(output, 'images'); mkdirSync(stagingDirectory, { recursive: true });
  const routes = ['dashboard', 'users', 'organizations', 'roles', 'subscriptions', 'licenses', 'service-accounts', 'mail', 'audit'];
  async function capture(filename) {
    await page.evaluate(() => document.fonts.ready);
    await page.screenshot({ path: resolve(stagingDirectory, filename), fullPage: false,
      mask: [page.getByText(email, { exact: true })], maskColor: '#dbe3ed' });
    captures.push({ filename, sha256: createHash('sha256').update(readFileSync(resolve(stagingDirectory, filename))).digest('hex') });
  }
  for (const route of routes) {
    await page.goto(`http://127.0.0.1:16177/${route}`);
    await page.locator('.admin-page-header h1').waitFor();
    await page.waitForLoadState('networkidle');
    assert.ok(!await page.locator('body').innerText().then(text => /Local PXA Administrator/.test(text)));
    await capture(`${route}.png`);
    if (route === 'users') {
      const link = page.locator('a[href^="/users/"]').first();
      await link.click(); await page.waitForLoadState('networkidle');
      await page.locator('.admin-page-header h1').waitFor(); await capture('user-detail.png');
    }
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('http://127.0.0.1:16177/dashboard');
  await page.locator('.admin-page-header h1').waitFor(); await page.waitForLoadState('networkidle');
  await capture('dashboard-mobile.png');
  assert.deepEqual(failedApiRequests, [], 'Admin captures must not hide failing API requests.');
  for (const capture of captures) copyFileSync(resolve(stagingDirectory, capture.filename), resolve(directory, capture.filename));
  writeFileSync(resolve(output, 'capture-evidence.json'), `${JSON.stringify({ schemaVersion: 1,
    source: 'real-admin-and-api-with-isolated-postgresql', syntheticDataOnly: true, protectedAssetsOnly: true, captures }, null, 2)}\n`);
  console.log(`Captured ${captures.length} protected Admin screenshots using isolated synthetic data.`);
} finally {
  await browser?.close();
  for (const child of children.reverse()) {
    if (child.exitCode !== null) continue;
    child.kill('SIGTERM');
    await Promise.race([new Promise(done => child.once('exit', done)), delay(5000)]);
    if (child.exitCode === null) { child.kill('SIGKILL'); await new Promise(done => child.once('exit', done)); }
  }
  for (const log of logs) log.end();
  if (database) docker(['rm', '--force', '--volumes', database]);
}
