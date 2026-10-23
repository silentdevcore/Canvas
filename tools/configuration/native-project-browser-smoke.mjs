import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from '../../pvad-designer/node_modules/playwright/index.mjs';

const origin = process.env.PVAD_DESIGNER_SMOKE_ORIGIN ?? 'http://localhost:5176';
const url = new URL(origin);
if (!['localhost', '127.0.0.1', '[::1]'].includes(url.hostname)) throw new Error('Local Designer origin required.');
const output = new URL('../../artifacts/rename/native-project-browser/', import.meta.url);
await mkdir(output, { recursive: true });
const browser = await chromium.launch();
const results = [];
try {
  for (const width of [1280, 390]) {
    const context = await browser.newContext({ viewport: { width, height: 844 } });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => {
      window.__fileReads = 0;
      Object.defineProperty(window, 'launchQueue', { configurable: true, value: { setConsumer(callback) {
        callback({ files: [{ kind: 'file', name: 'Synthetic project.pvadp', getFile: async () => {
          window.__fileReads += 1;
          return new File(['synthetic fixture'], 'Synthetic project.pvadp');
        } }] });
      } } });
    });
    let authorized = false;
    await page.route('**/api/**', async route => {
      if (authorized && new URL(route.request().url()).pathname.endsWith('/auth/me'))
        return route.fulfill({ json: { id: 'synthetic', displayName: 'Synthetic User', username: 'synthetic',
          email: 'synthetic@example.test', activeOrganizationId: 'synthetic-org',
          organizations: [{ id: 'synthetic-org', name: 'Synthetic Company', slug: 'synthetic' }],
          permissions: ['designer.pdf.access', 'designer.pdf.create', 'designer.pdf.import'], roles: [] } });
      return route.fulfill({ status: 401, json: { title: 'Synthetic unauthenticated session' } });
    });
    await page.goto(`${origin}/open-project`);
    await page.getByRole('heading', { name: 'Open project', exact: true }).waitFor();
    const open = page.getByRole('button', { name: 'Open in Designer', exact: true });
    assert.equal(await open.isDisabled(), true);
    assert.equal(await page.evaluate(() => window.__fileReads), 0);
    await page.screenshot({ path: fileURLToPath(new URL(`anonymous-${width}.png`, output)), fullPage: true });
    authorized = true;
    await page.getByRole('button', { name: 'Check access', exact: true }).click();
    await page.getByText('Synthetic User · Synthetic Company', { exact: true }).waitFor();
    assert.equal(await open.isEnabled(), true);
    assert.equal(await page.evaluate(() => window.__fileReads), 0);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.screenshot({ path: fileURLToPath(new URL(`authorized-${width}.png`, output)), fullPage: true });
    await page.getByRole('button', { name: 'Discard', exact: true }).click();
    assert.equal(await open.isDisabled(), true);
    assert.equal(await page.evaluate(() => window.__fileReads), 0);
    assert.deepEqual(errors, []);
    results.push({ width, anonymousUnread: true, explicitAccessRefresh: true, discardUnread: true, noHorizontalOverflow: true });
    await context.close();
  }
  const report = { status: 'passed', scope: 'Synthetic browser launch and session stubs only; not installed OS associations or live API import.', results };
  await writeFile(new URL('report.json', output), `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify(report)}\n`);
} finally { await browser.close(); }
