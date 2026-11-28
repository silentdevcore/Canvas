import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, lstatSync, mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { spawnImageMagick } from '../visual-parity/image-tools.mjs';

const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const escape = text => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
const calls = ['png-direct-1', 'png-direct-2', 'png-interface', 'png-interface-options'];
const cases = calls.flatMap(call => ['single', 'multiple:page-1.png', 'multiple:page-2.png'].map(suffix => `${call}:${suffix}`)).sort();

export function converterRasterDimensions(name) {
  assert.ok(cases.includes(name), 'Unknown synthetic raster case.');
  const landscape = name.endsWith(':page-2.png');
  const scale = /^(png-direct-1|png-interface):/.test(name) ? Math.fround(150 / 72) : 1;
  return { Width: Math.trunc((landscape ? 160 : 120) * scale), Height: Math.trunc((landscape ? 120 : 160) * scale) };
}

function raster(execution, directory, name) {
  assert.ok(lstatSync(directory).isDirectory() && !lstatSync(directory).isSymbolicLink(), 'Synthetic raster directory required.');
  const metadata = execution.converterRasterDiagnostics[name];
  assert.ok(metadata && Number.isInteger(metadata.Width) && Number.isInteger(metadata.Height));
  assert.ok(metadata.Width > 0 && metadata.Height > 0 && metadata.Width * metadata.Height <= 1_000_000);
  assert.deepEqual({ Width: metadata.Width, Height: metadata.Height }, converterRasterDimensions(name));
  const file = join(directory, hash(name) + '.png');
  const stat = lstatSync(file);
  assert.ok(stat.isFile() && !stat.isSymbolicLink() && stat.size <= 4 * 1024 * 1024, 'Bounded regular PNG required.');
  const bytes = readFileSync(file);
  assert.ok(bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])));
  const [call, mode, page] = name.split(':');
  const encoded = execution.converters[`${call}:${mode}`];
  assert.equal(hash(bytes), page ? encoded[page] : encoded, 'Synthetic image must match the actual Converter output hash.');
  return { file, metadata, encodedSha256: hash(bytes) };
}

export function createRasterReview({ left, right, leftDirectory, rightDirectory, outputDirectory,
  labels = ['Original first run', 'Original repeat run'] }, execute = spawnImageMagick) {
  assert.equal(labels.length, 2);
  assert.ok(labels.every(label => ['Original first run', 'Original repeat run', 'Protected run'].includes(label)));
  for (const execution of [left, right]) {
    assert.equal(execution.converterFixtureId, 'native-converter-fixed-120x160-v1');
    assert.deepEqual(Object.keys(execution.converterRasterDiagnostics).filter(name => name.startsWith('png-')).sort(), cases,
      'Only the complete fixed synthetic Converter PNG fixture may be published.');
  }
  assert.ok(!existsSync(outputDirectory), 'Never overwrite existing review evidence.');
  const parent = dirname(outputDirectory);
  assert.ok(lstatSync(parent).isDirectory() && !lstatSync(parent).isSymbolicLink());
  const temporary = mkdtempSync(join(parent, '.raster-review-'));
  const rows = [];
  const deadline = Date.now() + 120_000;
  const invoke = args => {
    const remaining = deadline - Date.now();
    assert.ok(remaining > 0, 'Synthetic review exceeded its runtime limit.');
    return execute(args, { encoding: 'utf8', timeout: Math.min(10_000, remaining), maxBuffer: 1024 * 1024 });
  };
  try {
    for (const name of cases) {
      const first = raster(left, leftDirectory, name), second = raster(right, rightDirectory, name);
      assert.equal(first.metadata.Width, second.metadata.Width);
      assert.equal(first.metadata.Height, second.metadata.Height);
      for (const source of [first, second]) {
        const dimensions = invoke(['identify', '-ping', '-format', '%w %h', source.file]);
        assert.ok(!dimensions.error && !dimensions.signal && dimensions.status === 0);
        assert.equal(dimensions.stdout.trim(), `${source.metadata.Width} ${source.metadata.Height}`);
      }
      const prefix = hash(name).slice(0, 16);
      const files = { first: `${prefix}-first.png`, second: `${prefix}-second.png`, diff: `${prefix}-diff.png` };
      copyFileSync(first.file, join(temporary, files.first));
      copyFileSync(second.file, join(temporary, files.second));
      const difference = invoke(['compare', '-metric', 'AE', '-fuzz', '0%',
        '-highlight-color', '#ff006e', '-lowlight-color', '#ffffff',
        join(temporary, files.first), join(temporary, files.second), join(temporary, files.diff)]);
      assert.ok(!difference.error && !difference.signal && [0, 1].includes(difference.status));
      const metric = difference.stderr.trim();
      assert.match(metric, /^\d+(?:\.\d+)?(?:e[+-]?\d+)?(?:\s+\([\d.e+-]+\))?$/i);
      const changedPixels = Number(metric.split(/\s/)[0]);
      assert.ok(Number.isSafeInteger(changedPixels) && changedPixels >= 0 && changedPixels <= first.metadata.Width * first.metadata.Height);
      assert.equal(difference.status === 0, changedPixels === 0);
      if (first.encodedSha256 === second.encodedSha256) assert.equal(changedPixels, 0);
      assert.ok(lstatSync(join(temporary, files.diff)).isFile() && !lstatSync(join(temporary, files.diff)).isSymbolicLink());
      rows.push({ name, width: first.metadata.Width, height: first.metadata.Height, changedPixels,
        firstSha256: first.encodedSha256, secondSha256: second.encodedSha256, files });
    }
    const report = { schemaVersion: 1, scope: 'fixed-synthetic-converter-png-review', syntheticOnly: true,
      diagnosticOnly: true, baselineApproved: false, caseCount: rows.length,
      changedCases: rows.filter(row => row.changedPixels > 0).length, labels, cases: rows };
    writeFileSync(join(temporary, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    writeFileSync(join(temporary, 'index.html'), `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Converter raster review</title>
<style>body{font:14px system-ui;margin:24px;color:#172326}h2{font-size:18px;overflow-wrap:anywhere}section{border-top:1px solid #bbc8cc;padding:16px 0}.images{display:grid;grid-template-columns:repeat(3,minmax(180px,1fr));gap:16px;overflow:auto}figure{margin:0;overflow:auto}img{max-width:none;image-rendering:pixelated}figcaption{margin-bottom:8px}</style>
<h1>Converter raster review</h1><p>Fixed synthetic fixture at 200%. Exact pixel differences, zero fuzz. Diagnostic only; no baseline approval.</p>
${rows.map(row => `<section><h2>${escape(row.name)}</h2><p>${row.width} x ${row.height}; changed pixels: ${row.changedPixels}</p><div class="images">${[
        [labels[0], row.files.first], [labels[1], row.files.second], ['Exact difference', row.files.diff],
      ].map(([label, file]) => `<figure><figcaption>${escape(label)}</figcaption><img alt="${escape(label)} for ${escape(row.name)}" src="${file}" width="${row.width * 2}" height="${row.height * 2}"></figure>`).join('')}</div></section>`).join('\n')}</html>`);
    assert.ok(!existsSync(outputDirectory), 'Never overwrite existing review evidence.');
    renameSync(temporary, outputDirectory);
    return report;
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}
