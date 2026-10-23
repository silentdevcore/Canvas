import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [html, mainSource, shellSource, css] = await Promise.all([
  readFile(new URL('../index.html', import.meta.url), 'utf8'),
  readFile(new URL('../src/main.ts', import.meta.url), 'utf8'),
  readFile(new URL('../src/shell.ts', import.meta.url), 'utf8'),
  readFile(new URL('../src/site.css', import.meta.url), 'utf8'),
]);

test('document metadata protects and scales the standalone Account app', () => {
  assert.match(html, /<html lang="en">/);
  assert.match(html, /name="viewport"/);
  assert.match(html, /name="robots" content="noindex, nofollow"/);
});

test('authenticated portal shell exposes landmarks and keyboard navigation', () => {
  assert.match(shellSource, /class="account-skip-link" href="#account-content"/);
  assert.match(shellSource, /<nav class="account-navigation" id="account-navigation" aria-label="Account">/);
  assert.match(shellSource, /<main class="account-content" id="account-content" tabindex="-1">/);
  assert.match(shellSource, /aria-controls="account-navigation" aria-expanded="false"/);
  assert.match(mainSource, /event\.key !== 'Escape'/);
  assert.match(mainSource, /closeAccountNavigation\(true\)/);
  assert.doesNotMatch(shellSource, /tabindex="[1-9]/);
  assert.doesNotMatch(mainSource, /tabindex="[1-9]/);
});

test('styles preserve visible focus and responsive Account layouts', () => {
  assert.match(css, /:focus-visible/);
  assert.match(css, /\.account-skip-link:focus/);
  assert.match(css, /@media \(max-width: 900px\)/);
  assert.match(css, /@media \(max-width: 620px\)/);
  assert.match(css, /min-width: 320px/);
});
