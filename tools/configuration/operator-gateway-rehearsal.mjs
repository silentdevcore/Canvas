import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash, randomUUID } from 'node:crypto';
import { readFile, mkdir, writeFile, lstat } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const { load } = createRequire(resolve(root, 'pvad-designer/package.json'))('js-yaml');
const label = 'pvade.operator-gateway-rehearsal';
const fixture = 'tools/configuration/fixtures/operator-gateway-fixture.mjs';
const sources = ['deploy/observability/operator-gateway/templates/default.conf.template',
  'deploy/observability/operator-gateway/15-pvad-operator.envsh', 'tools/configuration/environment.sh',
  'deploy/observability/operator-gateway/site/index.html', 'deploy/observability/operator-gateway/site/app.js',
  'deploy/observability/operator-gateway/site/styles.css', 'PVAD.WebApi/Controllers/AdminOperatorAccessController.cs',
  'PVAD.WebApi/Infrastructure/PvadResponseHeaders.cs'];
const hash = value => createHash('sha256').update(value).digest('hex');
const dockerCommand = (args, options = {}) => execFileSync('docker', args, {
  encoding: 'utf8', timeout: 60_000, stdio: ['pipe', 'pipe', 'pipe'], ...options,
  env: { ...process.env, ...options.env },
}).trim();

export async function gatewayProtocol() {
  const publicContracts = JSON.parse(await readFile(resolve(root, 'docs/reference/compatibility-identifiers.v1.json')));
  const operatorContracts = JSON.parse(await readFile(resolve(root, 'operator-docs/compatibility-identifiers.v1.json')));
  const contract = (catalog, id) => {
    const matches = catalog.entries.filter(entry => entry.id === id);
    assert.equal(matches.length, 1, 'A pinned protocol contract is required.');
    assert.equal(typeof matches[0].value, 'string');
    return matches[0].value;
  };
  return {
    legacyOperatorHeader: contract(operatorContracts, 'compat-49ffd55c615e'),
    credentialHeaders: ['X-PVAD-API-Key', 'X-PVAD-Application',
      contract(publicContracts, 'compat-0ad1b8ff55d1'), contract(publicContracts, 'compat-d9438056ecaa')],
  };
}

export async function runOperatorGatewayRehearsal({ docker = dockerCommand, progress = () => {} } = {}) {
  const config = load(await readFile(resolve(root, 'docker-compose.yml'), 'utf8'));
  const nginxTag = config.services['pvad-operator-gateway'].image;
  const nodeTag = 'node:22-alpine';
  const images = [nginxTag, nodeTag].map(tag => {
    const id = docker(['image', 'inspect', '--format', '{{.Id}}', tag]);
    assert.match(id, /^sha256:[a-f0-9]{64}$/, 'Cached test images are required.');
    return { tag, id };
  });
  const protocol = await gatewayProtocol();
  const baseline = await sourceChecksums();
  const harness = await sourceChecksums([fixture, 'tools/configuration/operator-gateway-rehearsal.mjs']);
  const runId = randomUUID(), network = `pvad-gateway-rehearsal-${runId}`;
  const backend = `${network}-upstreams`, gateway = `${network}-gateway`, token = randomUUID();
  const resources = [], checks = [];
  let phase = 'prepare', report, failure;
  const step = value => { phase = value; try { progress(value); } catch { /* Reporting must not stop cleanup. */ } };
  const owner = (kind, name) => {
    try { return docker([kind, 'inspect', '--format', `{{index .${kind === 'container' ? 'Config.Labels' : 'Labels'} "${label}"}}`, name]); }
    catch (error) {
      if (/no such (?:object|container|network)|not found/i.test(String(error.stderr))) return null;
      throw new Error('Synthetic gateway ownership inspection failed.');
    }
  };
  const requireOwned = (kind, name) => assert.equal(owner(kind, name), runId, 'Synthetic gateway ownership mismatch.');
  const call = (path, headers = {}, { target = 'gateway', method = 'GET' } = {}) => {
    requireOwned('container', backend);
    return JSON.parse(docker(['exec', '-i', backend, 'node', '/lab/fixture.mjs', 'client'], {
      input: JSON.stringify({ target, path, method, headers: { Host: 'operator.test', ...headers } }),
    }));
  };
  const control = (path, method = 'GET') => call(path, { 'X-Lab-Token': token }, { target: 'control', method });
  const state = () => JSON.parse(control('/state').body);
  const scenario = (name, operation) => { step(name); operation(); checks.push(name); };
  const wait = async () => {
    for (let attempt = 0; attempt < 60; attempt++) {
      try { if (call('/operator-health').status === 200 && control('/state').status === 200) return; } catch { /* New synthetic containers are starting. */ }
      await new Promise(resolve => setTimeout(resolve, 250));
    }
    throw new Error('Synthetic gateway readiness timed out.');
  };
  const denied = (path, headers, status) => {
    const before = state();
    const response = call(path, headers);
    assert.equal(response.status, status, 'Protected route did not return the expected denial.');
    assert.match(response.headers['cache-control'], /no-store/);
    assert.equal(response.headers['x-content-type-options'], 'nosniff');
    const after = state();
    assert.equal(after.grafanaRequests, before.grafanaRequests, 'Denied request reached the Grafana upstream.');
    assert.equal(after.uiRequests, before.uiRequests, 'Denied request reached the UI upstream.');
  };
  try {
    resources.push(['network', network]);
    docker(['network', 'create', '--internal', '--label', `${label}=${runId}`, network]);
    requireOwned('network', network);
    assert.equal(docker(['network', 'inspect', '--format', '{{.Internal}}', network]), 'true');
    step('start');
    resources.push(['container', backend]);
    docker(['run', '-d', '--pull', 'never', '--name', backend, '--label', `${label}=${runId}`,
      '--network', network, '--network-alias', 'pvad-api-fixture', '--network-alias', 'pvad-ui-fixture', '--network-alias', 'pvad-grafana',
      '--user', '1000:1000', '--read-only', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges', '--pids-limit', '64', '--memory', '128m',
      '--mount', `type=bind,src=${resolve(root, fixture)},dst=/lab/fixture.mjs,readonly`,
      '--env', 'PVAD_LAB_PROTOCOL', '--env', 'PVAD_LAB_CONTROL_TOKEN', images[1].id, 'node', '/lab/fixture.mjs', 'serve'], {
      env: { PVAD_LAB_PROTOCOL: JSON.stringify(protocol), PVAD_LAB_CONTROL_TOKEN: token },
    });
    requireOwned('container', backend);
    resources.push(['container', gateway]);
    docker(['run', '-d', '--pull', 'never', '--name', gateway, '--label', `${label}=${runId}`,
      '--network', network, '--network-alias', 'pvad-gateway-lab', '--security-opt', 'no-new-privileges', '--pids-limit', '64', '--memory', '128m',
      '--mount', `type=bind,src=${resolve(root, sources[0])},dst=/etc/nginx/templates/default.conf.template,readonly`,
      '--mount', `type=bind,src=${resolve(root, sources[1])},dst=/docker-entrypoint.d/15-pvad-operator.envsh,readonly`,
      '--mount', `type=bind,src=${resolve(root, sources[2])},dst=/etc/pvad/environment.sh,readonly`,
      '--mount', `type=bind,src=${resolve(root, 'deploy/observability/operator-gateway/site')},dst=/usr/share/nginx/html/documentation,readonly`,
      '--env', 'PVAD_OPERATOR_SERVER_NAME=operator.test', '--env', 'PVAD_OPERATOR_API_UPSTREAM=http://pvad-api-fixture:8081',
      '--env', 'PVAD_OPERATOR_UI_UPSTREAM=http://pvad-ui-fixture:8082', images[0].id]);
    requireOwned('container', gateway);
    await wait();
    scenario('public-entry-routes', () => {
      const before = state().auth.length;
      for (const path of ['/operator-health', '/login', '/forgot-password', '/assets/synthetic.js']) assert.equal(call(path).status, 200);
      const rootResponse = call('/');
      assert.equal(rootResponse.status, 302);
      assert.ok(new URL(rootResponse.headers.location, 'http://operator.test').pathname === '/login');
      assert.equal(state().auth.length, before);
    });
    scenario('internal-auth-route-hidden', () => {
      const before = state().auth.length;
      assert.equal(call('/_pvad_operator_auth').status, 404);
      assert.equal(state().auth.length, before);
    });
    scenario('protected-routes-anonymous', () => {
      for (const path of ['/operator/grafana/', '/documentation', '/documentation/app.js', '/dashboard']) denied(path, {}, 401);
    });
    scenario('role-expiry-and-disabled-denials', () => {
      for (const [value, status] of [['forbidden', 403], ['disabled', 403], ['expired', 401]])
        for (const path of ['/operator/grafana/', '/documentation', '/dashboard']) denied(path, { Cookie: `lab_session=${value}` }, status);
    });
    const spoofed = { Authorization: 'Bearer synthetic-forged', 'X-WEBAUTH-USER': 'forged-root', 'X-WEBAUTH-ROLE': 'Admin',
      ...Object.fromEntries(protocol.credentialHeaders.map(header => [header, 'synthetic-forged'])) };
    scenario('spoofed-headers-without-session', () => { denied('/operator/grafana/', spoofed, 401); });
    scenario('authorized-identity-and-credentials', () => {
      const response = call('/operator/grafana/', { ...spoofed, Cookie: 'lab_session=allowed' });
      assert.equal(response.status, 200);
      assert.equal(JSON.parse(response.body).authenticated, true);
      assert.ok(response.headers['set-cookie'].every(cookie => /Path=\/operator\/grafana\//.test(cookie)));
      assert.match(response.headers['cache-control'], /no-store/);
      assert.equal(response.headers['x-content-type-options'], 'nosniff');
      assert.equal(state().grafanaCredentialsStripped, true);
      assert.equal(state().grafanaIdentityReplaced, true);
    });
    scenario('retained-response-header', () => { assert.equal(call('/operator/grafana/', { Cookie: 'lab_session=legacy' }).status, 200); });
    scenario('missing-identity-cannot-use-client-identity', () => {
      const response = call('/operator/grafana/', { ...spoofed, Cookie: 'lab_session=empty' });
      assert.equal(response.status, 401);
    });
    scenario('authorized-documentation-and-ui', () => {
      const document = call('/documentation', { Cookie: 'lab_session=allowed' });
      assert.equal(document.status, 200);
      assert.equal(hash(document.body), baseline.find(value => value.path === sources[3]).sha256);
      assert.match(document.headers['cache-control'], /no-store/);
      assert.match(document.headers['content-security-policy'], /frame-ancestors 'none'/);
      assert.equal(call('/dashboard', { Cookie: 'lab_session=allowed' }).status, 200);
    });
    scenario('authorization-is-not-cached', () => { denied('/operator/grafana/', {}, 401); });
    scenario('api-identity-headers-stripped', () => {
      const response = call('/api/pvad/v1/admin/operator/echo', { ...spoofed, Authorization: 'Bearer synthetic-api-credential' });
      assert.equal(response.status, 200);
      assert.deepEqual(JSON.parse(response.body), { identityHeadersStripped: true, authorizationPreserved: true });
    });
    scenario('auth-errors-and-redirects-deny', () => {
      for (const value of ['error', 'redirect']) denied('/operator/grafana/', { Cookie: `lab_session=${value}` }, 500);
    });
    scenario('auth-service-unavailable', () => {
      assert.equal(control('/auth/stop', 'POST').status, 200);
      denied('/operator/grafana/', { Cookie: 'lab_session=allowed' }, 500);
    });
    scenario('auth-service-recovery', () => {
      assert.equal(control('/auth/start', 'POST').status, 200);
      assert.equal(call('/operator/grafana/', { Cookie: 'lab_session=allowed' }).status, 200);
    });
    const observed = state();
    assert.ok(observed.auth.length > 0 && observed.auth.every(value => value.credentialHeadersCleared && value.forwardedHostValid && value.forwardedProtocolValid));
    assert.equal(observed.apiIdentityHeadersStripped, true);
    assert.equal(observed.grafanaIdentityReplaced, true, 'A client-controlled identity reached the Grafana upstream.');
    assert.equal(observed.grafanaCredentialsStripped, true, 'Client credentials reached the Grafana upstream.');
    assert.deepEqual(await sourceChecksums(), baseline, 'Gateway sources changed during the rehearsal.');
    assert.deepEqual(await sourceChecksums(harness.map(value => value.path)), harness, 'Gateway harness changed during the rehearsal.');
    report = { schemaVersion: 1, status: 'passed', deploymentAuthorized: false, syntheticOnly: true,
      images, checks, sourceChecksums: baseline, harnessChecksums: harness, authSubrequests: observed.auth.length,
      limits: ['mock-api-cookie-decisions-not-identity-or-postgresql', 'mock-grafana-not-live-dashboard-or-query-test',
        'http-internal-network-not-public-tls-or-sso', 'not-production-gateway-approval', 'retained-operator-header-reader-remains'] };
  } catch { failure = new Error(`Isolated operator gateway rehearsal failed during ${phase}.`); }
  step('cleanup');
  const failures = [];
  for (const [kind, name] of resources.reverse()) {
    try {
      const actual = owner(kind, name);
      if (actual === null) continue;
      assert.equal(actual, runId, 'Synthetic resource ownership changed.');
      docker(kind === 'container' ? ['rm', '-f', name] : ['network', 'rm', name]);
      assert.equal(owner(kind, name), null);
    } catch { failures.push(kind); }
  }
  if (failures.length) throw new Error('Gateway rehearsal cleanup incomplete; inspect only the rehearsal ownership label.');
  if (failure) throw failure;
  return { ...report, temporaryResourcesRemoved: true };
}

async function sourceChecksums(paths = sources) {
  const values = [];
  for (const path of paths) {
    const file = resolve(root, path);
    assert.ok((await lstat(file)).isFile(), 'Gateway rehearsal requires regular source files.');
    values.push({ path, sha256: hash(await readFile(file)) });
  }
  return values;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const output = resolve(root, 'artifacts/rename/operator-gateway-rehearsal');
  const reportFile = resolve(output, 'report.json');
  let startedAt;
  try {
    if (process.argv.length !== 2) throw new Error('No arguments or external resource names are accepted.');
    await mkdir(output, { recursive: true }); startedAt = new Date().toISOString();
    await writeFile(reportFile, JSON.stringify({ schemaVersion: 1, status: 'running', startedAt, deploymentAuthorized: false }) + '\n');
    const report = await runOperatorGatewayRehearsal({ progress: phase => console.error(`Gateway rehearsal: ${phase}`) });
    await writeFile(reportFile, JSON.stringify({ ...report, startedAt, completedAt: new Date().toISOString() }, null, 2) + '\n');
    console.log(JSON.stringify(report));
  } catch (error) {
    if (startedAt) await writeFile(reportFile, JSON.stringify({ schemaVersion: 1, status: 'failed', startedAt, deploymentAuthorized: false }) + '\n');
    console.error(error.message); process.exitCode = 1;
  }
}
