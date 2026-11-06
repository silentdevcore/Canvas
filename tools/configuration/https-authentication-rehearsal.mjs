import assert from 'node:assert/strict';
import { execFileSync, spawn } from 'node:child_process';
import { createHash, randomBytes, randomUUID } from 'node:crypto';
import { createWriteStream } from 'node:fs';
import { cp, lstat, mkdir, mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { request as httpsRequest } from 'node:https';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { apiListeningAddress, childEnvironment, freePort, removeOwnedResources } from './operator-browser-rehearsal.mjs';
import { hosts, prepareCertificates, publicOrigins, tlsWebsiteConfiguration } from './https-auth-pki.mjs';
import { safeFailureEvidence, validateBrowserEvidence } from './https-auth-evidence.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = resolve(root, 'artifacts/rename/https-authentication');
const label = 'pvade.operator-browser-rehearsal';
const digest = value => createHash('sha256').update(value).digest('hex');
const helperProject = 'tools/identity-browser-rehearsal/PVAD.IdentityBrowserRehearsal.csproj';
const helperBinary = 'tools/identity-browser-rehearsal/bin/Debug/net10.0/PVAD.IdentityBrowserRehearsal.dll';
const dockerCommand = (args, options = {}) => {
  const result = execFileSync('docker', args, {
    encoding: 'utf8', timeout: 90_000, maxBuffer: 32 * 1024 * 1024,
    stdio: ['pipe', 'pipe', 'pipe'], ...options, env: { ...process.env, ...options.env },
  });
  return Buffer.isBuffer(result) ? result : result.trim();
};
const sources = [
  'tools/configuration/https-authentication-rehearsal.mjs', 'tools/configuration/https-auth-pki.mjs',
  'tools/configuration/https-auth-evidence.mjs',
  'tools/configuration/https-auth-browser/scenarios.mjs', 'tools/configuration/https-auth-browser/Dockerfile',
  helperProject, 'tools/identity-browser-rehearsal/Program.cs',
  'tools/configuration/operator-browser-rehearsal.mjs', 'PVAD.WebApi/Program.cs', 'PVAD.WebApi/appsettings.Production.json',
  'PVAD.WebApi/Security/PvadCookieNames.cs', 'PVAD.WebApi/Security/PvadCookieAuthenticationEvents.cs',
  'PVAD.WebApi/Security/PvadMigratingTicketDataFormat.cs', 'PVAD.WebApi/Security/PvadDataProtection.cs',
  'PVAD.WebApi/Security/PvadSessionService.cs', 'PVAD.WebApi/Controllers/AuthController.cs',
  'PVAD.WebApi/Controllers/DesignerAuthenticationController.cs', 'PVAD.WebApi/Application/Identity/DesignerAuthorizationCodeService.cs',
  'PVAD.WebApi/Application/Retention/PvadRetentionStartupGate.cs', 'PVAD.WebApi/Application/Retention/PvadRetentionPolicyCatalog.cs',
  'deploy/websites/nginx.conf.template', 'deploy/websites/route-patterns.env.example', 'deploy/websites/designer.headers.conf',
  'websites/PVAD.Account/src/main.ts', 'websites/PVAD.Account/src/api.ts', 'websites/PVAD.Account/src/shell.ts', 'websites/PVAD.Admin/src/main.js',
  'websites/PVAD.Admin/src/api.js', 'pvad-designer/src/auth/designerAuth.ts', 'pvad-designer/src/config/runtimeConfig.ts',
];

export async function runHttpsAuthentication({ docker = dockerCommand, progress = () => {} } = {}) {
  const images = ['postgres:17-alpine', 'nginx:1.29-alpine', 'pvad-https-auth-browser:1.64.1'].map(tag => {
    const id = docker(['image', 'inspect', '--format', '{{.Id}}', tag]);
    assert.match(id, /^sha256:[a-f0-9]{64}$/, 'Cached images are required; no image is pulled.');
    return { tag, id };
  });
  assert.equal(JSON.parse(await readFile(resolve(root, 'pvad-designer/node_modules/playwright/package.json'))).version, '1.64.1');
  const checksums = async () => Promise.all(sources.map(async path => {
    assert.ok((await lstat(resolve(root, path))).isFile());
    return { path, sha256: digest(await readFile(resolve(root, path))) };
  }));
  const baseline = await checksums();
  await mkdir(output, { recursive: true });
  const work = await mkdtemp(resolve(output, 'work-'));
  const runId = randomUUID().replaceAll('-', ''), network = `pvad-https-${runId}`;
  await writeFile(resolve(work, 'owner.txt'), runId);
  const resources = [], children = [], streams = [];
  const names = { database: `${network}-database`, gateway: `${network}-gateway`, browser: `${network}-browser` };
  let phase = 'prepare', failure, report, api, apiPort, fixture, keys = resolve(work, 'keys'), databaseName = 'pvad_https_' + runId;
  const password = `Aa1!${randomBytes(24).toString('hex')}`, databasePassword = randomBytes(24).toString('hex');
  const step = value => { phase = value; try { progress(value); } catch { /* Progress cannot prevent cleanup. */ } };
  const owned = name => assert.equal(docker(['container', 'inspect', '--format', `{{index .Config.Labels "${label}"}}`, name]), runId);
  const run = (name, image, args, command = [], env = {}) => {
    resources.push(['container', name]);
    docker(['run', '-d', '--pull', 'never', '--name', name, '--label', `${label}=${runId}`, '--network', network,
      '--security-opt', 'no-new-privileges', '--pids-limit', '512', ...args, image, ...command], { env });
    owned(name);
  };
  const command = (name, args, cwd = root, environment = {}) => {
    const log = createWriteStream(resolve(output, `${name}.log`)); streams.push(log);
    const child = spawn(args[0], args.slice(1), { cwd, detached: process.platform !== 'win32',
      env: { ...childEnvironment(process.env), ...environment }, stdio: ['pipe', 'pipe', 'pipe'] });
    const handle = { child, address: null, group: process.platform !== 'win32' };
    handle.exited = new Promise(yes => { child.once('error', () => yes(1)); child.once('exit', code => yes(code ?? 1)); });
    child.stdout.pipe(log); child.stderr.pipe(log);
    let pending = '';
    child.stdout.on('data', chunk => {
      pending += chunk.toString(); const lines = pending.split('\n'); pending = lines.pop().slice(-8192);
      for (const line of lines) handle.address ??= apiListeningAddress(line, 'https:');
    });
    children.push(handle); return handle;
  };
  const stop = async handle => {
    if (!handle || handle.child.exitCode !== null || handle.child.signalCode !== null || !handle.child.pid) return;
    const signal = value => { try { if (handle.group) process.kill(-handle.child.pid, value); else handle.child.kill(value); } catch (error) { if (error.code !== 'ESRCH') throw error; } };
    signal('SIGTERM'); await Promise.race([handle.exited, delay(5000)]);
    if (handle.child.exitCode === null && handle.child.signalCode === null) signal('SIGKILL');
    await handle.exited;
  };
  const finish = async handle => {
    const timeout = setTimeout(() => { void stop(handle); }, 600_000); timeout.unref();
    try { assert.equal(await handle.exited, 0, 'Owned build failed; inspect its phase log.'); } finally { clearTimeout(timeout); }
  };
  let connectionString, originalKeys, backupSnapshot;
  const sql = statement => { owned(names.database); return docker(['exec', '-i', names.database, 'psql', '-U', 'postgres', '-d', databaseName, '-At', '-v', 'ON_ERROR_STOP=1'], { input: statement }); };
  const helper = async values => {
    const child = spawn('dotnet', [resolve(root, helperBinary)], { cwd: root, env: childEnvironment(process.env), stdio: ['pipe', 'pipe', 'pipe'] });
    let text = '', diagnostics = '';
    child.stdout.on('data', chunk => { text += chunk.toString(); if (text.length > 128 * 1024) child.kill(); });
    child.stderr.on('data', chunk => { diagnostics = (diagnostics + chunk.toString()).slice(-1024); });
    const result = new Promise((yes, no) => { child.once('error', no); child.once('exit', code => code === 0 ? yes() :
      no(new Error(/^Fixture failure: [a-z-]+\s*$/.test(diagnostics) ? diagnostics.trim() : 'Private fixture helper failed.'))); });
    const handle = { child, exited: result.catch(() => 1), group: false }; children.push(handle);
    const timer = setTimeout(() => child.kill(), 60_000); timer.unref();
    child.stdin.end(JSON.stringify({ Action: values.Action, RunId: runId, WorkDirectory: work,
      ConnectionString: connectionString, KeysDirectory: keys, ...values }));
    try { await result; return JSON.parse(text); } finally { clearTimeout(timer); }
  };
  let apiEnvironment, ca;
  const probe = url => new Promise((yes, no) => {
    const request = httpsRequest(url, { ca, timeout: 2000 }, response => { response.resume(); yes(response.statusCode); });
    request.once('timeout', () => request.destroy()); request.once('error', no); request.end();
  });
  const startApi = async (name, mode = 'Staging') => {
    api = command(name, ['dotnet', 'bin/Debug/net10.0/PVAD.WebApi.dll'], resolve(root, 'PVAD.WebApi'), {
      ...apiEnvironment, ASPNETCORE_ENVIRONMENT: mode, ASPNETCORE_URLS: `https://127.0.0.1:${apiPort ?? 0}`,
      ConnectionStrings__PvadDatabase: connectionString, DataProtection__KeysDirectory: keys,
    });
    if (mode === 'Production') {
      const result = await Promise.race([api.exited, delay(60_000).then(() => null)]);
      assert.equal(result, 78, 'Production must reject incomplete retention governance with its controlled configuration exit.');
      assert.ok(!api.address, 'Production must fail closed before accepting traffic.');
      const logs = await readFile(resolve(output, name + '.log'), 'utf8');
      assert.match(logs, /RetentionApprovalRequired|PVADSTART001/);
      assert.doesNotMatch(logs, /Unhandled exception/);
      return;
    }
    for (let i = 0; i < 180 && !api.address; i++) {
      assert.ok(api.child.exitCode === null && api.child.signalCode === null, 'The owned HTTPS API exited.'); await delay(500);
    }
    assert.ok(api.address, 'The owned HTTPS listener was not announced.');
    apiPort = Number(new URL(api.address).port);
    for (let i = 0; i < 180; i++) {
      try { if (await probe(`${api.address}/health/ready`) === 200) return; } catch { /* Owned HTTPS startup. */ }
      await delay(500);
    }
    throw new Error('Owned HTTPS API readiness timed out.');
  };
  const dataSnapshot = () => JSON.parse(sql(`SELECT json_build_object('users', (SELECT count(*) FROM identity.users),
    'sessions', (SELECT json_agg(json_build_array("Id", "UserId", "OrganizationId", "RevokedAt")) FROM (SELECT * FROM identity.user_sessions ORDER BY "Id") s),
    'templates', (SELECT count(*) FROM designer.designer_templates));`));
  try {
    resources.push(['network', network]);
    docker(['network', 'create', '--label', `${label}=${runId}`, network]);
    step('build');
    await finish(command('helper-build', ['dotnet', 'build', helperProject, '--no-restore', '--disable-build-servers', '-m:1',
      '-p:SkipOcrWorkerPackaging=true', '-p:SkipCodeWorkerPackaging=true', '--verbosity', 'quiet']));
    await finish(command('schema', ['dotnet', 'ef', 'migrations', 'script', '--project',
      'src/Infrastructure/PVAD.Infrastructure.Persistence/PVAD.Infrastructure.Persistence.csproj', '--no-build', '--output', resolve(work, 'schema.sql')]));
    const certificates = await prepareCertificates(work); ca = await readFile(resolve(certificates.directory, 'ca.crt'));
    run(names.database, images[0].id, ['--publish', '127.0.0.1::5432', '--env', 'POSTGRES_PASSWORD', '--tmpfs', '/var/lib/postgresql/data:rw,size=512m', '--memory', '1g'], [],
      { POSTGRES_PASSWORD: databasePassword });
    let ready = false;
    for (let i = 0; i < 120; i++) { try { docker(['exec', names.database, 'pg_isready', '--host', '127.0.0.1', '-U', 'postgres']); ready = true; break; } catch { await delay(500); } }
    assert.ok(ready);
    docker(['exec', names.database, 'createdb', '-U', 'postgres', databaseName]);
    const databasePort = JSON.parse(docker(['inspect', names.database]))[0].NetworkSettings.Ports['5432/tcp'][0].HostPort;
    connectionString = `Host=127.0.0.1;Port=${databasePort};Database=${databaseName};Username=postgres;Password=${databasePassword}`;
    sql(await readFile(resolve(work, 'schema.sql')));
    step('synthetic-fixtures');
    fixture = await helper({ Action: 'seed', Password: password });
    const urls = publicOrigins(8443);
    apiEnvironment = { Observability__Enabled: 'false', Mail__Transport: 'Disabled', CodeWorker__Enabled: 'false',
      AdminSecurity__RequireExplicitSystemOperators: 'true', AdminSecurity__SystemOperatorEmails__0: 'operator@https.test',
      Registration__RequireDatabaseLegalDocuments: 'true', ProductAccess__Enabled: 'true',
      Registration__AllowedReturnOrigins__30: urls.account, Registration__AllowedReturnOrigins__31: urls.designer,
      DesignerAuthentication__AllowedOrigins__30: urls.designer,
      OAuth__Issuer: urls.account, OAuth__McpResource: urls.account + '/mcp',
      OAuth__SigningCertificatePath: resolve(certificates.directory, 'server.pfx'), OAuth__SigningCertificatePassword: '',
      Kestrel__Certificates__Default__Path: resolve(certificates.directory, 'server.pfx'), Kestrel__Certificates__Default__Password: '',
      Storage__RootPath: resolve(work, 'objects'), PdfViewer__AnnotationStoragePath: resolve(work, 'viewer'),
      PdfTrust__RevocationEvidence__CacheDirectory: resolve(work, 'cache'), Licensing__PrivateKeyPath: resolve(work, 'license/private.pem'),
      Licensing__PublicKeyPath: resolve(work, 'license/public.pem'),
      Mail__AccountBaseUrl: urls.account, Mail__DesignerBaseUrl: urls.designer, Mail__AdminBaseUrl: urls.admin,
    };
    step('production-startup-denial'); await startApi('production-denied', 'Production');
    step('staging-https-api'); await startApi('api');
    const siteEnvironment = Object.fromEntries(Object.entries(urls).map(([key, value]) => [`VITE_PVAD_${key.toUpperCase()}_URL`, value + '/']));
    for (const [site, directory] of [['account', 'websites/PVAD.Account'], ['designer', 'pvad-designer'], ['admin', 'websites/PVAD.Admin']])
      await finish(command(`${site}-build`, ['npm', 'run', 'build', '--', '--outDir', resolve(work, 'sites', site)], resolve(root, directory), siteEnvironment));
    // Runtime configuration is a supported Designer seam, not an authentication mock.
    const designerIndex = resolve(work, 'sites/designer/index.html');
    await writeFile(designerIndex, (await readFile(designerIndex, 'utf8')).replace('<head>', '<head>\n<script src="/pvad-runtime.js"></script>'));
    await writeFile(resolve(work, 'sites/designer/pvad-runtime.js'), `window.__PVAD_CONFIG__=${JSON.stringify({ accountUrl: urls.account + '/' })};\n`);
    const template = await readFile(resolve(root, 'deploy/websites/nginx.conf.template'), 'utf8');
    const patterns = await readFile(resolve(root, 'deploy/websites/route-patterns.env.example'), 'utf8');
    const configs = [];
    for (const site of ['account', 'designer', 'admin']) {
      const routePattern = patterns.split('\n').find(line => line.startsWith(`PVAD_${site.toUpperCase()}_ROUTE_PATTERN=`)).split('=').slice(1).join('=');
      configs.push(tlsWebsiteConfiguration(template, { host: `${site}.pvad.test`, site, apiPort, routePattern }));
    }
    configs.push(`server { listen 8443 ssl default_server; server_name _; ssl_certificate /certificates/server.crt; ssl_certificate_key /certificates/server.key; return 421; }`,
      'server { listen 8443 ssl; server_name untrusted.pvad.test; ssl_certificate /certificates/untrusted.crt; ssl_certificate_key /certificates/untrusted.key; return 200; }');
    await writeFile(resolve(work, 'sites.conf'), configs.join('\n'));
    const bind = (source, target, readonly = true) => ['--mount', `type=bind,src=${source},dst=${target}${readonly ? ',readonly' : ''}`];
    const gatewayPort = await freePort();
    step('https-gateway');
    run(names.gateway, images[1].id, [...hosts.flatMap(value => ['--network-alias', value]), '--network-alias', 'wrong-host.pvad.test',
      '--network-alias', 'untrusted.pvad.test', '--publish', `127.0.0.1:${gatewayPort}:8443`, '--memory', '256m',
      ...bind(resolve(work, 'sites.conf'), '/etc/nginx/conf.d/default.conf'), ...bind(certificates.directory, '/certificates'),
      ...bind(resolve(work, 'sites'), '/sites'), ...bind(resolve(root, 'deploy/websites/designer.headers.conf'), '/etc/nginx/pvad/designer.headers.conf')]);
    const browserConfig = resolve(work, 'browser-config'); await mkdir(browserConfig);
    await writeFile(resolve(browserConfig, 'config.json'), JSON.stringify({ urls, password, fixture }));
    const screenshots = resolve(output, 'browser'); await mkdir(screenshots, { recursive: true });
    for (const name of ['browser-report.json', 'browser-failure.json']) await rm(resolve(screenshots, name), { force: true });
    step('browser');
    resources.push(['container', names.browser]);
    const browser = spawn('docker', ['run', '--rm', '-i', '--pull', 'never', '--name', names.browser, '--label', `${label}=${runId}`, '--network', network,
      '--security-opt', 'no-new-privileges', '--shm-size', '256m', '--memory', '2g', '--pids-limit', '512',
      ...bind(resolve(root, 'pvad-designer/node_modules'), '/deps/node_modules'), ...bind(resolve(root, 'tools/configuration/https-auth-browser/scenarios.mjs'), '/lab/scenarios.mjs'),
      ...bind(resolve(root, 'tools/configuration/https-auth-evidence.mjs'), '/lab/https-auth-evidence.mjs'),
      ...bind(browserConfig, '/config'), ...bind(resolve(certificates.directory, 'ca.crt'), '/trust/ca.crt'), ...bind(screenshots, '/output', false),
      '--env', 'NODE_EXTRA_CA_CERTS=/trust/ca.crt', '--entrypoint', 'node', images[2].id, '/lab/scenarios.mjs'],
    { cwd: root, env: { ...process.env }, stdio: ['pipe', 'pipe', 'pipe'] });
    const handle = { child: browser, group: false }; handle.exited = new Promise(yes => { browser.once('error', () => yes(1)); browser.once('exit', code => yes(code ?? 1)); }); children.push(handle);
    const errorLog = createWriteStream(resolve(output, 'browser.log')); streams.push(errorLog); browser.stderr.pipe(errorLog);
    let pending = '', processing = Promise.resolve();
    const action = async message => {
      assert.ok(message && message.id && message.protocol === 'pvad-https-control.v1');
      step(`browser/${message.action}`);
      const editor = fixture.userIds.find(value => value.email === 'editor@https.test');
      switch (message.action) {
        case 'session-count': return { count: Number(sql('SELECT count(*) FROM identity.user_sessions;')) };
        case 'legacy-ticket': return helper({ Action: 'legacy-ticket', Surface: message.surface, Cookie: message.cookie });
        case 'restart': await stop(api); await startApi('api-restarted'); return { sessions: sql('SELECT count(*) FROM identity.user_sessions;') };
        case 'backup':
          await stop(api); backupSnapshot = dataSnapshot(); originalKeys = keys;
          assert.ok(backupSnapshot.templates > 0, 'The real Designer must persist a template before backup.');
          await helper({ Action: 'create-protected-data' });
          await cp(keys, resolve(work, 'keys-backup'), { recursive: true });
          owned(names.database);
          await writeFile(resolve(work, 'database.dump'), docker(['exec', names.database, 'pg_dump', '-U', 'postgres', '-d', databaseName, '-Fc'], { encoding: 'buffer' }));
          await startApi('api-after-backup'); return { saved: true };
        case 'mark-after-backup': {
          assert.ok(backupSnapshot);
          sql(`UPDATE designer.designer_templates SET "Name" = 'Synthetic post-backup marker';`);
          return { marked: true };
        }
        case 'restore': {
          await stop(api); owned(names.database);
          const restore = 'pvad_https_restore_' + runId;
          docker(['exec', names.database, 'createdb', '-U', 'postgres', restore]);
          docker(['exec', '-i', names.database, 'pg_restore', '-U', 'postgres', '-d', restore, '--single-transaction', '--exit-on-error'], { input: await readFile(resolve(work, 'database.dump')) });
          databaseName = restore; connectionString = connectionString.replace('Database=pvad_https_' + runId, 'Database=' + restore);
          keys = resolve(work, 'keys-restored'); await cp(resolve(work, 'keys-backup'), keys, { recursive: true });
          assert.deepEqual(dataSnapshot(), backupSnapshot);
          assert.equal(Number(sql(`SELECT count(*) FROM designer.designer_templates WHERE "Name" = 'Synthetic post-backup marker';`)), 0);
          await startApi('api-restored'); return { restored: true, consumers: await helper({ Action: 'protected-data' }) };
        }
        case 'wrong-keys': await stop(api); originalKeys = keys; keys = resolve(work, 'keys-wrong'); await mkdir(keys); await startApi('api-wrong-keys'); return { ready: true };
        case 'correct-keys': await stop(api); keys = originalKeys; await startApi('api-correct-keys'); return { ready: true };
        case 'disable-user': sql(`UPDATE identity.users SET "IsActive" = false WHERE "Id" = '${editor.id}';`); return { changed: true };
        case 'enable-user': sql(`UPDATE identity.users SET "IsActive" = true WHERE "Id" = '${editor.id}';`); return { changed: true };
        case 'expire-sessions': sql(`UPDATE identity.user_sessions SET "ExpiresAt" = now() - interval '1 minute' WHERE "UserId" = '${editor.id}';`); return { changed: true };
        case 'suspend-organization': sql(`UPDATE administration.organizations SET "Status" = 'Suspended' WHERE "Id" = '${editor.organizationId}';`); return { changed: true };
        case 'resume-organization': sql(`UPDATE administration.organizations SET "Status" = 'Active' WHERE "Id" = '${editor.organizationId}';`); return { changed: true };
        case 'disable-entitlement':
        case 'enable-entitlement':
          sql(`UPDATE administration.subscription_entitlements SET "Enabled" = ${message.action === 'enable-entitlement'} WHERE "SubscriptionId" IN (SELECT "Id" FROM administration.subscriptions WHERE "OrganizationId"='${editor.organizationId}');`);
          return { changed: true };
        default: throw new Error('Unknown private browser action.');
      }
    };
    browser.stdout.on('data', chunk => {
      pending += chunk.toString(); if (pending.length > 128 * 1024) { browser.kill(); return; }
      const lines = pending.split('\n'); pending = lines.pop();
      for (const line of lines) processing = processing.then(async () => {
        let message;
        try {
          message = JSON.parse(line); const result = await action(message);
          browser.stdin.write(JSON.stringify({ id: message.id, result }) + '\n');
        } catch (error) {
          await writeFile(resolve(output, 'action-failure.json'), JSON.stringify({ phase,
            reason: /^Fixture failure: [a-z-]+$/.test(error.message) ? error.message : 'Private action failed.' }));
          browser.stdin.write(JSON.stringify({ id: message?.id, error: 'Private action failed.' }) + '\n');
        }
      });
    });
    const browserTimer = setTimeout(() => browser.kill(), 900_000); browserTimer.unref();
    const result = await handle.exited; clearTimeout(browserTimer); await processing;
    assert.equal(result, 0, 'HTTPS browser checks failed; inspect the bounded synthetic browser log.');
    const browserResult = JSON.parse(await readFile(resolve(screenshots, 'browser-report.json')));
    validateBrowserEvidence(browserResult);
    const screenshotChecksums = await Promise.all(browserResult.screenshots.map(async path => {
      const bytes = await readFile(resolve(screenshots, path));
      assert.equal(bytes.subarray(0, 8).toString('hex'), '89504e470d0a1a0a', 'Screenshot evidence must be a real PNG.');
      assert.ok(bytes.length > 1024, 'Screenshot evidence is unexpectedly small.');
      return { path, sha256: digest(bytes), bytes: bytes.length };
    }));
    assert.deepEqual(await checksums(), baseline, 'Reviewed HTTPS sources changed during execution.');
    report = { schemaVersion: 1, status: 'passed', sourceCommit: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
      sourceChecksums: baseline, images, caSha256: certificates.caSha256, apiBinarySha256: digest(await readFile(resolve(root, 'PVAD.WebApi/bin/Debug/net10.0/PVAD.WebApi.dll'))),
      helperBinarySha256: digest(await readFile(resolve(root, helperBinary))), productionStartupBlocked: true, apiEnvironment: 'Staging',
      syntheticOnly: true, deploymentAuthorized: false, hostTrustModified: false, screenshotChecksums, ...browserResult };
  } catch (cause) {
    await writeFile(resolve(output, 'failure.json'), JSON.stringify({ phase, ...safeFailureEvidence(cause, [password, databasePassword]) }, null, 2));
    failure = new Error(`HTTPS authentication rehearsal failed during ${phase}; inspect its local phase logs.`, { cause });
  }
  step('cleanup');
  const cleanupFailures = [];
  for (const child of children.reverse()) { try { await stop(child); } catch (error) { cleanupFailures.push(error); } }
  for (const stream of streams) { try { await new Promise(yes => stream.end(yes)); } catch (error) { cleanupFailures.push(error); } }
  try { await removeOwnedResources(resources, runId, docker); } catch (error) { cleanupFailures.push(error); }
  // Even unavailable Docker must not leave private credentials and key material on the host.
  try { await rm(work, { recursive: true, force: true }); } catch (error) { cleanupFailures.push(error); }
  if (cleanupFailures.length) throw new AggregateError(cleanupFailures, 'HTTPS rehearsal cleanup incomplete; no success is reported.');
  if (failure) throw failure;
  return { ...report, temporaryResourcesRemoved: true };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if (process.argv.length !== 2) throw new Error('No arguments or installed resource names are accepted.');
    await mkdir(output, { recursive: true });
    for (const path of ['failure.json', 'action-failure.json']) await rm(resolve(output, path), { force: true });
    await writeFile(resolve(output, 'report.json'), JSON.stringify({ status: 'running', deploymentAuthorized: false }) + '\n');
    try {
      const result = await runHttpsAuthentication({ progress: value => console.log(`HTTPS authentication: ${value}`) });
      await writeFile(resolve(output, 'report.json'), JSON.stringify(result, null, 2) + '\n');
      console.log('Isolated HTTPS authentication and recovery passed.');
    } catch (error) { await writeFile(resolve(output, 'report.json'), JSON.stringify({ status: 'failed', deploymentAuthorized: false }) + '\n'); throw error; }
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
