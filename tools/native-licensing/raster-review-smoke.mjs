import assert from 'node:assert/strict';
import { createHash, randomUUID } from 'node:crypto';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { converterRasterDimensions, createRasterReview } from './raster-review.mjs';
import { spawnImageMagick } from '../visual-parity/image-tools.mjs';

assert.equal(process.argv.length, 2, 'The smoke test accepts no external document or output overrides.');
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const output = join(root, 'artifacts/native-raster-tool-smoke', `${Date.now()}-${randomUUID().slice(0, 8)}`);
mkdirSync(output, { recursive: true });
const input = mkdtempSync(join(tmpdir(), 'pvad-raster-tool-smoke-'));
const hash = value => createHash('sha256').update(value).digest('hex');
let succeeded = false;
try {
  function side(directory, changed) {
    mkdirSync(directory);
    const result = { converterFixtureId: 'native-converter-fixed-120x160-v1', converters: {}, converterRasterDiagnostics: {} };
    for (const call of ['png-direct-1', 'png-direct-2', 'png-interface', 'png-interface-options']) {
      result.converters[`${call}:multiple`] = {};
      for (const suffix of ['single', 'multiple:page-1.png', 'multiple:page-2.png']) {
        const name = `${call}:${suffix}`, dimensions = converterRasterDimensions(name);
        const file = join(directory, hash(name) + '.png');
        const generated = spawnImageMagick(['-size', `${dimensions.Width}x${dimensions.Height}`, 'xc:white',
          ...(changed ? ['-fill', 'black', '-draw', 'point 0,0'] : []), file], { encoding: 'utf8', timeout: 10_000 });
        assert.ok(!generated.error && !generated.signal && generated.status === 0, 'ImageMagick sample generation failed.');
        const checksum = hash(readFileSync(file));
        if (suffix === 'single') result.converters[`${call}:single`] = checksum;
        else result.converters[`${call}:multiple`][suffix.split(':')[1]] = checksum;
        result.converterRasterDiagnostics[name] = dimensions;
      }
    }
    return result;
  }
  const leftDirectory = join(input, 'left'), rightDirectory = join(input, 'right');
  const left = side(leftDirectory, false), right = side(rightDirectory, true);
  const report = createRasterReview({ left, right, leftDirectory, rightDirectory, outputDirectory: join(output, 'review') });
  assert.equal(report.caseCount, 12); assert.equal(report.changedCases, 12);
  assert.ok(report.cases.every(row => row.changedPixels === 1));
  assert.equal(report.baselineApproved, false);
  writeFileSync(join(output, 'smoke-result.json'), JSON.stringify({ scope: 'review-tool-smoke-not-native-renderer',
    passed: true, cases: 12, expectedChangedPixelsPerCase: 1, baselineApproved: false }, null, 2) + '\n');
  succeeded = true;
  console.log('ImageMagick smoke passed: ' + join(output, 'review/index.html'));
} finally {
  rmSync(input, { recursive: true, force: true });
  writeFileSync(join(output, 'cleanup.json'), JSON.stringify({ succeeded, sourceSamplesRemoved: true }, null, 2) + '\n');
}
