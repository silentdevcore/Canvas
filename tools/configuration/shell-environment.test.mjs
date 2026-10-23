import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const helper = fileURLToPath(new URL('./environment.sh', import.meta.url));
const script = `
. "$1"
pvad_environment_alias PVAD_TEST PXA_TEST "\${PVAD_TEST+x}" "\${PVAD_TEST-}" "\${PXA_TEST+x}" "\${PXA_TEST-}"
exec "$2" -p 'JSON.stringify({current:process.env.PVAD_TEST,legacy:process.env.PXA_TEST})'
`;

test('POSIX adapter accepts either spelling and exports only the canonical value', () => {
  for (const environment of [
    {}, { PVAD_TEST: '' }, { PXA_TEST: 'example' },
    { PVAD_TEST: 'example', PXA_TEST: 'example' },
    { PXA_TEST: 'quotes"\n$(printf not-executed)' },
  ]) {
    const run = spawnSync('sh', ['-eu', '-c', script, 'pvad-test', helper, process.execPath],
      { encoding: 'utf8', env: environment });
    assert.equal(run.status, 0, run.stderr);
    const result = JSON.parse(run.stdout);
    assert.equal(result.current, environment.PVAD_TEST ?? environment.PXA_TEST);
    assert.equal(result.legacy, undefined);
  }
});

test('POSIX adapter rejects conflicts without logging either value', () => {
  const run = spawnSync('sh', ['-eu', '-c', script, 'pvad-test', helper, process.execPath],
    { encoding: 'utf8', env: { PVAD_TEST: 'new-private-value', PXA_TEST: 'old-private-value' } });
  assert.equal(run.status, 1);
  assert.match(run.stderr, /Conflicting configuration keys: PVAD_TEST and PXA_TEST/);
  assert.doesNotMatch(run.stderr, /private-value/);
  assert.equal(run.stdout, '');
});
test('validated multi-key shell adapter preserves opaque values without evaluating them', () => {
  const value = '$(exit 92); "literal" $HOME `exit 93`';
  const script = new URL('./environment.sh', import.meta.url).pathname;
  for (const key of ['PVAD_TEST_SAFE', 'PXA_TEST_SAFE']) {
    const run = spawnSync('sh', ['-e', '-c', '. "$1"; pvad_environment_keys PVAD_TEST_SAFE; printf "%s" "$PVAD_TEST_SAFE"; test -z "${PXA_TEST_SAFE+x}"', 'test', script],
      { encoding: 'utf8', env: { PATH: process.env.PATH, [key]: value } });
    assert.equal(run.status, 0, run.stderr);
    assert.equal(run.stdout, value);
  }
  const invalid = spawnSync('sh', ['-e', '-c', '. "$1"; pvad_environment_keys "PVAD_BAD;exit"', 'test', script], { encoding: 'utf8' });
  assert.notEqual(invalid.status, 0);
});
