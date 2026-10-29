import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { chromium } from '../../pvad-designer/node_modules/playwright/index.mjs';
import { createRequire } from 'node:module';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = new URL('../../artifacts/rename/native-project-production/', import.meta.url);
mkdirSync(output, { recursive: true });
const label = randomUUID(), name = `pvad-native-host-${label}`;
const docker = args => execFileSync('docker', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], timeout: 60_000 }).trim();
const image = docker(['image', 'inspect', '--format', '{{.Id}}', 'nginx:1.29-alpine']);
assert.match(image, /^sha256:[a-f0-9]{64}$/);
const key = 'PVAD_DESIGNER_ROUTE_PATTERN=';
const pattern = readFileSync(new URL('../../deploy/websites/route-patterns.env.example', import.meta.url), 'utf8')
  .split('\n').find(line => line.startsWith(key))?.slice(key.length);
assert.ok(pattern?.includes('open-project'));
const mount = (source, target) => ['--mount', `type=bind,src=${root}${source},dst=${target},readonly`];
let container, browser;
try {
  container = docker(['run', '--rm', '-d', '--name', name, '--label', `pvade.native-smoke=${label}`, '-p', '127.0.0.1::8080',
    '-e', 'PVAD_API_UPSTREAM=http://127.0.0.1:9/', '-e', `PVAD_SPA_ROUTE_PATTERN=${pattern}`,
    ...mount('pvad-designer/dist', '/usr/share/nginx/html'),
    ...mount('deploy/websites/nginx.conf.template', '/etc/nginx/templates/default.conf.template'),
    ...mount('deploy/websites/designer.headers.conf', '/etc/nginx/pvad/designer.headers.conf'),
    ...mount('deploy/websites/15-pvad-environment.envsh', '/docker-entrypoint.d/15-pvad-environment.envsh'), image]);
  const port = JSON.parse(docker(['inspect', container]))[0].NetworkSettings.Ports['8080/tcp'][0].HostPort;
  const origin = `http://127.0.0.1:${port}`;
  let response;
  for (let attempt = 0; attempt < 50; attempt++) {
    try { response = await fetch(`${origin}/open-project`, { signal: AbortSignal.timeout(1000) }); if (response.ok) break; } catch { /* Starting isolated proxy. */ }
    await delay(100);
  }
  assert.equal(response?.status, 200);
  const cspPresent = response.headers.has('content-security-policy');
  assert.equal(cspPresent, true);
  assert.match(response.headers.get('content-security-policy'), /frame-ancestors 'none'/);
  assert.match(response.headers.get('content-security-policy-report-only'), /worker-src 'self' blob:/);
  assert.match(response.headers.get('cache-control'), /no-store/);
  const unknown = await fetch(`${origin}/definitely-not-a-designer-route`);
  assert.equal(unknown.status, 404);
  assert.equal(unknown.headers.get('content-security-policy'), response.headers.get('content-security-policy'));
  const manifestResponse = await fetch(`${origin}/manifest.webmanifest`);
  assert.equal(manifestResponse.status, 200);
  const manifest = await manifestResponse.json();
  assert.equal(manifest.name, 'PVADE Designer');
  assert.equal(manifest.file_handlers[0].action, '/open-project');
  for (const icon of manifest.icons) assert.equal((await fetch(new URL(icon.src, origin))).status, 200);
  const worker = await fetch(`${origin}/native-project-worker.js`);
  assert.equal(worker.status, 200);
  assert.match(worker.headers.get('content-type'), /javascript/);
  const api = await fetch(`${origin}/api/pvad/v1/version`);
  assert.equal(api.status, 503);
  assert.match(api.headers.get('content-type'), /application\/problem\+json/);
  assert.equal(api.headers.get('content-security-policy'), response.headers.get('content-security-policy'));
  browser = await chromium.launch();
  const page = await browser.newPage(), errors = [];
  await page.addInitScript(() => {
    window.__syntheticCspViolations = [];
    document.addEventListener('securitypolicyviolation', event => {
      window.__syntheticCspViolations.push({ directive: event.effectiveDirective, disposition: event.disposition });
    });
  });
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${origin}/open-project`);
  await page.getByRole('heading', { name: 'Open project', exact: true }).waitFor();
  await page.evaluate(() => Promise.race([
    navigator.serviceWorker.ready.then(() => true),
    new Promise((_, reject) => setTimeout(() => reject(new Error('Service worker readiness timed out.')), 15_000)),
  ]));
  const cdp = await page.context().newCDPSession(page);
  const parsed = await cdp.send('Page.getAppManifest');
  assert.equal(parsed.errors.filter(error => error.critical).length, 0);
  assert.equal(JSON.parse(parsed.data).name, 'PVADE Designer');
  const installability = await cdp.send('Page.getInstallabilityErrors');
  assert.deepEqual(errors, []);
  const csp = await page.evaluate(async () => {
    const events = [];
    const listener = event => events.push({ directive: event.effectiveDirective, disposition: event.disposition });
    document.addEventListener('securitypolicyviolation', listener);
    const original = document.baseURI;
    const base = document.createElement('base'); base.href = 'https://blocked.example.invalid/'; document.head.append(base);
    const baseBlocked = document.baseURI === original;
    base.remove();
    const script = document.createElement('script'); script.textContent = 'window.__syntheticCspProbe = 7'; document.head.append(script);
    await new Promise(resolve => setTimeout(resolve, 100));
    script.remove(); document.removeEventListener('securitypolicyviolation', listener);
    return { baseBlocked, inlineProbeExecuted: window.__syntheticCspProbe === 7, events };
  });
  assert.equal(csp.baseBlocked, true);
  assert.equal(csp.inlineProbeExecuted, true);
  assert.ok(csp.events.some(event => event.directive === 'base-uri' && event.disposition === 'enforce'));
  assert.ok(csp.events.some(event => event.directive === 'script-src-elem' && event.disposition === 'report'));
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const { PDFDocument } = require('pdf-lib');
  const pdf = await PDFDocument.create(); pdf.addPage([320, 200]).drawText('Synthetic CSP PDF');
  const bytes = Array.from(await pdf.save());
  const preview = await page.evaluate(async () => {
    const url = URL.createObjectURL(new Blob(['<!doctype html><p>Synthetic preview frame</p>'], { type: 'text/html' }));
    const frame = document.createElement('iframe'); frame.title = 'Synthetic CSP blob frame';
    frame.style.width = '320px'; frame.style.height = '200px';
    const loaded = new Promise((resolve, reject) => {
      frame.onload = () => resolve(true);
      setTimeout(() => reject(new Error('Synthetic blob frame timed out.')), 10_000);
    });
    frame.src = url; document.querySelector('main').append(frame);
    try { await loaded; return true; } finally { frame.remove(); URL.revokeObjectURL(url); }
  });
  assert.equal(preview, true);
  const workerResult = await page.evaluate(async () => {
    const url = URL.createObjectURL(new Blob(['onmessage = () => postMessage(7)'], { type: 'text/javascript' }));
    const worker = new Worker(url);
    try {
      return await new Promise((resolve, reject) => {
        worker.onmessage = event => resolve(event.data); worker.onerror = () => reject(new Error('Synthetic worker failed.'));
        setTimeout(() => reject(new Error('Synthetic worker timed out.')), 10_000); worker.postMessage(1);
      });
    } finally { worker.terminate(); URL.revokeObjectURL(url); }
  });
  assert.equal(workerResult, 7);
  const downloadEvent = page.waitForEvent('download');
  await page.evaluate(bytes => {
    const url = URL.createObjectURL(new Blob([new Uint8Array(bytes)], { type: 'application/pdf' }));
    const link = document.createElement('a'); link.href = url; link.download = 'synthetic-csp.pdf'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }, bytes);
  const download = await downloadEvent;
  assert.equal(await download.failure(), null);
  assert.equal(readFileSync(await download.path()).subarray(0, 5).toString(), '%PDF-');
  execFileSync(process.execPath, ['tools/configuration/native-project-import-browser-smoke.mjs'], {
    cwd: root, env: { ...process.env, PVAD_DESIGNER_SMOKE_ORIGIN: origin }, stdio: 'pipe', timeout: 120_000,
  });
  await page.screenshot({ path: fileURLToPath(new URL('intake.png', output)), fullPage: true });
  const report = { status: 'passed', scope: 'Built Designer behind isolated Nginx; no live API, TLS, OS installation or association changes.',
    spaRoute: 200, unknownRoute: 404, unavailableApi: 503, manifestAndIconsServed: true, workerReady: true,
    manifestCriticalErrors: 0, installabilityErrors: installability.installabilityErrors,
    publicCspPresent: cspPresent, cspBaselineEnforced: true, resourcePolicyReportOnly: true,
    cspProbes: csp, importUnderBaselinePassed: true, syntheticHtmlBlobFrameLoaded: preview,
    blobWorkerPassed: true, pdfDownloadSignaturePassed: true, nativePdfViewerAndPrint: 'unverified',
    publicCspApproval: 'unverified', osAssociations: 'unverified' };
  writeFileSync(new URL('report.json', output), `${JSON.stringify(report, null, 2)}\n`);
  console.log(JSON.stringify(report));
} finally {
  await browser?.close();
  if (container) {
    const owner = docker(['inspect', '--format', '{{index .Config.Labels "pvade.native-smoke"}}', container]);
    if (owner === label) docker(['rm', '-f', container]);
    else throw new Error('Cannot confirm synthetic proxy ownership for cleanup.');
  }
}
