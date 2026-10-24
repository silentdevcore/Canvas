import { readdir, readFile, mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const { load } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');

export async function dashboardQueryRules(repoRoot = root) {
  const directory = resolve(repoRoot, 'deploy/observability/grafana/dashboards');
  const rules = [];
  function visit(panel) {
    if (panel.datasource?.type === 'prometheus') {
      for (const target of panel.targets ?? []) {
        if (target.expr)
          rules.push({ record: `pvad_dashboard_contract_${rules.length}`, expr: target.expr });
      }
    }
    for (const child of panel.panels ?? []) visit(child);
  }
  for (const file of (await readdir(directory)).filter(file => file.endsWith('.json')).sort()) {
    const dashboard = JSON.parse(await readFile(resolve(directory, file), 'utf8'));
    for (const panel of dashboard.panels ?? []) visit(panel);
  }
  return { groups: [{ name: 'pvad-dashboard-contract', rules }] };
}

export async function identityCompatibilityTests(repoRoot = root) {
  const main = load(await readFile(resolve(repoRoot, 'deploy/observability/prometheus/rules/pvad-alerts.yml'), 'utf8'));
  const container = load(await readFile(resolve(repoRoot, 'deploy/observability/prometheus/rules/pvad-webapi-container-alerts.yml'), 'utf8'));
  const alerts = new Map([...main.groups, ...container.groups].flatMap(group => group.rules)
    .map(rule => [rule.alert, rule]));
  const expected = (name, labels = {}) => ({
    exp_labels: { ...alerts.get(name).labels, ...labels },
    exp_annotations: alerts.get(name).annotations,
  });
  const resourceSeries = [];
  for (const service of ['pvad-test', 'pxa-test', 'unrelated-test']) {
    const label = `container_label_com_docker_compose_service="${service}"`;
    for (const [metric, values, extra] of [
      ['container_cpu_usage_seconds_total', '0+60x25', ',cpu="total"'],
      ['container_spec_cpu_quota', '100000+0x25', ''],
      ['container_spec_cpu_period', '100000+0x25', ''],
      ['container_memory_working_set_bytes', '90+0x25', ''],
      ['container_spec_memory_limit_bytes', '100+0x25', ''],
    ]) resourceSeries.push({ series: `${metric}{${label}${extra}}`, values });
  }
  const identities = ['pvad', 'pxa'];
  const resourceTests = ['PvadContainerCpuHigh', 'PvadContainerMemoryHigh'].map(name => ({
    eval_time: '21m', alertname: name,
    exp_alerts: identities.map(prefix => expected(name, { container_label_com_docker_compose_service: `${prefix}-test` })),
  }));
  const queries = (await dashboardQueryRules(repoRoot)).groups[0].rules;
  const resourceQueries = queries.filter(rule => rule.expr.includes('container_label_com_docker_compose_service'));
  if (resourceQueries.length !== 2) throw new Error('Review changed CPU/memory dashboard query coverage.');
  const healthQueries = queries.filter(rule => rule.expr.includes('job=~"(pvad|pxa)-(postgresql|containers)"'));
  if (healthQueries.length !== 1) throw new Error('Review changed exporter dashboard query coverage.');
  const jobs = ['otel-collector', 'postgresql', 'containers'];
  const healthSeries = identities.flatMap(prefix => [
    ...jobs.map(job => ({ series: `up{job="${prefix}-${job}"}`, values: '0+0x20' })),
    { series: `probe_success{job="${prefix}-tls",instance="https://test.example.test"}`, values: '0+0x20' },
  ]);
  const tests = [{
    name: 'Both container generations trigger resource alerts; unrelated containers are excluded',
    interval: '1m', input_series: resourceSeries, alert_rule_test: resourceTests,
    promql_expr_test: resourceQueries.map(rule => ({
      expr: rule.expr, eval_time: '21m',
      exp_samples: identities.map(prefix => ({
        labels: `{container_label_com_docker_compose_service="${prefix}-test"}`,
        value: rule.expr.includes('container_cpu_usage_seconds_total') ? 100 : 90,
      })),
    })),
  }, {
    name: 'Both scrape job generations retain pipeline, exporter, container and TLS alerts',
    interval: '1m', input_series: healthSeries,
    alert_rule_test: [
      ['PvadTelemetryPipelineUnavailable', 'otel-collector'],
      ['PvadPostgreSqlExporterUnavailable', 'postgresql'],
      ['PvadContainerMetricsUnavailable', 'containers'],
      ['PvadTlsProbeUnavailable', 'tls'],
    ].map(([name, job]) => ({ eval_time: '6m', alertname: name,
      exp_alerts: identities.map(prefix => expected(name, {
        job: `${prefix}-${job}`, ...(job === 'tls' ? { instance: 'https://test.example.test' } : {}),
      })),
    })),
  }];
  tests.push({
    name: 'Exporter dashboard includes canonical jobs without requiring legacy samples', interval: '1m',
    input_series: [
      { series: 'up{job="pvad-postgresql"}', values: '0' },
      { series: 'up{job="pvad-containers"}', values: '1' },
      { series: 'up{job="unrelated"}', values: '1' },
    ],
    promql_expr_test: [{ expr: healthQueries[0].expr, eval_time: '0m',
      exp_samples: [{ labels: '{}', value: 0 }] }],
  });
  const tlsExpiry = 'PvadTlsCertificateExpiringSoon';
  tests.push({
    name: 'Both TLS job generations retain certificate expiry alerts', interval: '1m',
    input_series: identities.flatMap(prefix => [
      { series: `probe_success{job="${prefix}-tls",instance="https://test.example.test"}`, values: '1+0x20' },
      { series: `probe_ssl_earliest_cert_expiry{job="${prefix}-tls",instance="https://test.example.test"}`, values: '1200000+0x20' },
    ]),
    alert_rule_test: [{ eval_time: '15m', alertname: tlsExpiry,
      exp_alerts: identities.map(prefix => expected(tlsExpiry, {
        job: `${prefix}-tls`, instance: 'https://test.example.test',
      })),
    }],
  });
  for (const metric of identities) for (const instance of identities) {
    const name = 'PvadWebApiContainerTelemetryMissing';
    tests.push({
      name: `Heartbeat ${metric} metrics with ${instance} instance fires and recovers`, interval: '1m',
      input_series: [
        { series: `${metric}_service_heartbeat_seconds{service_instance_id="${instance}-webapi-container"}`, values: '0 0 0 0 240' },
        { series: `${metric}_service_heartbeat_seconds{service_instance_id="unrelated-container"}`, values: '99999+0x4' },
      ],
      alert_rule_test: [
        { eval_time: '3m', alertname: name, exp_alerts: [expected(name)] },
        { eval_time: '4m', alertname: name, exp_alerts: [] },
      ],
    });
  }
  tests.push({
    name: 'A fresh canonical heartbeat is not masked by stale legacy history', interval: '1m',
    input_series: [
      { series: 'pxa_service_heartbeat_seconds{service_instance_id="pxa-webapi-container"}', values: '0+0x4' },
      { series: 'pvad_service_heartbeat_seconds{service_instance_id="pvad-webapi-container"}', values: '0+60x4' },
    ],
    alert_rule_test: [{ eval_time: '4m', alertname: 'PvadWebApiContainerTelemetryMissing', exp_alerts: [] }],
  });
  return { rule_files: ['/etc/prometheus/rules/pvad-alerts.yml', '/etc/prometheus/rules/pvad-webapi-container-alerts.yml'],
    evaluation_interval: '1m', tests };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const output = process.argv[2];
  if (!output) throw new Error('Provide an output path for the dashboard query contract.');
  const document = await dashboardQueryRules();
  await mkdir(dirname(resolve(output)), { recursive: true });
  await writeFile(output, JSON.stringify(document, null, 2) + '\n');
  console.log(`Prepared ${document.groups[0].rules.length} PromQL queries for promtool check rules.`);
  if (process.argv[3]) {
    const tests = await identityCompatibilityTests();
    await mkdir(dirname(resolve(process.argv[3])), { recursive: true });
    await writeFile(process.argv[3], JSON.stringify(tests, null, 2) + '\n');
    console.log(`Prepared ${tests.tests.length} canonical/legacy monitoring identity scenarios.`);
  }
}
