import { mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { spawnImageMagick, requireImageMagick } from './image-tools.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const output = path.join(root, 'artifacts', 'flow-table-parity');

function run(command, args, extra = {}) {
  const result = spawnSync(command, args, {
    cwd: root, encoding: 'utf8', timeout: 180_000,
    env: { ...process.env, ...extra }, maxBuffer: 8 * 1024 * 1024,
  });
  if (result.status !== 0)
    throw new Error(`${command} failed: ${result.error?.message ?? ''}\n${result.stdout ?? ''}\n${result.stderr ?? ''}`);
  return result.stdout;
}

function image(args) {
  const result = spawnImageMagick(args, { cwd: root, encoding: 'utf8', timeout: 30_000 });
  if (result.status !== 0) throw new Error(`ImageMagick failed: ${result.stderr ?? ''}`);
  return result.stdout;
}

async function main() {
  requireImageMagick();
  await rm(output, { recursive: true, force: true });
  await mkdir(output, { recursive: true });
  run('dotnet', ['test', 'tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj', '--no-build', '--no-restore',
    '--filter', 'FullyQualifiedName~StyledFlowMatchesIndependentlyPlannedExplicitPages', '--nologo'],
  { PVAD_FLOW_TABLE_ARTIFACTS: output });
  const manifest = JSON.parse(await readFile(path.join(output, 'pages.json'), 'utf8'));
  if (manifest.pages !== 3 || manifest.width !== 300 || manifest.height !== 180)
    throw new Error('Unexpected flow-table fixture geometry or page count.');
  for (const name of ['flow', 'explicit']) {
    run('pdftoppm', ['-r', '72', '-png', path.join(output, `${name}.pdf`), path.join(output, name)]);
    const files = (await readdir(output)).filter(file => new RegExp(`^${name}-\\d+\\.png$`).test(file));
    if (files.length !== manifest.pages) throw new Error(`Missing raster pages for ${name}.`);
  }
  const results = [];
  for (let page = 1; page <= manifest.pages; page++) {
    const flow = path.join(output, `flow-${page}.png`);
    const expected = path.join(output, `explicit-${page}.png`);
    for (const file of [flow, expected])
      if (image(['identify', '-format', '%w %h', file]).trim() !== '300 180')
        throw new Error(`Unexpected raster geometry on page ${page}.`);
    image([flow, expected, '-alpha', 'set', '-compose', 'blend', '-define', 'compose:args=50,50',
      '-composite', path.join(output, `overlay-${page}.png`)]);
    const diff = spawnImageMagick(['compare', '-metric', 'AE', flow, expected, path.join(output, `diff-${page}.png`)],
      { cwd: root, encoding: 'utf8', timeout: 30_000 });
    const changedPixels = Number(diff.stderr?.trim().split(/\s+/)[0]);
    const match = diff.status === 0 && Number.isFinite(changedPixels) && changedPixels === 0;
    results.push({ page, changedPixels: Number.isFinite(changedPixels) ? changedPixels : null, match });
  }
  await writeFile(path.join(output, 'report.json'), JSON.stringify({ dpi: 72, ...manifest, comparisons: results }, null, 2));
  await writeFile(path.join(output, 'report.html'), `<!doctype html><html lang="en"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Flow table visual comparison</title>
<style>body{font:14px system-ui;margin:24px;color:#202020}section{margin:24px 0}figure{margin:0}main{display:flex;gap:16px;flex-wrap:wrap}img{width:300px;height:180px;max-width:100%;object-fit:contain;border:1px solid #bbb}</style>
<h1>Flow table visual comparison</h1><p>Automatic pagination versus independently planned explicit pages, 72 DPI. No tolerance adjustment.</p>
${results.map(result => `<section><h2>Page ${result.page}: ${result.match ? 'Match' : 'Mismatch'}</h2><main>
${[['flow', 'Automatic pagination'], ['explicit', 'Explicit pages'], ['overlay', 'Overlay'], ['diff', 'Pixel difference']]
    .map(([file, label]) => `<figure><figcaption>${label}</figcaption><img src="${file}-${result.page}.png" alt="${label}, page ${result.page}"></figure>`).join('')}
</main></section>`).join('')}</html>`);
  if (results.some(result => !result.match)) throw new Error('Flow table raster output differs from the explicit-page contract.');
  console.log(`Flow table visual parity: ${results.length}/${results.length} pages match exactly.\n${path.join(output, 'report.html')}`);
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
