import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { gatewayProtocol, runOperatorGatewayRehearsal } from './operator-gateway-rehearsal.mjs';
import { gatewayClient } from './fixtures/operator-gateway-fixture.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));

test('the synthetic protocol reads exact existing aliases without editing compatibility registers', async () => {
  const protocol = await gatewayProtocol();
  assert.equal(protocol.credentialHeaders.length, 4);
  assert.equal(new Set(protocol.credentialHeaders).size, 4);
  assert.equal(typeof protocol.legacyOperatorHeader, 'string');
  assert.notEqual(protocol.legacyOperatorHeader, 'X-PVAD-Operator');
  for (const path of ['docs/reference/compatibility-identifiers.v1.json', 'operator-docs/compatibility-identifiers.v1.json'])
    assert.deepEqual(readFileSync(new URL('../../' + path, import.meta.url)), execFileSync('git', ['show', `HEAD:${path}`], { cwd: root }));
});

test('the fixture HTTP client cannot target arbitrary hosts or inject request-line headers', async () => {
  for (const input of [{ target: 'external', path: '/' }, { target: 'gateway', path: 'https://external.invalid/' },
    { target: 'gateway', path: '/\r\nX-Header: value' }, { target: 'control', path: null }])
    await assert.rejects(gatewayClient(input), /Invalid synthetic/);
});

test('missing cached images are rejected before creating any network or container', async () => {
  const calls = [];
  await assert.rejects(runOperatorGatewayRehearsal({ docker: args => { calls.push(args); return 'not-an-image'; } }), /Cached/);
  assert.equal(calls.length, 1);
  assert.deepEqual(calls[0].slice(0, 2), ['image', 'inspect']);
});

function failedDocker({ foreign = false, lostNetworkReply = false } = {}) {
  const calls = [], resources = new Map();
  const docker = args => {
    calls.push(args);
    if (args[0] === 'image') return `sha256:${'b'.repeat(64)}`;
    if (args[0] === 'network' && args[1] === 'create') {
      resources.set(args.at(-1), foreign ? 'foreign-owner' : args[args.indexOf('--label') + 1].split('=')[1]);
      if (lostNetworkReply) throw new Error('Synthetic network-create reply lost');
      return args.at(-1);
    }
    if (args[1] === 'inspect') {
      if (!resources.has(args.at(-1))) {
        const error = new Error('Synthetic absent resource');
        error.stderr = 'No such object';
        throw error;
      }
      return args.includes('{{.Internal}}') ? 'true' : resources.get(args.at(-1));
    }
    if (args[0] === 'run') throw new Error('Synthetic start failure');
    if (args[0] === 'network' && args[1] === 'rm') { resources.delete(args.at(-1)); return ''; }
    throw new Error('Unexpected synthetic Docker command');
  };
  return { docker, calls, resources };
}

test('a failed fixture startup removes only the owned internal network', async () => {
  const { docker, calls, resources } = failedDocker();
  await assert.rejects(runOperatorGatewayRehearsal({ docker, progress: () => { throw new Error('Synthetic reporter failure'); } }), /failed during start/);
  assert.equal(resources.size, 0);
  const create = calls.find(args => args[0] === 'network' && args[1] === 'create');
  assert.ok(create.includes('--internal'));
  const run = calls.find(args => args[0] === 'run');
  assert.equal(run[run.indexOf('--pull') + 1], 'never');
  assert.equal(run[run.indexOf('PVAD_LAB_CONTROL_TOKEN') - 1], '--env');
  assert.ok(!run.includes('-p') && !run.includes('--publish'));
  assert.ok(!calls.some(args => args[0] === 'volume'));
});

test('a foreign network owner is neither used nor removed', async () => {
  const { docker, calls, resources } = failedDocker({ foreign: true });
  await assert.rejects(runOperatorGatewayRehearsal({ docker }), /cleanup incomplete/);
  assert.equal(resources.size, 1);
  assert.ok(!calls.some(args => args[0] === 'run' || args.includes('rm')));
});

test('a lost network-create response still cleans the confirmed owned network', async () => {
  const { docker, calls, resources } = failedDocker({ lostNetworkReply: true });
  await assert.rejects(runOperatorGatewayRehearsal({ docker }), /failed during prepare/);
  assert.equal(resources.size, 0);
  assert.equal(calls.filter(args => args[0] === 'network' && args[1] === 'rm').length, 1);
});

test('CLI arguments cannot select existing resources or activate deployments', () => {
  const result = spawnSync(process.execPath, ['tools/configuration/operator-gateway-rehearsal.mjs', '--apply'],
    { cwd: root, encoding: 'utf8', env: { ...process.env, DOCKER_HOST: 'unix:///nonexistent-lab.sock' } });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /No arguments or external resource names/);
});
