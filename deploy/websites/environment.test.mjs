import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const script = new URL('./15-pvad-environment.envsh', import.meta.url).pathname;
const defaults = { PVAD_API_UPSTREAM: 'http://api.example:8080/', PVAD_SPA_ROUTE_PATTERN: '^/(|pdf)/?$' };
function run(settings) {
  const env = { ...process.env };
  for (const key of Object.keys(env)) if (/^(PVAD|PXA)_(API_UPSTREAM|SPA_ROUTE_PATTERN)$/.test(key)) delete env[key];
  return spawnSync('sh', ['-c', '. "$1"; printf "%s\\n%s\\n%s\\n%s" "$PVAD_API_UPSTREAM" "$PVAD_SPA_ROUTE_PATTERN" "${PXA_API_UPSTREAM-unset}" "${PXA_SPA_ROUTE_PATTERN-unset}"', 'test', script],
    { env: { ...env, ...settings }, encoding: 'utf8' });
}

test('canonical, legacy-only and matching mixed inputs resolve identically', () => {
  const legacy = Object.fromEntries(Object.entries(defaults).map(([key, value]) => [key.replace('PVAD_', 'PXA_'), value]));
  for (const settings of [defaults, legacy, { ...defaults, ...legacy }]) {
    const result = run(settings);
    assert.equal(result.status, 0, result.stderr);
    assert.equal(result.stdout, `${defaults.PVAD_API_UPSTREAM}\n${defaults.PVAD_SPA_ROUTE_PATTERN}\nunset\nunset`);
  }
});

test('conflicts and empty canonical values fail without disclosing values', () => {
  for (const settings of [
    { ...defaults, PXA_API_UPSTREAM: 'http://private-old.example/' },
    { ...defaults, PXA_SPA_ROUTE_PATTERN: '^/private-old$' },
    { ...defaults, PVAD_API_UPSTREAM: '', PXA_API_UPSTREAM: 'http://private-old.example/' },
  ]) {
    const result = run(settings);
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Conflicting configuration keys/);
    assert.doesNotMatch(result.stderr, /private-old|http:/);
    assert.equal(result.stdout, '');
  }
});

test('missing, blank and unanchored route configuration fails before template generation', () => {
  for (const settings of [{}, { ...defaults, PVAD_API_UPSTREAM: '' },
    { ...defaults, PVAD_SPA_ROUTE_PATTERN: '/pdf' }]) {
    assert.notEqual(run(settings).status, 0);
  }
});
