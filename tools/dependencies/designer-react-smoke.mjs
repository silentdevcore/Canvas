import assert from 'node:assert/strict';
import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../pxa-designer/', import.meta.url));
const require = createRequire(new URL('../../pxa-designer/package.json', import.meta.url));
const { chromium, expect } = require('@playwright/test');
const { build, createServer, preview } = await import(pathToFileURL(require.resolve('vite')).href);
const output = new URL('../../artifacts/designer-react-smoke/', import.meta.url);
const fixtureBuild = fileURLToPath(new URL('fixture-build/', output));
const ordinary = new URL('../../pxa-designer/dist/', import.meta.url);
for (const file of await readdir(ordinary, { recursive: true })) {
  assert.ok(!file.includes('react-compatibility'), 'Exclude fixture entry from ordinary build');
  if (/\.(js|html|map)$/.test(file)) {
    assert.ok(!(await readFile(new URL(file, ordinary), 'utf8')).includes('synthetic-react-compatibility-template'));
  }
}
await mkdir(output, { recursive: true });
await build({ root, logLevel: 'error', build: { outDir: fixtureBuild, emptyOutDir: true,
  rollupOptions: { input: `${root}e2e/fixtures/react-compatibility.html` } } });
const browser = await chromium.launch({ headless: true });
const results = [];
try {
  for (const mode of ['development', 'preview']) {
    const server = mode === 'development'
      ? await createServer({ root, logLevel: 'error', server: { host: '127.0.0.1', port: 0, strictPort: true, hmr: false } })
      : await preview({ root, logLevel: 'error', build: { outDir: fixtureBuild }, preview: { host: '127.0.0.1', port: 0, strictPort: true } });
    try {
      if (mode === 'development') await server.listen();
      const origin = new URL(server.resolvedUrls.local[0]).origin;
      for (const [device, viewport] of Object.entries({ desktop: { width: 1280, height: 1000 }, mobile: { width: 390, height: 844 } })) {
        for (const view of ['json', 'legacy', 'spreadsheet']) {
          const context = await browser.newContext({ viewport, locale: 'en-US', reducedMotion: 'reduce', serviceWorkers: 'block' });
          const errors = [];
          const page = await context.newPage();
          try {
            await context.route('**/*', route => {
              const url = new URL(route.request().url());
              if (url.origin !== origin || url.pathname.startsWith('/api/')) {
                errors.push(`Unexpected request: ${url.origin}${url.pathname}`);
                return route.abort();
              }
              return route.continue();
            });
            page.setDefaultTimeout(10_000);
            page.on('pageerror', error => errors.push(error.message));
            page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
            page.on('response', response => { if (response.status() >= 400) errors.push(`HTTP ${response.status()}: ${response.url()}`); });
            const response = await page.goto(`${origin}/e2e/fixtures/react-compatibility.html?view=${view}`, { waitUntil: 'networkidle' });
            assert.equal(response.status(), 200);
            if (view === 'json') {
              const quantity = page.locator('.variable-row').filter({ has: page.locator('.object-key', { hasText: 'quantity' }) });
              await quantity.hover();
              await quantity.locator('.click-to-edit').click();
              await page.locator('textarea.variable-editor').fill('7');
              await page.locator('.edit-check.detected').click();
              await expect(page.locator('#json-state')).toContainText('"quantity":7');
              await quantity.hover();
              await quantity.locator('.click-to-remove').click();
              await expect(page.locator('#json-state')).not.toContainText('quantity');
              await page.locator('.object-key-val').first().hover();
              await page.locator('.object-meta-data .click-to-add-icon:visible').last().click();
              await page.locator('.key-modal-input').fill('reference');
              await page.locator('.key-modal-input').press('Enter');
              await expect(page.locator('#json-state')).toContainText('"reference":null');
              assert.deepEqual(JSON.parse(await page.locator('#json-state').textContent()), {
                title: 'Invoice', nested: { enabled: true }, reference: null,
              });
            } else if (view === 'legacy') {
              await page.getByText('Synthetic document title', { exact: true }).click();
              await expect(page.locator('#selection-state')).toHaveText('synthetic-compatible-text');
              const handle = page.locator('[data-rfd-drag-handle-draggable-id]');
              await handle.focus();
              await page.keyboard.press('Space');
              await expect(page.locator('[aria-live="assertive"]')).toContainText('lifted');
              await page.keyboard.press('Escape');
              await expect(handle).toBeFocused();
              await expect(page.getByText('Synthetic document title', { exact: true })).toBeVisible();
              // The unrouted legacy surface has no reorder implementation; do not claim to add one.
            } else {
              const canvas = page.locator('#grid canvas').first();
              await expect(canvas).toBeVisible();
              const ink = await canvas.evaluate(element => {
                const pixels = element.getContext('2d').getImageData(0, 0, element.width, element.height).data;
                let count = 0;
                for (let i = 0; i < pixels.length; i += 4) {
                  if (pixels[i + 3] && Math.min(pixels[i], pixels[i + 1], pixels[i + 2]) < 220) count++;
                }
                return count;
              });
              assert.ok(ink > 100, 'The actual grid canvas must contain rendered content');
              await page.locator('#grid .dvn-scroller').click({ position: { x: 80, y: 50 } });
              await canvas.press('Enter');
              const input = page.locator('.gdg-input');
              await expect(input, JSON.stringify(errors)).toBeVisible();
              await input.fill('11');
              await input.press('Enter');
              await expect(page.locator('#sheet-state')).toContainText('"input":11');
              await expect(page.locator('#sheet-state')).toContainText('"result":22');
              await expect(page.locator('#sheet-state')).toContainText('"formula":"=A1*2"');
            }
            await page.screenshot({ path: fileURLToPath(new URL(`${mode}-${device}-${view}.png`, output)), animations: 'disabled' });
            assert.deepEqual(errors, [], `${mode}/${device}/${view}: no runtime, console or request errors`);
            results.push({ mode, device, view, passed: true });
            console.log(`PASS React compatibility: ${mode}/${device}/${view}`);
          } catch (error) {
            await page.screenshot({ path: fileURLToPath(new URL(`${mode}-${device}-${view}-failed.png`, output)) });
            console.error(JSON.stringify({ mode, device, view, errors }));
            throw error;
          } finally { await context.close(); }
        }
      }
    } finally { await server.close(); }
  }
} finally {
  await browser.close();
  await writeFile(new URL('report.json', output), `${JSON.stringify({
    scope: 'Actual components under StrictMode and synthetic local data; no authentication/backend claim.',
    completed: results.length === 12, results,
  }, null, 2)}\n`);
}
