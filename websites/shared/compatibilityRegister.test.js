import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { publicCompatibilityRegisterPath, publicCompatibilityRegisterPlugin, readPublicCompatibilityRegister } from './compatibilityRegister.js';

test('public reference plugin emits exactly one allowlisted public JSON asset', () => {
  const emitted = [];
  publicCompatibilityRegisterPlugin().generateBundle.call({ emitFile: value => emitted.push(value) });
  assert.equal(emitted.length, 1);
  assert.equal(emitted[0].fileName, publicCompatibilityRegisterPath);
  assert.deepEqual(emitted[0].source, readPublicCompatibilityRegister());
  const publicRegister = JSON.parse(emitted[0].source);
  assert.equal(publicRegister.visibility, 'public');
  const operator = JSON.parse(readFileSync(new URL('../../operator-docs/compatibility-identifiers.v1.json', import.meta.url)));
  const ids = new Set(publicRegister.entries.map(entry => entry.id));
  assert.ok(operator.entries.every(entry => !ids.has(entry.id)));
});

test('development middleware serves only the public allowlisted route and safe HTTP methods', () => {
  let middleware;
  publicCompatibilityRegisterPlugin().configureServer({ middlewares: { use: callback => { middleware = callback; } } });
  let next = false, status, headers, body;
  const response = { writeHead: (value, values) => { status = value; headers = values; }, end: value => { body = value; } };
  middleware({ method: 'GET', url: '/operator-docs/compatibility-identifiers.v1.json' }, response, () => { next = true; });
  assert.equal(next, true);
  assert.equal(status, undefined);
  middleware({ method: 'POST', url: '/' + publicCompatibilityRegisterPath }, response, () => {});
  assert.equal(status, 405);
  assert.equal(headers.Allow, 'GET, HEAD');
  middleware({ method: 'GET', url: '/' + publicCompatibilityRegisterPath + '?version=1' }, response, () => {});
  assert.equal(status, 200);
  assert.match(headers['Content-Type'], /application\/json/);
  assert.deepEqual(body, readPublicCompatibilityRegister());
  middleware({ method: 'HEAD', url: '/' + publicCompatibilityRegisterPath }, response, () => {});
  assert.equal(body, undefined);
});
