import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const [adminSource, adminApiSource, adminCss, publicDocumentationSource, companySource, handbookJson] = await Promise.all([
  readFile(new URL('../src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/api.js', import.meta.url), 'utf8'),
  readFile(new URL('../src/site.css', import.meta.url), 'utf8'),
  readFile(new URL('../../PVAD.Documentation/src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../../PVAD.Company/src/main.js', import.meta.url), 'utf8'),
  readFile(new URL('../../../PVAD.WebApi/AdminDocumentation/admin-documentation.json', import.meta.url), 'utf8'),
]);
const { groups: adminDocGroups, routeCoverage: adminRouteCoverage } = JSON.parse(handbookJson);
const topics = adminDocGroups.flatMap((group) => group.topics);

test('Admin handbook is an authenticated PVAD.Admin route', () => {
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
  assert.doesNotMatch(companySource, /admin\.(?:powerdoxautomation|pvade)\.com|\/documentation#admin-/);
});

test('protected handbook uses selected host targets and retains dashboard and diagnostic identities', async () => {
  const targets = JSON.parse(await readFile(
    new URL('../../../deploy/rename/pvade-targets.candidate.json', import.meta.url), 'utf8'));
  assert.doesNotMatch(handbookJson, /powerdoxautomation\.com/i);
  const links = topics.flatMap(topic => (topic.references ?? []).map(reference => reference.href));
  for (const [role, path] of [
    ['admin', '/system-status'],
    ['admin', '/dependency-compliance'],
    ['admin', '/openapi/v1.json'],
    ['operator', '/operator/grafana/'],
    ['operator', '/documentation/'],
  ]) assert.ok(links.includes(targets.hosts[role] + path), `${role}${path}`);
  assert.ok(topics.some(topic => topic.notes.some(note => note.includes(new URL(targets.hosts.admin).hostname))));
  const dashboardIds = ['pxa-platform-overview', 'pxa-infrastructure', 'pxa-operations',
    'pxa-document-operations', 'pxa-browser-health'];
  for (const id of dashboardIds) {
    const dashboard = JSON.parse(await readFile(new URL(
      `../../../deploy/observability/grafana/dashboards/${id.replace(/^pxa-/, 'pvad-')}.json`,
      import.meta.url), 'utf8'));
    assert.equal(dashboard.uid, id);
    assert.ok(links.some(link => new URL(link).pathname.includes(`/d/${dashboard.uid}/`)), id);
  }
  assert.match(handbookJson, /X-PXA-CSRF remains a compatibility alias/);
  assert.match(handbookJson, /PXAAPI001/);
});

test('handbook content is fetched after authentication instead of bundled in the SPA', () => {
  assert.match(adminApiSource, /request\('\/api\/pvad\/v1\/admin\/documentation'\)/);
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

test('Admin handbook exposes safe Legal recovery escalation without restore secrets', () => {
  const recovery = topics.find((topic) => topic.title === 'Legal continuity and recovery escalation');
  assert.ok(recovery);
  assert.equal(recovery.permission, 'System Administrator');
  assert.match(recovery.result, /fail-closed/i);
  assert.doesNotMatch(JSON.stringify(recovery), /restore-postgres|PXA_RESTORE_CONFIRM|database_url/i);
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
    '/dependency-compliance',
    '/system-status',
    '/settings',
  ]);
});

test('screenshots are packaged only behind the Admin API', () => {
  for (const screenshot of new Set(topics.map((topic) => topic.screenshot))) {
    assert.ok(
      existsSync(new URL(`../../../PVAD.WebApi/AdminDocumentation/images/${screenshot}`, import.meta.url)),
      `Missing protected Admin screenshot: ${screenshot}`,
    );
    assert.equal(
      existsSync(new URL(`../public/images/documentation/${screenshot}`, import.meta.url)),
      false,
      `Admin screenshot leaked into the public PVAD.Admin static asset directory: ${screenshot}`,
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
