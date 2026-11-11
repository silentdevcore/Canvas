import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const helper = new URL('./environment.sh', import.meta.url).pathname;

test('shell key validation preserves opaque canonical values without evaluating them', () => {
  const value = '$(exit 92); "literal" $HOME `exit 93`';
  const run = spawnSync('sh', ['-e', '-c', '. "$1"; pvad_environment_keys PVAD_TEST_SAFE; printf "%s" "$PVAD_TEST_SAFE"', 'test', helper],
    { encoding: 'utf8', env: { PATH: process.env.PATH, PVAD_TEST_SAFE: value } });
  assert.equal(run.status, 0, run.stderr);
  assert.equal(run.stdout, value);
});

test('former keys are neither promoted nor accepted as key names', () => {
  const run = spawnSync('sh', ['-e', '-c', '. "$1"; pvad_environment_keys PVAD_TEST_SAFE; printf "%s" "${PVAD_TEST_SAFE-unset}"', 'test', helper],
    { encoding: 'utf8', env: { PATH: process.env.PATH, PXA_TEST_SAFE: 'former' } });
  assert.equal(run.status, 0, run.stderr);
  assert.equal(run.stdout, 'unset');
  for (const key of ['PXA_TEST_SAFE', 'PVAD_BAD;exit'])
    assert.notEqual(spawnSync('sh', ['-e', '-c', `. "$1"; pvad_environment_keys '${key}'`, 'test', helper], { encoding: 'utf8' }).status, 0);
});
