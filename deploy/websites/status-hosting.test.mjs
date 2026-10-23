import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const configuration = await readFile(new URL('./nginx.conf.template', import.meta.url), 'utf8');
const patterns = await readFile(new URL('./route-patterns.env.example', import.meta.url), 'utf8');

test('unknown HTML routes retain real error statuses', () => {
  assert.match(configuration, /location \/ \{\s*include \/etc\/nginx\/pvad\/\*\.headers\.conf;\s*try_files \$uri \$uri\/ =404;/s);
  assert.match(configuration, /error_page 404 \/404\.html;/);
  assert.match(configuration, /error_page 500 502 504 \/500\.html;/);
  assert.match(configuration, /error_page 503 \/maintenance\.html;/);
  assert.doesNotMatch(configuration, /error_page\s+404\s+=200/);
});

test('API Problem Details are not replaced with HTML', () => {
  assert.match(configuration, /location \/api\/ \{/);
  assert.match(configuration, /proxy_intercept_errors off;/);
  assert.match(configuration, /proxy_pass \$\{PVAD_API_UPSTREAM\};/);
  assert.match(configuration, /error_page 502 503 504 =503 @api_unavailable;/);
  assert.match(configuration, /default_type application\/problem\+json;/);
  assert.match(configuration, /"code":"PXAAPI018"/);
  assert.match(configuration, /"canonicalCode":"PVADAPI018"/);
  assert.match(configuration, /"title":"PVAD API is unavailable"/);
});

test('status documents and the app shell cannot trap users in stale errors', () => {
  for (const file of ['404', '500', 'maintenance']) {
    assert.match(configuration, new RegExp(`location = /${file}\\.html \\{[\\s\\S]*?Cache-Control \\"no-store\\"`));
  }
  assert.match(configuration, /location = \/index\.html \{[\s\S]*?Cache-Control "no-cache, no-store, must-revalidate"/);
  assert.match(configuration, /max-age=31536000, immutable/);
});

test('every browser application has one anchored route allowlist', () => {
  for (const app of ['COMPANY', 'DOCUMENTATION', 'DEMO', 'ACCOUNT', 'ADMIN', 'DESIGNER']) {
    assert.match(patterns, new RegExp(`PVAD_${app}_ROUTE_PATTERN=\\^/`));
  }
  assert.match(configuration, /location ~ \$\{PVAD_SPA_ROUTE_PATTERN\}/);
});
