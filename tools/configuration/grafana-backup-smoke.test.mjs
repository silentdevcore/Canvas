import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { curlRequest, grafanaRestoreScript, runGrafanaRehearsal } from './grafana-backup-smoke.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));

test('backup evidence uses its own output directory and creates it before the first report write', () => {
  const source = readFileSync(new URL('./grafana-backup-smoke.mjs', import.meta.url), 'utf8');
  const cli = source.slice(source.indexOf('if (process.argv[1]'));
  assert.match(cli, /const reportFile = resolve\(root, 'artifacts\/grafana-backup-rehearsal\/report\.json'\)/);
  const create = cli.indexOf('await mkdir(dirname(reportFile), { recursive: true })');
  assert.ok(create !== -1 && create < cli.indexOf('await writeFile(reportFile,'));
});

test('the rehearsal provisions only the validated current provider and never rewrites it', () => {
  const source = readFileSync(new URL('./grafana-backup-smoke.mjs', import.meta.url), 'utf8');
  assert.match(source, /validateGrafanaProvider\(providerSource\)/);
  assert.equal(source.match(/provisioning\/dashboards\/dashboards\.yml/g).length, 1);
  assert.doesNotMatch(source, /PUT', `\/api\/folders/);
});

test('curl configuration is stdin-only, fixed to container loopback and does not follow redirects', () => {
  const credentials = 'synthetic:unusable-password';
  const config = curlRequest('POST', '/api/admin/users', credentials, { name: 'Synthetic "reader"\nvalue' });
  assert.ok(config.includes('url = "http://127.0.0.1:3000/api/admin/users"'));
  assert.ok(config.includes(`user = ${JSON.stringify(credentials)}`));
  const payload = config.split('\n').find(line => line.startsWith('data = ')).slice(7);
  assert.deepEqual(JSON.parse(JSON.parse(payload)), { name: 'Synthetic "reader"\nvalue' });
  assert.doesNotMatch(config, /^(?:location|proxy)(?:\s|$)/m);
  for (const path of ['https://external.test/api/health', '//external.test/api/health', '/api/../private', '/api/health\nheader=evil'])
    assert.throws(() => curlRequest('GET', path, credentials));
  assert.throws(() => curlRequest('PATCH', '/api/health', credentials));
});

test('missing cached image fails before creating any Docker resources', async () => {
  const calls = [];
  await assert.rejects(runGrafanaRehearsal({ docker: args => { calls.push(args); return 'not-an-image'; } }), /cached/);
  assert.equal(calls.length, 1);
  assert.deepEqual(calls[0].slice(0, 2), ['image', 'inspect']);
});

test('restore checks integrity before extraction and rejects unsafe hashes and archive names', () => {
  const script = grafanaRestoreScript('a'.repeat(64));
  assert.ok(script.indexOf('sha256sum -c') < script.indexOf('tar -xpf'));
  assert.ok(script.includes('|| exit 42; tar'));
  assert.throws(() => grafanaRestoreScript('not-a-checksum'));
  assert.throws(() => grafanaRestoreScript('a'.repeat(64), '../external.tar'));
});

function failedDocker({ foreignVolume = false, lostCreateReply = false } = {}) {
  const resources = new Map(), calls = [];
  const docker = args => {
    calls.push(args);
    if (args[0] === 'image') return `sha256:${'a'.repeat(64)}`;
    if (args[0] === 'volume' && args[1] === 'create') {
      const owner = args[args.indexOf('--label') + 1].split('=')[1];
      resources.set(args.at(-1), foreignVolume ? 'foreign-owner' : owner);
      if (lostCreateReply) throw new Error('Synthetic volume-create reply lost');
      return args.at(-1);
    }
    if (args[1] === 'inspect') {
      if (resources.has(args.at(-1))) return resources.get(args.at(-1));
      const error = new Error('Synthetic absent resource');
      error.stderr = 'No such object';
      throw error;
    }
    if (args[0] === 'run') throw new Error('Synthetic container start failed');
    if (args[0] === 'volume' && args[1] === 'rm') { resources.delete(args.at(-1)); return ''; }
    throw new Error('Unexpected synthetic command');
  };
  return { docker, calls, resources };
}

test('failed container startup removes only the three owned synthetic volumes', async () => {
  const { docker, calls, resources } = failedDocker();
  await assert.rejects(runGrafanaRehearsal({ docker, progress: () => { throw new Error('Synthetic reporter failure'); } }), /failed during baseline/);
  assert.equal(resources.size, 0);
  assert.equal(calls.filter(args => args[0] === 'volume' && args[1] === 'rm').length, 3);
  const run = calls.find(args => args[0] === 'run');
  assert.equal(run[run.indexOf('--network') + 1], 'none');
  assert.equal(run[run.indexOf('--pull') + 1], 'never');
  assert.ok(!run.includes('-p') && !run.includes('--publish'));
  assert.equal(run.at(-1), `sha256:${'a'.repeat(64)}`);
});

test('an unconfirmed volume owner is never deleted or used by a container', async () => {
  const { docker, calls, resources } = failedDocker({ foreignVolume: true });
  await assert.rejects(runGrafanaRehearsal({ docker }), /cleanup incomplete/);
  assert.equal(resources.size, 1);
  assert.ok(!calls.some(args => args[0] === 'run' || args.includes('rm')));
});

test('a lost volume-create reply still cleans the owned volume without starting a container', async () => {
  const { docker, calls, resources } = failedDocker({ lostCreateReply: true });
  await assert.rejects(runGrafanaRehearsal({ docker }), /failed during prepare/);
  assert.equal(resources.size, 0);
  assert.equal(calls.filter(args => args[0] === 'volume' && args[1] === 'rm').length, 1);
  assert.ok(!calls.some(args => args[0] === 'run'));
});

test('CLI refuses external resource names and deployment flags without contacting Docker', () => {
  const result = spawnSync(process.execPath, ['tools/configuration/grafana-backup-smoke.mjs', '--apply'],
    { cwd: root, encoding: 'utf8', env: { ...process.env, DOCKER_HOST: 'unix:///nonexistent-synthetic.sock' } });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No arguments or external resource names/);
});
