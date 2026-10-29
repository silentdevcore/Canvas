import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const runner = fileURLToPath(new URL('./native-project-installed-smoke.mjs', import.meta.url));
function rejects(env, message) {
  const result = spawnSync(process.execPath, [runner], { encoding: 'utf8', timeout: 5000,
    env: { ...process.env, PVAD_RUN_NATIVE_INSTALL: '', PVAD_NATIVE_TEST_DISPOSABLE: '', ...env } });
  assert.equal(result.error, undefined);
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, message);
  assert.doesNotMatch(result.stdout, /OUTPUT|PASS|WAITING_FOR_NATIVE_PERMISSION/);
}

test('installed smoke cannot run without explicit installation opt-in', () => {
  rejects({}, /AssertionError/);
});

test('installed smoke requires an explicitly disposable environment', { skip: process.platform !== 'darwin' }, () => {
  rejects({ PVAD_RUN_NATIVE_INSTALL: '1' }, /explicitly disposable/);
});

test('installed smoke refuses missing service origins before touching Chrome', { skip: process.platform !== 'darwin' }, () => {
  rejects({ PVAD_RUN_NATIVE_INSTALL: '1', PVAD_NATIVE_TEST_DISPOSABLE: '1', PVAD_NATIVE_TEST_ORIGIN: '' }, /PVAD_NATIVE_TEST_ORIGIN is required/);
});

test('installed smoke refuses public and credential-bearing service origins', { skip: process.platform !== 'darwin' }, () => {
  for (const origin of ['https://pvade.com', 'http://user:secret@localhost:16376', 'http://localhost:16376/not-a-root'])
    rejects({ PVAD_RUN_NATIVE_INSTALL: '1', PVAD_NATIVE_TEST_DISPOSABLE: '1', PVAD_NATIVE_TEST_ORIGIN: origin }, /isolated loopback HTTP origin/);
});
