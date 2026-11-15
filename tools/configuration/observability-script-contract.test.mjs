import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
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
  test(`${filename} parses and validates its canonical keys`, () => {
    const syntax = spawnSync('bash', ['-n', path.pathname], { encoding: 'utf8' });
    assert.equal(syntax.status, 0, syntax.stderr);
    const source = readFileSync(path, 'utf8');
    assert.match(source, /tools\/configuration\/environment\.sh/);
    assert.match(source, new RegExp(`pvad_environment_keys[^\n]*PVAD_${key}`));
    assert.doesNotMatch(source, /PXA_/);
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
  assert.equal([...emailSmoke.matchAll(/PVAD-OBS-TEST/g)].length, 2);
  assert.doesNotMatch(emailSmoke, /PXA-OBS-TEST/);
});

test('synthetic mail smoke executes with the current marker and rejects its old replacement', () => {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-mail-marker-'));
  try {
    const scripts = join(directory, 'deploy/observability');
    const config = join(directory, 'tools/configuration');
    const bin = join(directory, 'bin');
    for (const path of [scripts, config, bin]) mkdirSync(path, { recursive: true });
    writeFileSync(join(config, 'environment.sh'), readFileSync(new URL('./environment.sh', import.meta.url)));
    const state = join(directory, 'payload.json');
    const mock = `#!/usr/bin/env node
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
if (path.basename(process.argv[1]) === 'docker') {
  const post = process.argv.find(arg => arg.startsWith('--post-data='));
  assert.ok(post);
  const payload = JSON.parse(post.slice('--post-data='.length));
  assert.equal(payload[0].annotations.runbook_id, 'PVAD-OBS-TEST');
  fs.writeFileSync(process.env.PVAD_MAIL_SMOKE_STATE, JSON.stringify(payload));
} else {
  const alert = JSON.parse(fs.readFileSync(process.env.PVAD_MAIL_SMOKE_STATE))[0];
  const subject = '[PVAD][FIRING][CRITICAL] ' + alert.labels.alertname;
  const url = process.argv.at(-1);
  if (url.endsWith('/api/v1/messages')) {
    process.stdout.write(JSON.stringify({messages: [{From: {Address: 'pvad-alerts@example.test'}, Subject: subject, ID: 'synthetic-mail'}]}));
  } else {
    assert.ok(url.endsWith('/api/v1/message/synthetic-mail'));
    process.stdout.write(JSON.stringify({Subject: subject,
      Text: alert.annotations.summary + '\\nService: pvad-observability\\nEnvironment: local-evaluation\\nDashboard: http://localhost:3001/operator/grafana/\\nRunbook: PVAD-OBS-TEST',
      HTML: '<a>Open protected dashboard</a>'}));
  }
}
`;
    for (const name of ['docker', 'curl']) writeFileSync(join(bin, name), mock, { mode: 0o755 });
    const source = readFileSync(new URL('../../deploy/observability/alertmanager-email-smoke-test.sh', import.meta.url), 'utf8');
    const script = join(scripts, 'mail-smoke.sh');
    const env = {
      PATH: `${bin}:${process.env.PATH}`,
      PVAD_ALERTMANAGER_CONTAINER: 'synthetic-alertmanager',
      PVAD_MAILPIT_URL: 'http://mailpit.example.test',
      PVAD_MAIL_SMOKE_STATE: state,
    };
    writeFileSync(script, source);
    const current = spawnSync('bash', [script], { env, encoding: 'utf8', timeout: 10000 });
    assert.equal(current.status, 0, current.stderr);
    assert.match(current.stdout, /PVAD Alertmanager email smoke test passed/);
    const alert = JSON.parse(readFileSync(state))[0];
    assert.equal(alert.annotations.runbook_id, 'PVAD-OBS-TEST');
    assert.equal(alert.annotations.dashboard_path, '/d/pvad-platform-overview/pvad-platform-overview');
    writeFileSync(script, source.replaceAll('PVAD-OBS-TEST', 'PXA-OBS-TEST'));
    const regressed = spawnSync('bash', [script], { env, encoding: 'utf8', timeout: 10000 });
    assert.equal(regressed.status, 1, regressed.stderr);
    assert.match(regressed.stderr, /AssertionError/);
    assert.match(regressed.stderr, /PVAD-OBS-TEST/);
    assert.match(regressed.stderr, /PXA-OBS-TEST/);
    assert.doesNotMatch(regressed.stdout, /smoke test passed/);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
