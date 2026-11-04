import assert from 'node:assert/strict';
import { execFileSync, spawn, spawnSync } from 'node:child_process';
import { createHash, randomBytes, randomUUID } from 'node:crypto';
import { createWriteStream } from 'node:fs';
import { lstat, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { createServer } from 'node:net';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { createRequire } from 'node:module';
import { prepareGrafanaInventory } from './rename-grafana-provider.mjs';
import { exerciseOperatorBrowser } from './operator-browser-scenarios.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
const label = 'pvade.operator-browser-rehearsal';
const output = resolve(root, 'artifacts/rename/operator-browser-rehearsal');
const operatorEmail = 'operator@browser.test';
export const memberEmail = 'member@browser.test';
const persistence = 'src/Infrastructure/PVAD.Infrastructure.Persistence/PVAD.Infrastructure.Persistence.csproj';
const gatewayTemplate = 'deploy/observability/operator-gateway/templates/default.conf.template';
const gatewayAdapter = 'deploy/observability/operator-gateway/15-pvad-operator.envsh';
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const dockerCommand = (args, { mergeOutput = false, ...options } = {}) => {
  const settings = { encoding: 'utf8', timeout: 60_000, stdio: ['pipe', 'pipe', 'pipe'], ...options, env: { ...process.env, ...options.env } };
  if (!mergeOutput) return execFileSync('docker', args, settings).trim();
  const result = spawnSync('docker', args, settings);
  assert.equal(result.status, 0, 'Owned container diagnostics unavailable.');
  return result.stdout + result.stderr;
};

export function childEnvironment(environment) {
  const allow = ['PATH', 'HOME', 'TMPDIR', 'DOTNET_ROOT', 'DOTNET_CLI_HOME', 'NUGET_PACKAGES', 'LANG', 'LC_ALL'];
  return Object.fromEntries(allow.filter(key => environment[key] !== undefined).map(key => [key, environment[key]]));
}

export function gatewayAddress(subnet) {
  assert.match(subnet, /^(?:\d{1,3}\.){3}\d{1,3}\/(?:16|24)$/);
  const [address, prefix] = subnet.split('/');
  const octets = address.split('.').map(Number);
  assert.ok(octets.every(value => value >= 0 && value <= 255));
  assert.ok(octets[3] === 0 && (prefix !== '16' || octets[2] === 0));
  assert.ok(octets[0] === 10 || octets[0] === 172 && octets[1] >= 16 && octets[1] <= 31 || octets[0] === 192 && octets[1] === 168);
  return [...octets.slice(0, 3), 10].join('.');
}

export function apiListeningAddress(line) {
  try {
    const event = JSON.parse(line);
    if (event.category !== 'Microsoft.Hosting.Lifetime' || event.eventName !== 'ListeningOnAddress') return null;
    const address = new URL(event.attributes?.address);
    if (address.protocol !== 'http:' || address.hostname !== '127.0.0.1' || address.pathname !== '/' || address.search || address.hash ||
      address.username || address.password || !address.port || Number(address.port) < 1 || Number(address.port) > 65535) return null;
    return address.origin;
  } catch { return null; }
}

export async function removeOwnedResources(resources, runId, docker) {
  const failures = [];
  for (const [kind, name] of [...resources].reverse()) {
    try {
      let actual;
      try { actual = docker([kind, 'inspect', '--format', `{{index .${kind === 'container' ? 'Config.Labels' : 'Labels'} "${label}"}}`, name]); }
      catch (error) {
        if (/no such (?:object|container|network)|not found/i.test(String(error.stderr))) continue;
        throw new Error('Browser rehearsal ownership inspection failed.');
      }
      assert.equal(actual, runId, 'Browser rehearsal ownership mismatch.');
      docker(kind === 'container' ? ['rm', '-f', '-v', name] : ['network', 'rm', name]);
      let absent = false;
      try { docker([kind, 'inspect', name]); }
      catch (error) { absent = /no such (?:object|container|network)|not found/i.test(String(error.stderr)); }
      assert.ok(absent, 'Synthetic resource removal could not be verified.');
    } catch (error) { failures.push(error); }
  }
  if (failures.length) throw new AggregateError(failures, 'Browser rehearsal cleanup incomplete: ownership inspection failed or ownership mismatch.');
}

async function freePort() {
  return new Promise((yes, no) => {
    const server = createServer();
    server.once('error', no);
    server.listen(0, '127.0.0.1', () => { const port = server.address().port; server.close(error => error ? no(error) : yes(port)); });
  });
}

export async function runOperatorBrowserRehearsal({ docker = dockerCommand, progress = () => {}, recordFailure = false } = {}) {
  const config = load(await readFile(resolve(root, 'docker-compose.yml'), 'utf8'));
  const images = [config.services['pvad-grafana'].image, config.services['pvad-operator-gateway'].image, 'postgres:17-alpine']
    .map(tag => {
      const id = docker(['image', 'inspect', '--format', '{{.Id}}', tag]);
      assert.match(id, /^sha256:[a-f0-9]{64}$/, 'Cached images are required; this runner never pulls.');
      return { tag, id };
    });
  const inventory = prepareGrafanaInventory(root);
  const sourcePaths = [...new Set([...inventory.sources.map(value => value.path), gatewayAdapter, 'tools/configuration/environment.sh',
    'deploy/observability/operator-gateway/site/index.html', 'deploy/observability/operator-gateway/site/app.js',
    'deploy/observability/operator-gateway/site/styles.css', 'deploy/websites/nginx.conf.template',
    'deploy/websites/15-pvad-environment.envsh', 'deploy/websites/route-patterns.env.example',
    'websites/PVAD.Admin/src/main.js', 'websites/PVAD.Admin/src/api.js', 'PVAD.WebApi/Program.cs',
    'PVAD.WebApi/Security/PvadCookieAuthenticationEvents.cs', 'PVAD.WebApi/Security/PvadPermissionClaimsTransformation.cs'])].sort();
  const checksums = async () => {
    const values = [];
    for (const path of sourcePaths) {
      assert.ok((await lstat(resolve(root, path))).isFile());
      values.push({ path, sha256: hash(await readFile(resolve(root, path))) });
    }
    return values;
  };
  const baseline = await checksums();
  const harnessPaths = ['tools/configuration/operator-browser-rehearsal.mjs', 'tools/configuration/operator-browser-scenarios.mjs'];
  const harnessChecksums = async () => {
    const values = [];
    for (const path of harnessPaths) values.push({ path, sha256: hash(await readFile(resolve(root, path))) });
    return values;
  };
  const harnessBaseline = await harnessChecksums();
  const runId = randomUUID(), network = `pvad-operator-browser-${runId}`;
  const names = { database: `${network}-db`, grafana: `${network}-grafana`, ui: `${network}-ui`, gateway: `${network}-gateway` };
  const resources = [], children = [], logs = [], secrets = [];
  const step = value => { phase = value; try { progress(value); } catch { /* Diagnostics must not prevent cleanup. */ } };
  let phase = 'prepare', work, result, failure;
  const owner = (kind, name) => docker([kind, 'inspect', '--format', `{{index .${kind === 'container' ? 'Config.Labels' : 'Labels'} "${label}"}}`, name]);
  const owned = (kind, name) => assert.equal(owner(kind, name), runId, 'Browser rehearsal ownership mismatch.');
  const runContainer = (name, image, args, env = {}) => {
    resources.push(['container', name]);
    docker(['run', '-d', '--pull', 'never', '--name', name, '--label', `${label}=${runId}`, '--network', network,
      '--security-opt', 'no-new-privileges', ...args, image], { env });
    owned('container', name);
  };
  const command = (name, args, cwd = root, extra = {}) => {
    const log = createWriteStream(resolve(output, `${name}.log`)); logs.push(log);
    const group = process.platform !== 'win32';
    const child = spawn(args[0], args.slice(1), { cwd, detached: group, env: { ...childEnvironment(process.env), ...extra }, stdio: ['ignore', 'pipe', 'pipe'] });
    const exited = new Promise(yes => { child.once('error', () => yes(1)); child.once('exit', code => yes(code ?? 1)); });
    child.stdout.pipe(log); child.stderr.pipe(log);
    const handle = { child, exited, group, address: null };
    let pending = '';
    child.stdout.on('data', chunk => {
      pending += chunk.toString();
      const lines = pending.split('\n'); pending = lines.pop().slice(-8192);
      for (const line of lines) handle.address ??= apiListeningAddress(line);
    });
    const timeout = setTimeout(() => { void stop(handle).catch(() => {}); }, 600_000);
    timeout.unref(); void exited.then(() => clearTimeout(timeout));
    children.push(handle); return handle;
  };
  const finish = async process => assert.equal(await process.exited, 0, 'Isolated build failed; inspect the local phase log.');
  const stop = async handle => {
    if (handle.child.exitCode !== null || handle.child.signalCode !== null || !handle.child.pid) return;
    const signal = name => {
      try { if (handle.group) process.kill(-handle.child.pid, name); else handle.child.kill(name); }
      catch (error) { if (error.code !== 'ESRCH') throw error; }
    };
    signal('SIGTERM');
    await Promise.race([handle.exited, delay(5000)]);
    if (handle.child.exitCode === null && handle.child.signalCode === null) signal('SIGKILL');
    await handle.exited;
  };
  const wait = async (url, process) => {
    for (let i = 0; i < 180; i++) {
      if (process) assert.ok(process.child.exitCode === null && process.child.signalCode === null, 'Isolated API exited during startup.');
      try { if ((await fetch(url, { signal: AbortSignal.timeout(1500) })).ok) return; } catch { /* Startup probe. */ }
      await delay(500);
    }
    throw new Error('Isolated service readiness timed out.');
  };
  try {
    await mkdir(output, { recursive: true });
    work = await mkdtemp(resolve(output, 'work-'));
    resources.push(['network', network]);
    docker(['network', 'create', '--label', `${label}=${runId}`, network]);
    owned('network', network);
    const address = gatewayAddress(JSON.parse(docker(['network', 'inspect', network]))[0].IPAM.Config[0].Subnet);
    let apiPort;
    const gatewayPort = await freePort();
    const origin = `http://localhost:${gatewayPort}`;
    const password = `Aa1!${randomBytes(24).toString('hex')}`;
    const databasePassword = randomBytes(24).toString('hex');
    const grafanaPassword = randomBytes(32).toString('hex');
    secrets.push(password, databasePassword, grafanaPassword);
    step('build');
    await finish(command('api-build', ['dotnet', 'build', 'PVAD.WebApi/PVAD.WebApi.csproj', '--no-restore', '--disable-build-servers', '-m:1',
      '-p:SkipOcrWorkerPackaging=true', '-p:SkipCodeWorkerPackaging=true', '--verbosity', 'quiet']));
    await finish(command('schema', ['dotnet', 'ef', 'migrations', 'script', '--project', persistence, '--no-build', '--output', resolve(work, 'schema.sql')]));
    await finish(command('ui-build', ['npm', 'run', 'build', '--', '--outDir', resolve(work, 'admin')], resolve(root, 'websites/PVAD.Admin')));
    step('database');
    runContainer(names.database, images[2].id, ['--publish', '127.0.0.1::5432', '--env', 'POSTGRES_PASSWORD',
      '--tmpfs', '/var/lib/postgresql/data:rw,size=512m', '--memory', '1g'], { POSTGRES_PASSWORD: databasePassword });
    let ready = false;
    for (let i = 0; i < 120; i++) {
      try { docker(['exec', names.database, 'pg_isready', '--host', '127.0.0.1', '-U', 'postgres']); ready = true; break; } catch { await delay(500); }
    }
    assert.ok(ready, 'Disposable PostgreSQL startup timed out.');
    const sql = statement => {
      owned('container', names.database);
      return docker(['exec', '-i', names.database, 'psql', '-U', 'postgres', '-At', '-v', 'ON_ERROR_STOP=1'], { input: statement });
    };
    sql(await readFile(resolve(work, 'schema.sql')));
    const databasePort = JSON.parse(docker(['inspect', names.database]))[0].NetworkSettings.Ports['5432/tcp'][0].HostPort;
    const apiEnv = {
      ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:0',
      ConnectionStrings__PvadDatabase: `Host=127.0.0.1;Port=${databasePort};Database=postgres;Username=postgres;Password=${databasePassword}`,
      PVAD_BOOTSTRAP_ADMIN_EMAIL: operatorEmail, PVAD_BOOTSTRAP_ADMIN_PASSWORD: password,
      AdminSecurity__RequireExplicitSystemOperators: 'true', AdminSecurity__SystemOperatorEmails__0: operatorEmail,
      Observability__Enabled: 'false', Mail__Transport: 'Disabled', CodeWorker__Enabled: 'false',
      DataProtection__KeysDirectory: resolve(work, 'keys'), Storage__RootPath: resolve(work, 'objects'),
      PdfViewer__AnnotationStoragePath: resolve(work, 'viewer'), PdfTrust__RevocationEvidence__CacheDirectory: resolve(work, 'cache'),
      Licensing__PrivateKeyPath: resolve(work, 'license/private.pem'), Licensing__PublicKeyPath: resolve(work, 'license/public.pem'),
    };
    let api;
    const startApi = async name => {
      if (apiPort) apiEnv.ASPNETCORE_URLS = `http://127.0.0.1:${apiPort}`;
      api = command(name, ['dotnet', 'bin/Debug/net10.0/PVAD.WebApi.dll'], resolve(root, 'PVAD.WebApi'), apiEnv);
      for (let i = 0; i < 180 && !api.address; i++) {
        assert.ok(api.child.exitCode === null && api.child.signalCode === null, 'Isolated API exited before binding.');
        await delay(500);
      }
      assert.ok(api.address, 'The owned API did not announce its listener.');
      apiPort = Number(new URL(api.address).port);
      await wait(`${api.address}/health/ready`, api);
    };
    step('api');
    await startApi('api');
    // Clone only this freshly bootstrapped synthetic password hash; never import a real account.
    sql(`INSERT INTO identity.users SELECT (jsonb_populate_record(NULL::identity.users, to_jsonb(u) ||
      jsonb_build_object('Id', gen_random_uuid(), 'UserName', '${memberEmail}', 'NormalizedUserName', upper('${memberEmail}'),
      'Email', '${memberEmail}', 'NormalizedEmail', upper('${memberEmail}'), 'DisplayName', 'Synthetic organization administrator',
      'SecurityStamp', gen_random_uuid()::text, 'ConcurrencyStamp', gen_random_uuid()::text))).*
      FROM identity.users u WHERE "Email" = '${operatorEmail}';
      INSERT INTO administration.organization_memberships SELECT (jsonb_populate_record(NULL::administration.organization_memberships,
      to_jsonb(m) || jsonb_build_object('Id', gen_random_uuid(), 'UserId', (SELECT "Id" FROM identity.users WHERE "Email" = '${memberEmail}')))).*
      FROM administration.organization_memberships m JOIN identity.users u ON u."Id" = m."UserId" WHERE u."Email" = '${operatorEmail}';
      INSERT INTO administration.organization_membership_roles ("Id", "OrganizationMembershipId", "RoleId", "CreatedAt", "AssignedByUserId")
      SELECT gen_random_uuid(), m."Id", r."Id", now(), NULL FROM administration.organization_memberships m
      JOIN identity.users u ON u."Id" = m."UserId" CROSS JOIN identity.roles r
      WHERE u."Email" = '${memberEmail}' AND r."Name" = 'Organization Administrator';`);
    step('gateway');
    const mount = (source, target) => ['--mount', `type=bind,src=${resolve(root, source)},dst=${target},readonly`];
    runContainer(names.grafana, images[0].id, ['--network-alias', 'pvad-grafana', '--memory', '512m',
      '--tmpfs', '/var/lib/grafana:uid=472,gid=0,size=256m',
      ...mount('deploy/observability/grafana/provisioning', '/etc/grafana/provisioning'),
      ...mount('deploy/observability/grafana/dashboards', '/var/lib/grafana/dashboards'),
      '--env', 'GF_SECURITY_ADMIN_PASSWORD',
      ...Object.entries({ GF_AUTH_ANONYMOUS_ENABLED: 'false',
        GF_AUTH_BASIC_ENABLED: 'false', GF_AUTH_DISABLE_LOGIN_FORM: 'true', GF_AUTH_PROXY_ENABLED: 'true',
        GF_AUTH_PROXY_HEADER_NAME: 'X-WEBAUTH-USER', GF_AUTH_PROXY_HEADER_PROPERTY: 'username', GF_AUTH_PROXY_AUTO_SIGN_UP: 'true',
        GF_AUTH_PROXY_SYNC_TTL: '0', GF_AUTH_PROXY_WHITELIST: address, GF_AUTH_PROXY_HEADERS: 'Role:X-WEBAUTH-ROLE',
        GF_AUTH_PROXY_ENABLE_LOGIN_TOKEN: 'false', GF_USERS_ALLOW_SIGN_UP: 'false', GF_USERS_AUTO_ASSIGN_ORG_ROLE: 'Admin',
        GF_SERVER_ROOT_URL: `${origin}/operator/grafana/`, GF_SERVER_SERVE_FROM_SUB_PATH: 'true', GF_PLUGINS_PREINSTALL_DISABLED: 'true',
        GF_ANALYTICS_CHECK_FOR_UPDATES: 'false', GF_ANALYTICS_CHECK_FOR_PLUGIN_UPDATES: 'false', GF_ANALYTICS_REPORTING_ENABLED: 'false',
      }).flatMap(([key, value]) => ['--env', `${key}=${value}`])], { GF_SECURITY_ADMIN_PASSWORD: grafanaPassword });
    const adminPattern = (await readFile(resolve(root, 'deploy/websites/route-patterns.env.example'), 'utf8'))
      .split('\n').find(line => line.startsWith('PVAD_ADMIN_ROUTE_PATTERN=')).split('=').slice(1).join('=');
    runContainer(names.ui, images[1].id, ['--network-alias', 'pvad-admin-ui', '--memory', '128m',
      ...mount(resolve(work, 'admin'), '/usr/share/nginx/html'),
      ...mount('deploy/websites/nginx.conf.template', '/etc/nginx/templates/default.conf.template'),
      ...mount('deploy/websites/15-pvad-environment.envsh', '/docker-entrypoint.d/15-pvad-environment.envsh'),
      '--env', `PVAD_API_UPSTREAM=http://host.docker.internal:${apiPort}`, '--env', `PVAD_SPA_ROUTE_PATTERN=${adminPattern}`]);
    runContainer(names.gateway, images[1].id, ['--ip', address, '--publish', `127.0.0.1:${gatewayPort}:8080`, '--memory', '128m',
      ...mount(gatewayTemplate, '/etc/nginx/templates/default.conf.template'),
      ...mount(gatewayAdapter, '/docker-entrypoint.d/15-pvad-operator.envsh'), ...mount('tools/configuration/environment.sh', '/etc/pvad/environment.sh'),
      ...mount('deploy/observability/operator-gateway/site', '/usr/share/nginx/html/documentation'),
      '--env', 'PVAD_OPERATOR_SERVER_NAME=localhost', '--env', `PVAD_OPERATOR_API_UPSTREAM=http://host.docker.internal:${apiPort}`,
      '--env', 'PVAD_OPERATOR_UI_UPSTREAM=http://pvad-admin-ui:8080']);
    await wait(`${origin}/operator-health`);
    await wait(`${origin}/api/pvad/v1/version`, api);
    for (const name of [names.grafana, names.ui])
      assert.ok(!Object.values(JSON.parse(docker(['inspect', name]))[0].NetworkSettings.Ports ?? {}).some(value => value?.length), 'Protected upstream was published.');
    step('browser');
    const browserResult = await exerciseOperatorBrowser({ origin, password, operatorEmail, memberEmail, sql,
      expectedDashboards: inventory.dashboards.map(value => value.uid), output,
      stopApi: () => stop(api), startApi: () => startApi('api-restarted'), onCheck: name => step(`browser/${name}`) });
    assert.deepEqual(await checksums(), baseline, 'Production sources changed during the browser rehearsal.');
    assert.deepEqual(await harnessChecksums(), harnessBaseline, 'Rehearsal sources changed during execution.');
    result = { schemaVersion: 1, status: 'passed', sourceCommit: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
      uncommittedTrackedChanges: execFileSync('git', ['diff', '--name-only', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim().length > 0,
      syntheticOnly: true, deploymentAuthorized: false, images, sourceChecksums: baseline, harnessChecksums: harnessBaseline,
      apiBinarySha256: hash(await readFile(resolve(root, 'PVAD.WebApi/bin/Debug/net10.0/PVAD.WebApi.dll'))), ...browserResult,
      limits: ['local-http-development-cookies-not-production-tls', 'not-account-designer-pkce-browser-coverage',
        'dashboard-provisioning-and-ui-not-live-datasource-queries', 'not-sso-or-production-approval', 'retained-provisioning-and-operator-identities-unchanged'] };
  } catch (cause) {
    if (recordFailure) {
      try {
        owned('container', names.gateway);
        let gatewayLog = docker(['logs', '--tail', '60', names.gateway], { mergeOutput: true });
        for (const secret of secrets) gatewayLog = gatewayLog.replaceAll(secret, '[redacted]');
        await writeFile(resolve(output, 'gateway.log'), gatewayLog);
      } catch { /* Diagnostics cannot skip resource cleanup. */ }
    }
    let stderr = String(cause.stderr ?? '');
    for (const secret of secrets) stderr = stderr.replaceAll(secret, '[redacted]');
    if (recordFailure) await writeFile(resolve(output, 'failure.json'), JSON.stringify({ phase, type: cause.name,
      actual: typeof cause.actual === 'number' ? cause.actual : undefined,
      expected: typeof cause.expected === 'number' ? cause.expected : undefined,
      frames: String(cause.stack ?? '').split('\n').filter(line => /^\s+at /.test(line)), stderr: stderr.slice(0, 4096) }, null, 2));
    failure = new Error(`Combined operator browser rehearsal failed during ${phase}; inspect local phase logs.`, { cause });
  }
  step('cleanup');
  for (const process of children.reverse()) await stop(process);
  for (const log of logs) await new Promise(yes => log.end(yes));
  await removeOwnedResources(resources, runId, docker);
  if (work) await rm(work, { recursive: true, force: true });
  if (failure) throw failure;
  return { ...result, temporaryResourcesRemoved: true };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if (process.argv.length !== 2) throw new Error('No arguments or external resource names are accepted.');
    await mkdir(output, { recursive: true });
    await rm(resolve(output, 'failure.json'), { force: true });
    await rm(resolve(output, 'gateway.log'), { force: true });
    await writeFile(resolve(output, 'report.json'), JSON.stringify({ schemaVersion: 1, status: 'running', deploymentAuthorized: false }) + '\n');
    try {
      const report = await runOperatorBrowserRehearsal({ recordFailure: true, progress: phase => console.log(`Operator browser rehearsal: ${phase}`) });
      await writeFile(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
      console.log('Combined real API/PostgreSQL/Nginx/Grafana browser rehearsal passed.');
    } catch (error) {
      await writeFile(resolve(output, 'report.json'), JSON.stringify({ schemaVersion: 1, status: 'failed', deploymentAuthorized: false }) + '\n');
      throw error;
    }
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
