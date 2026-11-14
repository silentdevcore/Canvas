import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { PLATFORM_FIXTURES } from './platforms.mjs';

const read = file => readFileSync(new URL(file, import.meta.url), 'utf8');

test('native platform matrix covers the same five packs and retains each failed result', () => {
  assert.deepEqual(PLATFORM_FIXTURES, ['font-families', 'date-format', 'date-historical', 'date-layout', 'all-elements']);
  const source = read('./platforms.mjs');
  assert.match(source, /PVAD_VISUAL_PARITY_ENFORCE: 'true'/);
  assert.match(source, /PVAD_VISUAL_PARITY_REUSE_ARTIFACTS: 'false'/);
  assert.match(source, /'--bundled-browser'/);
  assert.match(source, /metadata\.results\.push/);
  assert.match(source, /if \(failed\) process\.exitCode = 1/);
  assert.doesNotMatch(source, /UPDATE_BASELINE|git push|git commit/);
});

test('renderer tooling uses native executables and portable ImageMagick null output', () => {
  const source = read('./run.mjs');
  assert.doesNotMatch(source, /spawnSync\('sh'|spawnSync\('compare'|\/dev\/null/);
  assert.match(source, /spawnSync\(name, \['-v'\]/);
  assert.match(source, /spawnImageMagick\(\['compare'/);
  assert.match(source, /'null:'/);
  assert.match(source, /process\.argv\.includes\('--bundled-browser'\)/);
});

test('platform CI uses real x64 hosts, immutable action pins and no deployment credentials', () => {
  const workflow = read('../../.github/workflows/visual-parity-platforms.yml');
  assert.match(workflow, /os: \[ubuntu-24\.04, windows-2025\]/);
  assert.match(workflow, /fail-fast: false/);
  assert.match(workflow, /core\.autocrlf false/);
  assert.match(workflow, /persist-credentials: false/);
  assert.match(workflow, /contents: read/);
  assert.match(workflow, /if: always\(\)/);
  assert.match(workflow, /path: artifacts\/platform-parity/);
  assert.match(workflow, /--version=26\.9\.0/);
  assert.doesNotMatch(workflow, /pull_request_target|secrets\.|packages: write|deploy|publish/);
  for (const action of workflow.matchAll(/uses: ([^\n]+)/g))
    assert.match(action[1], /@[a-f0-9]{40}(?:\s|$)/);
});
