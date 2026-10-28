import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const root = process.env.PXA_DESIGNER_SMOKE_ROOT ?? fileURLToPath(new URL('../../pxa-designer/', import.meta.url));
const output = process.argv[2]
  ? pathToFileURL(`${resolve(process.argv[2])}/`)
  : new URL('../../artifacts/designer-tooling-smoke/', import.meta.url);
const baselineOnly = process.argv.includes('--baseline-only');
const require = createRequire(new URL('../../pxa-designer/package.json', import.meta.url));
const { chromium, expect } = require('@playwright/test');
const { createServer, preview } = await import(pathToFileURL(require.resolve('vite')).href);
const user = {
  id: 'tooling-user', username: 'designer@example.test', email: 'designer@example.test',
  displayName: 'Designer Tooling Test', roles: ['Editor'], apiVersion: '1',
  organizations: [{ id: 'tooling-org', name: 'Synthetic Workspace', slug: 'tooling' }],
  activeOrganizationId: 'tooling-org', lastLoginAt: null,
};
const responses = new Map([
  ['/api/pxa/v1/auth/me', user],
  ['/api/pxa/v1/auth/csrf', { token: 'synthetic-tooling-csrf' }],
  ['/api/pxa/v1/designer/features', []],
  ['/api/pxa/v1/designer/releases', { releases: [], readVersions: [] }],
  ['/api/pxa/v1/designer/notifications', { items: [], page: 1, pageSize: 50, total: 0 }],
  ['/api/pxa/v1/designer/notifications/unread-count', { unread: 0, persistent: 0, releases: 0 }],
  ['/api/pxa/v1/designer/templates', { items: [], page: 1, pageSize: 100, total: 0 }],
]);
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const results = [];
try {
  for (const mode of ['development', 'preview']) {
    const options = { root, logLevel: 'warn' };
    const server = mode === 'development'
      ? await createServer({ ...options, server: { host: '127.0.0.1', port: 0, strictPort: true, hmr: false } })
      : await preview({ ...options, preview: { host: '127.0.0.1', port: 0, strictPort: true } });
    try {
      if (mode === 'development') await server.listen();
      const origin = new URL(server.resolvedUrls.local[0]).origin;
      for (const [device, viewport] of Object.entries({
        desktop: { width: 1440, height: 1000 }, mobile: { width: 390, height: 844 },
      })) {
        const context = await browser.newContext({ viewport, locale: 'en-US', serviceWorkers: 'block' });
        const errors = [];
        const abortedRequests = [];
        const telemetryOutcomes = [];
        try {
          // Synthetic API fixtures exercise the frontend bundle, not authentication or backend integration.
          await context.route('**/*', async route => {
            const request = route.request();
            const url = new URL(request.url());
            if (url.origin !== origin) {
              if (url.hostname === 'fonts.googleapis.com') {
                await route.fulfill({ status: 200, contentType: 'text/css', body: '' });
              } else {
                errors.push(`Unexpected external request: ${url.origin}`);
                await route.abort();
              }
              return;
            }
            if (url.pathname.startsWith('/api/')) {
              if (url.pathname === '/api/pxa/v1/telemetry/browser' && request.method() === 'POST') {
                for (const event of request.postDataJSON()?.events ?? []) {
                  telemetryOutcomes.push(`${event.type}/${event.outcome}`);
                  if (['error', 'unhandled_rejection'].includes(event.type)) {
                    errors.push(`Browser telemetry failure: ${event.type}/${event.outcome}`);
                  }
                }
                await route.fulfill({ status: 204 });
                return;
              }
              const body = responses.get(url.pathname);
              if (request.method() !== 'GET' || body === undefined) {
                errors.push(`Unexpected API operation: ${request.method()} ${url.pathname}`);
                await route.fulfill({ status: 500, json: { title: 'Unconfigured synthetic API operation' } });
              } else {
                await route.fulfill({ status: 200, json: body });
              }
              return;
            }
            await route.continue();
          });
          const page = await context.newPage();
          page.on('pageerror', error => errors.push(error.message));
          page.on('requestfailed', request => {
            const failure = request.failure()?.errorText;
            if (failure === 'net::ERR_ABORTED') {
              abortedRequests.push(new URL(request.url()).pathname);
            } else {
              errors.push(`Request failed: ${new URL(request.url()).pathname}: ${failure}`);
            }
          });
          page.on('response', response => {
            if (response.status() >= 400 && ['script', 'stylesheet'].includes(response.request().resourceType())) {
              errors.push(`Asset HTTP ${response.status()}: ${new URL(response.url()).pathname}`);
            }
          });
          const home = await page.goto(origin, { waitUntil: 'networkidle' });
          assert.equal(home.status(), 200);
          await expect(page.locator('.pdf-home')).toBeVisible();
          await expect(page.locator('.pdf-hero h1')).toBeVisible();
          await expect(page.locator('.pdf-tool-card svg').first()).toBeVisible();
          assert.ok(await page.locator('.pdf-tool-card').count() >= 10);
          assert.notEqual(await page.locator('.pdf-hero').evaluate(element => getComputedStyle(element).display), 'none');
          await page.screenshot({ path: fileURLToPath(new URL(`${mode}-${device}-home.png`, output)) });

          const gallery = await page.goto(`${origin}/pdf/template`, { waitUntil: 'networkidle' });
          assert.equal(gallery.status(), 200, 'SPA deep links must return the application');
          await expect(page.locator('.tpl-toolbar-heading')).toHaveText('Templates');
          if (device === 'mobile') {
            const toggle = page.locator('.hub-sidebar-toggle');
            if (await toggle.getAttribute('aria-expanded') === 'true') await toggle.click();
            await expect(toggle).toHaveAttribute('aria-expanded', 'false');
          }
          await expect(page.locator('.pdf-template-grid')).toBeVisible();
          await page.locator('.pdf-search input').fill('no-such-tooling-template');
          await expect(page.locator('.pdf-empty-results')).toBeVisible();
          await page.locator('.pdf-empty-results button').click();
          await expect(page.locator('.pdf-template-grid')).toBeVisible();
          await page.mouse.move(0, 0);
          await expect(page.locator('.pdf-template-card').first().locator('..')).toHaveCSS('opacity', '1');
          await page.waitForFunction(() => [...document.querySelectorAll('.pdf-template-card')].every(card => {
            const style = getComputedStyle(card.parentElement);
            return style.opacity === '1' && (style.transform === 'none' || new DOMMatrixReadOnly(style.transform).m42 === 0);
          }));
          await page.evaluate(() => {
            window.scrollTo(0, 0);
            document.querySelector('.hub-layout-content')?.scrollTo(0, 0);
          });
          await page.screenshot({ path: fileURLToPath(new URL(`${mode}-${device}-gallery.png`, output)) });

          if (!baselineOnly) {
            const spreadsheet = await page.goto(`${origin}/spreadsheet/create`, { waitUntil: 'networkidle' });
            assert.equal(spreadsheet.status(), 200);
            if (device === 'mobile') {
              const toggle = page.locator('.hub-sidebar-toggle');
              if (await toggle.getAttribute('aria-expanded') === 'true') await toggle.click();
              await expect(toggle).toHaveAttribute('aria-expanded', 'false');
            }
            const grid = page.locator('.dvn-scroller');
            await expect(grid).toBeVisible();
            await grid.click({ position: { x: 80, y: 50 } });
            await page.locator('[data-testid="data-grid-canvas"]').press('Enter');
            const cellEditor = page.locator('.gdg-input');
            await expect(cellEditor).toBeVisible();
            await cellEditor.fill('17');
            await cellEditor.press('Enter');
            await grid.dblclick({ position: { x: 80, y: 50 } });
            await expect(cellEditor).toHaveValue('17');
            await cellEditor.press('Escape');
          }
        } finally {
          await context.close();
        }
        assert.deepEqual(errors, [], `${mode}/${device} must have no runtime or asset failures`);
        results.push({ mode, device, home: true, gallery: true, search: true, spreadsheetInput: !baselineOnly, errors, abortedRequests, telemetryOutcomes });
        console.log(`PASS Designer tooling: ${mode}/${device}, home, SPA gallery, search${baselineOnly ? '' : ' and actual Spreadsheet input'}`);
      }
    } finally {
      await server.close();
    }
  }
} finally {
  await browser.close();
  await writeFile(new URL('report.json', output), `${JSON.stringify({
    scope: 'Frontend tooling only; API responses and external font CSS are synthetic.', results,
  }, null, 2)}\n`);
}
