import { spawnSync } from 'node:child_process';
import { mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { rmSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import playwright from '../../pxa-designer/node_modules/@playwright/test/index.js';
import { createServer } from '../../pxa-designer/node_modules/vite/dist/node/index.js';

const { chromium } = playwright;

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(toolDir, '../..');
const designerDir = path.join(root, 'pxa-designer');
const output = path.join(root, 'artifacts', 'visual-parity');
const raw = path.join(output, 'raw');
const configuredFixture = process.env.PXA_VISUAL_PARITY_FIXTURE ?? 'test-assets/visual-parity/all-elements.json';
const fixturePath = path.isAbsolute(configuredFixture) ? configuredFixture : path.join(root, configuredFixture);
// The key the browser route uses to pick the same fixture. Derived from the filename so adding a
// fixture needs no change here.
const fixtureKey = path.basename(configuredFixture, '.json');
const baseUrl = 'http://127.0.0.1:4187';

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', stdio: 'pipe', ...options });
  if (result.status !== 0) {
    throw new Error(`${command} ${args.join(' ')} failed\n${result.stdout ?? ''}\n${result.stderr ?? ''}`);
  }
  return result;
}

function requireTool(name) {
  const result = spawnSync('sh', ['-c', `command -v ${name}`], { encoding: 'utf8' });
  if (result.status !== 0) throw new Error(`Visual parity requires '${name}' on PATH.`);
}

async function waitForServer() {
  const deadline = Date.now() + 30_000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${baseUrl}/__visual-parity?surface=preview`);
      if (response.ok) return;
    } catch { /* retry */ }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error('Timed out waiting for the visual parity route.');
}

async function renderBrowserSurfaces(fixture) {
  process.env.VITE_ENABLE_VISUAL_PARITY = 'true';
  const server = await createServer({
    root: designerDir,
    configFile: path.join(designerDir, 'vite.config.ts'),
    server: { host: '127.0.0.1', port: 4187, strictPort: true },
    logLevel: 'warn',
  });

  try {
    await server.listen();
    await waitForServer();
    let browser;
    try {
      browser = await chromium.launch({ channel: 'chrome', headless: true });
    } catch {
      browser = await chromium.launch({ headless: true });
    }
    const page = await browser.newPage({
      viewport: { width: 720, height: 960 },
      deviceScaleFactor: 1,
      locale: 'en-US',
      timezoneId: 'UTC',
    });
    await page.route('**/api/pxa/v1/auth/csrf', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ token: 'visual-parity-csrf' }),
    }));
    await page.route('**/api/pxa/v1/designer/symbols/render', async route => {
      const request = route.request();
      let type;
      try { type = request.postDataJSON()?.type; } catch { type = undefined; }
      if (type !== 'qrcode' && type !== 'barcode') {
        await route.fulfill({ status: 400, contentType: 'application/problem+json', body: '{}' });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'image/png',
        path: path.join(raw, `${type}.png`),
      });
    });
    await page.route('**/api/pxa/v1/designer/preferences/element-favorites', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ elementTypes: [] }),
    }));
    const geometry = {};
    const expectedSymbols = fixture.pages
      .flatMap(pageFixture => pageFixture.elements)
      .filter(element => element.type === 'qrcode' || element.type === 'barcode').length;
    for (const surface of ['canvas', 'preview']) {
      await page.goto(`${baseUrl}/__visual-parity?surface=${surface}&fixture=${fixtureKey}`, { waitUntil: 'networkidle' });
      await page.locator(`[data-parity-surface="${surface}"]`).waitFor();
      try {
        if (expectedSymbols > 0) await page.waitForFunction(expected => {
          const symbols = [...document.querySelectorAll('[data-symbol-status]')];
          const images = symbols.flatMap(symbol => [...symbol.querySelectorAll('img')]);
          return symbols.length === expected &&
            symbols.every(symbol => symbol.getAttribute('data-symbol-status') === 'ready') &&
            images.length === expected && images.every(image => image.complete && image.naturalWidth > 0);
        }, expectedSymbols, { timeout: 5_000 });
      } catch (error) {
        const state = await page.evaluate(() => [...document.querySelectorAll('[data-symbol-status]')].map(symbol => ({
          type: symbol.getAttribute('data-symbol-type'),
          status: symbol.getAttribute('data-symbol-status'),
          error: symbol.getAttribute('data-symbol-error'),
          html: symbol.innerHTML.slice(0, 300),
          images: [...symbol.querySelectorAll('img')].map(image => ({
            source: image.src,
            complete: image.complete,
            naturalWidth: image.naturalWidth,
            naturalHeight: image.naturalHeight,
          })),
        })));
        throw new Error(`Symbol images did not become ready: ${JSON.stringify(state)}`, { cause: error });
      }
      await page.evaluate(async () => { await document.fonts.ready; });
      await page.waitForTimeout(300);
      geometry[surface] = {};
      const pages = page.locator('[data-parity-page]');
      for (let index = 0; index < await pages.count(); index += 1) {
        const pageNumber = index + 1;
        const pageLocator = pages.nth(index);
        await pageLocator.screenshot({ path: path.join(raw, `${surface}-page-${pageNumber}.png`) });
        const pageBox = await pageLocator.boundingBox();
        const elements = pageLocator.locator('[data-element-id]');
        for (let elementIndex = 0; elementIndex < await elements.count(); elementIndex += 1) {
          const element = elements.nth(elementIndex);
          const id = await element.getAttribute('data-element-id');
          const box = await element.boundingBox();
          const layoutBox = await element.evaluate(node => ({
            x: node.offsetLeft,
            y: node.offsetTop,
            width: node.offsetWidth,
            height: node.offsetHeight,
          }));
          if (!id || !box || !pageBox) continue;
          const elementDir = path.join(raw, 'elements', id);
          await mkdir(elementDir, { recursive: true });
          // Capture the canonical document box instead of the transformed DOM
          // bounding box. Rotated elements otherwise produce larger screenshots
          // than the matching fixed PDF crop.
          await page.screenshot({
            path: path.join(elementDir, `${surface}.png`),
            clip: {
              x: pageBox.x + layoutBox.x,
              y: pageBox.y + layoutBox.y,
              width: layoutBox.width,
              height: layoutBox.height,
            },
          });
          geometry[surface][id] = {
            page: pageNumber,
            x: layoutBox.x,
            y: layoutBox.y,
            width: layoutBox.width,
            height: layoutBox.height,
          };
        }
      }
    }
    await browser.close();
    await server.close();
    return geometry;
  } catch (error) {
    await server.close();
    throw error;
  }
}

/**
 * A PDF point is a CSS pixel here, so the reference bitmap has to come out at 72 DPI. It is rendered
 * at 4x and averaged down rather than rasterized at 72 DPI directly.
 *
 * At 72 DPI a 9pt line has a 9-pixel em, and poppler grid-fits every glyph to that coarse raster.
 * The rounding thickens and nudges small text by a fraction of a pixel per glyph, which a browser -
 * positioning glyphs on subpixels - does not do, so a direct 72 DPI render charges the Designer for
 * the hinter's error. Supersampling keeps the grid-fitting error well under a pixel before the
 * downsample, so what the comparison measures is the geometry the PDF encodes. On the e-invoice
 * fixture it turned four findings into exact matches and roughly halved the rest.
 *
 * `-filter Box` averages the 4x4 block, which is what an ideal rasterizer's coverage-based
 * antialiasing computes; a sharpening filter would invent edge contrast that is in neither surface.
 * Both the backend and the C# PDF go through this identical path, so their parity is unaffected.
 */
const RASTER_SUPERSAMPLE = 4;

async function rasterizePdf(name) {
  const prefix = path.join(raw, `${name}-page`);
  const supersampled = path.join(raw, `${name}-supersampled`);
  run('pdftoppm', ['-r', String(72 * RASTER_SUPERSAMPLE), '-png', path.join(raw, `${name}.pdf`), supersampled]);
  // pdftoppm pads the page number to the width of the last page, so a ten-page document emits
  // `-01`, not `-1`. Read back what it actually wrote instead of assuming either form.
  const base = path.basename(supersampled);
  const written = (await readdir(raw))
    .filter(file => file.startsWith(`${base}-`) && file.endsWith('.png'))
    .map(file => ({ file, page: Number(file.slice(base.length + 1, -4)) }))
    .filter(entry => Number.isInteger(entry.page))
    .sort((left, right) => left.page - right.page);

  for (const [index, entry] of written.entries()) {
    const source = path.join(raw, entry.file);
    run('magick', [source, '-filter', 'Box', '-resize', `${100 / RASTER_SUPERSAMPLE}%`, `${prefix}-${index + 1}.png`]);
    rmSync(source);
  }
}

function parseRmse(stderr) {
  const match = String(stderr).match(/\(([^)]+)\)/);
  return match ? Number(match[1]) : null;
}

function imageSize(file) {
  const result = run('magick', ['identify', '-format', '%w %h', file]);
  const [width, height] = result.stdout.trim().split(/\s+/).map(Number);
  return { width, height };
}

function compareImages(actual, expected, prefix) {
  const actualSize = imageSize(actual);
  const expectedSize = imageSize(expected);
  const normalized = `${prefix}-normalized.png`;
  if (actualSize.width !== expectedSize.width || actualSize.height !== expectedSize.height) {
    run('magick', [actual, '-background', 'white', '-gravity', 'northwest', '-extent',
      `${expectedSize.width}x${expectedSize.height}`, normalized]);
    actual = normalized;
  }
  const diff = `${prefix}-diff.png`;
  const comparison = spawnSync('compare', ['-metric', 'RMSE', '-highlight-color', '#ff0066',
    '-lowlight-color', '#ffffff', actual, expected, diff], { encoding: 'utf8' });
  if (![0, 1].includes(comparison.status)) {
    throw new Error(`Image comparison failed: ${comparison.stderr}`);
  }
  const overlay = `${prefix}-overlay.png`;
  run('magick', [expected, '(', actual, '-alpha', 'set', '-channel', 'A', '-evaluate', 'set', '45%',
    '+channel', ')', '-compose', 'over', '-composite', overlay]);
  const perceptualActual = `${prefix}-perceptual-actual.png`;
  const perceptualExpected = `${prefix}-perceptual-expected.png`;
  run('magick', [actual, '-blur', '0x1', perceptualActual]);
  run('magick', [expected, '-blur', '0x1', perceptualExpected]);
  const perceptual = spawnSync('compare', ['-metric', 'RMSE', perceptualActual, perceptualExpected, '/dev/null'],
    { encoding: 'utf8' });
  if (![0, 1].includes(perceptual.status)) {
    throw new Error(`Perceptual image comparison failed: ${perceptual.stderr}`);
  }
  return {
    rmse: parseRmse(comparison.stderr),
    perceptualRmse: parseRmse(perceptual.stderr),
    actualSize,
    expectedSize,
    diff: path.relative(output, diff),
    overlay: path.relative(output, overlay),
  };
}

async function cropPdfElements(fixture) {
  for (const [pageIndex, page] of fixture.pages.entries()) {
    for (const element of page.elements) {
      const elementDir = path.join(raw, 'elements', element.id);
      await mkdir(elementDir, { recursive: true });
      const geometry = `${Math.max(1, Math.round(element.width))}x${Math.max(1, Math.round(element.height))}` +
        `+${Math.round(element.x)}+${Math.round(element.y)}`;
      for (const pdf of ['backend', 'csharp']) {
        run('magick', [path.join(raw, `${pdf}-page-${pageIndex + 1}.png`), '-crop', geometry, '+repage',
          path.join(elementDir, `${pdf}.png`)]);
      }
    }
  }
}

async function verifyEmbeddedSymbolSources(fixture) {
  const result = {};
  const symbols = [...new Set(fixture.pages
    .flatMap(page => page.elements)
    .map(element => element.type)
    .filter(type => type === 'qrcode' || type === 'barcode'))];
  if (symbols.length === 0) return result;
  for (const pdf of ['backend', 'csharp']) {
    const extractedDirectory = path.join(raw, `extracted-${pdf}`);
    await rm(extractedDirectory, { recursive: true, force: true });
    await mkdir(extractedDirectory, { recursive: true });
    run('pdfimages', ['-png', path.join(raw, `${pdf}.pdf`), path.join(extractedDirectory, 'image')]);
    const extractedFiles = (await readdir(extractedDirectory))
      .filter(file => file.endsWith('.png'))
      .map(file => path.join(extractedDirectory, file));

    for (const symbol of symbols) {
      const source = path.join(raw, `${symbol}.png`);
      const sourceSize = imageSize(source);
      const candidates = extractedFiles.filter(file => {
        const size = imageSize(file);
        return size.width === sourceSize.width && size.height === sourceSize.height;
      });
      if (candidates.length !== 1)
        throw new Error(`Expected one ${symbol} image in ${pdf}.pdf, found ${candidates.length}.`);

      const normalizedSource = path.join(extractedDirectory, `${symbol}-source-gray.png`);
      run('magick', [source, '-colorspace', 'Gray', normalizedSource]);
      const comparison = spawnSync('compare', ['-metric', 'AE', normalizedSource, candidates[0], '/dev/null'],
        { encoding: 'utf8' });
      if (![0, 1].includes(comparison.status))
        throw new Error(`Could not verify ${symbol} source in ${pdf}.pdf: ${comparison.stderr}`);
      const differentPixels = Number.parseInt(String(comparison.stderr).trim().split(/\s+/)[0], 10);
      if (differentPixels !== 0)
        throw new Error(`${symbol} source differs from the image embedded in ${pdf}.pdf by ${differentPixels} pixels.`);

      result[symbol] ??= {};
      result[symbol][pdf] = { exactPixels: true, width: sourceSize.width, height: sourceSize.height };
    }
  }
  return result;
}

function htmlEscape(value) {
  return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
}

function severity(rmse, geometryDrift) {
  if (geometryDrift > 1 || (rmse ?? 0) > 0.2) return 'P0';
  if ((rmse ?? 0) > 0.08) return 'P1';
  if ((rmse ?? 0) > 0.02) return 'P2';
  return 'match';
}

async function buildReport(fixture, geometry, symbolSources, enforceThresholds) {
  const pages = [];
  const elements = [];
  for (const [pageIndex, page] of fixture.pages.entries()) {
    const pageNumber = pageIndex + 1;
    const pageComparisons = {};
    for (const surface of ['canvas', 'preview', 'csharp']) {
      pageComparisons[surface] = compareImages(
        path.join(raw, `${surface}-page-${pageNumber}.png`),
        path.join(raw, `backend-page-${pageNumber}.png`),
        path.join(raw, `${surface}-vs-backend-page-${pageNumber}`),
      );
    }
    pages.push({ page: pageNumber, id: page.id, comparisons: pageComparisons });

    for (const element of page.elements) {
      const expected = { x: element.x, y: element.y, width: element.width, height: element.height };
      const comparisons = {};
      let maxGeometryDrift = 0;
      for (const surface of ['canvas', 'preview']) {
        const measured = geometry[surface]?.[element.id];
        if (measured) {
          maxGeometryDrift = Math.max(maxGeometryDrift,
            Math.abs(measured.x - expected.x), Math.abs(measured.y - expected.y),
            Math.abs(measured.width - expected.width), Math.abs(measured.height - expected.height));
        }
      }
      for (const surface of ['canvas', 'preview', 'csharp']) {
        comparisons[surface] = compareImages(
          path.join(raw, 'elements', element.id, `${surface}.png`),
          path.join(raw, 'elements', element.id, 'backend.png'),
          path.join(raw, 'elements', element.id, `${surface}-vs-backend`),
        );
      }
      const maxRmse = Math.max(...Object.values(comparisons).map(value => value.perceptualRmse ?? 1));
      const sourceVerification = symbolSources[element.type];
      const measuredSeverity = severity(maxRmse, maxGeometryDrift);
      const resolvedSeverity = sourceVerification?.backend?.exactPixels && sourceVerification?.csharp?.exactPixels &&
        measuredSeverity === 'P1' ? 'P2' : measuredSeverity;
      elements.push({ id: element.id, type: element.type, page: pageNumber, expected,
        geometry: { canvas: geometry.canvas?.[element.id], preview: geometry.preview?.[element.id], maxDrift: maxGeometryDrift },
        comparisons, sourceVerification, severity: resolvedSeverity });
    }
  }
  const report = {
    generatedAt: new Date().toISOString(),
    mode: enforceThresholds ? 'enforced' : 'report-only',
    authoritativeSurface: 'backend',
    thresholdsApproved: true,
    comparison: {
      severityMetric: 'perceptualRmse',
      rawMetric: 'rmse',
      antiAliasingTolerance: 'Gaussian blur 0x1 on both inputs',
      rasterSourcePolicy: 'Exact source pixels embedded in backend and C# PDFs cap QR/barcode rasterizer-only findings at P2.',
    },
    summary: {
      pages: pages.length,
      elements: elements.length,
      elementTypes: new Set(elements.map(element => element.type)).size,
      findings: Object.fromEntries(['P0', 'P1', 'P2', 'match'].map(level =>
        [level, elements.filter(element => element.severity === level).length])),
    },
    pages,
    elements,
  };
  await writeFile(path.join(output, 'visual-parity-report.json'), JSON.stringify(report, null, 2));

  const rows = elements.map(element => {
    const dir = `raw/elements/${encodeURIComponent(element.id)}`;
    const score = surface => element.comparisons[surface].rmse?.toFixed(5) ?? 'n/a';
    const perceptualScore = surface => element.comparisons[surface].perceptualRmse?.toFixed(5) ?? 'n/a';
    const sourceNote = element.sourceVerification ? '<br><small>embedded source pixels verified</small>' : '';
    return `<tr class="${element.severity.toLowerCase()}"><td><strong>${htmlEscape(element.id)}</strong><br><small>${htmlEscape(element.type)} · page ${element.page}</small>${sourceNote}</td>` +
      `<td>${element.severity}<br><small>geometry ${element.geometry.maxDrift.toFixed(2)} pt</small></td>` +
      ['canvas', 'preview', 'backend', 'csharp'].map(surface =>
        `<td><img src="${dir}/${surface}.png" alt="${surface} ${htmlEscape(element.id)}"><small>${surface === 'backend' ? 'reference' : `raw ${score(surface)} · perceptual ${perceptualScore(surface)}`}</small></td>`).join('') +
      `<td><img src="${dir}/preview-vs-backend-diff.png" alt="diff ${htmlEscape(element.id)}"><small>Preview diff</small></td></tr>`;
  }).join('\n');
  const pageCards = pages.map(page => `<section><h2>Page ${page.page}: ${htmlEscape(page.id)}</h2><div class="pages">` +
    ['canvas', 'preview', 'backend', 'csharp'].map(surface =>
      `<figure><img src="raw/${surface}-page-${page.page}.png" alt="${surface} page ${page.page}"><figcaption>${surface}${surface === 'backend' ? ' (reference)' : ` · RMSE ${page.comparisons[surface]?.rmse?.toFixed(5) ?? 'n/a'}`}</figcaption></figure>`).join('') +
    `</div></section>`).join('\n');
  await writeFile(path.join(output, 'visual-parity-report.html'), `<!doctype html><html><head><meta charset="utf-8"><title>PXA visual parity</title><style>
body{font-family:Arial,sans-serif;margin:24px;color:#172033;background:#f4f7fa}h1,h2{letter-spacing:0}.summary{display:flex;gap:12px;margin:20px 0}.summary span{padding:8px 12px;background:#fff;border:1px solid #dbe2ea;border-radius:4px}.pages{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}.pages figure{margin:0}.pages img{width:100%;border:1px solid #aeb9c7;background:#fff}figcaption,small{display:block;color:#5b6777;margin-top:4px}table{width:100%;border-collapse:collapse;background:#fff}th,td{padding:8px;border:1px solid #dbe2ea;vertical-align:top}td img{width:150px;max-height:110px;object-fit:contain;object-position:left top;background:#fff}.p0{background:#fff1f2}.p1{background:#fff7ed}.p2{background:#fffbeb}.match{background:#f0fdf4}@media(max-width:1000px){.pages{grid-template-columns:1fr 1fr}table{display:block;overflow:auto}}</style></head><body>
<h1>PXA Designer and PDF Visual Parity</h1><p>Backend PDF is the authoritative printable surface. Approved thresholds reject P0/P1 findings and geometry drift above one point${enforceThresholds ? ' in this enforced run' : ''}.</p>
<div class="summary"><span>${report.summary.pages} pages</span><span>${report.summary.elements} elements</span><span>${report.summary.elementTypes} element types</span><span>P0 ${report.summary.findings.P0}</span><span>P1 ${report.summary.findings.P1}</span><span>P2 ${report.summary.findings.P2}</span></div>
${pageCards}<h2>Per-element comparison</h2><table><thead><tr><th>Element</th><th>Status</th><th>Canvas</th><th>Preview</th><th>Backend PDF</th><th>C# PDF</th><th>Difference</th></tr></thead><tbody>${rows}</tbody></table></body></html>`);
  return report;
}

async function main() {
  requireTool('pdftoppm');
  requireTool('magick');
  requireTool('compare');
  requireTool('pdfimages');
  const reuseArtifacts = process.env.PXA_VISUAL_PARITY_REUSE_ARTIFACTS === 'true';
  const enforceThresholds = process.env.PXA_VISUAL_PARITY_ENFORCE === 'true';
  if (!reuseArtifacts) await rm(output, { recursive: true, force: true });
  await mkdir(raw, { recursive: true });
  const fixture = JSON.parse(await readFile(fixturePath, 'utf8'));
  if (!reuseArtifacts) {
    process.stdout.write('Generating backend and C# PDF artifacts...\n');
    run('dotnet', ['test', 'tests/PXA.Api.Tests/PXA.Api.Tests.csproj', '--no-restore', '-m:1',
      '-p:BuildInParallel=false', '--filter', 'FullyQualifiedName~VisualParityArtifactTests', '--nologo'], {
      env: {
        ...process.env,
        PXA_VISUAL_PARITY_ARTIFACTS: raw,
        PXA_VISUAL_PARITY_FIXTURE: fixturePath,
      },
    });
  } else {
    process.stdout.write('Reusing existing backend and C# PDF artifacts...\n');
  }
  process.stdout.write('Capturing Canvas and Live Preview...\n');
  const geometry = await renderBrowserSurfaces(fixture);
  process.stdout.write('Rasterizing backend PDFs (4x supersampled to 72 DPI)...\n');
  await rasterizePdf('backend');
  await rasterizePdf('csharp');
  const symbolSources = await verifyEmbeddedSymbolSources(fixture);
  process.stdout.write('Creating per-element crops and comparisons...\n');
  await cropPdfElements(fixture);
  const report = await buildReport(fixture, geometry, symbolSources, enforceThresholds);
  process.stdout.write(`Visual parity report: ${path.join(output, 'visual-parity-report.html')}\n`);
  process.stdout.write(`Findings: ${JSON.stringify(report.summary.findings)}\n`);
  if (enforceThresholds) {
    const materialFindings = report.elements.filter(element => element.severity === 'P0' || element.severity === 'P1');
    const csharpDifferences = report.elements.filter(element => element.comparisons.csharp.rmse !== 0);
    if (materialFindings.length > 0 || csharpDifferences.length > 0) {
      const details = [
        ...materialFindings.map(element => `${element.id}:${element.severity}`),
        ...csharpDifferences.map(element => `${element.id}:csharp-pdf-difference`),
      ];
      throw new Error(`Approved visual-parity thresholds failed: ${details.join(', ')}`);
    }
  }
}

main().catch(error => {
  process.stderr.write(`${error.stack ?? error}\n`);
  process.exitCode = 1;
});
