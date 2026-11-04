import assert from 'node:assert/strict';
import { createHash, randomBytes } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { exerciseMonitoring } from './monitoring-operator-checks.mjs';

const services = ['pvad-otel-collector', 'pvad-prometheus', 'pvad-loki', 'pvad-tempo', 'pvad-alertmanager'];
export const monitoringSources = [
  'deploy/observability/otel-collector/config.yml', 'deploy/observability/loki/config.yml',
  'deploy/observability/tempo/config.yml', 'deploy/observability/prometheus/prometheus.yml',
  'deploy/observability/prometheus/rules/pvad-alerts.yml',
  'src/Observability/PVAD.Observability.WebhookRelay/Program.cs',
  'src/Observability/PVAD.Observability.WebhookRelay/AlertmanagerPayloadSanitizer.cs',
  'src/Observability/PVAD.Observability.WebhookRelay/WebhookSignature.cs',
  'src/Observability/PVAD.Observability.WebhookRelay/WebhookRelayOptions.cs',
  'src/Observability/PVAD.Observability.WebhookRelay/Dockerfile',
  'PVAD.WebApi/Observability/PvadObservabilityExtensions.cs', 'PVAD.WebApi/Observability/PvadTelemetry.cs',
  'PVAD.WebApi/Observability/PvadLogPrivacy.cs', 'PVAD.WebApi/Observability/PvadLogRecordSanitizingProcessor.cs',
  'PVAD.WebApi/Observability/PvadTelemetrySanitizingProcessor.cs',
];
export const monitoringImages = compose => [...services.map(key => compose.services[key].image),
  'node:22-alpine', 'mcr.microsoft.com/dotnet/aspnet:10.0'];

export function collectorBinding(port) {
  assert.ok(Number.isInteger(port) && port >= 1 && port <= 65535, 'A fixed loopback ingestion port is required.');
  return `127.0.0.1:${port}:4317`;
}

export function monitoringConfiguration({ prometheus, rules }) {
  const config = structuredClone(prometheus);
  config.global.scrape_interval = '2s';
  config.global.evaluation_interval = '2s';
  config.rule_files = ['/lab/alert.yml'];
  config.scrape_configs = config.scrape_configs.filter(value => value.job_name === 'pvad-otel-collector');
  assert.equal(config.scrape_configs.length, 1);
  const rule = rules.groups.flatMap(group => group.rules).find(value => value.alert === 'PvadTelemetryPipelineUnavailable');
  assert.ok(rule, 'The real telemetry outage rule must exist.');
  const alert = structuredClone(rule);
  alert.for = '4s';
  alert.labels.environment = 'isolated-rehearsal';
  return { prometheus: config, rules: { groups: [{ name: 'pvad-rehearsal', rules: [alert] }] }, alertName: alert.alert };
}

export async function createMonitoringLab(context) {
  const { root, work, output, runId, network, label, names, docker, runContainer, resources, images, command, finish, wait, step } = context;
  const { load, dump } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
  const directory = resolve(work, 'monitoring');
  await mkdir(directory);
  const bind = (source, target) => ['--mount', `type=bind,src=${source},dst=${target},readonly`];
  const mount = bind(directory, '/lab');
  const imageFor = tag => { const image = images.find(value => value.tag === tag); assert.ok(image); return image.id; };
  const compose = load(await readFile(resolve(root, 'docker-compose.yml'), 'utf8'));
  const volume = suffix => {
    const name = `pvad-monitoring-${runId}-${suffix}`;
    resources.push(['volume', name]);
    docker(['volume', 'create', '--label', `${label}=${runId}`, name]);
    assert.equal(docker(['volume', 'inspect', '--format', `{{index .Labels "${label}"}}`, name]), runId);
    return name;
  };
  const data = volume('grafana'), backup = volume('backup');
  const provisioning = resolve(directory, 'provisioning');
  for (const type of ['dashboards', 'datasources']) {
    await mkdir(resolve(provisioning, type), { recursive: true });
    const file = `${type}.yml`;
    await writeFile(resolve(provisioning, type, file), await readFile(resolve(root, 'deploy/observability/grafana/provisioning', type, file)));
  }
  const configuration = monitoringConfiguration({
    prometheus: load(await readFile(resolve(root, 'deploy/observability/prometheus/prometheus.yml'), 'utf8')),
    rules: load(await readFile(resolve(root, 'deploy/observability/prometheus/rules/pvad-alerts.yml'), 'utf8')),
  });
  await writeFile(resolve(directory, 'prometheus.yml'), dump(configuration.prometheus));
  await writeFile(resolve(directory, 'alert.yml'), dump(configuration.rules));
  for (const [source, target] of [['loki/config.yml', 'loki.yml'], ['tempo/config.yml', 'tempo.yml'], ['otel-collector/config.yml', 'collector.yml']])
    await writeFile(resolve(directory, target), await readFile(resolve(root, 'deploy/observability', source)));
  const relaySource = await readFile(resolve(root, 'src/Observability/PVAD.Observability.WebhookRelay/Program.cs'), 'utf8');
  const header = suffix => {
    const found = relaySource.match(new RegExp(`"(X-[A-Z]+-Webhook-${suffix})"`));
    assert.ok(found, 'The actual relay header contract could not be read.');
    return found[1];
  };
  await writeFile(resolve(directory, 'receiver.json'), JSON.stringify({ alertName: configuration.alertName,
    timestampHeader: header('Timestamp'), signatureHeader: header('Signature'), idempotencyHeader: header('Idempotency-Key') }));
  await writeFile(resolve(directory, 'signing.key'), randomBytes(32).toString('hex'), { mode: 0o644 });
  await writeFile(resolve(directory, 'receiver.mjs'), await readFile(resolve(root, 'tools/configuration/monitoring-webhook-receiver.mjs')));
  execFileSync('openssl', ['req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-sha256', '-days', '1',
    '-subj', '/CN=pvad-webhook-receiver', '-addext', 'subjectAltName=DNS:pvad-webhook-receiver',
    '-addext', 'basicConstraints=critical,CA:TRUE', '-keyout', resolve(directory, 'receiver.key'), '-out', resolve(directory, 'receiver.crt')],
  { stdio: 'pipe', timeout: 30_000 });
  await writeFile(resolve(directory, 'alertmanager.yml'), dump({ global: { resolve_timeout: '10s' },
    route: { receiver: 'isolated-relay', group_by: ['alertname'], group_wait: '1s', group_interval: '1s', repeat_interval: '1m' },
    receivers: [{ name: 'isolated-relay', webhook_configs: [{ url: 'http://pvad-webhook-relay:8080/alerts', send_resolved: true }] }] }));
  step('monitoring/build-relay');
  const relayDirectory = resolve(directory, 'relay');
  await finish(command('relay-build', ['dotnet', 'publish', 'src/Observability/PVAD.Observability.WebhookRelay/PVAD.Observability.WebhookRelay.csproj',
    '--no-restore', '--configuration', 'Release', '--output', relayDirectory, '-p:UseAppHost=false', '--verbosity', 'quiet']));
  const namesByService = {};
  const start = (key, args, env = {}, containerCommand = []) => {
    const name = `pvad-monitoring-${runId}-${key}`;
    namesByService[key] = name;
    const tag = compose.services[key]?.image ?? (key === 'receiver' ? 'node:22-alpine' : 'mcr.microsoft.com/dotnet/aspnet:10.0');
    runContainer(name, imageFor(tag), ['--network-alias', key === 'receiver' ? 'pvad-webhook-receiver' : key === 'relay' ? 'pvad-webhook-relay' : key,
      '--memory', key === 'pvad-loki' ? '768m' : '512m', '--pids-limit', '256', ...args], env, containerCommand);
    return name;
  };
  const port = (name, containerPort) => {
    const bindings = JSON.parse(docker(['inspect', name]))[0].NetworkSettings.Ports[`${containerPort}/tcp`];
    assert.equal(bindings.length, 1);
    assert.equal(bindings[0].HostIp, '127.0.0.1');
    return `http://127.0.0.1:${bindings[0].HostPort}`;
  };
  const receiver = start('receiver', [...mount, '--publish', '127.0.0.1::8080', '--entrypoint', 'node'], {}, ['/lab/receiver.mjs', '/lab']);
  const receiverUrl = port(receiver, 8080);
  await wait(`${receiverUrl}/health`);
  start('relay', [...mount, ...bind(relayDirectory, '/app'), '--user', '1654:1654', '--read-only', '--tmpfs', '/tmp:size=16m',
    '--env', 'ASPNETCORE_URLS=http://+:8080', '--env', 'SSL_CERT_FILE=/lab/receiver.crt',
    '--env', 'WebhookRelay__Destination=https://pvad-webhook-receiver:8443/alerts', '--env', 'WebhookRelay__SecretFile=/lab/signing.key',
    '--workdir', '/app', '--entrypoint', 'dotnet'], {}, ['/app/PVAD.Observability.WebhookRelay.dll']);
  const targets = [
    ['pvad-loki', 3100, '/loki:uid=10001,gid=10001,size=256m', '/usr/bin/loki', ['-config.file=/lab/loki.yml', '-config.expand-env=true']],
    ['pvad-tempo', 3200, '/var/tempo:uid=10001,gid=10001,size=256m', '/tempo', ['-config.file=/lab/tempo.yml', '-config.expand-env=true']],
    ['pvad-alertmanager', 9093, '/alertmanager:uid=65534,gid=65534,size=64m', '/bin/alertmanager', ['--config.file=/lab/alertmanager.yml', '--storage.path=/alertmanager', '--cluster.listen-address=']],
  ];
  const endpoints = {};
  step('monitoring/services');
  for (const [key, published, tmpfs, entrypoint, args] of targets) {
    const name = start(key, [...mount, '--publish', `127.0.0.1::${published}`, '--tmpfs', tmpfs, '--entrypoint', entrypoint], {}, args);
    endpoints[key] = port(name, published);
  }
  const collector = start('pvad-otel-collector', [...mount, '--publish', collectorBinding(await context.freePort()), '--publish', '127.0.0.1::13133',
    '--env', 'PVAD_TRACE_SAMPLE_PERCENTAGE=100', '--env', 'PVAD_OTEL_MEMORY_LIMIT_MIB=256', '--env', 'PVAD_OTEL_MEMORY_SPIKE_LIMIT_MIB=64',
    '--entrypoint', '/otelcol-contrib'], {}, ['--config=/lab/collector.yml']);
  await wait(port(collector, 13133));
  const prometheus = start('pvad-prometheus', [...mount, '--publish', '127.0.0.1::9090',
    '--tmpfs', '/prometheus:uid=65534,gid=65534,size=256m', '--entrypoint', '/bin/prometheus'], {},
  ['--config.file=/lab/prometheus.yml', '--storage.tsdb.path=/prometheus']);
  endpoints['pvad-prometheus'] = port(prometheus, 9090);
  for (const [key, path] of [['pvad-loki', '/ready'], ['pvad-tempo', '/ready'], ['pvad-alertmanager', '/-/ready'], ['pvad-prometheus', '/-/ready']])
    await wait(`${endpoints[key]}${path}`);
  const report = { status: 'running', productionConfigurationChanged: false,
    relayBinarySha256: createHash('sha256').update(await readFile(resolve(relayDirectory, 'PVAD.Observability.WebhookRelay.dll'))).digest('hex'),
    receiverCertificateSha256: createHash('sha256').update(await readFile(resolve(directory, 'receiver.crt'))).digest('hex'),
    configurationOverrides: ['2s-scrape-and-evaluation', '4s-outage-for', '100-percent-local-trace-sample', 'isolated-https-notification-target', 'collector-only-scrape'],
    limitations: ['no-cadvisor-postgres-exporter-tls-probes', 'no-external-receiver-smtp-or-production-silence-migration'] };
  return {
    report,
    apiEnvironment: { Observability__Enabled: 'true', Observability__OtlpEndpoint: port(collector, 4317),
      Observability__HealthCheckIntervalSeconds: '5', OTEL_METRIC_EXPORT_INTERVAL: '1000',
      'Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics': 'Information' },
    grafanaStorage: ['--mount', `type=volume,src=${data},dst=/var/lib/grafana`], grafanaProvisioning: provisioning,
    exercise: access => exerciseMonitoring({ ...context, ...access, directory, provisioning, namesByService,
      data, backup, configuration, receiverUrl, report, collectorEndpoint: port(collector, 4317),
      grafanaImage: imageFor(compose.services['pvad-grafana'].image) }),
  };
}
