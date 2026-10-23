import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const scripts = [
  ['alertmanager-email-smoke-test.sh', 'ALERTMANAGER_CONTAINER'],
  ['failure-recovery-smoke-test.sh', 'MAILPIT_URL'],
  ['ocr-failure-recovery-smoke-test.sh', 'API_URL'],
  ['performance-overhead-test.sh', 'BASELINE_API_URL'],
  ['retention-runtime-test.sh', 'RETENTION_KEEP_FAILED'],
  ['retention-verification.sh', 'RETENTION_TRACE_ID'],
];
for (const [filename, key] of scripts) {
  const path = new URL(`../../deploy/observability/${filename}`, import.meta.url);
  test(`${filename} parses and rejects conflicting aliases before operational commands`, () => {
    const syntax = spawnSync('bash', ['-n', path.pathname], { encoding: 'utf8' });
    assert.equal(syntax.status, 0, syntax.stderr);
    const source = readFileSync(path, 'utf8');
    assert.match(source, /tools\/configuration\/environment\.sh/);
    assert.match(source, new RegExp(`pvad_environment_keys[^\n]*PVAD_${key}`));
    const result = spawnSync('bash', [path.pathname], { encoding: 'utf8', timeout: 3000,
      env: { PATH: process.env.PATH, [`PVAD_${key}`]: 'canonical-conflict-value', [`PXA_${key}`]: 'legacy-conflict-value' } });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Conflicting configuration keys/);
    assert.doesNotMatch(result.stderr, /canonical-conflict-value|legacy-conflict-value/);
    assert.equal(result.stdout, '');
  });
}

test('operational mail subject and smoke expectations use the same current prefix', () => {
  const read = path => readFileSync(new URL(`../../deploy/observability/${path}`, import.meta.url), 'utf8');
  const template = read('alertmanager/templates/pvad-email.tmpl');
  const emailSmoke = read('alertmanager-email-smoke-test.sh');
  const recovery = read('failure-recovery-smoke-test.sh');
  assert.match(template, /\[PVAD\]\[\{\{ toUpper \.Status \}\}\]/);
  assert.ok(emailSmoke.includes('[PVAD][FIRING][CRITICAL]'));
  assert.ok(recovery.includes('[PVAD][${status}][CRITICAL]'));
  for (const source of [template, emailSmoke, recovery]) assert.ok(!source.includes('[PXA]'));
  assert.ok(emailSmoke.includes('PXA-OBS-TEST'));
});
