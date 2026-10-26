import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { createHash, generateKeyPairSync, randomUUID } from 'node:crypto';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { promisify } from 'node:util';
import { setTimeout as delay } from 'node:timers/promises';

const execute = promisify(execFile);
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const deadlineMs = 15_000;

export function requiresRetentionRejection(inventory) {
  assert.equal(typeof inventory.productionApproved, 'boolean');
  assert.ok(Array.isArray(inventory.activities) && inventory.activities.length > 0);
  assert.ok(inventory.activities.every(activity => typeof activity.retention?.approvalStatus === 'string'));
  return !inventory.productionApproved || inventory.activities.some(activity => activity.retention.approvalStatus !== 'approved');
}

export function verifyRejectedStartup(state, logs, elapsedMs) {
  assert.equal(state.Status, 'exited', 'Rejected startup did not exit independently.');
  assert.equal(state.ExitCode, 78, 'Rejected startup must return configuration exit code 78.');
  assert.equal(state.OOMKilled, false, 'An OOM kill is not a controlled startup rejection.');
  assert.equal(state.Error, '', 'Container startup itself failed.');
  assert.ok(Number.isFinite(elapsedMs) && elapsedMs >= 0 && elapsedMs <= deadlineMs,
    'Rejected startup exceeded its 15-second bound or has an invalid duration.');
  assert.match(logs, /PVADSTART001/);
  assert.doesNotMatch(logs, /Now listening on:|Unhandled exception\./);
}

export async function runProductionStartupSmoke(image) {
  assert.equal(typeof image, 'string');
  assert.ok(image.length > 0 && !image.startsWith('-'));
  const output = resolve(root, 'artifacts/sbom/webapi-startup');
  await mkdir(output, { recursive: true });
  await writeFile(resolve(output, 'report.json'), JSON.stringify({ status: 'failed',
    failure: 'Image and source-inventory validation have not completed.' }, null, 2) + '\n');
  const inventory = JSON.parse(await readFile(resolve(root, 'product-metadata/data-processing-inventory.json'), 'utf8'));
  if (!requiresRetentionRejection(inventory)) {
    const report = { status: 'not-applicable', reason: 'The source inventory is approved; no production retention rejection is expected.' };
    await writeFile(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    return report;
  }
  const docker = (args, input) => new Promise((resolve, reject) => {
    const child = execFile('docker', args, { timeout: 30_000, maxBuffer: 4 * 1024 * 1024 },
      (error, stdout) => error ? reject(error) : resolve(stdout.trim()));
    child.stdin.on('error', error => { if (error.code !== 'EPIPE') reject(error); });
    child.stdin.end(input);
  });
  const immutable = await docker(['image', 'inspect', '--format', '{{.Id}}', image]);
  assert.match(immutable, /^sha256:[a-f0-9]{64}$/);
  const owner = randomUUID(), container = `pvad-startup-${owner}`, seed = `pvad-startup-seed-${owner}`,
    volume = `pvad-startup-data-${owner}`;
  const label = `pvade.startup-smoke=${owner}`;
  const temporary = await mkdtemp(join(tmpdir(), 'pvad-startup-'));
  const report = { schemaVersion: 1, status: 'failed', image: immutable,
    scope: 'Retention rejection only; no production approval, database, browser, license approval or deployed host certification.',
    startedAt: new Date().toISOString(), resourcesRemoved: false };
  const allocated = new Set();
  const owns = async (kind, name) => {
    try {
      return await docker([kind, 'inspect', '--format', `{{index .${kind === 'container' ? 'Config.Labels' : 'Labels'} "pvade.startup-smoke"}}`, name]) === owner;
    } catch { return false; }
  };
  let failure;
  try {
    const keys = generateKeyPairSync('ec', { namedCurve: 'prime256v1',
      privateKeyEncoding: { type: 'sec1', format: 'pem' }, publicKeyEncoding: { type: 'spki', format: 'pem' } });
    await writeFile(join(temporary, 'private.pem'), keys.privateKey, { mode: 0o600 });
    await writeFile(join(temporary, 'public.pem'), keys.publicKey, { mode: 0o600 });
    const archive = await execute('tar', ['--no-xattrs', '-cf', '-', '-C', temporary, 'private.pem', 'public.pem'],
      { encoding: 'buffer', maxBuffer: 1024 * 1024, env: { ...process.env, COPYFILE_DISABLE: '1' } });
    await docker(['volume', 'create', '--label', label, volume]);
    allocated.add(volume);
    assert.ok(await owns('volume', volume), 'Fixture volume ownership could not be confirmed.');
    await docker(['create', '--name', seed, '--label', label, '-i', '--network', 'none', '--read-only', '--user', '0',
      '--mount', `type=volume,src=${volume},dst=/app/data`, '--entrypoint', 'sh', immutable, '-c',
      'mkdir -p /app/data/licenses; tar -xf - -C /app/data/licenses; chown -R 1654:1654 /app/data; chmod 600 /app/data/licenses/*.pem']);
    allocated.add(seed);
    await docker(['start', '-i', seed], archive.stdout);
    const seeded = JSON.parse(await docker(['inspect', '--format', '{{json .State}}', seed]));
    assert.equal(seeded.Status, 'exited', 'Synthetic key preparation did not complete.');
    assert.equal(seeded.ExitCode, 0, 'Synthetic key preparation failed.');
    const environment = {
      ASPNETCORE_ENVIRONMENT: 'Production', OAuth__Enabled: 'false', Observability__Enabled: 'false', Mail__Transport: 'Disabled',
      ConnectionStrings__PvadDatabase: 'Host=127.0.0.1;Port=9;Database=synthetic;Username=synthetic;Timeout=1',
      Licensing__PrivateKeyPath: '/app/data/licenses/private.pem', Licensing__PublicKeyPath: '/app/data/licenses/public.pem',
    };
    await docker(['create', '--name', container, '--label', label, '--network', 'none', '--read-only',
      '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges:true', '--pids-limit', '256', '--memory', '512m', '--cpus', '1',
      '--mount', `type=volume,src=${volume},dst=/app/data`,
      '--tmpfs', '/app/tmp:rw,noexec,nosuid,nodev,size=64m,uid=1654,gid=1654,mode=0700',
      ...Object.entries(environment).flatMap(([key, value]) => ['-e', `${key}=${value}`]), immutable]);
    allocated.add(container);
    const start = Date.now();
    await docker(['start', container]);
    let state;
    do {
      state = JSON.parse(await docker(['inspect', '--format', '{{json .State}}', container]));
      if (state.Status === 'exited') break;
      await delay(100);
    } while (Date.now() - start < deadlineMs);
    const elapsedMs = Date.now() - start;
    const captured = await execute('docker', ['logs', container], { timeout: 10_000, maxBuffer: 4 * 1024 * 1024 });
    const logs = captured.stdout + captured.stderr;
    await writeFile(resolve(output, 'startup.log'), logs);
    Object.assign(report, { elapsedMs, containerStatus: state.Status, exitCode: state.ExitCode,
      logSha256: createHash('sha256').update(logs).digest('hex') });
    verifyRejectedStartup(state, logs, elapsedMs);
    report.status = 'passed';
  } catch (error) {
    failure = error;
    report.failure = 'Production startup rejection verification failed; no process arguments or secrets are recorded.';
  } finally {
    const cleanup = [];
    for (const [kind, name] of [['container', container], ['container', seed], ['volume', volume]]) {
      if (await owns(kind, name)) {
        try { await docker(kind === 'container' ? ['rm', '--force', name] : ['volume', 'rm', name]); }
        catch { cleanup.push(kind); }
      } else if (allocated.has(name)) cleanup.push(`${kind}-ownership-unconfirmed`);
    }
    await rm(temporary, { recursive: true, force: true });
    report.resourcesRemoved = cleanup.length === 0;
    report.completedAt = new Date().toISOString();
    if (cleanup.length) { report.status = 'failed'; failure = new Error('Synthetic resource cleanup failed.'); }
    await writeFile(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
  }
  if (failure) throw new Error('Production startup smoke failed; inspect artifacts/sbom/webapi-startup without exposing secrets.');
  return report;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    assert.equal(process.argv[2], '--image');
    assert.equal(process.argv.length, 4);
    console.log(JSON.stringify(await runProductionStartupSmoke(process.argv[3])));
  } catch { console.error('Production startup smoke failed; inspect its local report.'); process.exitCode = 1; }
}
