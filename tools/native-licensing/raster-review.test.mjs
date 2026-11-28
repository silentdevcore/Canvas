import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { copyFileSync, existsSync, mkdtempSync, mkdirSync, readFileSync, readdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';
import test from 'node:test';
import { converterRasterDimensions, createRasterReview } from './raster-review.mjs';

const hash = value => createHash('sha256').update(value).digest('hex');
const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
const dimensionsByFile = new Map();
function fixture(t) {
  const root = mkdtempSync(join(tmpdir(), 'pvad-raster-review-test-'));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const execution = { converterFixtureId: 'native-converter-fixed-120x160-v1', converters: {}, converterRasterDiagnostics: {} };
  const leftDirectory = join(root, 'left'), rightDirectory = join(root, 'right');
  mkdirSync(leftDirectory); mkdirSync(rightDirectory);
  for (const call of ['png-direct-1', 'png-direct-2', 'png-interface', 'png-interface-options']) {
    execution.converters[`${call}:single`] = hash(png);
    execution.converters[`${call}:multiple`] = { 'page-1.png': hash(png), 'page-2.png': hash(png) };
    for (const mode of ['single', 'multiple:page-1.png', 'multiple:page-2.png']) {
      const name = `${call}:${mode}`;
      execution.converterRasterDiagnostics[name] = { ...converterRasterDimensions(name), pixelSha256: 'mock-decoded-pixel-hash' };
      dimensionsByFile.set(hash(name) + '.png', converterRasterDimensions(name));
      for (const directory of [leftDirectory, rightDirectory]) writeFileSync(join(directory, hash(name) + '.png'), png);
    }
  }
  return { left: execution, right: structuredClone(execution), leftDirectory, rightDirectory, outputDirectory: join(root, 'review') };
}
function mockedImageMagick(args, options) {
  assert.ok(options.timeout > 0 && options.timeout <= 10_000);
  if (args[0] === 'identify') {
    const dimensions = dimensionsByFile.get(basename(args.at(-1)));
    return { status: 0, stdout: `${dimensions.Width} ${dimensions.Height}`, stderr: '' };
  }
  assert.equal(args[0], 'compare');
  assert.equal(args[args.indexOf('-fuzz') + 1], '0%');
  copyFileSync(args.at(-3), args.at(-1));
  return { status: 0, stdout: '', stderr: '0' };
}

test('mocked comparator publishes only the 12 hash-matched synthetic pairs, never a baseline approval', t => {
  const input = fixture(t), before = JSON.stringify([input.left, input.right]);
  const report = createRasterReview(input, mockedImageMagick);
  assert.equal(report.caseCount, 12); assert.equal(report.changedCases, 0);
  assert.equal(report.diagnosticOnly, true); assert.equal(report.baselineApproved, false);
  assert.equal(JSON.stringify([input.left, input.right]), before);
  assert.equal(readdirSync(input.outputDirectory).length, 38);
  assert.ok(readFileSync(join(input.outputDirectory, 'index.html'), 'utf8').includes('200%'));
  assert.ok(readdirSync(join(input.outputDirectory, '..')).every(name => !name.startsWith('.raster-review-')));
});

test('rejects foreign fixtures, incomplete cases, extra PNGs and HTML labels before publication', t => {
  for (const change of [input => input.left.converterFixtureId = 'customer-document',
    input => delete input.right.converterRasterDiagnostics['png-direct-1:single'],
    input => input.left.converterRasterDiagnostics['png-../../private'] = {},
    input => input.labels = ['<script>secret</script>', 'Original repeat run']]) {
    const input = fixture(t); change(input);
    assert.throws(() => createRasterReview(input, mockedImageMagick));
    assert.equal(existsSync(input.outputDirectory), false);
  }
});

test('rejects changed bytes, forged dimensions, oversized geometry and file symlinks', t => {
  for (const mode of ['bytes', 'dimensions', 'oversized', 'symlink']) {
    const input = fixture(t), name = 'png-direct-1:single';
    const file = join(input.leftDirectory, hash(name) + '.png');
    if (mode === 'bytes') writeFileSync(file, Buffer.concat([png, Buffer.from('not-the-export')]));
    if (mode === 'dimensions') input.left.converterRasterDiagnostics[name].Width++;
    if (mode === 'oversized') input.left.converterRasterDiagnostics[name].Width = 2_000_000;
    if (mode === 'symlink') {
      rmSync(file); symlinkSync(join(input.rightDirectory, hash(name) + '.png'), file);
    }
    assert.throws(() => createRasterReview(input, mockedImageMagick));
    assert.equal(existsSync(input.outputDirectory), false);
    assert.ok(readdirSync(join(input.outputDirectory, '..')).every(name => !name.startsWith('.raster-review-')));
  }
});

test('tool failures and invalid metrics cannot publish partial review evidence', t => {
  for (const result of [{ status: 2, stdout: '', stderr: 'failed' },
    { status: null, signal: 'SIGTERM', stdout: '', stderr: '' },
    { status: 1, stdout: '', stderr: 'NaN' }, { status: 1, stdout: '', stderr: '100000000' },
    { status: 1, stdout: '', stderr: '1' }]) {
    const input = fixture(t);
    const execute = (args, options) => args[0] === 'identify' ? mockedImageMagick(args, options) : result;
    assert.throws(() => createRasterReview(input, execute));
    assert.equal(existsSync(input.outputDirectory), false);
    assert.ok(readdirSync(join(input.outputDirectory, '..')).every(name => !name.startsWith('.raster-review-')));
  }
});

test('fixed dimensions preserve float-scale geometry and reject unknown cases', () => {
  assert.deepEqual(converterRasterDimensions('png-direct-1:single'), { Width: 249, Height: 333 });
  assert.deepEqual(converterRasterDimensions('png-interface:multiple:page-2.png'), { Width: 333, Height: 249 });
  assert.deepEqual(converterRasterDimensions('png-direct-2:single'), { Width: 120, Height: 160 });
  assert.deepEqual(converterRasterDimensions('png-interface-options:multiple:page-2.png'), { Width: 160, Height: 120 });
  assert.throws(() => converterRasterDimensions('customer-input'));
});

test('existing evidence is never overwritten', t => {
  const input = fixture(t); mkdirSync(input.outputDirectory);
  writeFileSync(join(input.outputDirectory, 'original.txt'), 'preserve');
  assert.throws(() => createRasterReview(input, mockedImageMagick), /Never overwrite/);
  assert.equal(readFileSync(join(input.outputDirectory, 'original.txt'), 'utf8'), 'preserve');
});
