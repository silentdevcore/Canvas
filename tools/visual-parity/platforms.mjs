import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';
import playwright from '../../pvad-designer/node_modules/@playwright/test/index.js';
import { spawnImageMagick } from './image-tools.mjs';

export const PLATFORM_FIXTURES = ['font-families', 'date-format', 'date-historical', 'date-layout', 'all-elements'];
const root = fileURLToPath(new URL('../../', import.meta.url));

function version(command, args) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', windowsHide: true });
  if (result.status !== 0) throw new Error(`Cannot inspect ${command}: ${result.stderr ?? result.error}`);
  return `${result.stdout ?? ''}${result.stderr ?? ''}`.trim();
}

export async function runPlatformValidation() {
  const output = path.join(root, 'artifacts/platform-parity', `${process.platform}-${process.arch}`);
  mkdirSync(output, { recursive: true });
  const browser = await playwright.chromium.launch({ headless: true });
  let browserVersion;
  try { browserVersion = browser.version(); } finally { await browser.close(); }
  const magick = spawnImageMagick(['-version'], { encoding: 'utf8' });
  if (magick.status !== 0) throw new Error('Cannot inspect ImageMagick');
  const metadata = {
    schemaVersion: 1, sourceCommit: version('git', ['rev-parse', 'HEAD']), platform: process.platform,
    architecture: process.arch, osRelease: os.release(), runnerImage: process.env.ImageVersion ?? null,
    node: process.version, dotnetSdk: version('dotnet', ['--version']), browserVersion,
    playwright: JSON.parse(readFileSync(path.join(root, 'pvad-designer/node_modules/@playwright/test/package.json'))).version,
    poppler: version('pdftoppm', ['-v']), imageMagick: magick.stdout.trim(),
    dateFormattingContract: JSON.parse(readFileSync(path.join(root, 'product-metadata/designer-date-formatting.json'))),
    fonts: ['liberation/LiberationSans-Regular.ttf', 'liberation/LiberationSans-Bold.ttf',
      'liberation/LiberationSans-Italic.ttf', 'liberation/LiberationSans-BoldItalic.ttf',
      'liberation/LiberationSerif-Regular.ttf', 'liberation/LiberationSerif-Bold.ttf',
      'liberation/LiberationSerif-Italic.ttf', 'liberation/LiberationSerif-BoldItalic.ttf',
      'liberation/LiberationMono-Regular.ttf', 'liberation/LiberationMono-Bold.ttf',
      'liberation/LiberationMono-Italic.ttf', 'liberation/LiberationMono-BoldItalic.ttf',
      'noto-arabic/NotoSansArabic-Regular.ttf'].map(file => ({ file,
      sha256: createHash('sha256').update(readFileSync(path.join(root, 'assets/fonts', file))).digest('hex') })),
    fixtures: PLATFORM_FIXTURES, results: [],
    scope: 'Synthetic rendering only; P2 measurements remain visible and platform-specific visual approval is separate.',
  };
  const save = () => writeFileSync(path.join(output, 'environment.json'), JSON.stringify(metadata, null, 2) + '\n');
  save();
  let failed = false;
  for (const fixture of PLATFORM_FIXTURES) {
    console.log(`\n=== ${process.platform}/${process.arch}: ${fixture} ===`);
    const started = Date.now();
    const result = spawnSync(process.execPath, ['tools/visual-parity/run.mjs', '--bundled-browser'], {
      cwd: root, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, timeout: 12 * 60 * 1000,
      env: { ...process.env, PVAD_VISUAL_PARITY_FIXTURE: `test-assets/visual-parity/${fixture}.json`,
        PVAD_VISUAL_PARITY_ENFORCE: 'true', PVAD_VISUAL_PARITY_REUSE_ARTIFACTS: 'false' },
    });
    const destination = path.join(output, fixture);
    mkdirSync(destination, { recursive: true });
    writeFileSync(path.join(destination, 'run.log'), `${result.stdout ?? ''}\n${result.stderr ?? ''}\n${result.error ?? ''}`);
    const source = path.join(root, 'artifacts/visual-parity');
    if (existsSync(source)) cpSync(source, destination, { recursive: true });
    const report = path.join(destination, 'visual-parity-report.json');
    const summary = existsSync(report) ? JSON.parse(readFileSync(report)).summary : null;
    metadata.results.push({ fixture, exitCode: result.status, elapsedMs: Date.now() - started, summary });
    save();
    console.log((result.stdout ?? '').trim().split('\n').slice(-5).join('\n'));
    if (result.status !== 0) {
      failed = true;
      console.error((result.stderr || String(result.error ?? '')).slice(-5000));
    }
  }
  metadata.status = failed ? 'failed' : 'passed';
  save();
  const rows = metadata.results.map(entry => `<li><a href="${entry.fixture}/visual-parity-report.html">${entry.fixture}</a>: exit ${entry.exitCode}; ${entry.summary ? JSON.stringify(entry.summary.findings) : 'incomplete; inspect run.log'}</li>`).join('');
  writeFileSync(path.join(output, 'report.html'), `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Platform parity</title><style>body{font:16px system-ui;margin:24px;max-width:1100px}li{margin:16px 0}code{overflow-wrap:anywhere}</style><h1>${process.platform}/${process.arch}: ${metadata.status}</h1><p>Source <code>${metadata.sourceCommit}</code>; bundled Chromium ${browserVersion}. New backend and C# PDFs for each pack. Synthetic browser API/session responses are not a customer login test.</p><ul>${rows}</ul><p>P2 findings are measured, not automatically approved. Existing thresholds and other platform references are unchanged.</p><a href="environment.json">Environment and source evidence</a></html>\n`);
  if (failed) process.exitCode = 1;
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url)
  await runPlatformValidation();
