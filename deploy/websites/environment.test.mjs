import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const script = new URL('./15-pvad-environment.envsh', import.meta.url).pathname;
const defaults = { PVAD_API_UPSTREAM: 'http://api.example:8080/', PVAD_SPA_ROUTE_PATTERN: '^/(|pdf)/?$' };
function run(settings) {
  const env = { ...process.env };
  for (const key of Object.keys(env)) if (/^PVAD_(API_UPSTREAM|SPA_ROUTE_PATTERN)$/.test(key)) delete env[key];
  return spawnSync('sh', ['-c', '. "$1"; printf "%s\\n%s\\n%s\\n%s" "$PVAD_API_UPSTREAM" "$PVAD_SPA_ROUTE_PATTERN" "${PVAD_API_UPSTREAM-unset}" "${PVAD_SPA_ROUTE_PATTERN-unset}"', 'test', script],
    { env: { ...env, ...settings }, encoding: 'utf8' });
}

test('canonical inputs resolve unchanged', () => {
  const result = run(defaults);
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout, `${defaults.PVAD_API_UPSTREAM}\n${defaults.PVAD_SPA_ROUTE_PATTERN}\n${defaults.PVAD_API_UPSTREAM}\n${defaults.PVAD_SPA_ROUTE_PATTERN}`);
});

test('former keys alone do not configure the website', () => {
  const former = { PXA_API_UPSTREAM: defaults.PVAD_API_UPSTREAM, PXA_SPA_ROUTE_PATTERN: defaults.PVAD_SPA_ROUTE_PATTERN };
  const result = run(former);
  assert.notEqual(result.status, 0);
  assert.doesNotMatch(result.stderr, /api\.example/);
});

test('missing, blank and unanchored route configuration fails before template generation', () => {
  for (const settings of [{}, { ...defaults, PVAD_API_UPSTREAM: '' },
    { ...defaults, PVAD_SPA_ROUTE_PATTERN: '/pdf' }]) {
    assert.notEqual(run(settings).status, 0);
  }
});
