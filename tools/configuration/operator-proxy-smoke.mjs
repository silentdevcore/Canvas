import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const mounts = [
  ['deploy/observability/operator-gateway/templates/default.conf.template', '/etc/nginx/templates/default.conf.template'],
  ['deploy/observability/operator-gateway/15-pvad-operator.envsh', '/docker-entrypoint.d/15-pvad-operator.envsh'],
  ['tools/configuration/environment.sh', '/etc/pvad/environment.sh'],
];
const values = { OPERATOR_SERVER_NAME: 'operator.test', OPERATOR_API_UPSTREAM: 'http://127.0.0.1:8081', OPERATOR_UI_UPSTREAM: 'http://127.0.0.1:8082' };
for (const scenario of ['canonical', 'legacy', 'conflict']) {
  const env = Object.entries(values).flatMap(([key, value]) => ['-e', `${scenario === 'legacy' ? 'PXA' : 'PVAD'}_${key}=${value}`]);
  if (scenario === 'conflict') env.push('-e', 'PXA_OPERATOR_SERVER_NAME=other.test');
  const result = spawnSync('docker', ['run', '--rm', '--network', 'none', '--add-host', 'pvad-grafana:127.0.0.1',
    ...mounts.flatMap(([source, target]) => ['-v', `${root}${source}:${target}:ro`]),
    ...env, 'nginx:1.29-alpine', 'nginx', '-t'], { encoding: 'utf8', timeout: 60_000 });
  const output = `${result.stdout ?? ''}${result.stderr ?? ''}`;
  assert.equal(result.error, undefined, result.error?.message);
  if (scenario === 'conflict') {
    assert.notEqual(result.status, 0);
    assert.match(output, /Conflicting configuration keys: PVAD_OPERATOR_SERVER_NAME and PXA_OPERATOR_SERVER_NAME/);
  } else {
    assert.equal(result.status, 0, output);
    assert.match(output, /test is successful/);
  }
  console.log(`Operator proxy ${scenario}: passed`);
}
