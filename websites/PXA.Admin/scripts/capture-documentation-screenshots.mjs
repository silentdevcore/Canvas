import { mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import { chromium } from '../../../pxa-designer/node_modules/playwright/index.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const outputDirectory = resolve(root, '../../PXA.WebApi/AdminDocumentation/images');
const adminUrl = process.env.PXA_ADMIN_CAPTURE_URL || 'http://localhost:5177';
const now = '2026-07-25T10:30:00Z';
const organizationId = '10000000-0000-4000-8000-000000000001';
const userId = '20000000-0000-4000-8000-000000000001';
const memberId = '30000000-0000-4000-8000-000000000001';
const subscriptionId = '40000000-0000-4000-8000-000000000001';
const licenseId = '50000000-0000-4000-8000-000000000001';
const serviceAccountId = '60000000-0000-4000-8000-000000000001';
const auditId = '70000000-0000-4000-8000-000000000001';

const entitlements = [
  'generator', 'designer', 'migration', 'importer', 'pdf-viewer', 'spreadsheet', 'api', 'sdk',
].map((capability) => ({
  capability,
  enabled: true,
  limit: capability === 'generator' ? 250000 : null,
  unit: capability === 'generator' ? 'pages' : null,
  source: 'EditionDefault',
  expiresAt: null,
}));

const subscription = {
  id: subscriptionId,
  organizationId,
  organizationName: 'Northwind Labs',
  edition: 'Enterprise',
  accountType: 'Company',
  status: 'Active',
  billingPeriod: 'Annual',
  deploymentMode: 'Hybrid',
  seatLimit: 25,
  assignedSeats: 12,
  startsAt: '2026-01-01T00:00:00Z',
  currentPeriodStartsAt: '2026-01-01T00:00:00Z',
  currentPeriodEndsAt: '2027-01-01T00:00:00Z',
  trialEndsAt: null,
  cancellationEffectiveAt: null,
  gracePeriodEndsAt: null,
  entitlements,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: now,
};

const users = [
  syntheticUser(userId, memberId, 'Alex Morgan', 'a***@example.test', ['System Administrator', 'Organization Administrator']),
  syntheticUser('20000000-0000-4000-8000-000000000002', '30000000-0000-4000-8000-000000000002', 'Jamie Rivera', 'j***@example.test', ['Manager']),
  syntheticUser('20000000-0000-4000-8000-000000000003', '30000000-0000-4000-8000-000000000003', 'Taylor Chen', 't***@example.test', ['Editor']),
];

const permissions = [
  ['users.read', 'Identity', 'Read organization users.'],
  ['users.update', 'Identity', 'Update organization users.'],
  ['roles.assign', 'Identity', 'Assign organization roles.'],
  ['subscriptions.manage', 'Commercial', 'Manage subscriptions and seats.'],
  ['licenses.manage', 'Commercial', 'Issue and revoke offline licenses.'],
  ['audit.read', 'Operations', 'Read tenant audit events.'],
].map(([key, group, description]) => ({ key, group, description }));

const roles = [
  ['organization-administrator', 'Organization Administrator', 'Manage the organization and its members.', 2, permissions],
  ['manager', 'Manager', 'Coordinate users and operational workflows.', 4, permissions.slice(0, 2)],
  ['editor', 'Editor', 'Use entitled document products.', 7, []],
  ['viewer', 'Viewer', 'View shared organization resources.', 5, []],
].map(([key, name, description, memberCount, rolePermissions]) => ({
  key, name, description, isProtected: true, memberCount, permissions: rolePermissions,
}));

const auditEvents = [
  auditEvent(auditId, 'subscriptions.update', 'subscription', 'succeeded'),
  auditEvent('70000000-0000-4000-8000-000000000002', 'users.roles.update', 'user', 'succeeded'),
  auditEvent('70000000-0000-4000-8000-000000000003', 'api_keys.revoke', 'service-account', 'succeeded'),
];

await mkdir(outputDirectory, { recursive: true });
const browser = await chromium.launch({ headless: true });

try {
  const context = await browser.newContext({
    viewport: { width: 1440, height: 980 },
    deviceScaleFactor: 1,
    colorScheme: 'light',
  });
  await installSyntheticApi(context);
  const page = await context.newPage();

  const captures = [
    ['/dashboard', 'dashboard.png', 'Dashboard'],
    ['/users', 'users.png', 'Users'],
    [`/users/${userId}`, 'user-detail.png', 'Alex Morgan'],
    ['/organizations', 'organizations.png', 'Organizations'],
    ['/roles', 'roles.png', 'Roles & permissions'],
    ['/subscriptions', 'subscriptions.png', 'Subscriptions'],
    ['/licenses', 'licenses.png', 'Offline licenses'],
    ['/service-accounts', 'service-accounts.png', 'Service accounts'],
    ['/mail', 'mail.png', 'Mail delivery'],
    ['/audit', 'audit.png', 'Audit'],
  ];

  for (const [path, filename, heading] of captures) {
    await page.goto(`${adminUrl}${path}`, { waitUntil: 'networkidle' });
    await page.getByRole('heading', { name: heading, exact: true }).waitFor();
    await page.screenshot({ path: resolve(outputDirectory, filename), fullPage: false });
  }

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${adminUrl}/dashboard`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Dashboard', exact: true }).waitFor();
  await page.screenshot({ path: resolve(outputDirectory, 'dashboard-mobile.png'), fullPage: false });

  await context.close();
} finally {
  await browser.close();
}

async function installSyntheticApi(context) {
  await context.route('**/api/pxa/v1/**', async (route) => {
    const requestUrl = new URL(route.request().url());
    const path = requestUrl.pathname;
    const json = responseFor(path);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(json),
    });
  });
}

function responseFor(path) {
  if (path === '/api/pxa/v1/auth/me') {
    return {
      id: userId,
      username: 'alex.admin',
      email: 'a***@example.test',
      displayName: 'Alex Morgan',
      roles: ['System Administrator', 'Organization Administrator'],
      permissions: permissions.map((item) => item.key),
      organizations: [{ id: organizationId, name: 'Northwind Labs', slug: 'northwind-labs' }],
      activeOrganizationId: organizationId,
      lastLoginAt: now,
      apiVersion: '1',
    };
  }
  if (path === '/api/pxa/v1/admin/users') return pageOf(users);
  if (path === `/api/pxa/v1/admin/users/${userId}`) return users[0];
  if (path === `/api/pxa/v1/admin/users/${userId}/sessions`) {
    return [{
      id: '80000000-0000-4000-8000-000000000001',
      userAgent: 'PXA documentation browser',
      createdAt: now,
      lastSeenAt: now,
      expiresAt: '2026-08-24T10:30:00Z',
      revokedAt: null,
      revocationReason: null,
      isCurrent: true,
      isActive: true,
    }];
  }
  if (path === `/api/pxa/v1/admin/users/${userId}/audit`) return auditEvents.slice(0, 2);
  if (path === '/api/pxa/v1/admin/organizations') {
    return pageOf([
      { id: organizationId, name: 'Northwind Labs', slug: 'northwind-labs', status: 'Active', memberCount: 18, createdAt: '2026-01-01T00:00:00Z', updatedAt: now },
      { id: '10000000-0000-4000-8000-000000000002', name: 'Contoso Research', slug: 'contoso-research', status: 'Active', memberCount: 7, createdAt: '2026-02-01T00:00:00Z', updatedAt: now },
    ]);
  }
  if (path === '/api/pxa/v1/admin/roles') return { roles, permissions };
  if (path === '/api/pxa/v1/admin/subscriptions') return pageOf([subscription]);
  if (path === `/api/pxa/v1/admin/subscriptions/${subscriptionId}/seats`) {
    return users.map((user, index) => ({
      membershipId: user.membershipId,
      userId: user.id,
      displayName: user.displayName,
      email: user.email,
      membershipStatus: 'Active',
      assigned: index < 2,
    }));
  }
  if (path === '/api/pxa/v1/admin/licenses') {
    return [{
      id: licenseId,
      licenseNumber: 'PXA-ENT-2026-0042',
      organizationId,
      organizationName: 'Northwind Labs',
      edition: 'Enterprise',
      deploymentMode: 'OnPremise',
      status: 'Active',
      validFrom: '2026-01-01T00:00:00Z',
      validUntil: '2027-01-01T00:00:00Z',
      instanceLimit: 3,
      keyId: 'production-key',
      algorithm: 'ECDSA_P256_SHA256',
      issuedAt: '2026-01-01T00:00:00Z',
      revokedAt: null,
      revocationReason: null,
    }];
  }
  if (path === '/api/pxa/v1/admin/service-accounts') {
    return [{
      id: serviceAccountId,
      name: 'Document pipeline',
      isActive: true,
      createdAt: '2026-04-10T09:00:00Z',
      revokedAt: null,
      keys: [{
        id: '60000000-0000-4000-8000-000000000002',
        serviceAccountId,
        name: 'Production worker',
        prefix: 'pxa_live_****',
        expiresAt: '2026-12-31T00:00:00Z',
        lastUsedAt: now,
        createdAt: '2026-04-10T09:05:00Z',
        revokedAt: null,
      }],
    }];
  }
  if (path === '/api/pxa/v1/admin/mail/status') {
    return { transport: 'Synthetic provider', deliveryEnabled: true, pending: 2, failed: 1, deadLetter: 0 };
  }
  if (path === '/api/pxa/v1/admin/mail') {
    return pageOf([
      { id: '90000000-0000-4000-8000-000000000001', recipientEmail: 'j***@example.test', templateKey: 'user.invitation', status: 'Delivered', attempts: 1, providerMessageId: null, failureReason: null, createdAt: now, deliveredAt: now },
      { id: '90000000-0000-4000-8000-000000000002', recipientEmail: 't***@example.test', templateKey: 'security.organization-changed', status: 'Pending', attempts: 0, providerMessageId: null, failureReason: null, createdAt: now, deliveredAt: null },
    ]);
  }
  if (path === '/api/pxa/v1/admin/audit') {
    return { ...pageOf(auditEvents), actions: ['subscriptions.update', 'users.roles.update', 'api_keys.revoke'], targetTypes: ['subscription', 'user', 'service-account'], outcomes: ['succeeded'], canExport: true };
  }
  if (path.startsWith('/api/pxa/v1/admin/subscriptions/')) return subscription;
  if (path.startsWith('/api/pxa/v1/admin/audit/')) return auditEvents[0];
  if (path === '/api/pxa/v1/auth/csrf') return { token: 'synthetic-csrf-token' };
  return [];
}

function pageOf(items) {
  return { items, page: 1, pageSize: 25, total: items.length };
}

function syntheticUser(id, membershipId, displayName, email, userRoles) {
  return {
    id,
    membershipId,
    displayName,
    email,
    username: displayName.toLowerCase().replace(' ', '.'),
    pendingEmail: null,
    isActive: true,
    membershipStatus: 'Active',
    roles: userRoles,
    lastLoginAt: now,
    createdAt: '2026-01-10T09:00:00Z',
  };
}

function auditEvent(id, action, targetType, outcome) {
  return {
    id,
    action,
    targetType,
    targetId: 'masked',
    outcome,
    details: { source: 'synthetic documentation fixture' },
    actorUserId: null,
    actorName: 'Alex Morgan',
    actorEmail: 'a***@example.test',
    createdAt: now,
  };
}
