import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const require = createRequire(resolve(root, 'pvad-designer/package.json'));
const { chromium } = require('@playwright/test');
const renderer = await readFile(resolve(root,
  'pvad-designer/src/components/Rendering/PdfTextRenderer.tsx'), 'utf8');
const family = renderer.match(/default: return "'([^']+)'/)?.[1];
assert.equal(family, 'PVADE Helvetica');
const css = await readFile(resolve(root, 'pvad-designer/src/styles/editor.css'), 'utf8');
const faces = [...css.matchAll(/@font-face\s*\{([^}]+)\}/g)]
  .map(match => match[0]).filter(face => face.includes(`font-family: '${family}'`));
assert.equal(faces.length, 4);
const assets = new Map();
const fontCss = faces.map((face, index) => face.replace(/url\('([^']+)'\)/, (_match, path) => {
  assets.set(`/font-${index}.ttf`, resolve(root, 'pvad-designer/src/styles', path));
  return `url('/font-${index}.ttf')`;
})).join('\n');
const output = resolve(root, 'artifacts/rename/designer-fonts');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const results = [];
try {
  for (const viewport of [{ width: 1280, height: 720 }, { width: 390, height: 844 }]) {
    const page = await browser.newPage({ viewport, deviceScaleFactor: 1 });
    const loaded = new Set();
    await page.route('**/*', async route => {
      const path = new URL(route.request().url()).pathname;
      if (assets.has(path)) {
        loaded.add(path);
        return route.fulfill({ contentType: 'font/ttf', body: await readFile(assets.get(path)) });
      }
      if (path !== '/') return route.abort();
      return route.fulfill({ contentType: 'text/html', body: `<!doctype html>
        <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <style>${fontCss}
          body { margin: 24px; background: white; color: #111; }
          p { font-family: '${family}'; font-size: 16px; overflow-wrap: anywhere; }
        </style>
        <h1>Designer local font check</h1>
        ${[['normal', 400], ['normal', 700], ['italic', 400], ['italic', 700]].map(([style, weight]) =>
          `<p style="font-style:${style};font-weight:${weight}">${style} ${weight}: Invoice 12345.67 - PDF layout sample</p>`).join('')}` });
    });
    await page.goto('https://designer-font-check.invalid/');
    await page.evaluate(() => document.fonts.ready);
    const metrics = await page.evaluate(() => [...document.querySelectorAll('p')].map(element => {
      const style = getComputedStyle(element);
      const box = element.getBoundingClientRect();
      return { family: style.fontFamily, weight: style.fontWeight, style: style.fontStyle,
        loaded: document.fonts.check(`${style.fontStyle} ${style.fontWeight} 16px ${style.fontFamily}`),
        width: box.width, height: box.height, right: box.right };
    }));
    assert.equal(loaded.size, 4, 'Every local face must be requested');
    for (const metric of metrics) {
      assert.ok(metric.loaded);
      assert.ok(metric.family.includes(family));
      assert.ok(metric.width > 0 && metric.height > 0);
      assert.ok(metric.right <= viewport.width);
    }
    await page.screenshot({ path: resolve(output, `fonts-${viewport.width}.png`), fullPage: true });
    results.push({ viewport, metrics, loadedFaces: loaded.size });
    await page.close();
  }
} finally {
  await browser.close();
}
await writeFile(resolve(output, 'report.json'), `${JSON.stringify({
  scope: 'isolated-browser-font-loading-not-full-designer-or-backend-parity',
  browser: 'chromium', family, results,
}, null, 2)}\n`);
console.log(`Verified four local font faces at desktop/mobile sizes. Evidence: ${output}`);
