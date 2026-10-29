import { readPvadEnvironment } from '../configuration/environment.mjs';
import { createHash } from 'node:crypto';
import { mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { requireImageMagick, spawnImageMagick } from './image-tools.mjs';
import { trustBaselinePath } from './trust-baseline.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const outputRoot = path.join(root, 'artifacts', 'pdf-trust-operations');
const baselinePath = trustBaselinePath(root, 'operation-visual-baseline');
const dpi = 72;
const operations = ['signature', 'redaction', 'pdf-a-2b', 'pdf-ua-1'];

function run(command, args, extraEnv = {}) {
  const execute = command === 'magick' ? spawnImageMagick : (args, options) => spawnSync(command, args, options);
  const result = execute(args, {
    cwd: root,
    env: { ...process.env, ...extraEnv },
    encoding: 'utf8',
    stdio: 'pipe'
  });
  if (result.status !== 0)
    throw new Error(`${command} ${args.join(' ')} failed\n${result.stdout ?? ''}\n${result.stderr ?? ''}`);
  return result;
}

function writeDiffMask(sourcePath, outputPath, diffMaskPath) {
  const result = spawnImageMagick([
    'compare', '-metric', 'AE',
    '-highlight-color', '#ff0000',
    '-lowlight-color', '#ffffff',
    sourcePath, outputPath, diffMaskPath
  ], {
    cwd: root,
    env: process.env,
    encoding: 'utf8',
    stdio: 'pipe'
  });
  if (result.status !== 0 && result.status !== 1)
    throw new Error(`magick compare failed\n${result.stdout ?? ''}\n${result.stderr ?? ''}`);
  const metric = Number.parseInt((result.stderr || result.stdout).trim().split(/\s+/)[0], 10);
  if (!Number.isFinite(metric))
    throw new Error(`Unable to read ImageMagick pixel difference for '${sourcePath}'.`);
  return metric;
}

function requireTool(name) {
  if (name === 'magick') return requireImageMagick();
  const result = spawnSync('sh', ['-c', `command -v ${name}`], { encoding: 'utf8' });
  if (result.status !== 0) throw new Error(`PDF Trust operation visuals require '${name}' on PATH.`);
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
    throw new Error('PDF Trust operation baseline has an unsupported schema or DPI.');
  const ids = new Set();
  for (const entry of baseline.entries) {
    if (!operations.includes(entry.id) || ids.has(entry.id) ||
      !Array.isArray(entry.sourcePages) || !Array.isArray(entry.outputPages) ||
      entry.sourcePages.length === 0 || entry.outputPages.length === 0 ||
      entry.sourcePages.length !== entry.outputPages.length)
      throw new Error(`Invalid or duplicate operation baseline entry: ${entry.id ?? '<missing>'}`);
    ids.add(entry.id);
    for (const pages of [entry.sourcePages, entry.outputPages]) {
      const pageNumbers = new Set();
      for (const page of pages) {
        if (!Number.isInteger(page.page) || page.page < 1 || pageNumbers.has(page.page) ||
          !/^[a-f0-9]{64}$/.test(page.pngSha256) ||
          !Number.isInteger(page.width) || page.width < 1 ||
          !Number.isInteger(page.height) || page.height < 1)
          throw new Error(`Invalid page baseline in operation '${entry.id}'.`);
        pageNumbers.add(page.page);
      }
      if (pages.some((page, index) => page.page !== index + 1))
        throw new Error(`Non-contiguous page baseline in operation '${entry.id}'.`);
    }
  }
  if (ids.size !== operations.length) throw new Error('PDF Trust operation baseline is incomplete.');
  return baseline;
}

async function rasterize(pdfPath, operation, side) {
  const directory = path.join(outputRoot, operation, side);
  await mkdir(directory, { recursive: true });
  run('pdftoppm', ['-r', String(dpi), '-png', pdfPath, path.join(directory, 'page')]);
  const files = (await readdir(directory))
    .filter(file => file.startsWith('page-') && file.endsWith('.png'))
    .sort((left, right) => left.localeCompare(right, undefined, { numeric: true }));
  if (files.length === 0) throw new Error(`No pages were rasterized for '${operation}/${side}'.`);
  return Promise.all(files.map(async (file, index) => {
    const absolute = path.join(directory, file);
    const bytes = await readFile(absolute);
    return {
      page: index + 1,
      file: path.relative(outputRoot, absolute),
      pngSha256: sha256(bytes),
      ...parseIdentify(run('magick', ['identify', '-format', '%w %h', absolute]).stdout),
    };
  }));
}

async function createReviewArtifacts(operation, sourcePages, outputPages) {
  const pages = [];
  for (let index = 0; index < sourcePages.length; index += 1) {
    const source = path.join(outputRoot, sourcePages[index].file);
    const output = path.join(outputRoot, outputPages[index].file);
    const reviewDirectory = path.join(outputRoot, operation, 'review');
    await mkdir(reviewDirectory, { recursive: true });
    const pageName = `page-${String(index + 1).padStart(3, '0')}`;
    const sideBySide = path.join(reviewDirectory, `${pageName}-side-by-side.png`);
    const overlay = path.join(reviewDirectory, `${pageName}-overlay.png`);
    const diffMask = path.join(reviewDirectory, `${pageName}-diff-mask.png`);

    run('magick', [source, output, '+append', sideBySide]);
    run('magick', [
      source, output, '-alpha', 'set', '-compose', 'blend',
      '-define', 'compose:args=50,50', '-composite', overlay
    ]);
    const pixelDifferences = writeDiffMask(source, output, diffMask);
    const pixelCount = sourcePages[index].width * sourcePages[index].height;
    pages.push({
      page: index + 1,
      source: sourcePages[index].file,
      output: outputPages[index].file,
      sideBySide: path.relative(outputRoot, sideBySide),
      overlay: path.relative(outputRoot, overlay),
      diffMask: path.relative(outputRoot, diffMask),
      pixelDifferences,
      changedPixelRatio: pixelDifferences / pixelCount,
    });
  }
  return pages;
}

function assertSamePageGeometry(sourcePages, outputPages, operation) {
  if (sourcePages.length !== outputPages.length)
    throw new Error(`${operation} changed the page count during a Trust operation.`);
  sourcePages.forEach((source, index) => {
    const output = outputPages[index];
    if (source.width !== output.width || source.height !== output.height)
      throw new Error(`${operation} changed page ${source.page} geometry during a Trust operation.`);
  });
}

async function main() {
  for (const tool of ['pdftoppm', 'magick']) requireTool(tool);
  await rm(outputRoot, { recursive: true, force: true });
  await mkdir(outputRoot, { recursive: true });

  const artifactDirectory = path.join(outputRoot, 'generated');
  run('dotnet', [
    'test', 'tests/PVAD.Importer.Tests/PVAD.Importer.Tests.csproj',
    '--no-restore', '--disable-build-servers',
    '--filter', 'FullyQualifiedName~PdfTrustVisualArtifactTests'
  ], { PVAD_PDF_TRUST_OPERATION_ARTIFACT_DIR: artifactDirectory });

  const updateBaseline = readPvadEnvironment('PVAD_PDF_TRUST_OPERATION_UPDATE_BASELINE') === 'true';
  const baseline = updateBaseline
    ? { schemaVersion: 1, dpi, entries: [] }
    : validateBaseline(JSON.parse(await readFile(baselinePath, 'utf8')));
  const results = [];

  for (const operation of operations) {
    const sourcePath = path.join(artifactDirectory, `source-${operation}.pdf`);
    const outputPath = path.join(artifactDirectory, `output-${operation}.pdf`);
    const [sourcePages, outputPages] = await Promise.all([
      rasterize(sourcePath, operation, 'source'),
      rasterize(outputPath, operation, 'output'),
    ]);
    assertSamePageGeometry(sourcePages, outputPages, operation);
    const reviewPages = await createReviewArtifacts(operation, sourcePages, outputPages);
    const expected = baseline.entries.find(entry => entry.id === operation);
    const changed = !expected ||
      JSON.stringify(sourcePages.map(({ page, pngSha256, width, height }) => ({ page, pngSha256, width, height }))) !==
        JSON.stringify(expected.sourcePages) ||
      JSON.stringify(outputPages.map(({ page, pngSha256, width, height }) => ({ page, pngSha256, width, height }))) !==
        JSON.stringify(expected.outputPages);
    results.push({ id: operation, sourcePages, outputPages, reviewPages, status: changed ? 'changed' : 'match' });
  }

  const report = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    mode: readPvadEnvironment('PVAD_PDF_TRUST_OPERATION_ENFORCE') === 'true' ? 'enforced' : 'report-only',
    baseline: path.relative(root, baselinePath),
    rasterizer: { command: 'pdftoppm', dpi, format: 'png' },
    operations: results,
    summary: {
      operations: results.length,
      changed: results.filter(result => result.status === 'changed').length,
      matching: results.filter(result => result.status === 'match').length,
    },
  };
  if (updateBaseline) {
    await writeFile(baselinePath, `${JSON.stringify({
      schemaVersion: 1,
      dpi,
      description: 'Operation output baselines generated from deterministic PVAD Trust service tests.',
      entries: results.map(result => ({
        id: result.id,
        sourcePages: result.sourcePages.map(({ page, pngSha256, width, height }) => ({ page, pngSha256, width, height })),
        outputPages: result.outputPages.map(({ page, pngSha256, width, height }) => ({ page, pngSha256, width, height })),
      })),
    }, null, 2)}\n`);
    report.mode = 'baseline-update';
  }
  await writeFile(path.join(outputRoot, 'pdf-trust-operations-report.json'), `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`PDF Trust operation visuals: ${report.summary.matching}/${report.summary.operations} operations match\n`);
  const changed = results.filter(result => result.status === 'changed');
  if (changed.length > 0 && report.mode === 'enforced')
    throw new Error(`PDF Trust operation baseline changed: ${changed.map(result => result.id).join(', ')}`);
}

if (process.argv[1] === fileURLToPath(import.meta.url))
  main().catch(error => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

export { validateBaseline };
