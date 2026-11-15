import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { createServer } from '../../pvad-designer/node_modules/vite/dist/node/index.js';
import playwright from '../../pvad-designer/node_modules/@playwright/test/index.js';
import { requireImageMagick, spawnImageMagick } from './image-tools.mjs';
import { comparePdfGlyphRun } from './text-metrics.mjs';
import { boldTextPixelHash, matchesBoldTextApproval } from './bold-text-approval.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const output = path.join(root, 'artifacts', 'bold-text-review');
const cases = [
  { fixture: 'test-assets/visual-parity/all-elements.json', id: 'text', page: 1 },
  { fixture: 'test-assets/page-layout/mixed-layout.json', id: 'custom-title', page: 3 },
];
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

function run(command, args, extra = {}) {
  const result = command === 'magick'
    ? spawnImageMagick(args, { cwd: root, encoding: 'utf8', timeout: 30_000 })
    : spawnSync(command, args, { cwd: root, encoding: 'utf8', timeout: 180_000,
      maxBuffer: 8 * 1024 * 1024, env: { ...process.env, ...extra } });
  if (result.status !== 0)
    throw new Error(`${command} failed: ${result.error?.message ?? ''}\n${result.stdout ?? ''}\n${result.stderr ?? ''}`);
  return result;
}

async function prepare(entry) {
  const directory = path.join(output, entry.id);
  await mkdir(directory, { recursive: true });
  run('dotnet', ['test', 'tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj', '--no-build', '--no-restore',
    '--filter', 'FullyQualifiedName~VisualParityArtifactTests', '--nologo'], {
    PVAD_VISUAL_PARITY_FIXTURE: entry.fixture, PVAD_VISUAL_PARITY_ARTIFACTS: directory,
  });
  const fixture = JSON.parse(await readFile(path.join(root, entry.fixture), 'utf8'));
  const element = fixture.pages[entry.page - 1].elements.find(element => element.id === entry.id);
  if (!element || element.type !== 'text') throw new Error(`Missing review text ${entry.id}.`);
  const pdfRuns = JSON.parse(await readFile(path.join(directory, 'text-metrics.json'), 'utf8'));
  for (const name of ['backend', 'csharp']) {
    const prefix = path.join(directory, `${name}-page`);
    // Render vectors at 8 pixels per point, then average to 2 pixels per point.
    run('pdftoppm', ['-f', String(entry.page), '-l', String(entry.page), '-singlefile', '-r', '576', '-png',
      path.join(directory, `${name}.pdf`), prefix]);
    run('magick', [`${prefix}.png`, '-filter', 'Box', '-resize', '25%', '-crop',
      `${element.width * 2}x${element.height * 2}+${element.x * 2}+${element.y * 2}`, '+repage',
      path.join(directory, `${name}-200.png`)]);
  }
  return { ...entry, directory, element, pdfRuns };
}

async function capture(page, base, entry, surface, fontHash) {
  await page.goto(`${base}/__visual-parity?surface=${surface}&fixture=${path.basename(entry.fixture, '.json')}`,
    { waitUntil: 'networkidle' });
  const element = page.locator(`[data-parity-surface="${surface}"] [data-parity-page="${entry.page}"] [data-element-id="${entry.id}"]`);
  await element.waitFor();
  await page.evaluate(async () => { await document.fonts.ready; });
  await element.scrollIntoViewIfNeeded();
  const metrics = await element.evaluate(async node => {
    const pageBox = node.closest('[data-parity-page]').getBoundingClientRect();
    const texts = [...node.querySelectorAll('svg text')];
    const face = [...document.fonts].find(face => face.family.replaceAll(/["']/g, '') === 'PVADE Helvetica'
      && face.weight === '700' && face.style === 'normal' && face.status === 'loaded');
    const resource = performance.getEntriesByType('resource').find(resource => resource.name.includes('LiberationSans-Bold.ttf'));
    if (!face || !resource) throw new Error('The packaged bold face was not loaded.');
    const response = await fetch(resource.name);
    if (!response.ok) throw new Error('Cannot verify the loaded bold font bytes.');
    const bytes = await response.arrayBuffer();
    const digest = await crypto.subtle.digest('SHA-256', bytes);
    const fontHash = [...new Uint8Array(digest)].map(value => value.toString(16).padStart(2, '0')).join('');
    const runs = texts.map(text => {
      const style = getComputedStyle(text);
      if (!style.fontFamily.startsWith('"PVADE Helvetica"') && !style.fontFamily.startsWith('PVADE Helvetica'))
        throw new Error('Text does not request the packaged font family.');
      const position = new DOMPoint(text.x.baseVal.getItem(0).value, text.y.baseVal.getItem(0).value)
        .matrixTransform(text.getScreenCTM());
      const content = text.textContent;
      // This review is deliberately restricted to the two current plain Latin fixtures.
      if (!/^[\x20-\x7e]+$/.test(content) || text.getNumberOfChars() !== content.length)
        throw new Error('The review requires one browser character per Latin glyph.');
      const start = text.getStartPositionOfChar(0).x;
      return {
        text: content, x: position.x - pageBox.x, baseline: position.y - pageBox.y,
        advance: text.getComputedTextLength(), fontSize: Number.parseFloat(style.fontSize),
        bold: Number(style.fontWeight) >= 600, italic: style.fontStyle !== 'normal', fontHash,
        glyphs: [...content].map((character, index) => ({
          text: character, offset: text.getStartPositionOfChar(index).x - start,
          advance: text.getEndPositionOfChar(index).x - text.getStartPositionOfChar(index).x,
        })),
      };
    });
    return { runs, loadedFontHash: fontHash, fontFamily: face.family, fontWeight: face.weight };
  });
  if (metrics.loadedFontHash !== fontHash || !metrics.runs.length) throw new Error(`Unexpected font or missing text on ${surface}.`);
  const comparisons = metrics.runs.map(run => comparePdfGlyphRun(run, entry.pdfRuns, entry.page, `${entry.id}/${surface}`));
  const box = await element.evaluate(node => {
    const page = node.closest('[data-parity-page]').getBoundingClientRect();
    return { x: page.x + node.offsetLeft, y: page.y + node.offsetTop, width: node.offsetWidth, height: node.offsetHeight };
  });
  await page.screenshot({ path: path.join(entry.directory, `${surface}-200.png`), clip: box, scale: 'device' });
  return { surface, fontFamily: metrics.fontFamily, fontWeight: metrics.fontWeight, loadedFontHash: metrics.loadedFontHash, comparisons };
}

async function main() {
  requireImageMagick();
  const entries = [];
  for (const entry of cases) entries.push(await prepare(entry));
  const fontHash = hash(await readFile(path.join(root, 'assets/fonts/liberation/LiberationSans-Bold.ttf')));
  const previousFlag = process.env.VITE_ENABLE_VISUAL_PARITY;
  process.env.VITE_ENABLE_VISUAL_PARITY = 'true';
  let server, browser;
  const results = [];
  try {
    server = await createServer({ root: path.join(root, 'pvad-designer'),
      configFile: path.join(root, 'pvad-designer/vite.config.ts'),
      server: { host: '127.0.0.1', port: 0 }, logLevel: 'error' });
    await server.listen();
    const base = `http://127.0.0.1:${server.httpServer.address().port}`;
    browser = await playwright.chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1000, height: 1000 }, deviceScaleFactor: 2,
      locale: 'en-US', timezoneId: 'UTC' });
    await page.route('**/*', route => new URL(route.request().url()).origin === base ? route.continue() : route.abort());
    let active;
    await page.route('**/api/**', async route => {
      if (route.request().url().endsWith('/designer/symbols/render')) {
        const type = route.request().postDataJSON()?.type;
        if (!['qrcode', 'barcode'].includes(type)) return route.fulfill({ status: 400, body: '{}' });
        return route.fulfill({ status: 200, contentType: 'image/png', path: path.join(active.directory, `${type}.png`) });
      }
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ token: 'synthetic-review', elementTypes: [] }) });
    });
    for (const entry of entries) {
      active = entry;
      const surfaces = [];
      for (const surface of ['canvas', 'preview']) surfaces.push(await capture(page, base, entry, surface, fontHash));
      run('magick', [path.join(entry.directory, 'canvas-200.png'), path.join(entry.directory, 'backend-200.png'),
        '+append', path.join(entry.directory, 'canvas-backend-200.png')]);
      const diff = spawnImageMagick(['compare', '-metric', 'AE', path.join(entry.directory, 'backend-200.png'),
        path.join(entry.directory, 'csharp-200.png'), path.join(entry.directory, 'pdf-diff-200.png')], { encoding: 'utf8' });
      if (diff.status !== 0) throw new Error(`Backend and C# PDF differ for ${entry.id}.`);
      results.push({ id: entry.id, fixture: entry.fixture, page: entry.page, surfaces,
        fixtureSha256: hash(await readFile(path.join(root, entry.fixture))),
        comparisonSha256: hash(await readFile(path.join(entry.directory, 'canvas-backend-200.png'))),
        comparisonPixelSha256: boldTextPixelHash(path.join(entry.directory, 'canvas-backend-200.png')),
        pdfCropsMatchExactly: true, rasterPixelsPerPoint: 2 });
    }
    const approvalPath = 'test-assets/visual-parity/approvals/2026-11-13-bold-text/approval.json';
    const pinnedApproval = JSON.parse(await readFile(path.join(root, approvalPath), 'utf8'));
    const evidence = { browserVersion: browser.version(), platform: process.platform,
      rasterPixelsPerPoint: 2, fontHash, cases: results };
    const approved = matchesBoldTextApproval(pinnedApproval, evidence);
    const approval = approved ? 'approved' : 'pending';
    await writeFile(path.join(output, 'report.json'), JSON.stringify({ generatedAt: new Date().toISOString(),
      ...evidence, approval, approvalSource: approved ? approvalPath : null }, null, 2));
    await writeFile(path.join(output, 'report.html'), `<!doctype html><html lang="en"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Bold text 200% visual review</title>
<style>body{font:14px system-ui;color:#222;margin:24px}section{margin:24px 0}.images{display:flex;gap:16px;overflow-x:auto}figure{margin:0;flex-shrink:0}img{display:block;max-width:none;border:1px solid #aaa}p{max-width:80ch}</style>
<h1>Bold text at 200%</h1><p>Human approval: ${approval}. ${approved ? 'Matches the explicitly approved images, fixtures, font and browser recorded on 2026-11-13.' : 'New or changed evidence requires explicit visual approval.'} Browser: 2 device pixels per PDF point. PDF: rerendered vectors at 144 DPI after supersampling. No enlarged 72 DPI screenshots, threshold changes or altered reference files.</p>
<p>Each pair below shows Canvas on the left and backend PDF on the right. The four separate surfaces follow. Minor native rasterization differences remain visible; passing geometry checks does not grant approval.</p>
${results.map(result => `<section><h2>${result.id}</h2><div class="images"><img src="${result.id}/canvas-backend-200.png" alt="Canvas left and backend PDF right at 200 percent"></div><div class="images">${['canvas', 'preview', 'backend', 'csharp'].map(name => `<figure><figcaption>${name}</figcaption><img src="${result.id}/${name}-200.png" alt="${result.id}, ${name}, 200 percent"></figure>`).join('')}</div></section>`).join('')}</html>`);
    console.log(`Bold text review: ${results.length} cases, exact font bytes and per-glyph layout verified. Human approval: ${approval}.\n${path.join(output, 'report.html')}`);
  } finally {
    await browser?.close();
    await server?.close();
    if (previousFlag === undefined) delete process.env.VITE_ENABLE_VISUAL_PARITY;
    else process.env.VITE_ENABLE_VISUAL_PARITY = previousFlag;
  }
}

main().catch(error => { console.error(error.stack ?? error); process.exitCode = 1; });
