import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { chromium } from '../../pvad-designer/node_modules/playwright/index.mjs';

const origin = process.env.PVAD_DESIGNER_SMOKE_ORIGIN ?? 'http://localhost:5176';
if (!['localhost', '127.0.0.1', '[::1]'].includes(new URL(origin).hostname)) throw new Error('Local origin required.');
const output = new URL('../../artifacts/rename/native-project-import/', import.meta.url);
await mkdir(output, { recursive: true });
const browser = await chromium.launch();
const results = [];
try {
  for (const kind of ['pdf', 'spreadsheet']) {
    for (const width of [1280, 390]) {
      const context = await browser.newContext({ viewport: { width, height: 900 } });
      const page = await context.newPage(), errors = [], uploads = [];
      page.on('pageerror', error => errors.push(error.message));
      const extension = kind === 'pdf' ? 'pvadp' : 'pvads';
      await page.addInitScript(({ extension }) => {
        window.__fileReads = 0;
        Object.defineProperty(window, 'launchQueue', { configurable: true, value: { setConsumer(callback) {
          callback({ files: [{ kind: 'file', name: `Synthetic.${extension}`, getFile: async () => {
            window.__fileReads++;
            return new File(['synthetic transport fixture'], `Synthetic.${extension}`);
          } }] });
        } } });
      }, { extension });
      const marker = `Synthetic ${kind} import`;
      const document = kind === 'pdf'
        ? { id: 'synthetic-project', name: marker, pages: [{ id: 'synthetic-page', elements: [
          { id: 'synthetic-text', type: 'text', x: 30, y: 30, width: 250, height: 40, content: marker },
        ] }] }
        : { id: 'synthetic-book', name: marker, definedNames: [], sheets: [{ id: 'synthetic-sheet', name: marker,
          rowCount: 20, colCount: 6, columns: [], rows: [], cells: [{ row: 0, col: 0, type: 'text', value: 'Synthetic cell' }],
          merges: [], frozenRows: 0, frozenCols: 0 }] };
      await page.route('**/api/**', async route => {
        const request = route.request(), path = new URL(request.url()).pathname;
        if (path.endsWith('/auth/me')) return route.fulfill({ json: {
          id: 'synthetic-user', displayName: 'Synthetic User', username: 'synthetic', email: 'synthetic@example.test',
          activeOrganizationId: 'synthetic-org', organizations: [{ id: 'synthetic-org', name: 'Synthetic Organization', slug: 'synthetic' }],
          roles: [], permissions: ['designer.access', `designer.${kind}.access`, `designer.${kind}.create`,
            `designer.${kind}.import`, 'designer.templates.read'],
        } });
        if (path.endsWith('/auth/csrf')) return route.fulfill({ json: { token: 'synthetic-csrf' } });
        if (path === `/api/pvad/v1/designer/projects/${kind}/open`) {
          uploads.push(request);
          assert.equal(request.headers()['x-pvad-application'], 'designer');
          assert.equal(request.headers()['x-pvad-csrf'], 'synthetic-csrf');
          assert.match(request.headers()['content-type'], /^multipart\/form-data;/);
          return route.fulfill({ json: { manifest: { title: marker, formatId: extension, documentSchemaVersion: '1.0' },
            document, packageSha256: 'a'.repeat(64) } });
        }
        if (path.endsWith('/features')) return route.fulfill({ json: [] });
        if (path.endsWith('/releases')) return route.fulfill({ json: { releases: [], readVersions: [] } });
        if (path.includes('/notifications')) return route.fulfill({ json: { items: [], unread: 0 } });
        return route.fulfill({ status: 403, json: { title: 'Synthetic operation not authorized' } });
      });
      await page.goto(`${origin}/open-project`);
      await page.getByText('Synthetic User · Synthetic Organization', { exact: true }).waitFor();
      assert.equal(await page.evaluate(() => window.__fileReads), 0);
      await page.getByRole('button', { name: 'Open in Designer', exact: true }).click();
      try {
        await page.getByText(marker, { exact: true }).first().waitFor({ timeout: 20_000 });
      } catch (error) {
        await page.screenshot({ path: fileURLToPath(new URL(`failed-${kind}-${width}.png`, output)), fullPage: true });
        await writeFile(new URL(`failed-${kind}-${width}.json`, output), JSON.stringify({ errors,
          url: page.url(), uploads: uploads.length, text: await page.locator('body').innerText() }, null, 2));
        throw error;
      }
      await page.waitForFunction(() => !location.search.includes('nativeLaunch'));
      assert.equal(new URL(page.url()).pathname, `/${kind}/create`);
      assert.equal(await page.evaluate(() => window.__fileReads), 1);
      assert.equal(uploads.length, 1);
      assert.deepEqual(errors, []);
      if (width < 600) {
        const expandedSidebar = page.locator('.hub-sidebar-toggle[aria-expanded="true"]');
        if (await expandedSidebar.count()) await expandedSidebar.click();
        await page.locator('.hub-sidebar-backdrop.is-visible').waitFor({ state: 'hidden' });
      }
      await page.screenshot({ path: fileURLToPath(new URL(`${kind}-${width}.png`, output)), fullPage: true });
      results.push({ kind, width, reads: 1, uploads: 1, csrfHeaders: true, editorContentVisible: true, noScriptErrors: true });
      await context.close();
    }
  }
  const report = { status: 'passed', scope: 'Real browser UI and upload transport with synthetic API responses; not archive validation, installed OS associations or live server authorization.', results };
  await writeFile(new URL('report.json', output), `${JSON.stringify(report, null, 2)}\n`);
  console.log(JSON.stringify(report));
} finally { await browser.close(); }
