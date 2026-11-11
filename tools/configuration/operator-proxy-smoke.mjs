import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const mounts = [
  ['deploy/observability/operator-gateway/templates/default.conf.template', '/etc/nginx/templates/default.conf.template'],
  ['deploy/observability/operator-gateway/15-pvad-operator.envsh', '/docker-entrypoint.d/15-pvad-operator.envsh'],
  ['tools/configuration/environment.sh', '/etc/pvad/environment.sh'],
];
const valid = {
  PVAD_OPERATOR_SERVER_NAME: 'operator.test',
  PVAD_OPERATOR_API_UPSTREAM: 'http://127.0.0.1:8081',
  PVAD_OPERATOR_UI_UPSTREAM: 'http://127.0.0.1:8082',
};
// Rejected values carry a sentinel that must never be echoed back by the entrypoint.
const sentinel = 'smoke-sentinel-7f3a';
const scenarios = [
  ['valid', valid, null],
  ['missing host', { ...valid, PVAD_OPERATOR_SERVER_NAME: undefined }, 'PVAD_OPERATOR_SERVER_NAME'],
  ['invalid host', { ...valid, PVAD_OPERATOR_SERVER_NAME: `${sentinel}.test; return 200` }, 'PVAD_OPERATOR_SERVER_NAME'],
  ['missing API upstream', { ...valid, PVAD_OPERATOR_API_UPSTREAM: undefined }, 'PVAD_OPERATOR_API_UPSTREAM'],
  ['invalid API upstream', { ...valid, PVAD_OPERATOR_API_UPSTREAM: `ftp://${sentinel}:8081` }, 'PVAD_OPERATOR_API_UPSTREAM'],
  ['invalid UI upstream', { ...valid, PVAD_OPERATOR_UI_UPSTREAM: `http://${sentinel}/path?x=1` }, 'PVAD_OPERATOR_UI_UPSTREAM'],
];
for (const [scenario, values, rejectedKey] of scenarios) {
  const env = Object.entries(values).filter(([, value]) => value !== undefined)
    .flatMap(([key, value]) => ['-e', `${key}=${value}`]);
  const result = spawnSync('docker', ['run', '--rm', '--network', 'none', '--read-only',
    '--tmpfs', '/etc/nginx/conf.d', '--tmpfs', '/var/cache/nginx', '--tmpfs', '/var/run', '--tmpfs', '/tmp',
    '--add-host', 'pvad-grafana:127.0.0.1',
    ...mounts.flatMap(([source, target]) => ['-v', `${root}${source}:${target}:ro`]),
    ...env, 'nginx:1.29-alpine', 'nginx', '-T'], { encoding: 'utf8', timeout: 60_000 });
  const output = `${result.stdout ?? ''}${result.stderr ?? ''}`;
  assert.equal(result.error, undefined, result.error?.message);
  if (rejectedKey) {
    assert.notEqual(result.status, 0, `${scenario} was accepted`);
    assert.match(output, new RegExp(`Invalid configuration key: ${rejectedKey}\\.`));
    assert.doesNotMatch(output, new RegExp(sentinel), `${scenario} echoed the rejected value`);
  } else {
    assert.equal(result.status, 0, output);
    assert.match(output, /test is successful/);
    assert.match(output, /server_name operator\.test;/);
    assert.match(output, /proxy_pass http:\/\/127\.0\.0\.1:8081\/api\/pvad\/v1\/admin\/operator\/access;/);
    assert.match(output, /location \/api\/ \{\s+proxy_pass http:\/\/127\.0\.0\.1:8081;/);
    assert.match(output, /proxy_pass http:\/\/127\.0\.0\.1:8082/);
    // Clients cannot forge operator identity or API credentials through the gateway.
    assert.match(output, /proxy_set_header X-WEBAUTH-USER "";/);
    assert.match(output, /proxy_set_header X-PVAD-API-Key "";/);
    assert.match(output, /auth_request \/_pvad_operator_auth;/);
  }
  console.log(`Operator proxy ${scenario}: passed`);
}
