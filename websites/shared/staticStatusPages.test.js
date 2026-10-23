import assert from 'node:assert/strict';
import test from 'node:test';
import { renderStaticStatusPage } from './vitePvadVersion.js';

for (const [fileName, expectedCode] of [['404.html', '404'], ['500.html', '500'], ['maintenance.html', '503']]) {
  test(`${fileName} is a standalone, script-free status document`, () => {
    const html = renderStaticStatusPage(fileName, 'PVADE Test');
    assert.match(html, /<!doctype html>/i);
    assert.match(html, new RegExp(`class="code">${expectedCode}<`));
    assert.match(html, /PVADE Test/);
    assert.doesNotMatch(html, /<script|https?:\/\//i);
    assert.match(html, /href="\/"/);
  });
}

test('static status page product names are escaped', () => {
  const html = renderStaticStatusPage('404.html', '<script>bad</script>');
  assert.doesNotMatch(html, /<script>/);
  assert.match(html, /&lt;script&gt;/);
});
