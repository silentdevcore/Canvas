import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { prepareGrafanaProvider } from './rename-grafana-provider.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load, dump } = createRequire(new URL('../../pvad-designer/package.json', import.meta.url))('js-yaml');
const path = 'deploy/observability/grafana/provisioning/dashboards/dashboards.yml';
const source = readFileSync(new URL('../../' + path, import.meta.url), 'utf8');

test('Grafana candidate changes only provider and folder labels; active source remains unchanged', () => {
  const before = load(source);
  const result = prepareGrafanaProvider(source);
  const expected = structuredClone(before);
  expected.providers[0].name = 'PVAD';
  expected.providers[0].folder = 'PVAD';
  assert.deepEqual(result.candidate, expected);
  assert.deepEqual(load(dump(result.candidate)), expected);
  assert.equal(result.originalSource, source);
  assert.equal(result.report.sourceSha256, createHash('sha256').update(source).digest('hex'));
  assert.equal(result.report.deploymentAuthorized, false);
  assert.equal(result.report.blockers.length, 4);
  assert.match(result.report.warning, /not a database backup/);
  assert.equal(readFileSync(new URL('../../' + path, import.meta.url), 'utf8'), source);
  assert.equal(execFileSync('git', ['show', `HEAD:${path}`], { cwd: root, encoding: 'utf8' }), source);
});

test('unexpected Grafana provider, folder UID, policy or additional provider fails closed', () => {
  const mutations = [
    config => { config.providers[0].name = 'another-provider'; },
    config => { config.providers[0].folder = 'customer-folder'; },
    config => { config.providers[0].folderUid = 'existing-folder'; },
    config => { config.providers[0].disableDeletion = false; },
    config => { config.providers[0].orgId = 2; },
    config => { config.providers.push(structuredClone(config.providers[0])); },
    config => { config.providers[0].options.path = '/another/path'; },
  ];
  for (const mutate of mutations) {
    const config = load(source);
    mutate(config);
    assert.throws(() => prepareGrafanaProvider(dump(config)), /provisioning changed/);
  }
  assert.throws(() => prepareGrafanaProvider('null'), /provisioning changed/);
  assert.throws(() => prepareGrafanaProvider('providers: ['));
});

test('Grafana preparation has no apply command', () => {
  const result = spawnSync(process.execPath, ['tools/configuration/rename-grafana-provider.mjs', '--apply'],
    { cwd: root, encoding: 'utf8' });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No apply or deployment arguments/);
});
