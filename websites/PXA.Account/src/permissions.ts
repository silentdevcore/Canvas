import type { UserInfo } from './api';

export const accountPermissions = {
  profileManage: 'account.profile.manage',
  organizationRead: 'account.organization.read',
  organizationManage: 'account.organization.manage',
  membersRead: 'account.members.read',
  membersInvite: 'account.members.invite',
  membersRemove: 'account.members.remove',
  subscriptionRead: 'account.subscription.read',
  licensesRead: 'account.licenses.read',
  serviceAccountsRead: 'account.serviceaccounts.read',
  serviceAccountsManage: 'account.serviceaccounts.manage',
  sessionsManage: 'account.sessions.manage',
  closureRequest: 'account.closure.request',
} as const;

export type AccountPermission = typeof accountPermissions[keyof typeof accountPermissions];

export function hasAccountPermission(user: UserInfo, permission: AccountPermission): boolean {
  return user.permissions?.includes(permission) ?? false;
}
