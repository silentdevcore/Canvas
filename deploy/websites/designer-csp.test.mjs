import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const policy = readFileSync(new URL('./designer.headers.conf', import.meta.url), 'utf8');
const nginx = readFileSync(new URL('./nginx.conf.template', import.meta.url), 'utf8');
const header = name => policy.match(new RegExp(`add_header ${name} "([^"]+)" always;`))?.[1];
const directives = value => new Map(value.split(';').filter(part => part.trim()).map(part => {
  const [name, ...sources] = part.trim().split(/\s+/); return [name, sources];
}));
test('enforced baseline limits base, plugin, embedding and form destinations', () => {
  const baseline = directives(header('Content-Security-Policy'));
  for (const name of ['base-uri', 'form-action']) assert.deepEqual(baseline.get(name), ["'self'"]);
  assert.deepEqual(baseline.get('frame-ancestors'), ["'none'"]);
  assert.deepEqual(baseline.get('object-src'), ["'self'", 'blob:']);
  assert.equal(baseline.has('script-src'), false);
});
test('strict resource candidate is explicitly report-only without third-party reporting', () => {
  const candidate = directives(header('Content-Security-Policy-Report-Only'));
  assert.deepEqual(candidate.get('worker-src'), ["'self'", 'blob:']);
  assert.deepEqual(candidate.get('frame-src'), ["'self'", 'blob:']);
  assert.ok(candidate.get('script-src').includes('https://cdn.jsdelivr.net/npm/monaco-editor@0.55.1/min/'));
  assert.ok(!candidate.get('script-src').includes("'unsafe-eval'"));
  assert.ok(!candidate.get('script-src').includes("'unsafe-inline'"));
  assert.ok(!candidate.has('report-uri') && !candidate.has('report-to'));
});
test('optional profile is included in every location despite Nginx header inheritance', () => {
  const locations = nginx.split(/\n    location /).slice(1);
  assert.equal(locations.length, 10);
  for (const location of locations) assert.match(location.split('\n    }')[0], /include \/etc\/nginx\/pvad\/\*\.headers\.conf;/);
  assert.match(policy, /Referrer-Policy "strict-origin-when-cross-origin" always;/);
});
