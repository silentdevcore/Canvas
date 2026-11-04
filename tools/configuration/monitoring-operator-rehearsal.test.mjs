import assert from 'node:assert/strict';
import { createHash, createHmac } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { collectorBinding, monitoringConfiguration, monitoringImages } from './monitoring-operator-lab.mjs';
import { eventually, normalizedSnapshot, traceIds } from './monitoring-operator-checks.mjs';
import { verifyDelivery } from './monitoring-webhook-receiver.mjs';
import { removeOwnedResources } from './operator-browser-rehearsal.mjs';
import { publicGuidesPlugin } from '../../websites/PVAD.Documentation/publicGuides.js';

test('operator recovery instructions are excluded from public Documentation assets and navigation', () => {
  const emitted = [];
  publicGuidesPlugin().generateBundle.call({ emitFile: file => emitted.push(file.fileName) });
  const navigation = readFileSync(new URL('../../websites/PVAD.Documentation/src/main.js', import.meta.url), 'utf8');
  for (const name of ['pvad-integrated-monitoring-rehearsal', 'pvad-monitoring-identity-migration', 'pvad-grafana-provider-cutover']) {
    assert.ok(!emitted.some(path => path.includes(name)));
    assert.ok(!navigation.includes(name));
  }
});

test('the lab uses configured image tags and never substitutes latest monitoring images', () => {
  const services = Object.fromEntries(['pvad-otel-collector', 'pvad-prometheus', 'pvad-loki', 'pvad-tempo', 'pvad-alertmanager']
    .map(key => [key, { image: `${key}:synthetic-pinned` }]));
  const images = monitoringImages({ services });
  assert.equal(images.length, 7);
  assert.ok(images.slice(0, 5).every(value => value.endsWith(':synthetic-pinned')));
  assert.ok(!images.some(value => value.endsWith(':latest')));
});

test('Collector restarts retain an explicit loopback ingestion port instead of reallocating an empty Docker host binding', () => {
  assert.equal(collectorBinding(12345), '127.0.0.1:12345:4317');
  for (const port of [0, -1, 65536, '12345', 1.5, undefined]) assert.throws(() => collectorBinding(port));
  const source = readFileSync(new URL('./monitoring-operator-checks.mjs', import.meta.url), 'utf8');
  assert.match(source, /ingestion endpoint/);
  assert.match(source, /collectorEndpoint/);
});

test('only synthetic timing/routing is changed, not the production outage expression or original configuration', () => {
  const input = { prometheus: { global: { scrape_interval: '15s', evaluation_interval: '15s' },
    rule_files: ['/production/rules'], scrape_configs: [{ job_name: 'pvad-otel-collector', static_configs: [{ targets: ['pvad-otel-collector:8889'] }] },
      { job_name: 'pvad-postgresql' }] },
  rules: { groups: [{ rules: [{ alert: 'PvadTelemetryPipelineUnavailable', expr: 'up{job="synthetic"} == 0',
    for: '2m', labels: { severity: 'critical', service: 'pvad-observability' }, annotations: { summary: 'Synthetic outage' } }] }] } };
  const before = structuredClone(input);
  const result = monitoringConfiguration(input);
  assert.deepEqual(input, before);
  const actual = result.rules.groups[0].rules[0];
  assert.equal(actual.expr, before.rules.groups[0].rules[0].expr);
  assert.equal(actual.for, '4s');
  assert.equal(actual.labels.severity, 'critical');
  assert.equal(actual.labels.environment, 'isolated-rehearsal');
  assert.deepEqual(result.prometheus.scrape_configs, [before.prometheus.scrape_configs[0]]);
  assert.throws(() => monitoringConfiguration({ ...input, rules: { groups: [] } }), /must exist/);
});

const secret = Buffer.from('synthetic-test-key-with-at-least-32-bytes');
const contract = { timestampHeader: 'X-Test-Time', signatureHeader: 'X-Test-Signature',
  idempotencyHeader: 'X-Test-Key', alertName: 'SyntheticAlert' };
const now = 1_800_000_000_000;
function signed(status = 'firing') {
  const body = Buffer.from(JSON.stringify({ status, alerts: [{ status, labels: {
    alertname: 'SyntheticAlert', severity: 'critical', service: 'pvad-observability', environment: 'isolated-rehearsal',
  }, annotations: { summary: 'Synthetic outage' } }] }));
  const timestamp = String(now / 1000);
  const headers = { 'x-test-time': timestamp,
    'x-test-signature': `sha256=${createHmac('sha256', secret).update(timestamp + '\n').update(body).digest('hex')}`,
    'x-test-key': createHash('sha256').update(body).digest('hex') };
  return { body, headers };
}

test('receiver verifies actual relay signature format, timestamp and payload idempotency for firing and recovery', () => {
  for (const status of ['firing', 'resolved']) {
    const { body, headers } = signed(status);
    assert.deepEqual(verifyDelivery(body, headers, contract, secret, now), { status,
      payloadSha256: headers['x-test-key'], signatureVerified: true, idempotencyVerified: true });
  }
});

test('receiver rejects modified payloads, wrong keys, stale/future timestamps and invalid idempotency', () => {
  const { body, headers } = signed();
  assert.throws(() => verifyDelivery(Buffer.concat([body, Buffer.from(' ')]), headers, contract, secret, now));
  assert.throws(() => verifyDelivery(body, headers, contract, Buffer.from('different'), now));
  for (const offset of [-61_000, 61_000])
    assert.throws(() => verifyDelivery(body, headers, contract, secret, now + offset), /Expired/);
  for (const key of Object.keys(headers))
    assert.throws(() => verifyDelivery(body, { ...headers, [key]: 'invalid' }, contract, secret, now));
});

test('unsupported payload fields cannot be mistaken for sanitizer coverage', () => {
  const original = signed();
  const payload = JSON.parse(original.body); payload.customerEmail = 'synthetic@invalid.test';
  const body = Buffer.from(JSON.stringify(payload));
  const headers = { ...original.headers,
    'x-test-signature': `sha256=${createHmac('sha256', secret).update(original.headers['x-test-time'] + '\n').update(body).digest('hex')}`,
    'x-test-key': createHash('sha256').update(body).digest('hex') };
  assert.throws(() => verifyDelivery(body, headers, contract, secret, now));
});

test('Tempo correlation handles hex and protobuf JSON trace IDs without matching unrelated values', () => {
  const id = '0123456789abcdef0123456789abcdef';
  assert.deepEqual(traceIds({ batches: [{ spans: [{ traceId: id }, { traceId: Buffer.from(id, 'hex').toString('base64') }] }] }), [id]);
  assert.deepEqual(traceIds({ documentName: id, spanId: id, traceId: 'invalid' }), []);
});

test('snapshot ignores only Grafana serialization revisions, not dashboard UIDs, content, folder or ACL changes', () => {
  const input = { folderUid: 'synthetic-folder', title: 'Original', permissions: [{ userId: 1, permission: 4, role: 'Admin' }],
    datasourceUids: ['metrics'], dashboards: [{ dashboard: { id: 7, version: 2, uid: 'synthetic', panels: [{ title: 'Real panel' }] },
      meta: { folderUid: 'synthetic-folder' } }] };
  const changedRevision = structuredClone(input); changedRevision.dashboards[0].dashboard.version++;
  assert.deepEqual(normalizedSnapshot(input), normalizedSnapshot(changedRevision));
  const changedPanel = structuredClone(input); changedPanel.dashboards[0].dashboard.panels[0].title = 'Changed';
  assert.notDeepEqual(normalizedSnapshot(input), normalizedSnapshot(changedPanel));
  const changedAcl = structuredClone(input); changedAcl.permissions[0].permission = 1;
  assert.notDeepEqual(normalizedSnapshot(input), normalizedSnapshot(changedAcl));
  const wrongFolder = structuredClone(input); wrongFolder.dashboards[0].meta.folderUid = 'other';
  assert.throws(() => normalizedSnapshot(wrongFolder));
});

test('eventual checks retry bounded convergence but fail instead of manufacturing successful evidence', async () => {
  let calls = 0;
  assert.equal(await eventually('synthetic', () => ++calls === 3 && 'ready', { attempts: 3, interval: 0 }), 'ready');
  await assert.rejects(eventually('missing', () => null, { attempts: 2, interval: 0 }), /timed out: missing/);
});

test('cleanup includes owned backup volumes and rejects unrelated volume ownership', async () => {
  const removed = new Set();
  await removeOwnedResources([['volume', 'synthetic']], 'owned', args => {
    if (removed.has(args.at(-1))) { const error = new Error(); error.stderr = 'No such volume'; throw error; }
    if (args[0] === 'volume' && args[1] === 'rm') removed.add(args.at(-1));
    return 'owned';
  });
  assert.ok(removed.has('synthetic'));
  await assert.rejects(removeOwnedResources([['volume', 'foreign']], 'owned', () => 'different'), /ownership mismatch/);
});

test('the lab uses the unchanged relay and real gateway without TLS or authentication bypasses', () => {
  const setup = readFileSync(new URL('./monitoring-operator-lab.mjs', import.meta.url), 'utf8');
  const scenarios = readFileSync(new URL('./monitoring-operator-checks.mjs', import.meta.url), 'utf8');
  assert.match(setup, /WebhookRelay__Destination=https:\/\/pvad-webhook-receiver:8443\/alerts/);
  assert.match(setup, /SSL_CERT_FILE=\/lab\/receiver\.crt/);
  assert.match(scenarios, /anonymousRequest\.get/);
  assert.match(scenarios, /sameRequestTraceId: trace/);
  assert.doesNotMatch(setup + scenarios, /rejectUnauthorized:\s*false|ignoreHTTPSErrors|route\.fulfill|NODE_TLS_REJECT_UNAUTHORIZED/);
});

test('the integrated CLI refuses deployment arguments before contacting Docker', () => {
  const result = spawnSync(process.execPath, ['tools/configuration/monitoring-operator-rehearsal.mjs', '--apply'],
    { encoding: 'utf8', env: { ...process.env, DOCKER_HOST: 'unix:///nonexistent-synthetic.sock' } });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No arguments or external resource names/);
});
