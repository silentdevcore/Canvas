import { readPvadEnvironment } from '../configuration/environment.mjs';
import { createHash } from 'node:crypto';
import { mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const corpusRoot = path.join(root, 'websites', 'PVAD.Documentation', 'public', 'downloads', 'pdf-trust');
const baselinePath = path.join(root, 'test-assets', 'pdf-trust', 'visual-baseline.json');
const outputRoot = path.join(root, 'artifacts', 'pdf-trust-visual');
const dpi = 72;

function run(command, args) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', stdio: 'pipe' });
  if (result.status !== 0)
    throw new Error(`${command} ${args.join(' ')} failed\n${result.stdout ?? ''}\n${result.stderr ?? ''}`);
  return result;
}

function requireTool(name) {
  const result = spawnSync('sh', ['-c', `command -v ${name}`], { encoding: 'utf8' });
  if (result.status !== 0) throw new Error(`PDF Trust visual regression requires '${name}' on PATH.`);
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function parseIdentify(output) {
  const [width, height] = output.trim().split(/\s+/).map(Number);
  if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1)
    throw new Error(`Invalid raster dimensions: ${output}`);
  return { width, height };
}

function validateBaseline(baseline) {
  if (baseline.schemaVersion !== 1 || baseline.dpi !== dpi || !Array.isArray(baseline.entries))
    throw new Error('PDF Trust visual baseline has an unsupported schema or DPI.');
  const ids = new Set();
  for (const entry of baseline.entries) {
    if (!entry.id || ids.has(entry.id) || !entry.pdfSha256 || !Array.isArray(entry.pages) || entry.pages.length === 0)
      throw new Error(`Invalid or duplicate PDF Trust visual baseline entry: ${entry.id ?? '<missing>'}`);
    ids.add(entry.id);
    for (const page of entry.pages) {
      if (!Number.isInteger(page.page) || page.page < 1 || !page.pngSha256 ||
        !Number.isInteger(page.width) || page.width < 1 ||
        !Number.isInteger(page.height) || page.height < 1)
        throw new Error(`Invalid baseline page in entry '${entry.id}'.`);
    }
  }
  return baseline;
}

async function loadCorpus() {
  const manifest = JSON.parse(await readFile(path.join(corpusRoot, 'corpus.json'), 'utf8'));
  const selected = new Map([
    ['signed', 'digital-signature'],
    ['redaction-risk', 'redaction'],
    ['archived', 'pdf-a-2b'],
    ['accessible', 'pdf-ua-1'],
  ]);
  return [...selected].map(([id, trustSurface]) => {
    const entry = manifest.entries.find(candidate => candidate.id === id);
    if (!entry) throw new Error(`PDF Trust corpus is missing required fixture '${id}'.`);
    return { id, trustSurface, path: path.join(corpusRoot, entry.pdf), expectedSha256: entry.sha256 };
  });
}

async function rasterize(fixture) {
  const directory = path.join(outputRoot, fixture.id);
  await mkdir(directory, { recursive: true });
  const prefix = path.join(directory, 'page');
  run('pdftoppm', ['-r', String(dpi), '-png', fixture.path, prefix]);
  const files = (await readdir(directory))
    .filter(file => file.startsWith('page-') && file.endsWith('.png'))
    .sort((left, right) => left.localeCompare(right, undefined, { numeric: true }));
  if (files.length === 0) throw new Error(`No pages were rasterized for '${fixture.id}'.`);
  const pages = [];
  for (const [index, file] of files.entries()) {
    const absolute = path.join(directory, file);
    const bytes = await readFile(absolute);
    pages.push({
      page: index + 1,
      file: path.relative(outputRoot, absolute),
      pngSha256: sha256(bytes),
      ...parseIdentify(run('magick', ['identify', '-format', '%w %h', absolute]).stdout),
    });
  }
  return pages;
}

async function main() {
  for (const tool of ['pdftoppm', 'magick']) requireTool(tool);
  await rm(outputRoot, { recursive: true, force: true });
  await mkdir(outputRoot, { recursive: true });
  const updateBaseline = readPvadEnvironment('PVAD_PDF_TRUST_VISUAL_UPDATE_BASELINE') === 'true';
  const baseline = updateBaseline
    ? { schemaVersion: 1, dpi, entries: [] }
    : validateBaseline(JSON.parse(await readFile(baselinePath, 'utf8')));
  const fixtures = await loadCorpus();
  const results = [];
  for (const fixture of fixtures) {
    const pdfBytes = await readFile(fixture.path);
    const actualPdfSha256 = sha256(pdfBytes);
    if (actualPdfSha256 !== fixture.expectedSha256)
      throw new Error(`PDF Trust fixture '${fixture.id}' does not match corpus.json sha256.`);
    const pages = await rasterize(fixture);
    const expected = baseline.entries.find(entry => entry.id === fixture.id);
    const changed = actualPdfSha256 !== expected?.pdfSha256 ||
      pages.length !== expected?.pages.length ||
      pages.some((page, index) => {
        const golden = expected.pages[index];
        return !golden || page.width !== golden.width || page.height !== golden.height || page.pngSha256 !== golden.pngSha256;
      });
    results.push({
      id: fixture.id,
      trustSurface: fixture.trustSurface,
      pdfSha256: actualPdfSha256,
      expectedPdfSha256: expected?.pdfSha256,
      pages,
      status: changed ? 'changed' : 'match',
    });
  }

  const report = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    mode: readPvadEnvironment('PVAD_PDF_TRUST_VISUAL_ENFORCE') === 'true' ? 'enforced' : 'report-only',
    rasterizer: { command: 'pdftoppm', dpi, format: 'png' },
    surfaces: ['digital-signature', 'redaction', 'pdf-a-2b', 'pdf-ua-1'],
    results,
    summary: {
      fixtures: results.length,
      changed: results.filter(result => result.status === 'changed').length,
      matching: results.filter(result => result.status === 'match').length,
    },
  };
  if (updateBaseline) {
    await writeFile(baselinePath, `${JSON.stringify({
      schemaVersion: 1,
      dpi,
      description: 'Deterministic PDF Trust visual baselines generated from the checked-in synthetic corpus.',
      entries: results.map(result => ({
        id: result.id,
        pdfSha256: result.pdfSha256,
        pages: result.pages.map(({ page, pngSha256, width, height }) => ({ page, pngSha256, width, height })),
      })),
    }, null, 2)}\n`);
    report.mode = 'baseline-update';
  }
  await writeFile(path.join(outputRoot, 'pdf-trust-visual-report.json'), `${JSON.stringify(report, null, 2)}\n`);
  const changed = results.filter(result => result.status === 'changed');
  process.stdout.write(`PDF Trust visual regression: ${report.summary.matching}/${report.summary.fixtures} fixtures match\n`);
  if (changed.length > 0 && report.mode === 'enforced')
    throw new Error(`PDF Trust visual baseline changed: ${changed.map(result => result.id).join(', ')}`);
}

if (process.argv[1] === fileURLToPath(import.meta.url))
  main().catch(error => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

export { validateBaseline };
