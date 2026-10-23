import test from 'node:test';
import assert from 'node:assert/strict';
import { runServiceCutover } from './service-cutover-smoke.mjs';

function syntheticDocker({ failInsert = false, foreignVolume = false } = {}) {
  const calls = [], containers = new Set();
  let label, rows = 'one';
  const docker = args => {
    calls.push(args);
    if (args[0] === 'image') return `sha256:${'a'.repeat(64)}`;
    if (args[0] === 'volume' && args[1] === 'create') { label = args[3].split('=')[1]; return 'created'; }
    if (args[1] === 'inspect') {
      if (args[0] === 'volume') return foreignVolume ? 'someone-else' : label;
      if (!containers.has(args.at(-1))) throw new Error('No synthetic container');
      return label;
    }
    if (args[0] === 'run') { containers.add(args[args.indexOf('--name') + 1]); return 'created'; }
    if (args[0] === 'exec') {
      const sql = args.at(-1);
      if (sql === 'SELECT 1') return '1';
      if (sql.startsWith('INSERT INTO')) { if (failInsert) throw new Error('Synthetic write failure'); rows = 'two'; }
      if (sql.startsWith('SELECT id')) return rows;
      if (sql.includes('count(*)')) return '2';
    }
    return '';
  };
  return { docker, calls };
}
test('stops each writer before its successor and cleans only owned resources', async () => {
  const { docker, calls } = syntheticDocker();
  const report = await runServiceCutover(docker);
  assert.equal(report.rollbackVerified, true);
  assert.equal(report.labeledResourcesRemoved, true);
  const lifecycle = calls.filter(args => ['run', 'stop', 'start'].includes(args[0])).map(args => args[0]);
  assert.deepEqual(lifecycle, ['run', 'stop', 'run', 'stop', 'start']);
  assert.equal(calls.filter(args => args[0] === 'rm').length, 2);
  assert.equal(calls.filter(args => args[0] === 'volume' && args[1] === 'rm').length, 1);
});
test('failed synthetic writes still clean the isolated labeled resources', async () => {
  const { docker, calls } = syntheticDocker({ failInsert: true });
  await assert.rejects(runServiceCutover(docker), /Synthetic write failure/);
  assert.equal(calls.filter(args => args[0] === 'rm').length, 2);
});
test('unconfirmed volume ownership never starts a container or deletes the volume', async () => {
  const { docker, calls } = syntheticDocker({ foreignVolume: true });
  await assert.rejects(runServiceCutover(docker), /ownership/);
  assert.equal(calls.some(args => args[0] === 'run' || args[0] === 'rm' || args[0] === 'volume' && args[1] === 'rm'), false);
});
