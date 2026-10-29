import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [html, source, css] = await Promise.all([
  readFile(new URL('../index.html', import.meta.url), 'utf8'),
  readFile(new URL('../src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/site.css', import.meta.url), 'utf8'),
]);

test('document metadata protects and scales the standalone Admin app', () => {
  assert.match(html, /<html lang="en">/);
  assert.match(html, /name="viewport"/);
  assert.match(html, /name="robots" content="noindex, nofollow"/);
});

test('authenticated shell exposes landmarks and keyboard navigation', () => {
  assert.match(source, /class="admin-skip-link" href="#admin-content"/);
  assert.match(source, /<nav class="admin-navigation" aria-label="Administration">/);
  assert.match(source, /<main class="admin-content" id="admin-content" tabindex="-1">/);
  assert.match(source, /aria-controls="admin-sidebar" aria-expanded="false"/);
  assert.match(source, /event\.key !== 'Escape'/);
  assert.match(source, /closeAdminSidebar\(true\)/);
  assert.doesNotMatch(source, /tabindex="[1-9]/);
});

test('styles preserve visible focus and responsive Admin layouts', () => {
  assert.match(css, /:focus-visible/);
  assert.match(css, /\.admin-skip-link:focus/);
  assert.match(css, /@media \(max-width: 900px\)/);
  assert.match(css, /@media \(max-width: 700px\)/);
  assert.match(css, /min-width: 320px/);
});
