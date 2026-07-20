import assert from 'node:assert/strict';
import test from 'node:test';
import { appendSignedInSignal, consumeSignedInSignal } from '../../shared/signedInSignal.js';

const COMPANY_ORIGIN = 'http://localhost:5173';

test('appendSignedInSignal appends the signal only for the given company origin', () => {
  const url = appendSignedInSignal('http://localhost:5173/pricing', COMPANY_ORIGIN);
  assert.equal(url, 'http://localhost:5173/pricing?pxa_signed_in=1');
});

test('appendSignedInSignal leaves a target on a different origin unchanged', () => {
  const url = appendSignedInSignal('http://localhost:5178/dashboard', COMPANY_ORIGIN);
  assert.equal(url, 'http://localhost:5178/dashboard');
});

test('appendSignedInSignal preserves existing query parameters on the company origin', () => {
  const url = appendSignedInSignal('http://localhost:5173/register?utm_source=ads', COMPANY_ORIGIN);
  assert.equal(url, 'http://localhost:5173/register?utm_source=ads&pxa_signed_in=1');
});

test('appendSignedInSignal returns the original value unchanged for an invalid URL', () => {
  assert.equal(appendSignedInSignal('not-a-url', COMPANY_ORIGIN), 'not-a-url');
});

test('consumeSignedInSignal detects and strips the signal, preserving other params', () => {
  const result = consumeSignedInSignal('?utm_source=ads&pxa_signed_in=1');
  assert.deepEqual(result, { signedIn: true, cleanedSearch: 'utm_source=ads' });
});

test('consumeSignedInSignal returns null when the signal is absent', () => {
  assert.equal(consumeSignedInSignal('?utm_source=ads'), null);
  assert.equal(consumeSignedInSignal(''), null);
});

test('consumeSignedInSignal ignores a falsy signal value', () => {
  assert.equal(consumeSignedInSignal('?pxa_signed_in=0'), null);
});
