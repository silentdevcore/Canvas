import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [source, api, css] = await Promise.all([
  readFile(new URL('../src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/api.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/site.css', import.meta.url), 'utf8'),
]);

test('Legal Admin uses the protected version comparison API', () => {
  assert.match(api, /versions\/compare\?\$\{query\}/);
  assert.match(source, /compareAdminLegalVersions\(baseVersionId, targetVersionId\)/);
  assert.match(source, /Compare this version with its recorded predecessor first\./);
  assert.match(source, /comparisonReady \? '' : 'disabled'/);
});

test('comparison renders metadata, summary counts, and side-by-side Markdown lines', () => {
  assert.match(source, /Compare versions/);
  assert.match(source, /comparison\.summary\.modified/);
  assert.match(source, /comparison\.baseVersion\.contentHash/);
  assert.match(source, /comparison\.targetVersion\.contentHash/);
  assert.match(source, /Side-by-side Markdown comparison/);
  assert.match(source, /line\.baseLineNumber/);
  assert.match(source, /line\.targetLineNumber/);
});

test('diff layout remains scrollable and responsive', () => {
  assert.match(css, /\.admin-legal-diff-scroll/);
  assert.match(css, /overflow: auto/);
  assert.match(css, /\.admin-legal-diff-row--modified/);
  assert.match(css, /\.admin-legal-diff-row--added/);
  assert.match(css, /\.admin-legal-diff-row--removed/);
  assert.match(css, /@media \(max-width: 700px\)/);
});

test('Legal Admin reports exact-version acceptance progress and minimized exports', () => {
  assert.match(api, /getAdminLegalAcceptance/);
  assert.match(api, /acceptance\/export/);
  assert.match(source, /Acceptance progress/);
  assert.match(source, /summary\.completionPercentage/);
  assert.match(source, /Exports exclude names, email addresses, tokens, and document contents/);
  assert.match(css, /\.admin-legal-acceptance-progress/);
});

test('Legal acceptance export is CSRF protected, audited, and accessibly announced', () => {
  assert.match(api, /method: 'POST'/);
  assert.match(api, /'X-PXA-CSRF': token/);
  assert.match(api, /body: JSON\.stringify\(\{ format, \.\.\.filters \}\)/);
  assert.match(source, /aria-describedby="legal-acceptance-description"/);
  assert.match(source, /aria-label="Acceptance completion"/);
  assert.match(source, /role="status" aria-live="polite"/);
  assert.match(source, /Preparing minimized legal evidence export/);
  assert.match(source, /legal\.acceptanceExporting \? 'disabled' : ''/);
});
