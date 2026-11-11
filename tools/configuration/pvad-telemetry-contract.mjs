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

// Fails on duplicate series, samples or alerts, which promtool would otherwise merge or reject.
function assertUnique(tests) {
  const unique = (values, what, name) => {
    if (new Set(values).size !== values.length) throw new Error(`Duplicate ${what} in monitoring scenario "${name}".`);
  };
  for (const test of tests) {
    unique(test.input_series.map(series => series.series), 'input series', test.name);
    for (const check of test.promql_expr_test ?? []) unique(check.exp_samples.map(sample => sample.labels), 'expected samples', test.name);
    for (const check of test.alert_rule_test ?? [])
      unique(check.exp_alerts.map(alert => JSON.stringify(alert.exp_labels)), 'expected alerts', test.name);
  }
  unique(tests.map(test => test.name), 'scenario name', 'all');
}

export async function canonicalMonitoringTests(repoRoot = root) {
  const main = load(await readFile(resolve(repoRoot, 'deploy/observability/prometheus/rules/pvad-alerts.yml'), 'utf8'));
  const container = load(await readFile(resolve(repoRoot, 'deploy/observability/prometheus/rules/pvad-webapi-container-alerts.yml'), 'utf8'));
  const alerts = new Map([...main.groups, ...container.groups].flatMap(group => group.rules)
    .map(rule => [rule.alert, rule]));
  const expected = (name, labels = {}) => ({
    exp_labels: { ...alerts.get(name).labels, ...labels },
    exp_annotations: alerts.get(name).annotations,
  });
  const queries = (await dashboardQueryRules(repoRoot)).groups[0].rules;
  const resourceQueries = queries.filter(rule => rule.expr.includes('container_label_com_docker_compose_service'));
  if (resourceQueries.length !== 2) throw new Error('Review changed CPU/memory dashboard query coverage.');
  const healthQueries = queries.filter(rule => rule.expr.includes('job=~"pvad-(postgresql|containers)"'));
  if (healthQueries.length !== 1) throw new Error('Review changed exporter dashboard query coverage.');

  // A busy and a quiet PVAD service plus an equally busy unrelated container.
  const services = [['pvad-busy-test', 60, 90], ['pvad-quiet-test', 30, 50], ['unrelated-test', 60, 90]];
  const resourceSeries = services.flatMap(([service, cpuPerMinute, memory]) => {
    const label = `container_label_com_docker_compose_service="${service}"`;
    return [
      ['container_cpu_usage_seconds_total', `0+${cpuPerMinute}x25`, ',cpu="total"'],
      ['container_spec_cpu_quota', '100000+0x25', ''],
      ['container_spec_cpu_period', '100000+0x25', ''],
      ['container_memory_working_set_bytes', `${memory}+0x25`, ''],
      ['container_spec_memory_limit_bytes', '100+0x25', ''],
    ].map(([metric, values, extra]) => ({ series: `${metric}{${label}${extra}}`, values }));
  });
  const busy = { container_label_com_docker_compose_service: 'pvad-busy-test' };
  const tests = [{
    name: 'Resource alerts fire only for PVAD services above their limits; unrelated containers are excluded',
    interval: '1m', input_series: resourceSeries,
    alert_rule_test: ['PvadContainerCpuHigh', 'PvadContainerMemoryHigh'].map(name => ({
      eval_time: '21m', alertname: name, exp_alerts: [expected(name, busy)],
    })),
    promql_expr_test: resourceQueries.map(rule => {
      const cpu = rule.expr.includes('container_cpu_usage_seconds_total');
      return { expr: rule.expr, eval_time: '21m', exp_samples: [
        { labels: '{container_label_com_docker_compose_service="pvad-busy-test"}', value: cpu ? 100 : 90 },
        { labels: '{container_label_com_docker_compose_service="pvad-quiet-test"}', value: 50 },
      ] };
    }),
  }];

  const jobs = [
    ['PvadTelemetryPipelineUnavailable', 'otel-collector'],
    ['PvadPostgreSqlExporterUnavailable', 'postgresql'],
    ['PvadContainerMetricsUnavailable', 'containers'],
    ['PvadTlsProbeUnavailable', 'tls'],
  ];
  const tlsInstance = 'https://test.example.test';
  const target = job => job === 'tls'
    ? `probe_success{job="pvad-tls",instance="${tlsInstance}"}` : `up{job="pvad-${job}"}`;
  tests.push({
    name: 'Unavailable PVAD scrape jobs alert; unrelated down jobs do not',
    interval: '1m',
    input_series: jobs.flatMap(([, job]) => [
      { series: target(job), values: '0+0x20' },
      { series: target(job).replace('pvad-', 'unrelated-'), values: '0+0x20' },
    ]),
    alert_rule_test: jobs.map(([name, job]) => ({ eval_time: '6m', alertname: name,
      exp_alerts: [expected(name, { job: `pvad-${job}`, ...(job === 'tls' ? { instance: tlsInstance } : {}) })],
    })),
  }, {
    name: 'Available PVAD scrape jobs do not alert',
    interval: '1m',
    input_series: jobs.map(([, job]) => ({ series: target(job), values: '1+0x20' })),
    alert_rule_test: jobs.map(([name]) => ({ eval_time: '6m', alertname: name, exp_alerts: [] })),
  }, {
    name: 'Exporter dashboard reports the lowest canonical exporter health and ignores unrelated jobs', interval: '1m',
    input_series: [
      { series: 'up{job="pvad-postgresql"}', values: '0' },
      { series: 'up{job="pvad-containers"}', values: '1' },
      { series: 'up{job="unrelated"}', values: '1' },
    ],
    promql_expr_test: [{ expr: healthQueries[0].expr, eval_time: '0m',
      exp_samples: [{ labels: '{}', value: 0 }] }],
  });

  const tlsExpiry = 'PvadTlsCertificateExpiringSoon';
  const renewed = 'https://renewed.example.test';
  tests.push({
    name: 'Only certificates expiring within 14 days alert', interval: '1m',
    input_series: [
      { series: `probe_success{job="pvad-tls",instance="${tlsInstance}"}`, values: '1+0x20' },
      { series: `probe_ssl_earliest_cert_expiry{job="pvad-tls",instance="${tlsInstance}"}`, values: '1200000+0x20' },
      { series: `probe_success{job="pvad-tls",instance="${renewed}"}`, values: '1+0x20' },
      { series: `probe_ssl_earliest_cert_expiry{job="pvad-tls",instance="${renewed}"}`, values: '9000000+0x20' },
      { series: `probe_ssl_earliest_cert_expiry{job="unrelated-tls",instance="${tlsInstance}"}`, values: '1200000+0x20' },
    ],
    alert_rule_test: [{ eval_time: '15m', alertname: tlsExpiry,
      exp_alerts: [expected(tlsExpiry, { job: 'pvad-tls', instance: tlsInstance })] }],
  });

  const heartbeat = 'PvadWebApiContainerTelemetryMissing';
  tests.push({
    name: 'A stale WebApi heartbeat fires and recovers; unrelated instances are ignored', interval: '1m',
    input_series: [
      { series: 'pvad_service_heartbeat_seconds{service_instance_id="pvad-webapi-container"}', values: '0 0 0 0 240' },
      { series: 'pvad_service_heartbeat_seconds{service_instance_id="unrelated-container"}', values: '99999+0x4' },
    ],
    alert_rule_test: [
      { eval_time: '3m', alertname: heartbeat, exp_alerts: [expected(heartbeat)] },
      { eval_time: '4m', alertname: heartbeat, exp_alerts: [] },
    ],
  }, {
    // A replaced container leaves a stale series beside the fresh one under the same instance ID.
    name: 'A fresh WebApi heartbeat is not masked by a stale replica series', interval: '1m',
    input_series: [
      { series: 'pvad_service_heartbeat_seconds{service_instance_id="pvad-webapi-container",instance="replaced"}', values: '0+0x4' },
      { series: 'pvad_service_heartbeat_seconds{service_instance_id="pvad-webapi-container",instance="current"}', values: '0+60x4' },
    ],
    alert_rule_test: [{ eval_time: '4m', alertname: heartbeat, exp_alerts: [] }],
  });
  assertUnique(tests);
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
    const tests = await canonicalMonitoringTests();
    await mkdir(dirname(resolve(process.argv[3])), { recursive: true });
    await writeFile(process.argv[3], JSON.stringify(tests, null, 2) + '\n');
    console.log(`Prepared ${tests.tests.length} canonical monitoring scenarios.`);
  }
}
