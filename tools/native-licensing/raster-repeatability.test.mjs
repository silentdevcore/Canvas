import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { test } from 'node:test';
import { main, summarize, variants } from './raster-repeatability.mjs';

function fixture() {
  const rows = [];
  for (const variant of variants) for (const dpi of [72, 150]) for (const landscape of [false, true]) for (let repeat = 0; repeat < 4; repeat++) {
    const scale = Math.fround(dpi / 72);
    rows.push({ variant, dpi, landscape, repeat, width: Math.trunc((landscape ? 160 : 120) * scale),
      height: Math.trunc((landscape ? 120 : 160) * scale), family: 'Synthetic font',
      lineWidths: [70, 60, 50], pixelSha256: 'a'.repeat(64), encodedSha256: 'b'.repeat(64),
      pathSha256: 'c'.repeat(64), sample: '#111827' });
  }
  return Array.from({ length: 16 }, () => ({ schemaVersion: 1, scope: 'fixed-synthetic-native-font-probe',
    diagnosticOnly: true, productRendererExecuted: false, baselineApproved: false,
    architecture: 'X64', rows: structuredClone(rows) }));
}

test('font diagnostics count all fixed renders without granting package or baseline acceptance', () => {
  const runs = fixture(), input = structuredClone(runs), report = summarize(runs);
  assert.equal(report.renderCount, 1536); assert.equal(report.cases.length, 24);
  assert.equal(report.unstableCases, 0);
  assert.equal(report.baselineApproved, false); assert.equal(report.productRendererExecuted, false);
  assert.equal(report.publicationPerformed, false); assert.equal(report.diagnosticOnly, true);
  assert.deepEqual(runs, input);
});

test('cross-process and intra-process instability are reported rather than filtered or retried', () => {
  const runs = fixture();
  runs[1].rows[0].pixelSha256 = 'd'.repeat(64);
  let report = summarize(runs);
  assert.equal(report.unstableCases, 1);
  assert.equal(report.cases[0].withinProcessStable, false);
  assert.equal(report.cases[0].acrossProcessesStable, false);
  for (let i = 0; i < 4; i++) runs[1].rows[i].pixelSha256 = 'd'.repeat(64);
  report = summarize(runs);
  assert.equal(report.cases[0].withinProcessStable, true);
  assert.equal(report.cases[0].acrossProcessesStable, false);
  assert.equal(report.cases[0].distinctPixelHashes.length, 2);
});

test('unknown, partial, duplicate, forged or approving native font results are rejected', () => {
  for (const mutate of [r => r.pop(), r => r[0].rows.pop(), r => r[0].baselineApproved = true,
    r => r[0].productRendererExecuted = true, r => r[0].scope = 'customer-render',
    r => r[1].architecture = 'Arm64', r => r[0].rows[0].variant = 'other',
    r => r[0].rows[0].dpi = 300, r => r[0].rows[0].landscape = 'false',
    r => r[0].rows[0].width++, r => r[0].rows[0].pixelSha256 = 'bad',
    r => r[0].rows[0].lineWidths = [], r => r[0].rows[0].repeat = 4,
    r => r[0].rows[0] = r[0].rows[1]]) {
    const runs = fixture(); mutate(runs); assert.throws(() => summarize(runs));
  }
  assert.throws(() => main(['--input=customer.json']), /No input/);
});

test('font investigation uses a fixed non-shipped native-only executable and cleans isolated builds', () => {
  const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
  const project = read('./NativeRasterProbe/NativeRasterProbe.csproj');
  assert.ok(project.includes('<IsPackable>false</IsPackable>'));
  assert.ok(!project.includes('ProjectReference'));
  assert.ok(project.includes('SkiaSharp" Version="3.119.2"'));
  const probe = read('./NativeRasterProbe/Program.cs');
  assert.ok(probe.includes('if (args.Length != 0) return 64;'));
  assert.ok(probe.includes('Synthetic converter content'));
  assert.ok(!probe.includes('File.') && !probe.includes('Environment.GetEnvironmentVariable'));
  assert.ok(!probe.includes('PvadLicense') && !probe.includes('ImageDocumentExporter'));
  const runner = read('./raster-repeatability.mjs');
  assert.ok(runner.includes("'--artifacts-path', build"));
  assert.ok(runner.includes('rmSync(build, { recursive: true, force: true })'));
  assert.ok(!runner.includes('DOTNET_TieredCompilation'));
});

test('Windows font diagnostics are isolated from required package checks and upload reports only', () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const workflow = require('js-yaml').load(readFileSync(new URL('../../.github/workflows/native-library-licensing.yml', import.meta.url), 'utf8'));
  const job = workflow.jobs['font-raster-diagnostics'];
  assert.equal(job['runs-on'], 'windows-2025');
  assert.equal(job['timeout-minutes'], 15);
  assert.deepEqual(workflow.permissions, { contents: 'read' });
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/raster-repeatability.mjs'));
  assert.ok(!JSON.stringify(job).includes('secrets.') && !JSON.stringify(job).includes('nuget push'));
  const upload = job.steps.find(step => step.uses?.startsWith('actions/upload-artifact@'));
  assert.equal(upload.if, 'always()');
  assert.deepEqual(upload.with.path.trim().split('\n').map(value => value.trim()), [
    'artifacts/native-font-repeatability/*/report.json', 'artifacts/native-font-repeatability/*/cleanup.json']);
  assert.ok(workflow.jobs['platform-package-protection'].steps.some(step => step.run === 'node tools/native-licensing/platform-package-check.mjs'));
});
