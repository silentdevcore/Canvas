import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

test('the security scan uses the exact image retained by the isolated TLS smoke test', () => {
  const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');
  const smoke = read('deploy/api/mcp-tls-smoke-test.sh');
  const workflow = read('.github/workflows/ci.yml');
  assert.ok(smoke.includes('image_id=$(docker compose -f "$COMPOSE_FILE" images --quiet pvad-mcp)'));
  assert.ok(smoke.includes('docker image tag "$image_id" pvad-mcp-smoke:local'));
  assert.equal(workflow.match(/pvad-mcp-smoke:local/g)?.length, 2);
  assert.ok(!workflow.includes('mcp-smoke-pvad-mcp:latest'));
});
