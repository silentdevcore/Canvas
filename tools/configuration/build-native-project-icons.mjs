import { mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { chromium } from '../../pvad-designer/node_modules/playwright/index.mjs';

const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
const { createElement } = require('react');
const { renderToStaticMarkup } = require('react-dom/server');
const { FiFileText } = require('react-icons/fi');
const output = new URL('../../pvad-designer/public/icons/', import.meta.url);
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
try {
  for (const size of [192, 512]) {
    const page = await browser.newPage({ viewport: { width: size, height: size }, deviceScaleFactor: 1 });
    const icon = renderToStaticMarkup(createElement(FiFileText, { size: Math.round(size * 0.65), color: '#ffffff' }));
    await page.setContent(`<html><body style="margin:0;display:grid;place-items:center;width:${size}px;height:${size}px;background:#087f73">${icon}</body></html>`);
    await page.screenshot({ path: fileURLToPath(new URL(`project-${size}.png`, output)) });
    await page.close();
  }
} finally { await browser.close(); }
