import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import test from 'node:test';
import type { UserInfo } from '../src/api.ts';
import { accountPermissions, hasAccountPermission } from '../src/permissions.ts';

function user(permissions: string[]): UserInfo {
  return {
    id: 'user',
    username: 'user@pxa.test',
    email: 'user@pxa.test',
    displayName: 'User',
    roles: ['Viewer'],
    permissions,
    organizations: [],
    activeOrganizationId: null,
    lastLoginAt: null,
  };
}

test('frontend capabilities use exact effective permission values from the API', () => {
  const viewer = user([accountPermissions.organizationRead]);
  assert.equal(hasAccountPermission(viewer, accountPermissions.organizationRead), true);
  assert.equal(hasAccountPermission(viewer, accountPermissions.organizationManage), false);
});

test('routing and sensitive Account actions are permission gated', async () => {
  const [main, shell, organization, developerAccess, closure] = await Promise.all([
    readFile(new URL('../src/main.ts', import.meta.url), 'utf8'),
    readFile(new URL('../src/shell.ts', import.meta.url), 'utf8'),
    readFile(new URL('../src/pages/organization.ts', import.meta.url), 'utf8'),
    readFile(new URL('../src/pages/developerAccess.ts', import.meta.url), 'utf8'),
    readFile(new URL('../src/pages/closure.ts', import.meta.url), 'utf8'),
  ]);

  assert.match(main, /hasAccountPermission\(state\.user, portalPage\.permission\)/);
  assert.match(main, /You do not have access to this page/);
  assert.match(shell, /\.filter\(\(item\) => !item\.permission \|\| hasAccountPermission/);
  assert.match(organization, /accountPermissions\.organizationManage/);
  assert.match(organization, /accountPermissions\.membersInvite/);
  assert.match(organization, /accountPermissions\.membersRemove/);
  assert.match(developerAccess, /accountPermissions\.serviceAccountsManage/);
  assert.match(closure, /accountPermissions\.closureRequest/);
});

test('every Account page with module state registers a context reset', async () => {
  const pagesDirectory = new URL('../src/pages/', import.meta.url);
  const files = (await readdir(pagesDirectory)).filter((name) => name.endsWith('.ts'));
  const statefulPages: string[] = [];

  for (const file of files) {
    const source = await readFile(new URL(file, pagesDirectory), 'utf8');
    if (!/const state:/.test(source)) continue;
    statefulPages.push(file);
    assert.match(source, /registerAccountStateReset\(/, `${file} must clear state on identity changes`);
  }

  assert.deepEqual(statefulPages.sort(), [
    'closure.ts',
    'dashboard.ts',
    'developerAccess.ts',
    'licenses.ts',
    'organization.ts',
    'profile.ts',
    'security.ts',
    'subscription.ts',
    'usage.ts',
  ]);
});
