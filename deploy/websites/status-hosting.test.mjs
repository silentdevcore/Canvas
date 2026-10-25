import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createRequire } from 'node:module';

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

test('hosting guidance preserves the API path when selecting the upstream origin', async () => {
  const guide = await readFile(new URL('./README.md', import.meta.url), 'utf8');
  assert.match(guide, /upstream origin without a trailing slash/);
  assert.match(guide, /`http:\/\/pvad-api:8080`/);
  assert.doesNotMatch(guide, /`http:\/\/pvad-api:8080\/`/);
});

test('status documents and the app shell cannot trap users in stale errors', () => {
  for (const file of ['404', '500', 'maintenance']) {
    assert.match(configuration, new RegExp(`location = /${file}\\.html \\{[\\s\\S]*?Cache-Control \\"no-store\\"`));
  }
  assert.match(configuration, /location = \/index\.html \{[\s\S]*?Cache-Control "no-cache, no-store, must-revalidate"/);
  assert.match(configuration, /max-age=31536000, immutable/);
});

test('PDF module workers have a JavaScript MIME fallback without SPA fallback', () => {
  const worker = configuration.split('location ~* ^/assets/.*\\.mjs$ {')[1]?.split('\n    }')[0];
  assert.ok(worker);
  assert.match(worker, /default_type application\/javascript;/);
  assert.match(worker, /X-Content-Type-Options "nosniff" always;/);
  assert.match(worker, /try_files \$uri =404;/);
});

test('every browser application has one anchored route allowlist', () => {
  for (const app of ['COMPANY', 'DOCUMENTATION', 'DEMO', 'ACCOUNT', 'ADMIN', 'DESIGNER']) {
    assert.match(patterns, new RegExp(`PVAD_${app}_ROUTE_PATTERN=\\^/`));
  }
  assert.match(configuration, /location ~ \$\{PVAD_SPA_ROUTE_PATTERN\}/);
});

test('Designer hosting serves every registered absolute route, including legacy redirects', async () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const ts = require('typescript');
  const source = await readFile(new URL('../../pvad-designer/src/App.tsx', import.meta.url), 'utf8');
  const tree = ts.createSourceFile('App.tsx', source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const pattern = new RegExp(patterns.split('\n').find(line => line.startsWith('PVAD_DESIGNER_ROUTE_PATTERN=')).split('=')[1]);
  const routes = [];
  function visit(node) {
    if ((ts.isJsxSelfClosingElement(node) || ts.isJsxOpeningElement(node)) && node.tagName.getText(tree) === 'Route') {
      const path = node.attributes.properties.find(attribute => ts.isJsxAttribute(attribute) && attribute.name.text === 'path');
      if (path?.initializer && ts.isStringLiteral(path.initializer) && path.initializer.text.startsWith('/'))
        routes.push(path.initializer.text);
    }
    ts.forEachChild(node, visit);
  }
  visit(tree);
  assert.ok(routes.length > 20);
  for (const route of routes) assert.ok(pattern.test(route), `Nginx must serve the app shell for ${route}`);
  for (const route of ['/not-a-pvad-route', '/template-other', '/importer-other', '/report-other'])
    assert.equal(pattern.test(route), false, `Unknown path must retain HTTP 404: ${route}`);
});

test('Account hosting serves every public, portal and legal-review route on direct navigation', async () => {
  const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));
  const ts = require('typescript');
  const source = await readFile(new URL('../../websites/PVAD.Account/src/main.ts', import.meta.url), 'utf8');
  const tree = ts.createSourceFile('main.ts', source, ts.ScriptTarget.Latest, true);
  const pattern = new RegExp(patterns.split('\n').find(line => line.startsWith('PVAD_ACCOUNT_ROUTE_PATTERN=')).split('=')[1]);
  const routes = new Set();
  function visit(node) {
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name)) {
      if (node.name.text === 'publicPaths' && ts.isNewExpression(node.initializer)
          && ts.isArrayLiteralExpression(node.initializer.arguments?.[0])) {
        for (const item of node.initializer.arguments[0].elements)
          if (ts.isStringLiteral(item)) routes.add(item.text);
      }
      if (node.name.text === 'portalPages' && ts.isObjectLiteralExpression(node.initializer))
        for (const item of node.initializer.properties)
          if (item.name && ts.isStringLiteral(item.name)) routes.add(item.name.text);
    }
    if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression)
        && node.expression.expression.getText(tree) === 'portalPaths' && node.expression.name.text === 'add'
        && ts.isStringLiteral(node.arguments[0])) routes.add(node.arguments[0].text);
    ts.forEachChild(node, visit);
  }
  visit(tree);
  assert.ok(routes.size >= 20, 'Account route extraction must not silently stop matching.');
  for (const route of routes) assert.ok(pattern.test(route), `Nginx must serve the Account app shell for ${route}`);
  for (const route of ['/not-an-account-route', '/reset-password-other', '/legal-review-other'])
    assert.equal(pattern.test(route), false, `Unknown path must retain HTTP 404: ${route}`);
});
