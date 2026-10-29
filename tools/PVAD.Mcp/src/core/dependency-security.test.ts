import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import test from 'node:test';

const require = createRequire(import.meta.url);
const uri = require('fast-uri');
const qs = require('qs');

test('URI normalization cannot promote an encoded scheme into an executable scheme', () => {
  assert.equal(require('fast-uri/package.json').version, '4.2.3');
  for (const input of ['%6Aavascript:alert(1)', '%u006Aavascript:alert(1)']) {
    assert.equal(uri.normalize(input), input);
    assert.notEqual(uri.parse(input).scheme, 'javascript');
  }
  assert.equal(uri.normalize('https://example.test/path'), 'https://example.test/path');
});

test('query serialization tolerates non-callable own toString without crashing the server', () => {
  assert.equal(require('qs/package.json').version, '6.16.1');
  assert.doesNotThrow(() => qs.stringify({ a: [{ toString: 'x' }] }, { arrayFormat: 'comma' }));
  assert.equal(qs.stringify({ a: [1, 2] }, { arrayFormat: 'comma' }), 'a=1%2C2');
});
