import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [adminSource, adminApiSource, adminCss, publicDocumentationSource, companySource, handbookJson] = await Promise.all([
  readFile(new URL('../src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/api.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/site.css', import.meta.url), 'utf8'),
  readFile(new URL('../../PXA.Documentation/src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../../PXA.Company/src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../../../PXA.WebApi/AdminDocumentation/admin-documentation.json', import.meta.url), 'utf8'),
]);
const { groups: adminDocGroups, routeCoverage: adminRouteCoverage } = JSON.parse(handbookJson);
const topics = adminDocGroups.flatMap((group) => group.topics);

test('Admin handbook is an authenticated PXA.Admin route', () => {
  assert.match(adminSource, /path: '\/documentation', label: 'Admin documentation'/);
  assert.match(adminSource, /if \(!state\.user\)/);
  assert.match(adminSource, /if \(!isAdministrator\(state\.user\)\)/);
  assert.match(adminSource, /if \(location\.pathname === '\/documentation'\)/);
  assert.ok(
    adminSource.indexOf("if (!state.user)") < adminSource.indexOf("if (location.pathname === '/documentation')"),
    'Authentication must be checked before rendering the handbook.',
  );
  assert.ok(
    adminSource.indexOf('if (!isAdministrator(state.user))') < adminSource.indexOf("if (location.pathname === '/documentation')"),
    'Administrator authorization must be checked before rendering the handbook.',
  );
});

test('public Documentation and Company do not expose Admin guidance', () => {
  assert.doesNotMatch(publicDocumentationSource, /adminDocGroups|id="administration"|renderAdminDocumentation|Admin documentation/);
  assert.doesNotMatch(companySource, /admin\.powerdoxautomation\.com|\/documentation#admin-/);
});

test('handbook content is fetched after authentication instead of bundled in the SPA', () => {
  assert.match(adminApiSource, /request\('\/api\/pxa\/v1\/admin\/documentation'\)/);
  assert.match(adminSource, /await getAdminDocumentation\(\)/);
  assert.doesNotMatch(adminSource, /Bulk actions report protected or rejected users/);
  assert.doesNotMatch(adminSource, /PXAAPI001-008 distinguish validation/);
});

test('all protected handbook topics have complete workflow contracts', () => {
  assert.ok(topics.length >= 13);
  for (const topic of topics) {
    assert.ok(topic.permission);
    assert.ok(topic.prerequisites.length >= 3);
    assert.ok(topic.steps.length >= 3);
    assert.ok(topic.result);
    assert.ok(topic.failures.length >= 2);
    assert.ok(topic.audit);
    assert.ok(topic.endpoint);
    assert.ok(topic.notes.length >= 2);
  }
});

test('protected handbook covers every Admin workspace', () => {
  assert.deepEqual(adminRouteCoverage.map(([route]) => route), [
    '/dashboard',
    '/users and /users/{id}',
    '/organizations and /organizations/{id}',
    '/roles and /roles/{key}',
    '/subscriptions and /subscriptions/{id}',
    '/licenses and /licenses/{id}',
    '/service-accounts',
    '/mail',
    '/audit',
    '/legal',
    '/system-status',
    '/settings',
  ]);
});

test('screenshots are packaged only behind the Admin API', () => {
  for (const screenshot of new Set(topics.map((topic) => topic.screenshot))) {
    assert.ok(
      existsSync(new URL(`../../../PXA.WebApi/AdminDocumentation/images/${screenshot}`, import.meta.url)),
      `Missing protected Admin screenshot: ${screenshot}`,
    );
    assert.equal(
      existsSync(new URL(`../public/images/documentation/${screenshot}`, import.meta.url)),
      false,
      `Admin screenshot leaked into the public PXA.Admin static asset directory: ${screenshot}`,
    );
  }
});

test('handbook UI provides search, single-topic navigation, and responsive layouts', () => {
  assert.match(adminSource, /id="admin-help-search"/);
  assert.match(adminSource, /const selected = topics\.find/);
  assert.match(adminSource, /bindDocumentationEvents\(\)/);
  assert.match(adminCss, /\.admin-help-layout/);
  assert.match(adminCss, /@media \(max-width: 700px\)/);
});
