import assert from 'node:assert/strict';
import { mkdtemp, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { prepareComposeEnvironment } from './pvad-compose.mjs';

test('Compose resolves legacy files and canonical environment without exposing values', async () => {
  const root = await mkdtemp(join(tmpdir(), 'pvad-compose-'));
  await writeFile(join(root, '.env'), 'PXA_API_HOST_PORT=5087\nNEUTRAL=value\n');
  const resolved = await prepareComposeEnvironment([], {}, root);
  assert.equal(resolved.PVAD_API_HOST_PORT, '5087');
  assert.equal(resolved.NEUTRAL, 'value');
  await assert.rejects(prepareComposeEnvironment([], { PVAD_API_HOST_PORT: '9999' }, root),
    /Conflicting configuration keys/);
});

test('Compose honors explicit env files and shell precedence for the same key', async () => {
  const root = await mkdtemp(join(tmpdir(), 'pvad-compose-'));
  await writeFile(join(root, 'chosen.env'), 'PVAD_API_HOST_PORT=5087\n');
  const resolved = await prepareComposeEnvironment(['--env-file', 'chosen.env'],
    { PVAD_API_HOST_PORT: '9999' }, root);
  assert.equal(resolved.PVAD_API_HOST_PORT, '9999');
  await assert.rejects(prepareComposeEnvironment(['--env-file'], {}, root), /requires a path/);
  await writeFile(join(root, 'interpolated.env'), 'PVAD_API_KEY=${OTHER_SECRET}\n');
  await assert.rejects(prepareComposeEnvironment(['--env-file=interpolated.env'], {}, root),
    /Resolve interpolation in env-file key PVAD_API_KEY/);
});
