import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { inspectLegalLaunchReadiness } from './validate-legal-launch-readiness.mjs';

const validator = fileURLToPath(new URL('./validate-legal-launch-readiness.mjs', import.meta.url));

test('current development state exposes every unresolved Legal launch class', () => {
  const result = inspectLegalLaunchReadiness();

  assert.deepEqual(result.violations, []);
  assert.ok(result.blockers.some(value => value.includes('operator identity')));
  assert.ok(result.blockers.some(value => value.includes('data-processing inventory')));
  assert.ok(result.blockers.some(value => value.includes('retention decisions')));
  assert.ok(result.blockers.some(value => value.includes('draft or launch-blocking copy')));
  assert.ok(result.blockers.some(value => value.includes('Imprint')));
});

test('development reports blockers without pretending that Production is ready', () => {
  const output = execFileSync(process.execPath, [validator], { encoding: 'utf8' });
  assert.match(output, /Development validation passed; Production remains fail-closed/);
  assert.match(output, /Launch blockers: [1-9]/);
});

test('production validation fails closed while Legal decisions remain unresolved', () => {
  const result = spawnSync(process.execPath, [validator, '--production'], { encoding: 'utf8' });
  assert.equal(result.status, 1);
  assert.match(result.stdout, /BLOCKER: Verified operator identity is missing/);
});
