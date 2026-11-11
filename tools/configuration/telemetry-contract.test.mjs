import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { createRequire } from 'node:module';
import { spawnSync } from 'node:child_process';
import { canonicalMonitoringTests, dashboardQueryRules } from './pvad-telemetry-contract.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const { load } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
const readYaml = async path => load(await readFile(resolve(root, path), 'utf8'));

test('OCR writes and the API subscribes to canonical spans only', async () => {
  const source = await readFile(resolve(root,
    'src/Importing/PVAD.FileImporter.ImageOcr/ProcessIsolatedTesseractOcrEngine.cs'), 'utf8');
  const registration = await readFile(resolve(root,
    'PVAD.WebApi/Observability/PvadObservabilityExtensions.cs'), 'utf8');
  assert.match(source, /ActivitySourceName = "PVAD\.ImageOcr"/);
  assert.doesNotMatch(source, /LegacyActivitySourceName/);
  assert.match(source, /"pvad\.ocr\.worker\.execute"/);
  assert.doesNotMatch(source, /"pxa\.ocr\.worker\.execute"|\$"pxa-image-ocr-/);
  assert.match(registration, /AddSource\(ProcessIsolatedTesseractOcrEngine\.ActivitySourceName\)/);
  assert.doesNotMatch(registration, /LegacyActivitySourceName/);
  const worker = await readFile(resolve(root,
    'src/Importing/PVAD.FileImporter.ImageOcr.Worker/Program.cs'), 'utf8');
  assert.match(worker, /new Activity\("pvad\.ocr\.worker\.process"\)/);
  assert.doesNotMatch(worker, /pxa\.ocr\.worker\.process/);
});

test('monitoring defaults use canonical jobs, labels and selectors', async () => {
  const config = await readYaml('deploy/observability/prometheus/prometheus.yml');
  assert.equal(config.global.external_labels.platform, 'pvad');
  assert.deepEqual(config.scrape_configs.map(job => job.job_name),
    ['pvad-otel-collector', 'pvad-postgresql', 'pvad-containers', 'pvad-mcp', 'pvad-tls']);
  const main = await readYaml('deploy/observability/prometheus/rules/pvad-alerts.yml');
  const container = await readYaml('deploy/observability/prometheus/rules/pvad-webapi-container-alerts.yml');
  assert.equal(main.groups[0].name, 'pvad-platform');
  assert.equal(main.groups[0].rules.length, 33);
  assert.equal(container.groups[0].name, 'pvad-api-container');
  for (const rule of [...main.groups[0].rules, ...container.groups[0].rules]) {
    assert.match(rule.labels.service, /^pvad-/);
    assert.match(rule.annotations.runbook_id, /^PVAD-OBS-/);
    assert.match(rule.annotations.dashboard_path, /^\/d\/pvad-/);
    assert.doesNotMatch(rule.expr, /job="pxa-|service_instance_id="pxa-|=~"pxa-\.\+"/);
  }
  for (const job of ['otel-collector', 'postgresql', 'containers', 'tls'])
    assert.ok(main.groups[0].rules.some(rule => rule.expr.includes(`job=~"pvad-${job}"`)), job);
  assert.match(container.groups[0].rules[0].expr, /service_instance_id=~"pvad-webapi-container"/);
  for (const path of ['deploy/observability/smoke-test.sh', 'deploy/observability/failure-recovery-smoke-test.sh']) {
    const script = await readFile(resolve(root, path), 'utf8');
    assert.doesNotMatch(script, /pxa-otel-collector|pxa-postgresql|pxa-containers/);
    const syntax = spawnSync('bash', ['-n', resolve(root, path)], { encoding: 'utf8' });
    assert.equal(syntax.status, 0, syntax.stderr);
  }
});

test('local and production maintenance routes use one canonical interval without changing schedules', async () => {
  const paths = ['deploy/observability/alertmanager/alertmanager.yml',
    'deploy/observability/alertmanager/alertmanager.production.yml.example'];
  for (const path of paths) {
    const config = await readYaml(path);
    assert.equal(config.time_intervals.length, 1);
    assert.equal(config.time_intervals[0].name, 'pvad-maintenance-window');
    assert.deepEqual(config.time_intervals[0].time_intervals, [{ weekdays: ['saturday', 'sunday'],
      times: [{ start_time: '01:00', end_time: '03:00' }], location: 'Europe/Berlin' }]);
    assert.equal(config.route.routes.length, 3);
    for (const route of config.route.routes)
      assert.deepEqual(route.mute_time_intervals, ['pvad-maintenance-window']);
    assert.deepEqual(config.route.group_by, ['alertname', 'service', 'environment']);
    assert.deepEqual(config.inhibit_rules[0].equal, ['service', 'environment']);
  }
});

test('native PromQL scenarios cover canonical resources, scrape jobs, TLS and heartbeats with unrelated controls in CI', async () => {
  const fixture = await canonicalMonitoringTests();
  assert.equal(fixture.tests.length, 7);
  assert.equal(new Set(fixture.tests.map(test => test.name)).size, fixture.tests.length);
  for (const test of fixture.tests) {
    const series = test.input_series.map(input => input.series);
    assert.equal(new Set(series).size, series.length, test.name);
    assert.ok(series.every(value => !/\bpxa[_-]/i.test(value)), test.name);
  }
  const resources = fixture.tests[0];
  assert.ok(resources.input_series.some(series => series.series.includes('unrelated-test')));
  assert.equal(resources.promql_expr_test.length, 2);
  for (const check of resources.promql_expr_test) {
    assert.ok(check.expr.includes('pvad-.+'));
    assert.deepEqual(check.exp_samples.map(sample => sample.labels), [
      '{container_label_com_docker_compose_service="pvad-busy-test"}',
      '{container_label_com_docker_compose_service="pvad-quiet-test"}']);
  }
  for (const check of resources.alert_rule_test)
    assert.deepEqual(check.exp_alerts.map(alert => alert.exp_labels.container_label_com_docker_compose_service), ['pvad-busy-test']);
  const [down, up] = fixture.tests.slice(1, 3);
  assert.ok(down.alert_rule_test.every(check => check.exp_alerts.length === 1 && check.exp_alerts[0].exp_labels.job.startsWith('pvad-')));
  assert.ok(up.alert_rule_test.every(check => check.exp_alerts.length === 0));
  const heartbeats = fixture.tests.filter(test => test.name.includes('WebApi heartbeat'));
  assert.equal(heartbeats.length, 2);
  assert.equal(heartbeats[0].alert_rule_test[0].exp_alerts.length, 1);
  assert.deepEqual(heartbeats[0].alert_rule_test[1].exp_alerts, []);
  assert.deepEqual(heartbeats[1].alert_rule_test[0].exp_alerts, []);
  const ci = await readFile(resolve(root, '.github/workflows/ci.yml'), 'utf8');
  assert.ok(ci.includes('/etc/prometheus/tests/pvad-alerts.test.yml /checks/identity-tests.json'));
  assert.ok(ci.includes('observability/identity-tests.json'));
  assert.ok(ci.includes('/bin/amtool prom/alertmanager:v0.32.1 check-config'));
  assert.ok(ci.includes('/etc/alertmanager/alertmanager.production.yml.example'));
});

test('all managed dashboard metric queries use canonical metric names', async () => {
  const document = await dashboardQueryRules();
  const rules = document.groups[0].rules;
  assert.ok(rules.length >= 40);
  assert.equal(new Set(rules.map(rule => rule.record)).size, rules.length);
  for (const rule of rules)
    assert.doesNotMatch(rule.expr, /\bpxa[_-]/i, 'Dashboard queries must not select former metric names.');
  assert.ok(rules.some(rule => rule.expr.includes('__name__=~"pvad_')));
});

test('dashboard and datasource identities stay stable while visible titles and log filters migrate', async () => {
  const directory = resolve(root, 'deploy/observability/grafana/dashboards');
  let logQueries = 0;
  for (const file of await readdir(directory)) {
    if (!file.endsWith('.json')) continue;
    const dashboard = JSON.parse(await readFile(resolve(directory, file), 'utf8'));
    assert.match(dashboard.uid, /^pvad-/);
    assert.match(dashboard.title, /^PVADE/);
    for (const panel of dashboard.panels ?? []) {
      if (panel.datasource?.type !== 'loki') continue;
      for (const target of panel.targets ?? []) {
        if (target.expr?.includes('service_name')) {
          assert.ok(target.expr.includes('service_name=~"pvad-webapi"'));
          logQueries++;
        }
      }
    }
  }
  assert.ok(logQueries >= 2);
});
