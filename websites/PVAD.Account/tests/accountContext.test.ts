import assert from 'node:assert/strict';
import test from 'node:test';
import {
  clearAccountContext,
  registerAccountStateReset,
  updateAccountContext,
} from '../src/accountContext.ts';
import type { UserInfo } from '../src/api.ts';

function user(id: string, organizationId: string): UserInfo {
  return {
    id,
    username: `${id}@pxa.test`,
    email: `${id}@pxa.test`,
    displayName: id,
    roles: [],
    permissions: [],
    organizations: [{ id: organizationId, name: organizationId, slug: organizationId }],
    activeOrganizationId: organizationId,
    lastLoginAt: null,
  };
}

test('tenant-scoped state resets across organization changes and user A to user B login', () => {
  let resetCount = 0;
  const unregister = registerAccountStateReset(() => { resetCount += 1; });

  updateAccountContext(user('user-a', 'organization-a'));
  assert.equal(resetCount, 0, 'initial bootstrap has no stale state to clear');

  updateAccountContext(user('user-a', 'organization-a'));
  assert.equal(resetCount, 0, 'rerendering the same identity context keeps its cache');

  updateAccountContext(user('user-a', 'organization-b'));
  assert.equal(resetCount, 1, 'organization switching clears tenant-scoped data');

  clearAccountContext();
  assert.equal(resetCount, 2, 'logout clears the active identity cache');

  updateAccountContext(user('user-b', 'organization-b'));
  assert.equal(resetCount, 2, 'user B starts from the cache cleared during user A logout');

  updateAccountContext(user('user-c', 'organization-b'));
  assert.equal(resetCount, 3, 'a direct identity replacement also clears the previous user cache');

  clearAccountContext();
  assert.equal(resetCount, 4);
  clearAccountContext();
  assert.equal(resetCount, 4, 'repeated logout is idempotent');

  unregister();
});
