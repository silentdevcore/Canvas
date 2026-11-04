import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import { apiListeningAddress, childEnvironment, gatewayAddress, removeOwnedResources, runOperatorBrowserRehearsal } from './operator-browser-rehearsal.mjs';

test('child processes do not inherit application credentials, proxy settings or storage configuration', () => {
  assert.deepEqual(childEnvironment({ PATH: '/synthetic/bin', HOME: '/synthetic/home', PVAD_BOOTSTRAP_ADMIN_PASSWORD: 'private',
    ConnectionStrings__PvadDatabase: 'private', HTTP_PROXY: 'private', AWS_SECRET_ACCESS_KEY: 'private' }),
  { PATH: '/synthetic/bin', HOME: '/synthetic/home' });
});

test('the Grafana proxy allowlist uses one private address in the newly allocated Docker subnet', () => {
  assert.equal(gatewayAddress('172.19.0.0/16'), '172.19.0.254');
  assert.equal(gatewayAddress('192.168.23.0/24'), '192.168.23.254');
  for (const subnet of ['8.8.8.0/24', '127.0.0.0/24', '172.999.0.0/16', '172.19.1.0/16', '10.0.0.1/24', '::1/128', '10.0.0.0/8'])
    assert.throws(() => gatewayAddress(subnet));
});

test('only the owned API lifetime event can supply a loopback listener', () => {
  const event = { category: 'Microsoft.Hosting.Lifetime', eventName: 'ListeningOnAddress', attributes: { address: 'http://127.0.0.1:12345' } };
  assert.equal(apiListeningAddress(JSON.stringify(event)), 'http://127.0.0.1:12345');
  assert.equal(apiListeningAddress(JSON.stringify({ ...event, category: 'Other' })), null);
  assert.equal(apiListeningAddress('Now listening on: http://127.0.0.1:12345'), null);
  for (const address of ['https://127.0.0.1:12345', 'http://external.test:12345', 'http://127.0.0.1:0', 'http://secret@127.0.0.1:12345', 'http://127.0.0.1:12345/path'])
    assert.equal(apiListeningAddress(JSON.stringify({ ...event, attributes: { address } })), null);
});

test('missing cached images cannot start builds, applications or Docker resources', async () => {
  const calls = [];
  await assert.rejects(runOperatorBrowserRehearsal({ docker: args => { calls.push(args); return 'unavailable'; } }), /Cached images/);
  assert.equal(calls.length, 1);
  assert.deepEqual(calls[0].slice(0, 2), ['image', 'inspect']);
});

test('cleanup removes only confirmed resources in reverse creation order', async () => {
  const calls = [], removed = new Set();
  await removeOwnedResources([['network', 'synthetic-network'], ['container', 'synthetic-db']], 'synthetic-owner', args => {
    calls.push(args);
    if (removed.has(args.at(-1))) { const error = new Error(); error.stderr = 'No such object'; throw error; }
    if (args.includes('rm')) removed.add(args.at(-1));
    return args[1] === 'inspect' ? 'synthetic-owner' : '';
  });
  assert.deepEqual(calls.filter(args => args.includes('rm')), [['rm', '-f', '-v', 'synthetic-db'], ['network', 'rm', 'synthetic-network']]);
});

test('foreign resources are never deleted and a missing daemon is not treated as absent resources', async () => {
  const calls = [];
  await assert.rejects(removeOwnedResources([['container', 'foreign']], 'expected', args => { calls.push(args); return 'other'; }), /ownership mismatch/);
  assert.equal(calls.length, 1);
  await assert.rejects(removeOwnedResources([['container', 'unknown']], 'expected', () => {
    const error = new Error(); error.stderr = 'permission denied while connecting to Docker'; throw error;
  }), /ownership inspection failed/);
});

test('lost network-create responses still remove the owned network before any build or container starts', async () => {
  const calls = [], owned = new Map();
  await assert.rejects(runOperatorBrowserRehearsal({ docker: args => {
    calls.push(args);
    if (args[0] === 'image') return `sha256:${'a'.repeat(64)}`;
    if (args[0] === 'network' && args[1] === 'create') {
      owned.set(args.at(-1), args[args.indexOf('--label') + 1].split('=')[1]);
      throw new Error('Synthetic lost response');
    }
    if (args[0] === 'network' && args[1] === 'inspect') {
      if (owned.has(args.at(-1))) return owned.get(args.at(-1));
      const error = new Error(); error.stderr = 'No such network'; throw error;
    }
    if (args[0] === 'network' && args[1] === 'rm') { owned.delete(args.at(-1)); return ''; }
    throw new Error('Unexpected synthetic operation');
  } }), /failed during prepare/);
  assert.equal(owned.size, 0);
  assert.ok(!calls.some(args => args[0] === 'run'));
});

test('browser scenarios use the real UI and API without authorization mocks or persisted browser credentials', () => {
  const source = readFileSync(new URL('./operator-browser-scenarios.mjs', import.meta.url), 'utf8');
  assert.match(source, /getByLabel\('Email or username'\)/);
  assert.match(source, /user\.login, identity/);
  assert.match(source, /member\.user\.roles\.includes\('Organization Administrator'\)/);
  assert.match(source, /ExpiresAt/);
  assert.doesNotMatch(source, /route\.fulfill|storageState\(|tracing\.start|recordVideo/);
});

test('database readiness waits for the final TCP server, and API readiness uses its owned ephemeral listener', () => {
  const source = readFileSync(new URL('./operator-browser-rehearsal.mjs', import.meta.url), 'utf8');
  assert.match(source, /'pg_isready', '--host', '127\.0\.0\.1'/);
  assert.match(source, /ASPNETCORE_URLS: 'http:\/\/127\.0\.0\.1:0'/);
  assert.match(source, /api\.address/);
  assert.match(source, /harnessBaseline/);
});

test('CLI refuses deployment flags and external resource names before contacting Docker', () => {
  const result = spawnSync(process.execPath, ['tools/configuration/operator-browser-rehearsal.mjs', '--apply'],
    { encoding: 'utf8', env: { ...process.env, DOCKER_HOST: 'unix:///nonexistent-synthetic.sock' } });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No arguments or external resource names/);
});
