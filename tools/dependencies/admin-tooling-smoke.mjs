import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFile } from 'node:child_process';
import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { promisify } from 'node:util';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(process.env.PVAD_ADMIN_SMOKE_ROOT || fileURLToPath(new URL('../../websites/PVAD.Admin/', import.meta.url)));
const output = resolve(process.argv[2] || fileURLToPath(new URL('../../artifacts/admin-tooling-smoke/current/', import.meta.url)));
const adminRequire = createRequire(pathToFileURL(resolve(root, 'package.json')));
const designerRequire = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
const { chromium, expect } = designerRequire('@playwright/test');
const { createServer, preview } = await import(pathToFileURL(adminRequire.resolve('vite')).href);
const user = {
  id: 'synthetic-admin', username: 'admin@example.test', email: 'admin@example.test',
  displayName: 'Synthetic Administrator', roles: ['System Administrator', 'Organization Administrator'],
  permissions: ['users.read', 'audit.read'], apiVersion: '1',
  organizations: [{ id: 'synthetic-org', name: 'Synthetic Workspace', slug: 'synthetic' }],
  activeOrganizationId: 'synthetic-org', lastLoginAt: '2026-07-26T10:30:00Z',
};
const listedUser = {
  id: 'synthetic-member', username: 'member@example.test', email: 'member@example.test',
  displayName: 'Synthetic Member', membershipId: 'synthetic-membership', roles: ['Editor'],
  isActive: true, membershipStatus: 'Active', lastLoginAt: null,
};
const manifest = JSON.parse(await readFile(new URL('../../product-metadata/pvad-releases.json', import.meta.url)));
const hash = data => createHash('sha256').update(data).digest('hex');
await mkdir(output, { recursive: true });
const results = [];
const browser = await chromium.launch({ headless: true });
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
        desktop: { width: 1440, height: 980 }, mobile: { width: 390, height: 844 },
      })) {
        const context = await browser.newContext({ viewport, locale: 'en-US', timezoneId: 'UTC', serviceWorkers: 'block' });
        const errors = [];
        const searches = [];
        const screenshots = {};
        let authenticated = false;
        let allowLogin = false;
        let currentRoles = user.roles;
        try {
          // Exercise real bundles with synthetic transport, not server authentication.
          await context.route('**/*', async route => {
            const request = route.request();
            const url = new URL(request.url());
            if (url.origin !== origin) {
              if (url.hostname === 'fonts.googleapis.com') await route.fulfill({ status: 200, contentType: 'text/css', body: '' });
              else { errors.push(`Unexpected external request: ${url.origin}`); await route.abort(); }
              return;
            }
            if (!url.pathname.startsWith('/api/')) return route.continue();
            const json = (body, status = 200) => route.fulfill({ status, json: body });
            if (url.pathname === '/api/pvad/v1/telemetry/browser' && request.method() === 'POST') {
              for (const event of request.postDataJSON()?.events ?? []) {
                if (['error', 'unhandled_rejection'].includes(event.type)) errors.push(`Telemetry: ${event.type}/${event.outcome}`);
              }
              return route.fulfill({ status: 204 });
            }
            if (url.pathname === '/api/pvad/v1/auth/me' && request.method() === 'GET') {
              return json(authenticated ? { ...user, roles: currentRoles } : { title: 'Authentication required' }, authenticated ? 200 : 401);
            }
            if (url.pathname === '/api/pvad/v1/auth/csrf' && request.method() === 'GET') return json({ token: 'synthetic-csrf' });
            if (url.pathname === '/api/pvad/v1/auth/login' && request.method() === 'POST') {
              assert.equal(request.headers()['x-pvad-csrf'], 'synthetic-csrf');
              if (!allowLogin) return json({ title: 'Synthetic login rejected' }, 401);
              authenticated = true;
              return json({ user });
            }
            if (authenticated && url.pathname === '/api/pvad/v1/admin/subscriptions' && request.method() === 'GET') return json({ items: [] });
            if (authenticated && url.pathname === '/api/pvad/v1/admin/users' && request.method() === 'GET') {
              const search = url.searchParams.get('search') || '';
              searches.push(search);
              const items = search === 'no-such-member' ? [] : [listedUser];
              return json({ items, total: items.length, page: 1, pageSize: 25 });
            }
            errors.push(`Unexpected API operation: ${request.method()} ${url.pathname}`);
            return json({ title: 'Unconfigured synthetic operation' }, 500);
          });
          const page = await context.newPage();
          page.on('pageerror', error => errors.push(error.message));
          page.on('requestfailed', request => {
            if (request.failure()?.errorText !== 'net::ERR_ABORTED') errors.push(`Request failed: ${new URL(request.url()).pathname}`);
          });
          page.on('response', response => {
            if (response.status() >= 400 && ['script', 'stylesheet'].includes(response.request().resourceType())) {
              errors.push(`Asset HTTP ${response.status()}: ${new URL(response.url()).pathname}`);
            }
          });
          const capture = async name => {
            await page.evaluate(() => document.fonts.ready);
            await page.evaluate(async () => {
              history.scrollRestoration = 'manual';
              window.scrollTo({ top: 0, left: 0, behavior: 'instant' });
              await new Promise(accept => requestAnimationFrame(() => requestAnimationFrame(accept)));
            });
            assert.equal(await page.evaluate(() => window.scrollY), 0, 'Screenshot scroll must be deterministic');
            assert.ok(await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth <= 1), `${name} must not overflow the viewport`);
            const png = await page.screenshot({ path: resolve(output, `${mode}-${device}-${name}.png`), animations: 'disabled', caret: 'hide' });
            screenshots[name] = hash(png);
          };
          const navigate = async path => {
            const menu = page.locator('#menu-button');
            if (await menu.isVisible() && await menu.getAttribute('aria-expanded') === 'false') await menu.click();
            await page.locator(`#admin-sidebar a[href="${path}"]`).click();
            await expect(page.locator(`#admin-sidebar a[href="${path}"]`)).toHaveAttribute('aria-current', 'page');
          };

          assert.equal((await page.goto(`${origin}/users`, { waitUntil: 'networkidle' })).status(), 200);
          await expect(page).toHaveURL(`${origin}/login`);
          await expect(page.getByRole('heading', { name: 'Sign in to PVADE Admin', exact: true })).toBeVisible();
          await capture('login');
          await page.getByLabel('Email or username').fill('admin@example.test');
          await page.getByLabel('Password', { exact: true }).fill('synthetic-only-password');
          await page.getByRole('button', { name: 'Sign in', exact: true }).click();
          await expect(page.getByRole('alert')).toHaveText('Synthetic login rejected');
          allowLogin = true;
          await page.getByRole('button', { name: 'Sign in', exact: true }).click();
          await expect(page.getByRole('heading', { name: 'Dashboard', exact: true })).toBeVisible();
          await capture('dashboard');

          await navigate('/users');
          await expect(page.getByRole('link', { name: /Synthetic Member/u })).toBeVisible();
          await capture('users');
          await page.getByRole('searchbox', { name: 'Search users' }).fill('no-such-member');
          await page.locator('#users-filter-form button').click();
          await expect(page.getByText('No users found', { exact: true })).toBeVisible();
          assert.ok(searches.includes('no-such-member'));
          await capture('empty-search');

          await navigate('/release-notes');
          await expect(page.getByRole('heading', { name: 'Release notes', exact: true })).toBeVisible();
          await expect(page.locator('.admin-release')).toHaveCount(manifest.releases.length);
          await page.reload({ waitUntil: 'networkidle' });
          await expect(page.getByRole('heading', { name: manifest.releases[0].title, exact: true })).toBeVisible();
          await capture('releases');

          currentRoles = ['Viewer'];
          await page.goto(`${origin}/users`, { waitUntil: 'networkidle' });
          await expect(page.getByRole('heading', { name: 'Access not permitted', exact: true })).toBeVisible();
          await expect(page.locator('#users-filter-form')).toHaveCount(0);
          await capture('forbidden');
        } finally { await context.close(); }
        assert.deepEqual(errors, [], `${mode}/${device} runtime and transport failures`);
        results.push({ mode, device, searches, screenshots, errors });
        console.log(`PASS Admin tooling: ${mode}/${device}, login, users, search, release notes and role-denied UI`);
      }
      await promisify(execFile)(process.execPath, [fileURLToPath(new URL('../../websites/PVAD.Admin/scripts/smoke-documentation.mjs', import.meta.url))], {
        // Protected handbook content must never enter public CI screenshot artifacts.
        env: { ...process.env, PVAD_ADMIN_SMOKE_URL: origin, PVAD_ADMIN_SMOKE_SCREENSHOT: '' },
        timeout: 90_000,
      });
      console.log(`PASS Admin protected handbook smoke: ${mode}`);
    } finally { await server.close(); }
  }
} finally { await browser.close(); }
const assets = [];
for (const name of (await readdir(resolve(root, 'dist/assets'))).sort()) {
  const content = await readFile(resolve(root, 'dist/assets', name));
  assets.push({ name, bytes: content.length, sha256: hash(content) });
}
await writeFile(resolve(output, 'report.json'), `${JSON.stringify({ cases: results.length, handbookRuns: 2, assets, results }, null, 2)}\n`);
