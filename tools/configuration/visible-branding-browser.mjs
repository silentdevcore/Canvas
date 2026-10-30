import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
const { chromium, expect } = require('@playwright/test');
const { preview } = await import(pathToFileURL(require.resolve('vite')).href);
const releases = JSON.parse(await readFile(resolve(root, 'product-metadata/pvad-releases.json'), 'utf8')).releases;
const output = resolve(root, 'artifacts/rename/visible-branding');
const user = { id: 'branding-user', email: 'synthetic@example.invalid', username: 'synthetic',
  displayName: 'Synthetic Reviewer', roles: ['System Administrator'], apiVersion: '1',
  permissions: ['designer.access', 'designer.pdf.access', 'designer.pdf.templates.read'],
  organizations: [{ id: 'branding-org', name: 'Synthetic Workspace', slug: 'synthetic' }],
  activeOrganizationId: 'branding-org', lastLoginAt: null };
const results = [];
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
try {
  for (const app of ['Company', 'Documentation', 'Demo', 'Admin', 'Account', 'Designer']) {
    const appRoot = resolve(root, app === 'Designer' ? 'pvad-designer' : `websites/PVAD.${app}`);
    const server = await preview({ root: appRoot, logLevel: 'error', preview: { host: '127.0.0.1', port: 0, strictPort: true } });
    try {
      const origin = new URL(server.resolvedUrls.local[0]).origin;
      for (const [device, viewport] of Object.entries({ desktop: { width: 1440, height: 1000 }, mobile: { width: 390, height: 844 } })) {
        const context = await browser.newContext({ viewport, serviceWorkers: 'block' });
        const errors = [];
        let denyDesigner = false;
        try {
          await context.route('**/*', async route => {
            const url = new URL(route.request().url());
            if (url.origin !== origin) return route.fulfill({ status: 200, contentType: 'text/css', body: '' });
            if (!url.pathname.startsWith('/api/')) return route.continue();
            if (url.pathname.endsWith('/auth/me')) return route.fulfill(denyDesigner
              ? { status: 403, json: { code: 'PXA_TRIAL_EXPIRED', detail: 'The evaluation has expired.' } }
              : { status: 200, json: user });
            if (url.pathname.endsWith('/auth/csrf')) return route.fulfill({ json: { token: 'synthetic-csrf' } });
            if (url.pathname.endsWith('/designer/releases')) return route.fulfill({ json: { releases, readVersions: releases.map(item => item.version) } });
            if (url.pathname.endsWith('/designer/features')) return route.fulfill({ json: [] });
            if (url.pathname.endsWith('/unread-count')) return route.fulfill({ json: { unread: 0, persistent: 0, releases: 0 } });
            if (url.pathname.endsWith('/notifications')) return route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 50 } });
            if (url.pathname.endsWith('/telemetry/browser')) return route.fulfill({ status: 204 });
            return route.fulfill({ status: 200, json: {} });
          });
          const page = await context.newPage();
          page.on('pageerror', error => errors.push(error.message));
          const path = { Company: '/about.html', Documentation: '/#release-notes-1-0-0', Demo: '/', Admin: '/release-notes', Account: '/not-a-page', Designer: '/' }[app];
          await page.goto(origin + path, { waitUntil: 'networkidle' });
          if (app === 'Company') {
            await expect(page.locator('#rename-history')).toContainText('PXA has been renamed to PVAD');
            assert.equal((await page.locator('body').innerText()).match(/PXA/g)?.length, 1);
          } else {
            await expect(page.locator('body')).not.toContainText('PXA');
          }
          if (app === 'Documentation') await expect(page.locator('#release-notes-1-0-0')).toContainText('PVADE');
          if (app === 'Admin') await expect(page.locator('.admin-release').first()).toBeVisible();
          await page.screenshot({ path: resolve(output, `${app}-${device}.png`), fullPage: true });
          if (app === 'Designer') {
            await expect(page.locator('.pdf-home')).toBeVisible();
            if (device === 'desktop') {
              await page.getByRole('button', { name: /Open user menu/ }).click();
              await expect(page.getByRole('button', { name: 'About PVADE' })).toBeVisible();
              await page.getByRole('button', { name: "What's New" }).click();
              await expect(page.locator('.pvad-release-notes')).toBeVisible();
              await expect(page.locator('.pvad-product-panel')).not.toContainText('PXA');
              await page.screenshot({ path: resolve(output, 'Designer-desktop-releases.png'), fullPage: true });
            }
            denyDesigner = true;
            for (const language of ['en', 'de', 'fr', 'es', 'it', 'ar']) {
              await page.evaluate(language => localStorage.setItem('pvad_locale', language), language);
              await page.reload({ waitUntil: 'networkidle' });
              await expect(page.locator('.designer-status-page code')).toHaveText('PVAD_TRIAL_EXPIRED');
              assert.equal(await page.locator('.pvad-status-page__mark').evaluate(element => element.scrollWidth <= element.clientWidth), true);
              await expect(page.locator('body')).not.toContainText('PXA');
              await page.screenshot({ path: resolve(output, `Designer-${device}-${language}-denied.png`), fullPage: true });
            }
          }
          assert.deepEqual(errors, []);
          results.push({ app, device, passed: true });
        } finally { await context.close(); }
      }
    } finally { await server.httpServer.close(); }
  }
} finally {
  await browser.close();
  await writeFile(resolve(output, 'report.json'), JSON.stringify({ scope: 'Production frontend bundles with synthetic API responses, not real authentication or backend acceptance.', results }, null, 2) + '\n');
}
console.log(`Visible branding passed: ${results.length} desktop/mobile application cases plus 12 localized Designer denial cases.`);
