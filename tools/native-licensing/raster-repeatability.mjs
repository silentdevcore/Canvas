import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export const variants = ['default', 'grayscale', 'unhinted', 'linearMetrics', 'noEmbeddedBitmaps', 'outline'];
const hashPattern = /^[a-f0-9]{64}$/;

export function summarize(runs) {
  assert.equal(runs.length, 16, 'Sixteen independent processes are required.');
  for (const run of runs) {
    assert.equal(run.schemaVersion, 1);
    assert.equal(run.scope, 'fixed-synthetic-native-font-probe');
    assert.equal(run.diagnosticOnly, true);
    assert.equal(run.productRendererExecuted, false);
    assert.equal(run.baselineApproved, false);
    assert.ok(['X64', 'Arm64'].includes(run.architecture));
    assert.equal(run.architecture, runs[0].architecture);
    assert.equal(run.rows.length, 96);
    const names = new Set();
    for (const row of run.rows) {
      assert.ok(variants.includes(row.variant) && [72, 150].includes(row.dpi));
      assert.equal(typeof row.landscape, 'boolean');
      assert.ok(Number.isInteger(row.repeat) && row.repeat >= 0 && row.repeat < 4);
      const name = `${row.variant}:${row.dpi}:${row.landscape}:${row.repeat}`;
      assert.ok(!names.has(name)); names.add(name);
      const scale = Math.fround(row.dpi / 72);
      assert.equal(row.width, Math.trunc((row.landscape ? 160 : 120) * scale));
      assert.equal(row.height, Math.trunc((row.landscape ? 120 : 160) * scale));
      for (const key of ['pixelSha256', 'encodedSha256', 'pathSha256']) assert.match(row[key], hashPattern);
      assert.ok(Array.isArray(row.lineWidths) && row.lineWidths.length >= 1 && row.lineWidths.length <= 3);
      assert.ok(row.lineWidths.every(width => Number.isFinite(width) && width > 0));
      assert.equal(typeof row.family, 'string'); assert.ok(row.family.length <= 200);
    }
  }
  const cases = [];
  for (const variant of variants) for (const dpi of [72, 150]) for (const landscape of [false, true]) {
    const perProcess = runs.map(run => run.rows.filter(row => row.variant === variant && row.dpi === dpi && row.landscape === landscape));
    const rows = perProcess.flat();
    const distinct = property => [...new Set(rows.map(row => typeof row[property] === 'object' ? JSON.stringify(row[property]) : row[property]))].sort();
    cases.push({ variant, dpi, landscape, renders: rows.length,
      distinctPixelHashes: distinct('pixelSha256'), distinctEncodedHashes: distinct('encodedSha256'),
      distinctPathHashes: distinct('pathSha256'), distinctLineWidths: distinct('lineWidths'),
      samples: distinct('sample'), families: distinct('family'),
      withinProcessStable: perProcess.every(values => new Set(values.map(row => row.pixelSha256)).size === 1),
      acrossProcessesStable: distinct('pixelSha256').length === 1,
    });
  }
  return { schemaVersion: 1, scope: 'native-font-repeatability-diagnostic', platform: process.platform,
    architecture: runs[0].architecture, syntheticOnly: true, diagnosticOnly: true,
    productRendererExecuted: false, baselineApproved: false, publicationPerformed: false,
    processCount: runs.length, renderCount: runs.reduce((sum, run) => sum + run.rows.length, 0),
    unstableCases: cases.filter(value => !value.acrossProcessesStable).length, cases, runs };
}

export function main(args) {
  assert.equal(args.length, 0, 'No input, output, runtime or publication overrides are accepted.');
  const output = join(root, 'artifacts/native-font-repeatability', `${Date.now()}-${randomUUID().slice(0, 8)}`);
  mkdirSync(output, { recursive: true });
  const build = join(output, 'private-build');
  let succeeded = false;
  const run = (args, timeout) => {
    const result = spawnSync('dotnet', args, { cwd: root, encoding: 'utf8', timeout, maxBuffer: 8 * 1024 * 1024 });
    assert.ok(!result.error && !result.signal && result.status === 0, 'The isolated font probe failed.');
    return result.stdout;
  };
  try {
    run(['build', 'tools/native-licensing/NativeRasterProbe/NativeRasterProbe.csproj', '-c', 'Release',
      '--artifacts-path', build, '--nologo'], 180_000);
    const assembly = join(build, 'bin/NativeRasterProbe/release/NativeRasterProbe.dll');
    const runs = Array.from({ length: 16 }, () => JSON.parse(run([assembly], 20_000)));
    const report = summarize(runs);
    writeFileSync(join(output, 'report.json'), JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
    succeeded = true;
    console.log(`Native font diagnostic: ${report.unstableCases} unstable cases; evidence: ${output}`);
  } finally {
    rmSync(build, { recursive: true, force: true });
    assert.ok(!existsSync(build));
    writeFileSync(join(output, 'cleanup.json'), JSON.stringify({ succeeded, privateBuildRemoved: true,
      sourceTrustTouched: false, productRendererChanged: false, baselineApproved: false }, null, 2) + '\n', { flag: 'wx' });
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try { main(process.argv.slice(2)); } catch { console.error('Native font diagnostic failed; no private build output is logged.'); process.exitCode = 1; }
}
