import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtemp, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const available = spawnSync('docker', ['compose', 'version'], { encoding: 'utf8' }).status === 0;
const fixtureRoot = await mkdtemp(resolve(tmpdir(), 'pvad-compose-contract-'));
const emptyEnvFile = resolve(fixtureRoot, 'empty.env');
await writeFile(emptyEnvFile, '');
const args = ['tools/configuration/pvad-compose.mjs', '--env-file', emptyEnvFile,
  '-f', 'deploy/api/docker-compose.api.yml', '--profile', 'mcp', 'config', '--format', 'json'];

function configuration(prefix, extra = {}, overlays = []) {
  return spawnSync(process.execPath, [...args.slice(0, -3), ...overlays, ...args.slice(-3)], {
    cwd: root,
    encoding: 'utf8',
    env: {
      PATH: process.env.PATH,
      HOME: process.env.HOME,
      [`${prefix}_LICENSE_PRIVATE_KEY_FILE`]: '/tmp/pvad-synthetic-private.pem',
      [`${prefix}_LICENSE_PUBLIC_KEY_FILE`]: '/tmp/pvad-synthetic-public.pem',
      [`${prefix}_API_HOST_PORT`]: '5099',
      ...extra,
    },
  });
}

test('fresh project has canonical physical storage names across both input generations',
  { skip: !available }, () => {
    const previous = configuration('PXA', { COMPOSE_PROJECT_NAME: 'synthetic-installed-project' });
    const current = configuration('PVAD', { COMPOSE_PROJECT_NAME: 'synthetic-installed-project' });
    assert.equal(previous.status, 0, previous.stderr);
    assert.equal(current.status, 0, current.stderr);
    const before = JSON.parse(previous.stdout), after = JSON.parse(current.stdout);
    assert.equal(after.name, 'synthetic-installed-project');
    assert.deepEqual(after.volumes, before.volumes);
    for (const key of ['pvad-api-data', 'pvad-database-data']) {
      assert.equal(after.volumes[key].name, `synthetic-installed-project_${key}`);
      const service = key === 'pvad-api-data' ? 'pvad-webapi' : 'pvad-database';
      assert.deepEqual(after.services[service].volumes, before.services[service].volumes);
      assert.ok(after.services[service].volumes.some(volume => volume.type === 'volume' && volume.source === key));
    }
  });

test('actual Compose resolves canonical and legacy variables to the same configuration',
  { skip: !available }, () => {
    const canonical = configuration('PVAD');
    const legacy = configuration('PXA');
    assert.equal(canonical.status, 0, canonical.stderr);
    assert.equal(legacy.status, 0, legacy.stderr);
    const resolved = JSON.parse(canonical.stdout);
    assert.deepEqual(JSON.parse(legacy.stdout), resolved);
    assert.equal(resolved.services['pvad-webapi'].ports[0].published, '5099');
    assert.equal(resolved.services['pvad-database'].environment.POSTGRES_DB, 'pvad');
    assert.ok(resolved.volumes['pvad-api-data']);
    assert.ok(resolved.volumes['pvad-database-data']);
    assert.equal(resolved.services['pvad-mcp'].environment.PVAD_API_URL, 'http://pvad-webapi:8080');
  });

test('Compose launcher rejects conflicts before executing Docker', { skip: !available }, () => {
  const result = configuration('PVAD', { PXA_API_HOST_PORT: '5999' });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /Conflicting configuration keys/);
  assert.equal(result.stdout, '');
});

test('API legacy overlay binds verified external resources and keeps database credentials paired',
  { skip: !available }, () => {
    const overlays = ['-f', 'deploy/rename/docker-compose.api-legacy.yml'];
    const bindings = {
      PVAD_LEGACY_DATABASE_VOLUME: 'synthetic-installed-database',
      PVAD_LEGACY_API_VOLUME: 'synthetic-installed-keyring',
      PVAD_LEGACY_API_HOST_NETWORK: 'synthetic-installed-api-network',
    };
    const result = configuration('PVAD', bindings, overlays);
    assert.equal(result.status, 0, result.stderr);
    const model = JSON.parse(result.stdout);
    assert.deepEqual(model.volumes['pvad-database-data'], { external: true, name: bindings.PVAD_LEGACY_DATABASE_VOLUME });
    assert.deepEqual(model.volumes['pvad-api-data'], { external: true, name: bindings.PVAD_LEGACY_API_VOLUME });
    assert.equal(model.networks['pvad-api-host'].external, true);
    assert.equal(model.networks['pvad-api-host'].name, bindings.PVAD_LEGACY_API_HOST_NETWORK);
    assert.deepEqual(model.networks['pvad-api-host'].ipam ?? {}, {});
    assert.equal(model.services['pvad-database'].environment.POSTGRES_DB, 'pxa');
    assert.match(model.services['pvad-webapi'].environment.ConnectionStrings__PvadDatabase, /Database=pxa;Username=pxa;Password=pxa-local$/);
    assert.ok(model.services['pvad-webapi'].networks.default.aliases.includes('pxa-webapi'));
    assert.notEqual(configuration('PVAD', {}, overlays).status, 0, 'Missing physical IDs must fail closed.');
    const custom = configuration('PVAD', { ...bindings, PVAD_DATABASE_NAME: 'custom',
      PVAD_DATABASE_USER: 'custom-user', PVAD_DATABASE_PASSWORD: 'synthetic-password' }, overlays);
    assert.equal(custom.status, 0, custom.stderr);
    const configured = JSON.parse(custom.stdout);
    assert.equal(configured.services['pvad-database'].environment.POSTGRES_DB, 'custom');
    assert.match(configured.services['pvad-webapi'].environment.ConnectionStrings__PvadDatabase, /Database=custom;Username=custom-user;Password=synthetic-password$/);
  });

test('root legacy overlay retains external storage, topology and credential pairing without resource creation',
  { skip: !available }, () => {
    const bindings = Object.fromEntries(['DATABASE_VOLUME', 'MAILPIT_VOLUME', 'PROMETHEUS_VOLUME',
      'LOKI_VOLUME', 'TEMPO_VOLUME', 'GRAFANA_VOLUME', 'ALERTMANAGER_VOLUME',
      'OBSERVABILITY_NETWORK', 'INGRESS_NETWORK', 'HOST_NETWORK', 'EGRESS_NETWORK']
      .map(key => [`PVAD_LEGACY_${key}`, `synthetic-existing-${key.toLowerCase()}`]));
    const run = environment => spawnSync(process.execPath, ['tools/configuration/pvad-compose.mjs',
      '--env-file', emptyEnvFile, '-f', 'docker-compose.yml', '-f', 'deploy/rename/docker-compose.legacy.yml',
      '--profile', 'observability', 'config', '--format', 'json'], {
      cwd: root, encoding: 'utf8', env: { PATH: process.env.PATH, HOME: process.env.HOME, ...environment },
    });
    const result = run(bindings);
    assert.equal(result.status, 0, result.stderr);
    const model = JSON.parse(result.stdout);
    for (const volume of Object.values(model.volumes)) assert.equal(volume.external, true);
    for (const [name, network] of Object.entries(model.networks)) {
      if (name === 'default') continue;
      assert.equal(network.external, true);
      assert.deepEqual(network.ipam ?? {}, {});
      assert.equal(network.internal, undefined);
    }
    assert.equal(model.services['pvad-database'].environment.POSTGRES_DB, 'pxa');
    assert.match(model.services['pvad-postgres-exporter'].environment.DATA_SOURCE_URI, /\/pxa\?/);
    assert.equal(model.services['pvad-postgres-exporter'].environment.DATA_SOURCE_USER, 'pxa');
    assert.equal(model.services['pvad-database'].networks['pvad-observability'].aliases[0], 'pxa-database');
    assert.equal(model.services['pvad-grafana'].networks['pvad-observability-ingress'].ipv4_address, '172.30.50.11');
    assert.notEqual(run({}).status, 0);
  });

test('observability Compose forwards canonical Collector settings from both input generations',
  { skip: !available }, () => {
    const run = prefix => spawnSync(process.execPath, [
      'tools/configuration/pvad-compose.mjs', '--env-file', emptyEnvFile,
      '-f', 'docker-compose.yml', '--profile', 'observability', 'config', '--format', 'json',
    ], { cwd: root, encoding: 'utf8', env: {
      PATH: process.env.PATH, HOME: process.env.HOME,
      [`${prefix}_TRACE_SAMPLE_PERCENTAGE`]: '23',
      [`${prefix}_OTEL_MEMORY_LIMIT_MIB`]: '256',
      [`${prefix}_OTEL_MEMORY_SPIKE_LIMIT_MIB`]: '64',
    } });
    const current = run('PVAD'), legacy = run('PXA');
    assert.equal(current.status, 0, current.stderr);
    assert.equal(legacy.status, 0, legacy.stderr);
    const document = JSON.parse(current.stdout);
    assert.deepEqual(JSON.parse(legacy.stdout), document);
    assert.deepEqual(document.services['pvad-otel-collector'].environment, {
      PVAD_TRACE_SAMPLE_PERCENTAGE: '23', PVAD_OTEL_MEMORY_LIMIT_MIB: '256',
      PVAD_OTEL_MEMORY_SPIKE_LIMIT_MIB: '64',
    });
    assert.equal(document.services['pvad-database'].environment.POSTGRES_DB, 'pvad');
    assert.ok(document.volumes['pvad-database-data']);
  });

test('Alertmanager, webhook overlay and retention fixtures resolve identical canonical and legacy inputs',
  { skip: !available }, () => {
    for (const scenario of ['alerting', 'webhook', 'retention']) {
      const run = prefix => {
        const inputs = scenario === 'alerting'
          ? { ALERTMANAGER_CONFIG_FILE: '/tmp/synthetic-alerts.yml', ALERTMANAGER_TEMPLATES_DIR: '/tmp/synthetic-templates',
            ALERTMANAGER_SMTP_PASSWORD_FILE: '/tmp/synthetic-smtp-secret' }
          : scenario === 'webhook'
            ? { ALERT_WEBHOOK_URL: 'https://notifications.example.test/events',
              ALERT_WEBHOOK_SIGNING_KEY_FILE: '/tmp/synthetic-webhook-secret',
              ALERT_WEBHOOK_MAX_PAYLOAD_BYTES: '131072', ALERT_WEBHOOK_TIMEOUT_SECONDS: '9',
              ALERT_WEBHOOK_CPU_LIMIT: '0.5', ALERT_WEBHOOK_MEMORY_LIMIT: '96M' }
            : { TEST_METRIC_RETENTION: '365d', TEST_LOG_RETENTION: '24h', TEST_TRACE_RETENTION: '1h' };
        const files = scenario === 'alerting'
          ? ['-f', 'docker-compose.yml', '-f', 'deploy/observability/docker-compose.alerting-production.yml', '--profile', 'observability']
          : scenario === 'webhook'
            ? ['-f', 'docker-compose.yml', '-f', 'deploy/observability/docker-compose.webhook.yml', '--profile', 'observability']
            : ['-f', 'deploy/observability/retention-test/docker-compose.yml'];
        return spawnSync(process.execPath, ['tools/configuration/pvad-compose.mjs', '--env-file', emptyEnvFile,
          ...files, 'config', '--format', 'json'], { cwd: root, encoding: 'utf8',
          env: { PATH: process.env.PATH, HOME: process.env.HOME,
            ...Object.fromEntries(Object.entries(inputs).map(([key, value]) => [`${prefix}_${key}`, value])) } });
      };
      const current = run('PVAD'), legacy = run('PXA');
      assert.equal(current.status, 0, current.stderr);
      assert.equal(legacy.status, 0, legacy.stderr);
      assert.deepEqual(JSON.parse(current.stdout), JSON.parse(legacy.stdout), scenario);
      if (scenario === 'webhook') {
        const configuration = JSON.parse(current.stdout);
        const relay = configuration.services['pvad-webhook-relay'];
        assert.equal(relay.environment.WebhookRelay__Destination, 'https://notifications.example.test/events');
        assert.equal(relay.environment.WebhookRelay__MaxPayloadBytes, '131072');
        assert.equal(relay.environment.WebhookRelay__RequestTimeoutSeconds, '9');
        assert.equal(configuration.secrets.pvad_alert_webhook_signing_key.file, '/tmp/synthetic-webhook-secret');
      }
    }
  });
