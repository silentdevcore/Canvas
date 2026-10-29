import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import test from 'node:test';

const require = createRequire(import.meta.url);
const ts = require('../../pvad-designer/node_modules/typescript');

function configuration(relative) {
  const source = readFileSync(new URL(`../../${relative}`, import.meta.url), 'utf8');
  const tree = ts.createSourceFile(relative, source, ts.ScriptTarget.Latest, true);
  let result;
  function visit(node) {
    if (ts.isExportAssignment(node)) {
      result = ts.isCallExpression(node.expression) ? node.expression.arguments[0] : node.expression;
    }
    ts.forEachChild(node, visit);
  }
  visit(tree);
  assert.ok(result && ts.isObjectLiteralExpression(result), relative);
  return result;
}

function property(object, name) {
  assert.ok(object && ts.isObjectLiteralExpression(object));
  const entry = object.properties.find(node => ts.isPropertyAssignment(node) && node.name.text === name);
  assert.ok(entry, `Missing configuration property: ${name}`);
  return entry.initializer;
}

test('all six development API proxies retain their same-origin upstream contract', () => {
  for (const file of ['pvad-designer/vite.config.ts', ...[
    'Company', 'Documentation', 'Demo', 'Admin', 'Account',
  ].map(app => `websites/PVAD.${app}/vite.config.js`)]) {
    const proxy = property(property(configuration(file), 'server'), 'proxy');
    const api = property(proxy, '/api');
    assert.equal(property(api, 'target').text, 'apiTarget', file);
    assert.match(readFileSync(new URL(`../../${file}`, import.meta.url), 'utf8'), /const apiTarget = resolvePvadDevApiTarget\(\)/);
    assert.equal(property(api, 'changeOrigin').kind, ts.SyntaxKind.TrueKeyword, file);
    if (file.startsWith('pvad-designer')) {
      assert.equal(property(property(api, 'headers'), 'X-PVAD-Application').text, 'designer');
    }
    if (file.includes('PVAD.Account')) {
      for (const path of ['/connect', '/.well-known']) {
        const endpoint = property(proxy, path);
        assert.equal(property(endpoint, 'target').text, 'apiTarget');
        assert.equal(property(endpoint, 'changeOrigin').kind, ts.SyntaxKind.FalseKeyword);
      }
    }
  }
});
