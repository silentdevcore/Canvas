import assert from 'node:assert/strict';
import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, resolve } from 'node:path';

const require = createRequire(new URL('../../pxa-designer/package.json', import.meta.url));
const { chromium, expect } = require('@playwright/test');
const { createServer, build, preview } = await import(pathToFileURL(require.resolve('vite')).href);
const root = fileURLToPath(new URL('../../pxa-designer/', import.meta.url));
const artifactRoot = new URL('../../artifacts/designer-chart-smoke/', import.meta.url);
const args = process.argv.slice(2);
const label = args.includes('--label') ? args[args.indexOf('--label') + 1] : 'current';
assert.match(label ?? '', /^[a-z0-9-]+$/);
const output = new URL(`${label}/`, artifactRoot);
const comparePath = args.includes('--compare') ? args[args.indexOf('--compare') + 1] : undefined;
const baseline = comparePath ? JSON.parse(await readFile(comparePath, 'utf8')) : null;
if (baseline) assert.equal(baseline.results.length, 60, 'Require a complete baseline');
const pkg = JSON.parse(await readFile(new URL('../../pxa-designer/node_modules/recharts/package.json', import.meta.url), 'utf8'));
const ordinaryBuild = new URL('../../pxa-designer/dist/', import.meta.url);
for (const file of await readdir(ordinaryBuild, { recursive: true })) {
  assert.ok(!file.includes('recharts-review'), 'Ordinary build must exclude the test entry');
  if (/\.(js|html|map)$/.test(file)) {
    assert.ok(!(await readFile(new URL(file, ordinaryBuild), 'utf8')).includes('synthetic-chart-template'), 'Ordinary bundle must exclude fixture data');
  }
}
await mkdir(output, { recursive: true });
const results = [];
const canvasResults = [];
const browser = await chromium.launch({ headless: true });
try {
  // Only this explicit test build includes the fixture; the ordinary Designer build is unchanged.
  const fixtureBuild = fileURLToPath(new URL('fixture-build/', artifactRoot));
  await build({ root, logLevel: 'warn', build: {
    outDir: fixtureBuild, emptyOutDir: true,
    rollupOptions: { input: `${root}e2e/fixtures/recharts-review.html` },
  } });
  for (const mode of ['development', 'preview']) {
    const server = mode === 'development'
      ? await createServer({ root, logLevel: 'warn', server: { host: '127.0.0.1', port: 0, strictPort: true, hmr: false } })
      : await preview({ root, logLevel: 'warn', build: { outDir: fixtureBuild }, preview: { host: '127.0.0.1', port: 0, strictPort: true } });
    try {
      if (mode === 'development') await server.listen();
      const origin = new URL(server.resolvedUrls.local[0]).origin;
      for (const device of ['desktop', 'mobile']) {
        const context = await browser.newContext({
          viewport: device === 'desktop' ? { width: 1440, height: 1000 } : { width: 390, height: 844 },
          locale: 'en-US', timezoneId: 'UTC', reducedMotion: 'reduce', serviceWorkers: 'block',
        });
        const errors = [];
        try {
          await context.route('**/*', async route => {
            const url = new URL(route.request().url());
            if (url.hostname === 'fonts.googleapis.com') return route.fulfill({ status: 200, contentType: 'text/css', body: '' });
            if (url.origin !== origin || url.pathname.startsWith('/api/')) {
              errors.push(`Unexpected request: ${url.origin}${url.pathname}`);
              return route.abort();
            }
            return route.continue();
          });
          const page = await context.newPage();
          page.on('pageerror', error => errors.push(error.message));
          page.on('requestfailed', request => {
            if (request.failure()?.errorText !== 'net::ERR_ABORTED') errors.push(`Transport failure: ${new URL(request.url()).pathname}`);
          });
          page.on('console', message => {
            if (message.type() === 'error') errors.push(`Console error: ${message.text()}`);
          });
          page.on('response', response => {
            if (response.status() >= 400) errors.push(`HTTP ${response.status()}: ${new URL(response.url()).pathname}`);
          });
          for (const type of ['bar', 'line', 'pie']) {
            for (const variant of ['standard', 'edge', 'gaps', 'empty', 'long']) {
              const key = `${mode}-${device}-${type}-${variant}`;
              const response = await page.goto(`${origin}/e2e/fixtures/recharts-review.html?${new URLSearchParams({ type, variant, device })}`, { waitUntil: 'networkidle' });
              assert.equal(response.status(), 200);
              const chart = page.locator('#preview-pages .recharts-wrapper');
              await expect(chart).toHaveCount(1);
              await expect(chart.locator('svg.recharts-surface[role="application"]')).toBeVisible();
              await page.waitForFunction(() => !document.querySelector('.recharts-animate-active'));
              await page.waitForTimeout(1800);
              const geometry = await chart.evaluate(element => {
                const rect = element.getBoundingClientRect();
                return {
                  width: rect.width, height: rect.height,
                  paths: [...element.querySelectorAll('path')].map(path => ({ d: path.getAttribute('d'), fill: path.getAttribute('fill'), stroke: path.getAttribute('stroke') })),
                  text: [...element.querySelectorAll('text')].map(node => node.textContent),
                  legend: [...element.querySelectorAll('.recharts-legend-item-text')].map(node => node.textContent),
                };
              });
              assert.equal(geometry.width, device === 'mobile' ? 260 : 480);
              assert.equal(geometry.height, 300);
              assert.ok(geometry.paths.every(path => !/NaN|Infinity/.test(path.d ?? '')), `${key}: finite paths`);
              if (variant !== 'empty') {
                const marks = chart.locator(type === 'bar' ? '.recharts-bar-rectangle path' : type === 'line' ? '.recharts-line-curve' : '.recharts-pie-sector path');
                assert.ok(await marks.count() > 0, `${key}: chart must not be blank`);
                if (type !== 'pie') assert.deepEqual(geometry.legend, ['pv', 'uv']);
              }
              const screenshot = await chart.screenshot({ path: fileURLToPath(new URL(`${key}.png`, output)), animations: 'disabled' });
              const oldImage = comparePath ? await readFile(resolve(dirname(comparePath), `${key}.png`)) : null;
              const pixels = await page.evaluate(async ({ current, previous }) => {
                const decode = async source => {
                  const image = new Image(); image.src = `data:image/png;base64,${source}`;
                  await image.decode();
                  const canvas = document.createElement('canvas'); canvas.width = image.width; canvas.height = image.height;
                  const context = canvas.getContext('2d'); context.drawImage(image, 0, 0);
                  return { canvas, context, data: context.getImageData(0, 0, image.width, image.height) };
                };
                const now = await decode(current);
                const before = previous ? await decode(previous) : null;
                if (before && (before.canvas.width !== now.canvas.width || before.canvas.height !== now.canvas.height)) {
                  throw new Error('Screenshot dimensions changed');
                }
                let colored = 0, changed = 0;
                for (let index = 0; index < now.data.data.length; index += 4) {
                  const rgb = [...now.data.data.slice(index, index + 3)];
                  if (Math.max(...rgb) - Math.min(...rgb) > 40) colored++;
                  if (before && rgb.some((value, channel) => Math.abs(value - before.data.data[index + channel]) > 16)) changed++;
                }
                return { colored, changed, total: now.canvas.width * now.canvas.height };
              }, { current: screenshot.toString('base64'), previous: oldImage?.toString('base64') });
              if (variant !== 'empty') assert.ok(pixels.colored > 100, `${key}: visible chart pixels`);
              const old = baseline?.results.find(result => result.key === key);
              if (baseline) assert.ok(old, `Missing baseline: ${key}`);
              if (old) {
                assert.equal(geometry.width, old.geometry.width);
                assert.equal(geometry.height, old.geometry.height);
                assert.deepEqual(geometry.legend, old.geometry.legend, `${key}: legend content`);
                assert.deepEqual(geometry.text, old.geometry.text, `${key}: chart labels`);
              }
              results.push({ key, geometry, pixels, geometryMatchesBaseline: old ? JSON.stringify(geometry) === JSON.stringify(old.geometry) : null });
              if (['standard', 'edge'].includes(variant) && type === 'bar') {
                const bar = chart.locator('.recharts-bar-rectangle path').first();
                await bar.hover();
                await expect(chart.locator('.recharts-tooltip-wrapper')).toBeVisible();
                await expect(chart.locator('.recharts-tooltip-item').first()).toContainText('pv');
                if (variant === 'edge') await expect(chart.locator('.recharts-tooltip-item').first()).toContainText('-12');
              }
            }
          }
          if (device === 'desktop') {
            for (const type of ['bar', 'line', 'pie']) {
              await page.goto(`${origin}/e2e/fixtures/recharts-review.html?${new URLSearchParams({ type, variant: 'standard', device, view: 'canvas' })}`, { waitUntil: 'networkidle' });
              const canvas = page.locator('.editor-canvas-element');
              await expect(canvas).toHaveCount(1);
              await expect(canvas).toContainText('Series 1');
              const key = `${mode}-canvas-${type}`;
              const screenshot = await canvas.screenshot({ path: fileURLToPath(new URL(`${key}.png`, output)), animations: 'disabled' });
              const unchanged = comparePath ? screenshot.equals(await readFile(resolve(dirname(comparePath), `${key}.png`))) : null;
              if (comparePath) assert.equal(unchanged, true, `${key}: the independent Canvas renderer must remain unchanged`);
              canvasResults.push({ key, unchanged });
            }
          }
          assert.deepEqual(errors, [], `${mode}/${device}: no runtime or request failures`);
          console.log(`PASS chart rendering: ${mode}/${device}, 15 preview fixtures${device === 'desktop' ? ' and 3 actual Canvas fixtures' : ''}`);
        } finally { await context.close(); }
      }
    } finally { await server.close(); }
  }
} finally {
  await browser.close();
  await writeFile(new URL('report.json', output), `${JSON.stringify({
    scope: 'Actual frontend chart components with synthetic local data; no backend PDF or real authentication claim.',
    recharts: pkg.version, completed: results.length === 60 && canvasResults.length === 6, results, canvasResults,
  }, null, 2)}\n`);
}
