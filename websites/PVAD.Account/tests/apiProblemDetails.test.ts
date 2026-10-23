import assert from 'node:assert/strict';
import test from 'node:test';
import { isJsonResponseContentType } from '../src/api.ts';

test('Account parses JSON and RFC 9457 Problem Details responses', () => {
  assert.equal(isJsonResponseContentType('application/json; charset=utf-8'), true);
  assert.equal(isJsonResponseContentType('application/problem+json; charset=utf-8'), true);
  assert.equal(isJsonResponseContentType('APPLICATION/PROBLEM+JSON'), true);
  assert.equal(isJsonResponseContentType('text/html'), false);
});
