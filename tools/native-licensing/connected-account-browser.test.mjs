import assert from 'node:assert/strict';
import test from 'node:test';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { connectedEnvironment } from './connected-account-browser.mjs';

test('connected rehearsal children inherit local tool paths and Docker selection, never deployment credentials or configuration', () => {
  const environment = connectedEnvironment({ PATH: '/fixture/bin', HOME: '/fixture/home', TMPDIR: '/fixture/tmp',
    DOCKER_HOST: 'unix:///fixture/docker.sock', TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE: '/var/run/docker.sock',
    AWS_SECRET_ACCESS_KEY: 'not-inherited', AZURE_CLIENT_SECRET: 'not-inherited', GITHUB_TOKEN: 'not-inherited',
    PVAD_BOOTSTRAP_ADMIN_PASSWORD: 'not-inherited', ConnectionStrings__PvadDatabase: 'not-inherited',
    PVAD_NATIVE_LICENSE_PATH: 'not-inherited', ASPNETCORE_ENVIRONMENT: 'Production' });
  assert.deepEqual(environment, { PATH: '/fixture/bin', HOME: '/fixture/home', TMPDIR: '/fixture/tmp',
    DOCKER_HOST: 'unix:///fixture/docker.sock', TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE: '/var/run/docker.sock' });
});

test('the rehearsal rejects deployment-like command inputs before creating keys or containers', () => {
  const script = fileURLToPath(new URL('./connected-account-browser.mjs', import.meta.url));
  const result = spawnSync(process.execPath, [script, '--connection-string', 'DO_NOT_ECHO'], { encoding: 'utf8' });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /accepts no deployment inputs/);
  assert.ok(!(result.stdout + result.stderr).includes('DO_NOT_ECHO'));
});
