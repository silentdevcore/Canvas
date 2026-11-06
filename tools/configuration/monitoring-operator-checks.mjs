import assert from 'node:assert/strict';
import { createHash, randomBytes } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { prepareGrafanaProvider } from './rename-grafana-provider.mjs';
import { checkFolderTarget, grafanaRestoreScript } from './grafana-cutover-smoke.mjs';

export async function eventually(description, operation, { attempts = 120, interval = 1000 } = {}) {
  for (let attempt = 0; attempt < attempts; attempt++) {
    try { const result = await operation(); if (result) return result; } catch { /* Bounded startup/export/retry convergence. */ }
    await delay(interval);
  }
  throw new Error(`Monitoring evidence timed out: ${description}.`);
}

export function traceIds(document) {
  const found = new Set();
  const visit = node => {
    if (!node || typeof node !== 'object') return;
    for (const [key, value] of Object.entries(node)) {
      if (key === 'traceId' && typeof value === 'string') {
        const hex = /^[a-f0-9]{32}$/i.test(value) ? value.toLowerCase() : Buffer.from(value, 'base64').toString('hex');
        if (/^[a-f0-9]{32}$/.test(hex)) found.add(hex);
      } else visit(value);
    }
  };
  visit(document); return [...found].sort();
}

export function normalizedSnapshot(value) {
  return { folderUid: value.folderUid, title: value.title,
    permissions: value.permissions.map(item => Object.fromEntries(['userId', 'teamId', 'role', 'permission', 'inherited', 'isInherited']
      .filter(key => item[key] !== undefined).map(key => [key, item[key]]))).sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b), 'en')),
    dashboards: value.dashboards.map(({ dashboard, meta }) => {
      const { id: _id, version: _version, ...content } = dashboard;
      assert.equal(meta.folderUid, value.folderUid);
      return content;
    }), datasourceUids: value.datasourceUids.sort(),
  };
}

export async function exerciseMonitoring(context) {
  const { root, directory, provisioning, runId, network, label, names, docker, resources, namesByService,
    data, backup, configuration, receiverUrl, report, grafanaImage, request, page, anonymousRequest, memberRequest, output, collectorEndpoint,
    screenshot, checked, origin, inventory, step, owned } = context;
  const { load, dump } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
  const grafana = '/operator/grafana';
  const api = async (method, path, body) => {
    const response = await request[method](`${origin}${grafana}${path}`, { data: body,
      headers: { Accept: 'application/json' }, timeout: 10_000 });
    assert.equal(response.status(), 200, `Protected Grafana request failed: ${path.split('?')[0]}.`);
    return response.json();
  };
  const source = type => { const result = inventory.datasources.find(value => value.type === type); assert.ok(result); return result.uid; };
  const proxy = type => `/api/datasources/proxy/uid/${source(type)}`;
  const metricsQuery = 'sum(http_server_request_duration_seconds_count{http_route=~".*version"})';
  const metric = async () => {
    const result = await api('get', `${proxy('prometheus')}/api/v1/query?query=${encodeURIComponent(metricsQuery)}`);
    assert.equal(result.status, 'success');
    const value = Number(result.data.result[0]?.value[1]);
    return Number.isFinite(value) ? value : null;
  };
  step('monitoring/live-correlation');
  const before = await eventually('real request counter baseline', async () => (await metric()) || null);
  const trace = randomBytes(16).toString('hex'), span = randomBytes(8).toString('hex');
  const privacyProbe = `never-persist-${randomBytes(12).toString('hex')}`;
  const response = await request.get(`${origin}/api/pvad/v1/version?token=${privacyProbe}`, {
    headers: { traceparent: `00-${trace}-${span}-01` }, timeout: 10_000 });
  assert.equal(response.status(), 200);
  const after = await eventually('request counter increase', async () => { const value = await metric(); return value > before ? value : null; });
  const query = `{service_name="pvad-webapi"} | trace_id = "${trace}"`;
  const logs = await eventually('same-request Loki logs', async () => {
    const result = await api('get', `${proxy('loki')}/loki/api/v1/query_range?query=${encodeURIComponent(query)}&limit=100`);
    assert.equal(result.status, 'success');
    const values = result.data.result.flatMap(stream => stream.values);
    return values.length ? values : null;
  });
  const traceDocument = await eventually('same-request Tempo trace', async () => {
    const document = await api('get', `${proxy('tempo')}/api/traces/${trace}`);
    return traceIds(document).includes(trace) ? document : null;
  });
  assert.ok(!JSON.stringify(logs).includes(privacyProbe), 'Query canary leaked into logs.');
  assert.ok(!JSON.stringify(traceDocument).includes(privacyProbe), 'Query canary leaked into traces.');
  assert.ok(!(await readFile(resolve(output, 'api.log'), 'utf8')).includes(privacyProbe), 'Query canary leaked into console logs.');
  report.correlation = { metricsViaGrafana: true, metricCounterIncreased: after > before, logsViaGrafana: true,
    traceViaGrafana: true, sameRequestTraceId: trace, logCount: logs.length, sensitiveQueryAbsent: true, consoleQueryAbsent: true };
  checked('real-api-metrics-logs-traces-correlated-through-grafana');

  step('monitoring/alert-firing-and-recovery');
  const events = async () => {
    const result = await fetch(`${receiverUrl}/events`, { signal: AbortSignal.timeout(3000) });
    assert.ok(result.ok); return result.json();
  };
  assert.equal((await events()).events.length, 0, 'No startup alert may masquerade as the outage test.');
  owned('container', namesByService['pvad-otel-collector']);
  docker(['stop', '--time', '15', namesByService['pvad-otel-collector']]);
  const firing = await eventually('signed firing delivery and real retry', async () => {
    const state = await events();
    return state.events.some(value => value.status === 'firing' && value.accepted) ? state : null;
  });
  assert.equal(firing.invalid, 0);
  const rejected = firing.events.find(value => value.status === 'firing' && !value.accepted);
  const accepted = firing.events.find(value => value.status === 'firing' && value.accepted);
  assert.ok(rejected, 'A real failed HTTPS delivery must precede retry.');
  assert.equal(accepted.payloadSha256, rejected.payloadSha256, 'Retries must retain payload idempotency.');
  const alertStatus = await api('get', `${proxy('prometheus')}/api/v1/alerts`);
  assert.ok(alertStatus.data.alerts.some(value => value.labels.alertname === configuration.alertName && value.state === 'firing'));
  owned('container', namesByService['pvad-otel-collector']);
  docker(['start', namesByService['pvad-otel-collector']]);
  const ingress = JSON.parse(docker(['inspect', namesByService['pvad-otel-collector']]))[0].NetworkSettings.Ports['4317/tcp'];
  assert.equal(ingress.length, 1);
  assert.equal(ingress[0].HostIp, '127.0.0.1');
  assert.equal(`http://127.0.0.1:${ingress[0].HostPort}`, collectorEndpoint, 'Collector restart changed the API ingestion endpoint.');
  const recovered = await eventually('signed recovery delivery', async () => {
    const state = await events();
    return state.events.some(value => value.status === 'resolved' && value.accepted) ? state : null;
  });
  assert.equal(recovered.invalid, 0);
  const healthy = await eventually('actual Prometheus target recovery', async () => {
    const result = await api('get', `${proxy('prometheus')}/api/v1/query?query=${encodeURIComponent('up{job="pvad-otel-collector"}')}`);
    return result.data.result[0]?.value[1] === '1';
  });
  assert.ok(healthy);
  report.alertDelivery = { firing: true, resolved: true, realRetry: true, signatureVerified: true, httpsCertificateValidated: true,
    idempotencyStableAcrossRetry: true, collectorIngressPreserved: true, invalidDeliveries: recovered.invalid, deliveryAttempts: recovered.events.length };
  checked('real-prometheus-alertmanager-signed-https-retry-and-recovery');

  step('monitoring/grafana-cutover');
  const folders = await api('get', '/api/folders?limit=100');
  assert.equal(folders.length, 1);
  const folderUid = folders[0].uid;
  const providerFile = resolve(provisioning, 'dashboards/dashboards.yml');
  const originalSource = await readFile(providerFile, 'utf8');
  const candidate = prepareGrafanaProvider(originalSource).candidate;
  candidate.providers[0].folderUid = folderUid;
  const snapshot = async () => {
    const folder = await api('get', `/api/folders/${folderUid}`);
    const permissions = await api('get', `/api/folders/${folderUid}/permissions`);
    const dashboards = [];
    for (const dashboard of inventory.dashboards) dashboards.push(await api('get', `/api/dashboards/uid/${dashboard.uid}`));
    const datasources = await api('get', '/api/datasources');
    assert.equal((await api('get', '/api/folders?limit=100')).length, 1);
    return normalizedSnapshot({ folderUid, title: folder.title, permissions, dashboards, datasourceUids: datasources.map(value => value.uid) });
  };
  const baseline = await snapshot();
  checkFolderTarget(folders, folderUid, candidate.providers[0].folder);
  const assertAccess = async phase => {
    assert.equal((await anonymousRequest.get(`${origin}${grafana}/api/user`, { maxRedirects: 0 })).status(), 401);
    assert.equal((await memberRequest.get(`${origin}${grafana}/api/user`, { maxRedirects: 0 })).status(), 403);
    assert.equal((await request.get(`${origin}/documentation`)).status(), 200);
    const user = await api('get', '/api/user');
    assert.ok(user.login);
    await page.setViewportSize({ width: 1440, height: 980 });
    await page.goto(`${origin}${grafana}/dashboards/f/${encodeURIComponent(folderUid)}`, { waitUntil: 'domcontentloaded' });
    for (const dashboard of inventory.dashboards) await page.getByText(dashboard.title, { exact: true }).first().waitFor();
    await screenshot(`monitoring-${phase}-desktop.png`);
    await page.setViewportSize({ width: 390, height: 844 });
    await screenshot(`monitoring-${phase}-mobile.png`);
    await page.setViewportSize({ width: 1440, height: 980 });
    return user.login;
  };
  const operatorIdentity = await assertAccess('before');
  const stopGrafana = () => {
    owned('container', names.grafana);
    docker(['stop', '--time', '30', names.grafana]);
    const state = JSON.parse(docker(['inspect', names.grafana]))[0].State;
    assert.ok(!state.Running && state.ExitCode === 0, 'Backup requires a cleanly stopped Grafana.');
  };
  const readyGrafana = async () => {
    owned('container', names.grafana);
    docker(['start', names.grafana]);
    await eventually('Grafana restart behind operator gateway', async () => {
      const health = await api('get', '/api/health');
      return health.database === 'ok' && (await api('get', '/api/search?type=dash-db')).length === inventory.dashboards.length;
    });
  };
  let helperCount = 0;
  const helper = (script, writeData = false) => {
    const helperName = `pvad-monitoring-${runId}-backup-${helperCount++}`;
    for (const name of [data, backup])
      assert.equal(docker(['volume', 'inspect', '--format', `{{index .Labels "${label}"}}`, name]), runId);
    resources.push(['container', helperName]);
    return docker(['run', '--rm', '--pull', 'never', '--name', helperName, '--label', `${label}=${runId}`,
      '--network', 'none', '--user', '472:0', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
      '--mount', `type=volume,src=${data},dst=/var/lib/grafana${writeData ? '' : ',readonly'}`,
      '--mount', `type=volume,src=${backup},dst=/tmp`, '--entrypoint', 'sh', grafanaImage, '-ec', script]);
  };
  stopGrafana();
  const backupHash = helper('tar -cpf /tmp/grafana-backup.tar -C /var/lib/grafana .; sha256sum /tmp/grafana-backup.tar').split(/\s/)[0];
  assert.match(backupHash, /^[a-f0-9]{64}$/);
  helper('cp /tmp/grafana-backup.tar /tmp/tampered.tar; printf tampered >> /tmp/tampered.tar');
  assert.throws(() => helper(grafanaRestoreScript(backupHash, 'tampered.tar'), true), error => error.status === 42);
  await readyGrafana();
  const folder = await api('get', `/api/folders/${folderUid}`);
  await api('put', `/api/folders/${folderUid}`, { title: candidate.providers[0].folder, version: folder.version });
  stopGrafana();
  await writeFile(providerFile, dump(candidate, { noRefs: true }));
  await readyGrafana();
  const afterCutover = await snapshot();
  assert.equal(afterCutover.title, candidate.providers[0].folder);
  assert.deepEqual({ ...afterCutover, title: baseline.title }, baseline, 'Cutover changed IDs, content or ACLs.');
  assert.equal(await assertAccess('renamed'), operatorIdentity);
  assert.equal((await request.get(`${origin}/api/pvad/v1/version`)).status(), 200);
  await eventually('live datasource after rename', async () => (await metric()) > 0);
  await api('post', '/api/annotations', { time: 1700000000000, tags: ['pvad-post-backup'], text: 'Synthetic post-backup marker' });
  assert.equal((await api('get', '/api/annotations?tags=pvad-post-backup')).length, 1);
  checked('protected-grafana-rename-preserves-uids-acls-and-live-query');

  step('monitoring/grafana-restore');
  stopGrafana();
  helper('find /var/lib/grafana -mindepth 1 -maxdepth 1 -exec rm -rf -- {} +', true);
  const restoredHash = helper(grafanaRestoreScript(backupHash), true).split(/\s/)[0];
  assert.equal(restoredHash, backupHash);
  await writeFile(providerFile, originalSource);
  await readyGrafana();
  const restored = await snapshot();
  assert.deepEqual(restored, baseline, 'Restored Grafana differs from its original point-in-time state.');
  assert.equal((await api('get', '/api/annotations?tags=pvad-post-backup')).length, 0);
  assert.equal(await assertAccess('restored'), operatorIdentity);
  assert.equal((await request.get(`${origin}/api/pvad/v1/version`)).status(), 200);
  await eventually('live datasource after restore', async () => (await metric()) > 0);
  const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex');
  report.cutover = { folderUidPreserved: true, datasourceUidsPreserved: true, dashboardContentPreserved: true,
    permissionsPreserved: true, operatorIdentityPreserved: true, anonymousDeniedAllPhases: true, organizationAdminDeniedAllPhases: true,
    corruptBackupRejected: true, pointInTimeRestoreVerified: true, postBackupMarkerRemoved: true,
    backupSha256: backupHash, baselineSha256: digest(baseline), restoredSha256: digest(restored) };
  report.status = 'passed';
  checked('protected-grafana-verified-backup-corruption-rejection-and-restore');
}
