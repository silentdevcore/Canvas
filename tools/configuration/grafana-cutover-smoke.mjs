import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { mkdtemp, mkdir, readFile, writeFile, rm } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { prepareGrafanaInventory, prepareGrafanaProvider } from './rename-grafana-provider.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load, dump } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
const label = 'pvade.grafana-rehearsal';
const sha256 = value => createHash('sha256').update(value).digest('hex');
const dockerCommand = (args, options = {}) => execFileSync('docker', args, {
  encoding: 'utf8', timeout: 90_000, stdio: ['pipe', 'pipe', 'pipe'],
  ...options, env: { ...process.env, ...options.env },
}).trim();

export function checkFolderTarget(folders, uid, title) {
  assert.ok(/^[a-zA-Z0-9_-]{1,64}$/.test(uid), 'Invalid synthetic folder UID.');
  assert.equal(folders.filter(folder => folder.uid === uid).length, 1, 'Original folder must exist exactly once.');
  assert.ok(!folders.some(folder => folder.title.toLowerCase() === title.toLowerCase() && folder.uid !== uid),
    'Target folder collision requires manual review.');
}

export function curlRequest(method, path, credentials, body) {
  assert.ok(['GET', 'POST', 'PUT', 'DELETE'].includes(method));
  assert.match(path, /^\/api\/[a-zA-Z0-9_/?=&.-]+$/);
  assert.ok(!path.includes('..'), 'Relative traversal is not supported.');
  const lines = [
    'silent', 'show-error', 'max-time = 10', 'noproxy = "*"',
    `url = ${JSON.stringify('http://127.0.0.1:3000' + path)}`,
    `request = ${JSON.stringify(method)}`, 'write-out = "\\n%{http_code}"',
    `user = ${JSON.stringify(credentials)}`, 'header = "Content-Type: application/json"',
  ];
  if (body !== undefined) lines.push(`data = ${JSON.stringify(JSON.stringify(body))}`);
  return lines.join('\n') + '\n';
}

export function grafanaRestoreScript(expectedHash, archive = 'grafana-backup.tar') {
  assert.match(expectedHash, /^[a-f0-9]{64}$/);
  assert.ok(['grafana-backup.tar', 'tampered.tar'].includes(archive));
  return `printf '${expectedHash}  /tmp/${archive}\\n' | sha256sum -c - >/dev/null || exit 42; tar -xpf /tmp/${archive} -C /var/lib/grafana; sha256sum /tmp/${archive}`;
}

export async function runGrafanaRehearsal({ docker = dockerCommand, progress = () => {} } = {}) {
  const compose = load(await readFile(resolve(root, 'docker-compose.yml'), 'utf8'));
  const imageTag = compose.services['pvad-grafana'].image;
  const image = docker(['image', 'inspect', '--format', '{{.Id}}', imageTag]);
  assert.match(image, /^sha256:[a-f0-9]{64}$/, 'A cached Compose-selected Grafana image is required.');
  const originalSource = await readFile(resolve(root, 'deploy/observability/grafana/provisioning/dashboards/dashboards.yml'), 'utf8');
  const prepared = prepareGrafanaProvider(originalSource);
  const inventory = prepareGrafanaInventory();
  const id = randomUUID(), prefix = `pvad-grafana-rehearsal-${id}`;
  const output = resolve(root, 'artifacts/rename/grafana-rehearsal');
  await mkdir(output, { recursive: true });
  const work = await mkdtemp(resolve(output, 'work-'));
  const resources = [], admin = `pvad-lab-admin:${randomUUID()}`, reader = `pvad-lab-reader:${randomUUID()}`;
  const denied = `pvad-lab-denied:${randomUUID()}`;
  let helperNumber = 0, report, failure, phase = 'prepare';
  const step = value => { phase = value; try { progress(value); } catch { /* Reporting cannot disable cleanup. */ } };
  const ownedLabel = (kind, name) => {
    try { return docker([kind, 'inspect', '--format', `{{index .${kind === 'volume' ? 'Labels' : 'Config.Labels'} "${label}"}}`, name]); }
    catch (error) {
      if (/no such (?:object|container|volume)|not found/i.test(String(error.stderr))) return null;
      throw new Error('Synthetic resource ownership could not be inspected.');
    }
  };
  const requireOwned = (kind, name) => assert.equal(ownedLabel(kind, name), id, 'Synthetic resource ownership mismatch.');
  const volume = suffix => {
    const name = `${prefix}-${suffix}`;
    resources.push(['volume', name]);
    docker(['volume', 'create', '--label', `${label}=${id}`, name]);
    requireOwned('volume', name);
    return name;
  };
  const request = (container, method, path, body, credentials = admin, statuses = [200]) => {
    requireOwned('container', container);
    const result = docker(['exec', '-i', container, 'curl', '--config', '-'], {
      input: curlRequest(method, path, credentials, body),
    });
    const separator = result.lastIndexOf('\n'), status = Number(result.slice(separator + 1));
    assert.ok(statuses.includes(status), `Synthetic Grafana request failed (${status}).`);
    const text = result.slice(0, separator);
    return text ? JSON.parse(text) : null;
  };
  const ready = async container => {
    for (let attempt = 0; attempt < 120; attempt++) {
      try {
        const health = request(container, 'GET', '/api/health');
        const dashboards = request(container, 'GET', '/api/search?type=dash-db&limit=1000');
        if (health.database === 'ok' && dashboards.length === inventory.dashboards.length) return;
      } catch { /* Synthetic Grafana migrations and provisioning are still starting. */ }
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    throw new Error('Synthetic Grafana readiness timed out.');
  };
  const start = async (suffix, data) => {
    const name = `${prefix}-${suffix}`;
    resources.push(['container', name]);
    docker(['run', '-d', '--pull', 'never', '--name', name, '--label', `${label}=${id}`,
      '--network', 'none', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
      '--cpus', '2', '--memory', '1g', '--pids-limit', '256',
      '--mount', `type=volume,src=${data},dst=/var/lib/grafana`,
      '--mount', `type=bind,src=${work}/provisioning,dst=/etc/grafana/provisioning,readonly`,
      '--mount', `type=bind,src=${work}/dashboards,dst=/var/lib/grafana/dashboards,readonly`,
      ...['GF_SECURITY_ADMIN_USER', 'GF_SECURITY_ADMIN_PASSWORD', 'GF_AUTH_BASIC_ENABLED',
        'GF_AUTH_ANONYMOUS_ENABLED', 'GF_AUTH_PROXY_ENABLED', 'GF_USERS_AUTO_ASSIGN_ORG_ROLE',
        'GF_PLUGINS_PREINSTALL_DISABLED', 'GF_ANALYTICS_CHECK_FOR_UPDATES',
        'GF_ANALYTICS_CHECK_FOR_PLUGIN_UPDATES', 'GF_ANALYTICS_REPORTING_ENABLED'].flatMap(key => ['--env', key]), image], {
      env: { GF_SECURITY_ADMIN_USER: admin.split(':')[0], GF_SECURITY_ADMIN_PASSWORD: admin.split(':')[1],
        GF_AUTH_BASIC_ENABLED: 'true', GF_AUTH_ANONYMOUS_ENABLED: 'false', GF_AUTH_PROXY_ENABLED: 'false',
        GF_USERS_AUTO_ASSIGN_ORG_ROLE: 'Viewer', GF_PLUGINS_PREINSTALL_DISABLED: 'true',
        GF_ANALYTICS_CHECK_FOR_UPDATES: 'false', GF_ANALYTICS_CHECK_FOR_PLUGIN_UPDATES: 'false', GF_ANALYTICS_REPORTING_ENABLED: 'false' },
    });
    requireOwned('container', name);
    await ready(name);
    return name;
  };
  const stop = name => {
    requireOwned('container', name);
    docker(['stop', '--time', '30', name]);
    assert.equal(docker(['container', 'inspect', '--format', '{{.State.Running}}', name]), 'false', 'Grafana must not be writing during backup.');
    assert.equal(docker(['container', 'inspect', '--format', '{{.State.ExitCode}}', name]), '0', 'Grafana must stop cleanly before backup.');
  };
  const helper = (mounts, script) => {
    const name = `${prefix}-helper-${helperNumber++}`;
    resources.push(['container', name]);
    for (const mount of mounts) requireOwned('volume', mount.source);
    return docker(['run', '--rm', '--pull', 'never', '--name', name, '--label', `${label}=${id}`,
      '--network', 'none', '--user', '472:0', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
      ...mounts.flatMap(mount => ['--mount', `type=volume,src=${mount.source},dst=${mount.target}${mount.readonly ? ',readonly' : ''}`]),
      '--entrypoint', 'sh', image, '-ec', script]);
  };
  const snapshot = (container, folderUid) => {
    const folder = request(container, 'GET', `/api/folders/${folderUid}`);
    const permissions = request(container, 'GET', `/api/folders/${folderUid}/permissions`).map(value => ({
      userId: value.userId ?? 0, teamId: value.teamId ?? 0, role: value.role ?? '',
      permission: value.permission, inherited: value.inherited ?? value.isInherited ?? false,
    })).sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b), 'en'));
    const dashboards = inventory.dashboards.map(expected => {
      const result = request(container, 'GET', `/api/dashboards/uid/${expected.uid}`);
      assert.equal(result.meta.folderUid, folderUid, 'Dashboard changed folder identity.');
      const { id: _id, version: _version, ...content } = result.dashboard;
      request(container, 'GET', `/api/dashboards/uid/${expected.uid}`, undefined, reader);
      request(container, 'GET', `/api/dashboards/uid/${expected.uid}`, undefined, denied, [403, 404]);
      return content;
    });
    const datasourceUids = request(container, 'GET', '/api/datasources').map(value => value.uid).sort();
    assert.deepEqual(datasourceUids, inventory.datasources.map(value => value.uid).sort());
    assert.equal(request(container, 'GET', '/api/folders?limit=100').length, 1, 'Duplicate or stale folders remain.');
    return { folderUid: folder.uid, title: folder.title, permissions, dashboards, datasourceUids };
  };
  try {
    await mkdir(resolve(work, 'provisioning/dashboards'), { recursive: true });
    await mkdir(resolve(work, 'provisioning/datasources'), { recursive: true });
    await mkdir(resolve(work, 'dashboards'));
    const providerFile = resolve(work, 'provisioning/dashboards/dashboards.yml');
    await writeFile(providerFile, originalSource);
    await writeFile(resolve(work, 'provisioning/datasources/datasources.yml'),
      await readFile(resolve(root, 'deploy/observability/grafana/provisioning/datasources/datasources.yml')));
    for (const dashboard of inventory.dashboards)
      await writeFile(resolve(work, 'dashboards', dashboard.path.split('/').at(-1)), await readFile(resolve(root, dashboard.path)));
    const data = volume('data'), backup = volume('backup'), restoredData = volume('restored');
    step('baseline');
    const original = await start('original', data);
    const folders = request(original, 'GET', '/api/folders?limit=100');
    assert.equal(folders.length, 1);
    const folderUid = folders[0].uid;
    assert.equal(folders[0].title, load(originalSource).providers[0].folder);
    const users = [];
    for (const credentials of [reader, denied]) {
      const [login, password] = credentials.split(':');
      users.push(request(original, 'POST', '/api/admin/users', { name: 'Synthetic rehearsal user', email: `${login}@synthetic.test`, login, password }).id);
    }
    request(original, 'POST', `/api/folders/${folderUid}/permissions`, { items: [{ userId: users[0], permission: 1 }] });
    const before = snapshot(original, folderUid);
    step('collision');
    request(original, 'POST', '/api/folders', { uid: 'pvad-rehearsal-collision', title: 'PVAD' });
    assert.throws(() => checkFolderTarget(request(original, 'GET', '/api/folders?limit=100'), folderUid, 'PVAD'), /collision/);
    request(original, 'DELETE', '/api/folders/pvad-rehearsal-collision');
    step('backup');
    stop(original);
    const backupHash = helper([{ source: data, target: '/var/lib/grafana', readonly: true }, { source: backup, target: '/tmp' }],
      'tar -cpf /tmp/grafana-backup.tar -C /var/lib/grafana .; sha256sum /tmp/grafana-backup.tar').split(/\s/)[0];
    assert.match(backupHash, /^[a-f0-9]{64}$/);
    step('corrupt-backup');
    helper([{ source: backup, target: '/tmp' }], 'cp /tmp/grafana-backup.tar /tmp/tampered.tar; printf tampered >> /tmp/tampered.tar');
    assert.throws(() => helper([{ source: backup, target: '/tmp', readonly: true }, { source: restoredData, target: '/var/lib/grafana' }],
      grafanaRestoreScript(backupHash, 'tampered.tar')), error => error.status === 42);
    assert.equal(helper([{ source: restoredData, target: '/var/lib/grafana', readonly: true }],
      'test ! -e /var/lib/grafana/grafana.db; printf untouched'), 'untouched');
    step('rename');
    docker(['start', original]);
    await ready(original);
    checkFolderTarget(request(original, 'GET', '/api/folders?limit=100'), folderUid, 'PVAD');
    const folder = request(original, 'GET', `/api/folders/${folderUid}`);
    request(original, 'PUT', `/api/folders/${folderUid}`, { title: 'PVAD', version: folder.version });
    stop(original);
    const candidate = structuredClone(prepared.candidate);
    candidate.providers[0].folderUid = folderUid;
    await writeFile(providerFile, dump(candidate, { noRefs: true }));
    const current = await start('current', data);
    const after = snapshot(current, folderUid);
    assert.equal(after.title, 'PVAD');
    assert.deepEqual({ ...after, title: before.title }, before, 'Cutover changed content, identities or effective permissions.');
    request(current, 'POST', '/api/annotations', { time: 1700000000000, tags: ['pvad-rehearsal-after-backup'], text: 'Synthetic post-backup marker' });
    assert.equal(request(current, 'GET', '/api/annotations?tags=pvad-rehearsal-after-backup').length, 1);
    stop(current);
    step('restore');
    const restoredHash = helper([{ source: backup, target: '/tmp', readonly: true }, { source: restoredData, target: '/var/lib/grafana' }],
      grafanaRestoreScript(backupHash)).split(/\s/)[0];
    assert.equal(restoredHash, backupHash, 'Backup checksum changed before restore.');
    await writeFile(providerFile, originalSource);
    const restored = await start('restored', restoredData);
    const rollback = snapshot(restored, folderUid);
    assert.deepEqual(rollback, before, 'Restored placement, permissions or content differ from the snapshot.');
    assert.equal(request(restored, 'GET', '/api/annotations?tags=pvad-rehearsal-after-backup').length, 0);
    stop(restored);
    assert.deepEqual(prepareGrafanaInventory(), inventory, 'Reviewed source changed during the rehearsal.');
    report = { schemaVersion: 1, status: 'passed', deploymentAuthorized: false, syntheticOnly: true,
      imageTag, image, dashboardCount: inventory.dashboards.length, datasourceCount: inventory.datasources.length,
      originalFolderUidPreserved: true, permissionsPreserved: true, unauthorizedReadersDenied: true,
      collisionRejected: true, corruptArchiveRejectedBeforeExtraction: true,
      pointInTimeRestoreVerified: true, postSnapshotWritesReplayed: false,
      backupSha256: backupHash, baselineSha256: sha256(JSON.stringify(before)), restoredSha256: sha256(JSON.stringify(rollback)),
      sourceInventorySha256: sha256(JSON.stringify(inventory)),
      limits: ['synthetic-basic-auth-not-pvad-gateway', 'no-live-query-or-alert-delivery-check',
        'sqlite-only-same-image', 'not-customer-backup-or-production-cutover-approval', 'post-backup-writes-require-separate-reconciliation'] };
  } catch { failure = new Error(`Isolated Grafana rehearsal failed during ${phase}; no external deployment was authorized.`); }
  step('cleanup');
  const cleanupFailures = [];
  for (const [kind, name] of [...resources.filter(([kind]) => kind === 'container').reverse(), ...resources.filter(([kind]) => kind === 'volume').reverse()]) {
    try {
      const owner = ownedLabel(kind, name);
      if (owner === null) continue;
      assert.equal(owner, id, 'Resource ownership changed.');
      docker(kind === 'container' ? ['rm', '-f', name] : ['volume', 'rm', name]);
      assert.equal(ownedLabel(kind, name), null, 'Synthetic resource remains.');
    } catch { cleanupFailures.push(kind); }
  }
  await rm(work, { recursive: true, force: true });
  if (cleanupFailures.length) throw new Error('Synthetic cleanup incomplete; inspect only resources with the rehearsal ownership label.');
  if (failure) throw failure;
  return { ...report, temporaryResourcesRemoved: true };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const reportFile = resolve(root, 'artifacts/rename/grafana-rehearsal/report.json');
  let startedAt;
  try {
    if (process.argv.length !== 2) throw new Error('No arguments or external resource names are accepted.');
    await mkdir(resolve(root, 'artifacts/rename/grafana-rehearsal'), { recursive: true });
    startedAt = new Date().toISOString();
    await writeFile(reportFile, JSON.stringify({ schemaVersion: 1, status: 'running', startedAt, deploymentAuthorized: false }) + '\n');
    const report = await runGrafanaRehearsal({ progress: phase => console.error(`Grafana rehearsal: ${phase}`) });
    await writeFile(reportFile, JSON.stringify({ ...report, startedAt, completedAt: new Date().toISOString() }, null, 2) + '\n');
    console.log(JSON.stringify(report));
  } catch (error) {
    if (startedAt) await writeFile(reportFile, JSON.stringify({ schemaVersion: 1, status: 'failed', startedAt,
      completedAt: new Date().toISOString(), deploymentAuthorized: false }) + '\n');
    console.error(error.message);
    process.exitCode = 1;
  }
}
