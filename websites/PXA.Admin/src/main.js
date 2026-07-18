import './site.css';
import {
  currentUser,
  acceptInvitation,
  addAdminOrganizationMember,
  assignAdminSubscriptionSeat,
  assignAdminRoleMember,
  cancelAdminSubscription,
  createAdminOrganization,
  createAdminSubscription,
  createAdminServiceAccount,
  createAdminApiKey,
  createAdminInvitation,
  cancelAdminMail,
  getAdminOrganization,
  getAdminOrganizationMembers,
  getAdminOrganizations,
  getAdminSubscriptions,
  getAdminSubscription,
  getAdminSubscriptionHistory,
  getAdminSubscriptionSeats,
  getAdminSubscriptionUsage,
  getAdminLicenses,
  getAdminServiceAccounts,
  getAdminMail,
  getAdminMailStatus,
  getAdminUser,
  getAdminUserSessions,
  getAdminUsers,
  login,
  logout,
  revokeAdminUserSession,
  revokeAllAdminUserSessions,
  issueAdminLicense,
  removeAdminOrganizationMember,
  requestPasswordReset,
  renewAdminSubscription,
  revokeAdminSubscriptionSeat,
  revokeAdminLicense,
  revokeAdminApiKey,
  revokeAdminServiceAccount,
  retryAdminMail,
  confirmPasswordReset,
  switchOrganization,
  startAdminGracePeriod,
  updateAdminOrganization,
  updateAdminSubscription,
  updateAdminUserRoles,
  updateAdminUserStatus,
  extendAdminTrial,
  exportAdminAudit,
  getAdminAudit,
  getAdminAuditEvent,
  getAdminRole,
  getAdminRoles,
  revokeAdminRoleMember,
  validateAdminLicense,
} from './api.js';

const app = document.querySelector('#app');

const navigation = [
  { path: '/dashboard', label: 'Dashboard', group: 'Overview' },
  { path: '/users', label: 'Users', group: 'Identity' },
  { path: '/organizations', label: 'Organizations', group: 'Identity' },
  { path: '/roles', label: 'Roles & permissions', group: 'Identity' },
  { path: '/subscriptions', label: 'Subscriptions', group: 'Commercial' },
  { path: '/licenses', label: 'Licenses', group: 'Commercial' },
  { path: '/service-accounts', label: 'Service accounts', group: 'Access' },
  { path: '/mail', label: 'Mail delivery', group: 'Operations' },
  { path: '/audit', label: 'Audit', group: 'Operations' },
  { path: '/settings', label: 'Settings', group: 'Operations' },
];

const pageDetails = {
  '/users': ['Users', 'Manage user status, memberships, roles, and active sessions.', ['Name', 'Email', 'Organization', 'Role', 'Status', 'Last login']],
  '/organizations': ['Organizations', 'Manage tenant ownership, memberships, and organization status.', ['Organization', 'Status', 'Members', 'Subscription', 'Updated']],
  '/mail': ['Mail delivery', 'Inspect transactional delivery state without exposing message secrets.', ['Recipient', 'Template', 'State', 'Attempts', 'Updated']],
  '/settings': ['Settings', 'Configure organization defaults and operational administration settings.', ['Setting', 'Value', 'Scope', 'Updated']],
};

const state = {
  user: null,
  loading: true,
  notice: null,
  users: {
    items: [],
    total: 0,
    page: 1,
    pageSize: 25,
    search: '',
    status: '',
    loading: false,
    loaded: false,
    error: null,
    notice: null,
  },
  userDetail: {
    id: null,
    data: null,
    sessions: [],
    loading: false,
    error: null,
    saving: false,
  },
  organizations: {
    items: [], total: 0, page: 1, pageSize: 25, search: '', status: '',
    loading: false, loaded: false, error: null,
  },
  organizationDetail: {
    id: null, data: null, members: [], loading: false, error: null, saving: false,
  },
  mail: {
    items: [], total: 0, page: 1, pageSize: 25, status: '', summary: null,
    loading: false, loaded: false, saving: false, error: null,
  },
  subscriptions: {
    items: [], total: 0, page: 1, pageSize: 25, status: '', edition: '',
    loading: false, loaded: false, saving: false, error: null,
  },
  subscriptionDetail: {
    id: null, data: null, seats: [], history: [], usage: null, loading: false, saving: false, error: null,
  },
  licenses: { items: [], loading: false, loaded: false, saving: false, error: null, notice: null },
  serviceAccounts: { items: [], loading: false, loaded: false, saving: false, error: null, secret: null },
  audit: {
    items: [], total: 0, page: 1, pageSize: 25, search: '', action: '', targetType: '', outcome: '',
    from: '', to: '', direction: 'desc', actions: [], targetTypes: [], outcomes: [], canExport: false,
    selected: null, loading: false, loaded: false, detailLoading: false, exporting: false, error: null,
  },
  roles: { items: [], permissions: [], loading: false, loaded: false, error: null },
  roleDetail: {
    key: null, data: null, users: [], page: 1, pageSize: 25, loading: false, saving: false, error: null,
  },
};

const organizationRoles = ['Organization Administrator', 'Manager', 'Editor', 'Viewer'];
const subscriptionCapabilities = [
  ['generator', 'Generator'], ['designer', 'Designer'], ['migration', 'Migration'],
  ['importer', 'Importer'], ['pdf-viewer', 'PDF Viewer'], ['spreadsheet', 'Spreadsheet'],
  ['api', 'API'], ['sdk', 'SDK'],
];
const subscriptionTransitions = {
  Pending: ['Trialing', 'Active', 'Cancelled'],
  Trialing: ['Active', 'Suspended', 'Cancelled', 'Expired'],
  Active: ['PastDue', 'Suspended', 'Cancelled'],
  PastDue: ['Active', 'GracePeriod', 'Suspended', 'Cancelled'],
  GracePeriod: ['Active', 'Suspended', 'Cancelled', 'Expired'],
  Suspended: ['Active', 'Cancelled', 'Expired'],
  Cancelled: ['Expired'],
  Expired: [],
};

function escapeHtml(value = '') {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function dateInputValue(value) {
  if (!value) return '';
  const date = new Date(value);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return local.toISOString().slice(0, 16);
}

function navigate(path, replace = false) {
  if (replace) history.replaceState({}, '', path);
  else history.pushState({}, '', path);
  render();
}

function isAdministrator(user) {
  return user?.roles?.some((role) =>
    role === 'System Administrator' || role === 'Organization Administrator');
}

function isSystemAdministrator(user = state.user) {
  return user?.roles?.includes('System Administrator');
}

function renderLogin() {
  document.title = 'Sign in | PXA Admin';
  app.innerHTML = `
    <main class="admin-login-shell">
      <section class="admin-login-brand" aria-label="Power Dox Automation">
        <div class="admin-brand-mark">PXA</div>
        <p class="pxa-kicker">Power Dox Automation</p>
        <h1>Administration</h1>
        <p>Secure access for authorized PXA operators and organization administrators.</p>
        <dl class="admin-login-context">
          <div><dt>Identity</dt><dd>Persistent PXA accounts</dd></div>
          <div><dt>Scope</dt><dd>Organization isolated</dd></div>
          <div><dt>Session</dt><dd>Protected browser cookie</dd></div>
        </dl>
      </section>
      <section class="admin-login-form-wrap">
        <form class="admin-login-form" id="login-form" novalidate>
          <div>
            <p class="pxa-kicker">Authorized access</p>
            <h2>Sign in to PXA Admin</h2>
            <p class="admin-form-copy">Use your verified administrator account.</p>
          </div>
          <div class="admin-alert admin-alert--error" id="login-error" role="alert" hidden></div>
          <label class="admin-field">
            <span>Email or username</span>
            <input name="identifier" type="text" autocomplete="username" required autofocus>
          </label>
          <label class="admin-field">
            <span>Password</span>
            <input name="password" type="password" autocomplete="current-password" required>
          </label>
          <label class="admin-checkbox">
            <input name="rememberMe" type="checkbox">
            <span>Keep me signed in on this device</span>
          </label>
          <button class="pxa-button pxa-button--primary admin-submit" type="submit">Sign in</button>
          <p class="admin-login-help"><a href="/forgot-password">Forgot your password?</a></p>
        </form>
      </section>
    </main>
  `;

  document.querySelector('#login-form').addEventListener('submit', handleLogin);
}

function publicActionPage(kind) {
  const token = new URLSearchParams(location.search).get('token') || '';
  const invitation = kind === 'invitation';
  const resetRequest = kind === 'request-reset';
  const title = invitation ? 'Accept invitation' : resetRequest ? 'Reset your password' : 'Choose a new password';
  return `
    <main class="admin-public-shell">
      <form class="admin-login-form admin-public-form" id="public-action-form">
        <div class="admin-brand-mark">PXA</div>
        <div><p class="pxa-kicker">Power Dox Automation</p><h1>${title}</h1></div>
        <div class="admin-alert admin-alert--error" id="public-action-error" hidden></div>
        <div class="admin-alert admin-alert--success" id="public-action-success" hidden></div>
        ${resetRequest ? `
          <label class="admin-field"><span>Email</span><input name="email" type="email" autocomplete="email" required></label>
        ` : `
          ${invitation ? '<label class="admin-field"><span>Display name</span><input name="displayName" autocomplete="name"></label>' : ''}
          <label class="admin-field"><span>New password</span><input name="password" type="password" autocomplete="new-password" minlength="12" required></label>
          <input name="token" type="hidden" value="${escapeHtml(token)}">
        `}
        <button class="pxa-button pxa-button--primary" type="submit">${resetRequest ? 'Send reset link' : invitation ? 'Activate account' : 'Change password'}</button>
        <a class="admin-back-link" href="/login">Return to sign in</a>
      </form>
    </main>`;
}

function bindPublicAction(kind) {
  document.querySelector('#public-action-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const button = form.querySelector('button');
    const error = document.querySelector('#public-action-error');
    const success = document.querySelector('#public-action-success');
    error.hidden = true;
    success.hidden = true;
    button.disabled = true;
    try {
      if (kind === 'request-reset') {
        await requestPasswordReset(data.get('email'));
        success.textContent = 'If the account exists, a password-reset message has been queued.';
      } else if (kind === 'invitation') {
        await acceptInvitation(data.get('token'), data.get('password'), data.get('displayName'));
        success.textContent = 'Your account is active. You can now sign in.';
      } else {
        await confirmPasswordReset(data.get('token'), data.get('password'));
        success.textContent = 'Your password was changed. You can now sign in.';
      }
      success.hidden = false;
      form.querySelectorAll('input').forEach((input) => { if (input.type !== 'hidden') input.disabled = true; });
    } catch (requestError) {
      error.textContent = requestError.message;
      error.hidden = false;
      button.disabled = false;
    }
  });
}

async function handleLogin(event) {
  event.preventDefault();
  const form = event.currentTarget;
  const button = form.querySelector('button[type="submit"]');
  const error = document.querySelector('#login-error');
  const data = new FormData(form);

  error.hidden = true;
  button.disabled = true;
  button.textContent = 'Signing in...';

  try {
    const response = await login(
      data.get('identifier'),
      data.get('password'),
      data.get('rememberMe') === 'on');
    state.user = response.user;
    navigate('/dashboard', true);
  } catch (requestError) {
    error.textContent = requestError.message;
    error.hidden = false;
    button.disabled = false;
    button.textContent = 'Sign in';
  }
}

function renderNavigation() {
  const groups = [...new Set(navigation.map((item) => item.group))];
  return groups.map((group) => `
    <div class="admin-nav-group">
      <span>${group}</span>
      ${navigation.filter((item) => item.group === group).map((item) => `
        <a href="${item.path}" ${location.pathname === item.path || location.pathname.startsWith(`${item.path}/`) ? 'aria-current="page"' : ''}>
          ${item.label}
        </a>
      `).join('')}
    </div>
  `).join('');
}

function renderShell(content, title) {
  const activeOrganization = state.user.organizations?.find(
    (organization) => organization.id === state.user.activeOrganizationId) || state.user.organizations?.[0];

  document.title = `${title} | PXA Admin`;
  app.innerHTML = `
    <div class="admin-app-shell">
      <aside class="admin-sidebar" id="admin-sidebar">
        <div class="admin-sidebar-brand">
          <span class="admin-brand-mark">PXA</span>
          <span><strong>Power Dox Automation</strong><small>Administration</small></span>
        </div>
        <nav class="admin-navigation" aria-label="Administration">${renderNavigation()}</nav>
        <div class="admin-sidebar-footer">
          <span>Signed in as</span>
          <strong>${escapeHtml(state.user.displayName)}</strong>
          <small>${escapeHtml(state.user.email)}</small>
        </div>
      </aside>
      <div class="admin-workspace">
        <header class="admin-topbar">
          <button class="admin-menu-button" id="menu-button" type="button" aria-controls="admin-sidebar" aria-expanded="false">Menu</button>
          <div class="admin-org-context">
            <span>Organization</span>
            <strong>${escapeHtml(activeOrganization?.name || 'System scope')}</strong>
          </div>
          <button class="admin-signout" id="signout-button" type="button">Sign out</button>
        </header>
        <main class="admin-content">${content}</main>
      </div>
    </div>
  `;

  bindShellEvents();
}

function dashboardPage() {
  const organizations = state.user.organizations?.length || 0;
  return `
    <header class="admin-page-header">
      <div><p class="pxa-kicker">Overview</p><h1>Dashboard</h1></div>
      <span class="admin-status admin-status--active">Session active</span>
    </header>
    <section class="admin-metrics" aria-label="Account overview">
      <article><span>Organizations</span><strong>${organizations}</strong><small>Available to this session</small></article>
      <article><span>Assigned roles</span><strong>${state.user.roles.length}</strong><small>${escapeHtml(state.user.roles.join(', '))}</small></article>
      <article><span>Last login</span><strong>${state.user.lastLoginAt ? new Date(state.user.lastLoginAt).toLocaleDateString() : 'First'}</strong><small>Security-stamp protected</small></article>
    </section>
    <section class="admin-section">
      <div class="admin-section-heading">
        <div><h2>Administration status</h2><p>Backend foundations currently available to this interface.</p></div>
      </div>
      <div class="admin-status-list">
        <div><strong>Persistent identity</strong><span class="admin-status admin-status--ready">Ready</span><p>PostgreSQL-backed users, roles, and claims.</p></div>
        <div><strong>Organization context</strong><span class="admin-status admin-status--ready">Ready</span><p>Active memberships are carried in the authenticated session.</p></div>
        <div><strong>User administration API</strong><span class="admin-status admin-status--ready">Ready</span><p>Tenant-scoped search, status, roles, and audit-protected mutations.</p></div>
        <div><strong>Mail and recovery</strong><span class="admin-status admin-status--planned">Planned</span><p>Password recovery depends on the transactional mail outbox.</p></div>
      </div>
    </section>
  `;
}

function formatDate(value) {
  if (!value) return 'Never';
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function userStatus(user) {
  if (!user.isActive) return 'Disabled';
  return user.membershipStatus === 'Active' ? 'Active' : user.membershipStatus;
}

function userStatusClass(user) {
  return user.isActive && user.membershipStatus === 'Active' ? 'admin-status--ready' : 'admin-status--inactive';
}

function usersPage() {
  const users = state.users;
  const totalPages = Math.max(1, Math.ceil(users.total / users.pageSize));
  const rows = users.items.map((user) => `
    <tr>
      <td><a class="admin-user-link" href="/users/${user.id}"><strong>${escapeHtml(user.displayName)}</strong><small>${escapeHtml(user.username)}</small></a></td>
      <td>${escapeHtml(user.email)}</td>
      <td><div class="admin-role-list">${user.roles.length ? user.roles.map((role) => `<span>${escapeHtml(role)}</span>`).join('') : '<span>Unassigned</span>'}</div></td>
      <td><span class="admin-status ${userStatusClass(user)}">${escapeHtml(userStatus(user))}</span></td>
      <td>${escapeHtml(formatDate(user.lastLoginAt))}</td>
    </tr>
  `).join('');

  return `
    <header class="admin-page-header">
      <div><p class="pxa-kicker">Identity</p><h1>Users</h1><p>Manage user status, organization roles, and access within the active tenant.</p></div>
      <span class="admin-record-count">${users.total} ${users.total === 1 ? 'user' : 'users'}</span>
    </header>
    <details class="admin-section admin-create-panel">
      <summary>Invite user</summary>
      <form class="admin-invitation-form" id="user-invitation-form">
        <label class="admin-field"><span>Display name</span><input name="displayName" required maxlength="200"></label>
        <label class="admin-field"><span>Email</span><input name="email" type="email" required></label>
        <label class="admin-field"><span>Initial role</span><select name="role">${organizationRoles.map((role) => `<option>${role}</option>`).join('')}</select></label>
        <button class="pxa-button pxa-button--primary" type="submit">Send invitation</button>
      </form>
    </details>
    ${users.notice ? `<div class="admin-alert admin-alert--success admin-detail-alert">${escapeHtml(users.notice)}</div>` : ''}
    <section class="admin-table-section" aria-busy="${users.loading}">
      <form class="admin-table-toolbar" id="users-filter-form">
        <label><span class="visually-hidden">Search users</span><input name="search" type="search" placeholder="Search name, email, or username" value="${escapeHtml(users.search)}"></label>
        <select name="status" aria-label="Filter by status">
          <option value="" ${users.status === '' ? 'selected' : ''}>All statuses</option>
          <option value="active" ${users.status === 'active' ? 'selected' : ''}>Active</option>
          <option value="disabled" ${users.status === 'disabled' ? 'selected' : ''}>Disabled</option>
          <option value="suspended" ${users.status === 'suspended' ? 'selected' : ''}>Suspended</option>
          <option value="invited" ${users.status === 'invited' ? 'selected' : ''}>Invited</option>
        </select>
        <button type="submit">Apply</button>
      </form>
      ${users.error ? `<div class="admin-alert admin-alert--error admin-inline-alert" role="alert">${escapeHtml(users.error)}</div>` : ''}
      ${users.loading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading users...</p></div>' : `
        <div class="admin-table-scroll">
          <table>
            <thead><tr><th>User</th><th>Email</th><th>Roles</th><th>Status</th><th>Last login</th></tr></thead>
            <tbody>${rows}</tbody>
          </table>
        </div>
        ${users.items.length === 0 ? '<div class="admin-empty-state"><strong>No users found</strong><p>Change the search or status filter to broaden the result.</p></div>' : ''}
        <footer class="admin-pagination">
          <span>Page ${users.page} of ${totalPages}</span>
          <div>
            <button id="users-previous" type="button" ${users.page <= 1 ? 'disabled' : ''}>Previous</button>
            <button id="users-next" type="button" ${users.page >= totalPages ? 'disabled' : ''}>Next</button>
          </div>
        </footer>
      `}
    </section>
  `;
}

function userDetailPage() {
  const detail = state.userDetail;
  if (detail.loading) {
    return '<section class="admin-message-page"><div class="admin-spinner"></div><p>Loading user...</p></section>';
  }
  if (!detail.data) {
    return `<section class="admin-message-page"><span class="admin-error-code">!</span><h1>User unavailable</h1><p>${escapeHtml(detail.error || 'The user could not be loaded.')}</p><a class="pxa-button pxa-button--secondary" href="/users">Return to users</a></section>`;
  }

  const user = detail.data;
  const globalRoles = user.roles.filter((role) => !organizationRoles.includes(role));
  const activeSessions = detail.sessions.filter((session) => session.isActive);
  const sessionRows = detail.sessions.map((session) => `
    <tr>
      <td><strong>${escapeHtml(session.userAgent)}</strong>${session.isCurrent ? '<small>Current session</small>' : ''}</td>
      <td>${escapeHtml(formatDate(session.lastSeenAt))}</td>
      <td>${escapeHtml(formatDate(session.expiresAt))}</td>
      <td><span class="admin-status ${session.isActive ? 'admin-status--ready' : 'admin-status--inactive'}">${session.isActive ? 'Active' : 'Ended'}</span></td>
      <td>${session.isActive && !session.isCurrent ? `<button class="admin-text-button admin-session-revoke" data-session-id="${session.id}" type="button" ${detail.saving ? 'disabled' : ''}>Revoke</button>` : ''}</td>
    </tr>`).join('');
  return `
    <header class="admin-page-header">
      <div><a class="admin-back-link" href="/users">Users</a><h1>${escapeHtml(user.displayName)}</h1><p>${escapeHtml(user.email)}</p></div>
      <span class="admin-status ${userStatusClass(user)}">${escapeHtml(userStatus(user))}</span>
    </header>
    ${detail.error ? `<div class="admin-alert admin-alert--error admin-detail-alert" role="alert">${escapeHtml(detail.error)}</div>` : ''}
    <div class="admin-detail-grid">
      <section class="admin-section">
        <div class="admin-section-heading"><h2>Account</h2><p>Identity and access state for this organization.</p></div>
        <dl class="admin-detail-list">
          <div><dt>Username</dt><dd>${escapeHtml(user.username)}</dd></div>
          <div><dt>Created</dt><dd>${escapeHtml(formatDate(user.createdAt))}</dd></div>
          <div><dt>Last login</dt><dd>${escapeHtml(formatDate(user.lastLoginAt))}</dd></div>
          <div><dt>Membership</dt><dd>${escapeHtml(user.membershipStatus)}</dd></div>
        </dl>
        <div class="admin-detail-action">
          <div><strong>${user.isActive ? 'Disable account' : 'Enable account'}</strong><p>${user.isActive ? 'Revoke this user\'s active access and sessions.' : 'Restore access to this organization.'}</p></div>
          <button class="pxa-button ${user.isActive ? 'admin-danger-button' : 'pxa-button--primary'}" id="user-status-button" type="button" ${detail.saving ? 'disabled' : ''}>${user.isActive ? 'Disable' : 'Enable'}</button>
        </div>
      </section>
      <section class="admin-section">
        <form id="user-roles-form">
          <div class="admin-section-heading"><h2>Organization roles</h2><p>Roles apply only inside the active organization.</p></div>
          <div class="admin-role-options">
            ${organizationRoles.map((role) => `<label><input type="checkbox" name="roles" value="${role}" ${user.roles.includes(role) ? 'checked' : ''}><span><strong>${role}</strong></span></label>`).join('')}
          </div>
          ${globalRoles.length ? `<div class="admin-global-roles"><span>System roles</span><strong>${globalRoles.map(escapeHtml).join(', ')}</strong></div>` : ''}
          <div class="admin-form-actions"><button class="pxa-button pxa-button--primary" type="submit" ${detail.saving ? 'disabled' : ''}>Save roles</button></div>
        </form>
      </section>
    </div>
    <section class="admin-table-section admin-members-section">
      <div class="admin-section-heading admin-section-heading--action">
        <div><h2>Active sessions</h2><p>Server-validated browser sessions for this user in the active organization.</p></div>
        <button class="pxa-button pxa-button--secondary" id="user-sessions-revoke-all" type="button" ${detail.saving || activeSessions.filter((session) => !session.isCurrent).length === 0 ? 'disabled' : ''}>Revoke ${user.id === state.user?.id ? 'other sessions' : 'all sessions'}</button>
      </div>
      <div class="admin-table-scroll"><table><thead><tr><th>Client</th><th>Last seen</th><th>Expires</th><th>Status</th><th></th></tr></thead><tbody>${sessionRows}</tbody></table></div>
      ${sessionRows ? '' : '<div class="admin-empty-state"><strong>No sessions recorded</strong><p>This user has not established a persistent browser session.</p></div>'}
    </section>
  `;
}

function organizationsPage() {
  const organizations = state.organizations;
  const totalPages = Math.max(1, Math.ceil(organizations.total / organizations.pageSize));
  const rows = organizations.items.map((organization) => `
    <tr>
      <td><a class="admin-user-link" href="/organizations/${organization.id}"><strong>${escapeHtml(organization.name)}</strong><small>${escapeHtml(organization.slug)}</small></a></td>
      <td><span class="admin-status ${organization.status === 'Active' ? 'admin-status--ready' : 'admin-status--inactive'}">${escapeHtml(organization.status)}</span></td>
      <td>${organization.memberCount}</td>
      <td>${escapeHtml(formatDate(organization.updatedAt))}</td>
    </tr>`).join('');

  return `
    <header class="admin-page-header">
      <div><p class="pxa-kicker">Identity</p><h1>Organizations</h1><p>Manage tenant ownership, membership, status, and active administration context.</p></div>
      <span class="admin-record-count">${organizations.total} ${organizations.total === 1 ? 'organization' : 'organizations'}</span>
    </header>
    ${isSystemAdministrator() ? `
      <details class="admin-section admin-create-panel">
        <summary>Create organization</summary>
        <form class="admin-inline-form" id="organization-create-form">
          <label class="admin-field"><span>Name</span><input name="name" required maxlength="200"></label>
          <label class="admin-field"><span>Slug</span><input name="slug" required pattern="[a-z0-9]+(?:-[a-z0-9]+)*" placeholder="customer-name"></label>
          <button class="pxa-button pxa-button--primary" type="submit">Create</button>
        </form>
      </details>` : ''}
    <section class="admin-table-section" aria-busy="${organizations.loading}">
      <form class="admin-table-toolbar" id="organizations-filter-form">
        <label><span class="visually-hidden">Search organizations</span><input name="search" type="search" placeholder="Search name or slug" value="${escapeHtml(organizations.search)}"></label>
        <select name="status" aria-label="Filter by status">
          <option value="">All statuses</option>
          ${['Active', 'Suspended', 'Closed'].map((status) => `<option value="${status}" ${organizations.status === status ? 'selected' : ''}>${status}</option>`).join('')}
        </select>
        <button type="submit">Apply</button>
      </form>
      ${organizations.error ? `<div class="admin-alert admin-alert--error admin-inline-alert">${escapeHtml(organizations.error)}</div>` : ''}
      ${organizations.loading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading organizations...</p></div>' : `
        <div class="admin-table-scroll"><table>
          <thead><tr><th>Organization</th><th>Status</th><th>Members</th><th>Updated</th></tr></thead>
          <tbody>${rows}</tbody>
        </table></div>
        ${organizations.items.length ? '' : '<div class="admin-empty-state"><strong>No organizations found</strong><p>Change the search or status filter.</p></div>'}
        <footer class="admin-pagination"><span>Page ${organizations.page} of ${totalPages}</span><div>
          <button id="organizations-previous" type="button" ${organizations.page <= 1 ? 'disabled' : ''}>Previous</button>
          <button id="organizations-next" type="button" ${organizations.page >= totalPages ? 'disabled' : ''}>Next</button>
        </div></footer>`}
    </section>`;
}

function organizationDetailPage() {
  const detail = state.organizationDetail;
  if (detail.loading) return '<section class="admin-message-page"><div class="admin-spinner"></div><p>Loading organization...</p></section>';
  if (!detail.data) return `<section class="admin-message-page"><span class="admin-error-code">!</span><h1>Organization unavailable</h1><p>${escapeHtml(detail.error || 'The organization could not be loaded.')}</p><a class="pxa-button pxa-button--secondary" href="/organizations">Return to organizations</a></section>`;
  const organization = detail.data;
  const active = state.user.activeOrganizationId === organization.id;
  const memberRows = detail.members.map((member) => `
    <tr>
      <td><a class="admin-user-link" href="/users/${member.userId}"><strong>${escapeHtml(member.displayName)}</strong><small>${escapeHtml(member.email)}</small></a></td>
      <td><div class="admin-role-list">${member.roles.length ? member.roles.map((role) => `<span>${escapeHtml(role)}</span>`).join('') : '<span>Unassigned</span>'}</div></td>
      <td><span class="admin-status ${member.membershipStatus === 'Active' && member.isActive ? 'admin-status--ready' : 'admin-status--inactive'}">${escapeHtml(member.membershipStatus)}</span></td>
      <td><button class="admin-text-danger organization-remove-member" type="button" data-user-id="${member.userId}" data-user-name="${escapeHtml(member.displayName)}" ${detail.saving ? 'disabled' : ''}>Remove</button></td>
    </tr>`).join('');

  return `
    <header class="admin-page-header">
      <div><a class="admin-back-link" href="/organizations">Organizations</a><h1>${escapeHtml(organization.name)}</h1><p>${escapeHtml(organization.slug)}</p></div>
      <div class="admin-header-actions">
        <span class="admin-status ${organization.status === 'Active' ? 'admin-status--ready' : 'admin-status--inactive'}">${escapeHtml(organization.status)}</span>
        <button class="pxa-button pxa-button--secondary" id="organization-switch-button" type="button" ${active || organization.status !== 'Active' || detail.saving ? 'disabled' : ''}>${active ? 'Current organization' : 'Work in organization'}</button>
      </div>
    </header>
    ${detail.error ? `<div class="admin-alert admin-alert--error admin-detail-alert">${escapeHtml(detail.error)}</div>` : ''}
    <div class="admin-detail-grid">
      <section class="admin-section">
        <form id="organization-edit-form">
          <div class="admin-section-heading"><h2>Organization details</h2><p>Update tenant identity and operational state.</p></div>
          <div class="admin-form-stack">
            <label class="admin-field"><span>Name</span><input name="name" value="${escapeHtml(organization.name)}" required maxlength="200"></label>
            ${isSystemAdministrator() ? `<label class="admin-field"><span>Status</span><select name="status">${['Active', 'Suspended', 'Closed'].map((status) => `<option ${organization.status === status ? 'selected' : ''}>${status}</option>`).join('')}</select></label>` : ''}
            <dl class="admin-detail-list admin-detail-list--compact"><div><dt>Created</dt><dd>${escapeHtml(formatDate(organization.createdAt))}</dd></div><div><dt>Members</dt><dd>${organization.memberCount}</dd></div></dl>
          </div>
          <div class="admin-form-actions"><button class="pxa-button pxa-button--primary" type="submit" ${detail.saving ? 'disabled' : ''}>Save details</button></div>
        </form>
      </section>
      <section class="admin-section">
        <form id="organization-add-member-form">
          <div class="admin-section-heading"><h2>Add existing user</h2><p>Attach a verified PXA account to this organization.</p></div>
          <div class="admin-form-stack">
            <label class="admin-field"><span>Email</span><input name="email" type="email" required></label>
            <label class="admin-field"><span>Initial role</span><select name="role">${organizationRoles.map((role) => `<option>${role}</option>`).join('')}</select></label>
          </div>
          <div class="admin-form-actions"><button class="pxa-button pxa-button--primary" type="submit" ${detail.saving ? 'disabled' : ''}>Add member</button></div>
        </form>
      </section>
    </div>
    <section class="admin-table-section admin-members-section">
      <div class="admin-section-heading"><h2>Members</h2><p>Active and suspended memberships in this tenant.</p></div>
      <div class="admin-table-scroll"><table><thead><tr><th>User</th><th>Roles</th><th>Status</th><th></th></tr></thead><tbody>${memberRows}</tbody></table></div>
      ${detail.members.length ? '' : '<div class="admin-empty-state"><strong>No members</strong><p>Add an existing PXA user to begin.</p></div>'}
    </section>`;
}

function mailPage() {
  const mail = state.mail;
  const totalPages = Math.max(1, Math.ceil(mail.total / mail.pageSize));
  const rows = mail.items.map((message) => {
    const canRetry = message.status === 'Failed' || message.status === 'DeadLetter';
    const canCancel = ['Pending', 'Scheduled', 'Failed', 'DeadLetter'].includes(message.status);
    return `
    <tr>
      <td>${escapeHtml(message.recipientEmail)}</td>
      <td>${escapeHtml(message.templateKey)}</td>
      <td><span class="admin-status ${message.status === 'Delivered' ? 'admin-status--ready' : message.status === 'DeadLetter' || message.status === 'Failed' ? 'admin-status--inactive' : 'admin-status--planned'}">${escapeHtml(message.status)}</span></td>
      <td>${message.attempts}</td>
      <td>${escapeHtml(formatDate(message.deliveredAt || message.createdAt))}</td>
      <td><div class="admin-row-actions">${canRetry ? `<button class="mail-retry" data-message-id="${message.id}" type="button" ${mail.saving ? 'disabled' : ''}>Retry</button>` : ''}${canCancel ? `<button class="mail-cancel" data-message-id="${message.id}" type="button" ${mail.saving ? 'disabled' : ''}>Cancel</button>` : ''}</div></td>
    </tr>`;
  }).join('');
  const summary = mail.summary;
  return `
    <header class="admin-page-header"><div><p class="pxa-kicker">Operations</p><h1>Mail delivery</h1><p>Inspect tenant-scoped transactional delivery metadata without exposing tokens or message bodies.</p></div><span class="admin-record-count">${mail.total} messages</span></header>
    ${summary ? `<section class="admin-summary-grid" aria-label="Mail transport summary"><article><span>Transport</span><strong>${escapeHtml(summary.transport)}</strong></article><article><span>Delivery</span><strong>${summary.deliveryEnabled ? 'Enabled' : 'Disabled'}</strong></article><article><span>Pending</span><strong>${summary.pending}</strong></article><article><span>Needs attention</span><strong>${summary.failed + summary.deadLetter}</strong></article></section>` : ''}
    <section class="admin-table-section" aria-busy="${mail.loading}">
      <form class="admin-table-toolbar" id="mail-filter-form">
        <select name="status" aria-label="Filter delivery status"><option value="">All statuses</option>${['Pending', 'Scheduled', 'Sending', 'Delivered', 'Failed', 'DeadLetter', 'Cancelled', 'Suppressed'].map((status) => `<option ${mail.status === status ? 'selected' : ''}>${status}</option>`).join('')}</select>
        <button type="submit">Apply</button>
      </form>
      ${mail.error ? `<div class="admin-alert admin-alert--error admin-inline-alert">${escapeHtml(mail.error)}</div>` : ''}
      ${mail.loading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading delivery status...</p></div>' : `<div class="admin-table-scroll"><table><thead><tr><th>Recipient</th><th>Template</th><th>Status</th><th>Attempts</th><th>Updated</th><th>Actions</th></tr></thead><tbody>${rows}</tbody></table></div>${mail.items.length ? '' : '<div class="admin-empty-state"><strong>No mail messages</strong><p>Transactional messages for this organization will appear here.</p></div>'}<footer class="admin-pagination"><span>Page ${mail.page} of ${totalPages}</span><div><button id="mail-previous" type="button" ${mail.page <= 1 ? 'disabled' : ''}>Previous</button><button id="mail-next" type="button" ${mail.page >= totalPages ? 'disabled' : ''}>Next</button></div></footer>`}
    </section>`;
}

function subscriptionsPage() {
  const subscriptions = state.subscriptions;
  const totalPages = Math.max(1, Math.ceil(subscriptions.total / subscriptions.pageSize));
  const rows = subscriptions.items.map((subscription) => `
    <tr>
      <td><a class="admin-table-link" href="/subscriptions/${subscription.id}"><strong>${escapeHtml(subscription.organizationName)}</strong></a><small>${escapeHtml(subscription.accountType.replace('IndividualDeveloper', 'Individual Developer'))}</small></td>
      <td>${escapeHtml(subscription.edition)}</td>
      <td>${isSystemAdministrator() ? `<select class="subscription-status" data-subscription-id="${subscription.id}" aria-label="Status for ${escapeHtml(subscription.organizationName)}">${[subscription.status, ...(subscriptionTransitions[subscription.status] || [])].map((status) => `<option ${subscription.status === status ? 'selected' : ''}>${status}</option>`).join('')}</select>` : `<span class="admin-status ${subscription.status === 'Active' || subscription.status === 'Trialing' ? 'admin-status--ready' : 'admin-status--planned'}">${escapeHtml(subscription.status)}</span>`}</td>
      <td>${escapeHtml(subscription.deploymentMode)}</td>
      <td>${subscription.assignedSeats} / ${subscription.seatLimit ?? 'unlimited'}</td>
      <td>${escapeHtml(formatDate(subscription.trialEndsAt || subscription.currentPeriodEndsAt))}</td>
      <td>${isSystemAdministrator() ? `<button class="subscription-save-status" data-subscription-id="${subscription.id}" type="button" ${subscriptions.saving ? 'disabled' : ''}>Save</button>` : ''}</td>
    </tr>`).join('');
  const availableOrganizations = state.organizations.items.filter((organization) =>
    !subscriptions.items.some((subscription) => subscription.organizationId === organization.id));
  return `
    <header class="admin-page-header"><div><p class="pxa-kicker">Commercial</p><h1>Subscriptions</h1><p>Manage edition, lifecycle, deployment, seats, and explicit product entitlements independently from application roles.</p></div><span class="admin-record-count">${subscriptions.total} subscriptions</span></header>
    ${subscriptions.error ? `<div class="admin-alert admin-alert--error admin-detail-alert">${escapeHtml(subscriptions.error)}</div>` : ''}
    ${isSystemAdministrator() ? `<section class="admin-section admin-subscription-create"><div class="admin-section-heading"><h2>Create subscription</h2><p>No prices or quotas are inferred. Select only the capabilities approved for this organization.</p></div><form id="subscription-create-form" class="admin-form-stack"><div class="admin-subscription-fields"><label class="admin-field"><span>Organization</span><select name="organizationId" required><option value="">Select organization</option>${availableOrganizations.map((organization) => `<option value="${organization.id}">${escapeHtml(organization.name)}</option>`).join('')}</select></label><label class="admin-field"><span>Edition</span><select name="edition">${['Free', 'Trial', 'Premium', 'Enterprise'].map((value) => `<option>${value}</option>`).join('')}</select></label><label class="admin-field"><span>Account type</span><select name="accountType"><option value="Company">Company</option><option value="IndividualDeveloper">Individual Developer</option></select></label><label class="admin-field"><span>Status</span><select name="status"><option>Active</option><option>Trialing</option><option>Pending</option></select></label><label class="admin-field"><span>Billing</span><select name="billingPeriod"><option>None</option><option>Monthly</option><option>Annual</option></select></label><label class="admin-field"><span>Deployment</span><select name="deploymentMode"><option>Cloud</option><option>OnPremise</option><option>Hybrid</option></select></label><label class="admin-field"><span>Seat limit</span><input name="seatLimit" type="number" min="1" placeholder="No fixed limit"></label></div><fieldset class="admin-capability-options"><legend>Enabled capabilities</legend>${subscriptionCapabilities.map(([key, label]) => `<label><input type="checkbox" name="capability" value="${key}"><span>${label}</span></label>`).join('')}</fieldset><div class="admin-form-actions"><button class="pxa-button pxa-button--primary" type="submit" ${subscriptions.saving || !availableOrganizations.length ? 'disabled' : ''}>Create subscription</button></div></form></section>` : ''}
    <section class="admin-table-section" aria-busy="${subscriptions.loading}"><form class="admin-table-toolbar" id="subscription-filter-form"><select name="edition" aria-label="Filter edition"><option value="">All editions</option>${['Free', 'Trial', 'Premium', 'Enterprise'].map((value) => `<option ${subscriptions.edition === value ? 'selected' : ''}>${value}</option>`).join('')}</select><select name="status" aria-label="Filter lifecycle"><option value="">All states</option>${['Pending', 'Trialing', 'Active', 'PastDue', 'GracePeriod', 'Suspended', 'Cancelled', 'Expired'].map((value) => `<option ${subscriptions.status === value ? 'selected' : ''}>${value}</option>`).join('')}</select><button type="submit">Apply</button></form>${subscriptions.loading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading subscriptions...</p></div>' : `<div class="admin-table-scroll"><table><thead><tr><th>Organization</th><th>Edition</th><th>State</th><th>Deployment</th><th>Seats</th><th>Renewal / expiry</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>${subscriptions.items.length ? '' : '<div class="admin-empty-state"><strong>No subscriptions</strong><p>Create the first organization subscription to define licensed capabilities.</p></div>'}<footer class="admin-pagination"><span>Page ${subscriptions.page} of ${totalPages}</span><div><button id="subscription-previous" type="button" ${subscriptions.page <= 1 ? 'disabled' : ''}>Previous</button><button id="subscription-next" type="button" ${subscriptions.page >= totalPages ? 'disabled' : ''}>Next</button></div></footer>`}</section>`;
}

function subscriptionDetailPage() {
  const detail = state.subscriptionDetail;
  if (detail.loading && !detail.data)
    return '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading subscription...</p></div>';
  if (!detail.data)
    return `<div class="admin-alert admin-alert--error">${escapeHtml(detail.error || 'Subscription not found.')}</div>`;
  const subscription = detail.data;
  const entitlementKeys = [...new Set([
    ...subscriptionCapabilities.map(([key]) => key),
    ...subscription.entitlements.map((item) => item.capability),
  ])];
  const entitlementRows = entitlementKeys.map((capability) => {
    const entitlement = subscription.entitlements.find((item) => item.capability === capability);
    const label = subscriptionCapabilities.find(([key]) => key === capability)?.[1] || capability;
    return `<tr data-entitlement-capability="${escapeHtml(capability)}"><td><strong>${escapeHtml(label)}</strong><small>${escapeHtml(capability)}</small></td><td><input class="entitlement-enabled" type="checkbox" ${entitlement?.enabled ? 'checked' : ''} ${detail.saving || !isSystemAdministrator() ? 'disabled' : ''}></td><td><input class="entitlement-limit" type="number" min="0" value="${entitlement?.limit ?? ''}" placeholder="No limit" ${detail.saving || !isSystemAdministrator() ? 'disabled' : ''}></td><td><input class="entitlement-unit" value="${escapeHtml(entitlement?.unit || '')}" placeholder="operations" maxlength="40" ${detail.saving || !isSystemAdministrator() ? 'disabled' : ''}></td><td><select class="entitlement-source" ${detail.saving || !isSystemAdministrator() ? 'disabled' : ''}>${['EditionDefault', 'NegotiatedOverride', 'TemporaryGrant'].map((source) => `<option ${entitlement?.source === source ? 'selected' : ''}>${source}</option>`).join('')}</select></td><td><input class="entitlement-expiry" type="datetime-local" value="${dateInputValue(entitlement?.expiresAt)}" ${detail.saving || !isSystemAdministrator() ? 'disabled' : ''}></td></tr>`;
  }).join('');
  const seatRows = detail.seats.map((seat) => `<tr><td><strong>${escapeHtml(seat.displayName)}</strong><small>${escapeHtml(seat.email)}</small></td><td>${escapeHtml(seat.membershipStatus)}</td><td><span class="admin-status ${seat.assigned ? 'admin-status--ready' : 'admin-status--planned'}">${seat.assigned ? 'Assigned' : 'Not assigned'}</span></td><td>${isSystemAdministrator() ? `<button class="${seat.assigned ? 'subscription-revoke-seat' : 'subscription-assign-seat'}" data-membership-id="${seat.membershipId}" type="button" ${detail.saving || seat.membershipStatus !== 'Active' ? 'disabled' : ''}>${seat.assigned ? 'Revoke' : 'Assign'}</button>` : ''}</td></tr>`).join('');
  const historyRows = detail.history.map((event) => `<tr><td>${escapeHtml(formatDate(event.createdAt))}</td><td>${escapeHtml(event.action)}</td><td>${escapeHtml(event.previousStatus || 'New')} → ${escapeHtml(event.currentStatus)}</td><td><strong>${escapeHtml(event.actorName)}</strong><small>${escapeHtml(event.actorUserId)}</small></td></tr>`).join('');
  const usageRows = (detail.usage?.items || []).map((item) => `<tr><td>${escapeHtml(item.capability)}</td><td>${escapeHtml(item.operation)}</td><td>${escapeHtml(item.source)}</td><td>${item.quantity}</td><td>${item.eventCount}</td><td>${escapeHtml(formatDate(item.lastOccurredAt))}</td></tr>`).join('');
  const lifecycleActions = isSystemAdministrator() ? `<section class="admin-section admin-subscription-actions"><div class="admin-section-heading"><h2>Lifecycle actions</h2><p>Each operation is validated and written to subscription history and the audit log.</p></div><div class="admin-action-grid">${subscription.edition === 'Trial' && !['Cancelled', 'Expired'].includes(subscription.status) ? `<form id="subscription-trial-form" class="admin-compact-action"><label class="admin-field"><span>Extend Trial by days</span><input name="days" type="number" min="1" max="365" value="7" required></label><button type="submit" ${detail.saving ? 'disabled' : ''}>Extend Trial</button></form>` : ''}${!['Trialing', 'Cancelled', 'Expired'].includes(subscription.status) ? `<form id="subscription-renew-form" class="admin-compact-action"><label class="admin-field"><span>Renew until</span><input name="periodEndsAt" type="datetime-local" required></label><button type="submit" ${detail.saving ? 'disabled' : ''}>Renew</button></form>` : ''}${['PastDue', 'GracePeriod'].includes(subscription.status) ? `<form id="subscription-grace-form" class="admin-compact-action"><label class="admin-field"><span>Grace period until</span><input name="endsAt" type="datetime-local" required></label><button type="submit" ${detail.saving ? 'disabled' : ''}>Apply grace period</button></form>` : ''}${!['Cancelled', 'Expired'].includes(subscription.status) ? `<form id="subscription-cancel-form" class="admin-compact-action"><label class="admin-field"><span>Cancellation effective</span><input name="effectiveAt" type="datetime-local" value="${dateInputValue(subscription.currentPeriodEndsAt)}"></label><button class="admin-danger-button" type="submit" ${detail.saving ? 'disabled' : ''}>Schedule cancellation</button></form>` : ''}</div></section>` : '';
  return `<header class="admin-page-header"><div><a class="admin-back-link" href="/subscriptions">Subscriptions</a><h1>${escapeHtml(subscription.organizationName)}</h1><p>${escapeHtml(subscription.edition)} · ${escapeHtml(subscription.accountType.replace('IndividualDeveloper', 'Individual Developer'))} · ${escapeHtml(subscription.deploymentMode)}</p></div><span class="admin-status ${subscription.status === 'Active' || subscription.status === 'Trialing' ? 'admin-status--ready' : 'admin-status--planned'}">${escapeHtml(subscription.status)}</span></header>${detail.error ? `<div class="admin-alert admin-alert--error admin-detail-alert">${escapeHtml(detail.error)}</div>` : ''}<section class="admin-summary-grid"><article><span>Billing</span><strong>${escapeHtml(subscription.billingPeriod)}</strong></article><article><span>Seats</span><strong>${subscription.assignedSeats} / ${subscription.seatLimit ?? 'unlimited'}</strong></article><article><span>Trial ends</span><strong>${escapeHtml(formatDate(subscription.trialEndsAt))}</strong></article><article><span>Period ends</span><strong>${escapeHtml(formatDate(subscription.currentPeriodEndsAt))}</strong></article></section>${lifecycleActions}<section class="admin-table-section"><form id="subscription-entitlements-form"><div class="admin-section-heading"><h2>Effective capability grants</h2><p>Limits and expiry are explicit. Application roles do not grant these products.</p></div><div class="admin-table-scroll"><table><thead><tr><th>Capability</th><th>Enabled</th><th>Limit</th><th>Unit</th><th>Source</th><th>Expires</th></tr></thead><tbody>${entitlementRows}</tbody></table></div>${isSystemAdministrator() ? `<div class="admin-form-stack"><label class="admin-field"><span>Additional capability key</span><input id="subscription-custom-capability" pattern="[a-z][a-z0-9.-]*" placeholder="feature.capability"></label></div><div class="admin-form-actions"><button class="pxa-button pxa-button--primary" type="submit" ${detail.saving ? 'disabled' : ''}>Save entitlements</button></div>` : ''}</form></section><section class="admin-table-section admin-members-section"><div class="admin-section-heading"><h2>Usage this period</h2><p>${escapeHtml(formatDate(detail.usage?.periodStartsAt))} to ${escapeHtml(formatDate(detail.usage?.periodEndsAt))} · ${detail.usage?.totalQuantity || 0} total units</p></div><div class="admin-table-scroll"><table><thead><tr><th>Capability</th><th>Operation</th><th>Source</th><th>Quantity</th><th>Events</th><th>Last activity</th></tr></thead><tbody>${usageRows}</tbody></table></div>${usageRows ? '' : '<div class="admin-empty-state"><strong>No usage recorded</strong><p>Metered product operations will appear here.</p></div>'}</section><section class="admin-table-section admin-members-section"><div class="admin-section-heading"><h2>Seat assignments</h2><p>Only active memberships in this organization can receive a seat.</p></div><div class="admin-table-scroll"><table><thead><tr><th>Member</th><th>Membership</th><th>Seat</th><th></th></tr></thead><tbody>${seatRows}</tbody></table></div>${detail.seats.length ? '' : '<div class="admin-empty-state"><strong>No memberships</strong></div>'}</section><section class="admin-table-section admin-members-section"><div class="admin-section-heading"><h2>Lifecycle history</h2><p>Append-only commercial state transitions.</p></div><div class="admin-table-scroll"><table><thead><tr><th>Time</th><th>Action</th><th>Transition</th><th>Actor</th></tr></thead><tbody>${historyRows}</tbody></table></div></section>`;
}

function licensesPage() {
  const licenses = state.licenses;
  const eligible = state.subscriptions.items.filter((item) => item.edition === 'Enterprise' && ['OnPremise', 'Hybrid'].includes(item.deploymentMode));
  const rows = licenses.items.map((license) => `<tr><td><strong>${escapeHtml(license.licenseNumber)}</strong><small>${escapeHtml(license.keyId)}</small></td><td>${escapeHtml(license.organizationName)}</td><td>${escapeHtml(license.deploymentMode)}</td><td>${license.instanceLimit}</td><td>${escapeHtml(formatDate(license.validUntil))}</td><td><span class="admin-status ${license.status === 'Active' ? 'admin-status--ready' : 'admin-status--inactive'}">${escapeHtml(license.status)}</span></td><td><div class="admin-row-actions"><a href="/api/pxa/v1/admin/licenses/${license.id}/download">Download</a><button class="license-validate" data-license-id="${license.id}" type="button">Validate</button>${isSystemAdministrator() && license.status === 'Active' ? `<button class="license-revoke" data-license-id="${license.id}" type="button">Revoke</button>` : ''}</div></td></tr>`).join('');
  const from = dateInputValue(new Date());
  const until = dateInputValue(new Date(Date.now() + 365 * 86400000));
  return `<header class="admin-page-header"><div><p class="pxa-kicker">Commercial</p><h1>Offline licenses</h1><p>Issue and verify signed licenses for approved Enterprise On-Premise and Hybrid deployments.</p></div><span class="admin-record-count">${licenses.items.length} licenses</span></header>${licenses.error ? `<div class="admin-alert admin-alert--error admin-detail-alert">${escapeHtml(licenses.error)}</div>` : ''}${licenses.notice ? `<div class="admin-alert admin-detail-alert">${escapeHtml(licenses.notice)}</div>` : ''}${isSystemAdministrator() ? `<section class="admin-section admin-subscription-create"><div class="admin-section-heading"><h2>Issue license</h2><p>Capabilities and limits are copied from the selected subscription into the signed envelope.</p></div><form id="license-issue-form" class="admin-form-stack"><div class="admin-subscription-fields"><label class="admin-field"><span>Subscription</span><select name="subscriptionId" required><option value="">Select Enterprise subscription</option>${eligible.map((item) => `<option value="${item.id}">${escapeHtml(item.organizationName)}</option>`).join('')}</select></label><label class="admin-field"><span>Valid from</span><input name="validFrom" type="datetime-local" value="${from}" required></label><label class="admin-field"><span>Valid until</span><input name="validUntil" type="datetime-local" value="${until}" required></label><label class="admin-field"><span>Instance limit</span><input name="instanceLimit" type="number" min="1" max="1000" value="1" required></label></div><div class="admin-form-actions"><button class="pxa-button pxa-button--primary" type="submit" ${licenses.saving || !eligible.length ? 'disabled' : ''}>Issue signed license</button></div></form></section>` : ''}<section class="admin-table-section" aria-busy="${licenses.loading}">${licenses.loading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading licenses...</p></div>' : `<div class="admin-table-scroll"><table><thead><tr><th>License</th><th>Organization</th><th>Deployment</th><th>Instances</th><th>Valid until</th><th>Status</th><th>Actions</th></tr></thead><tbody>${rows}</tbody></table></div>${rows ? '' : '<div class="admin-empty-state"><strong>No offline licenses</strong><p>Eligible Enterprise subscriptions can receive a signed license.</p></div>'}`}</section>`;
}

function serviceAccountsPage() {
  const view = state.serviceAccounts;
  const rows = view.items.flatMap((account) => {
    const accountRow = `<tr><td><strong>${escapeHtml(account.name)}</strong><small>${account.id}</small></td><td><span class="admin-status ${account.isActive ? 'admin-status--ready' : 'admin-status--inactive'}">${account.isActive ? 'Active' : 'Revoked'}</span></td><td>${account.keys.length}</td><td>${escapeHtml(formatDate(account.createdAt))}</td><td>${account.isActive ? `<button class="service-account-revoke" data-account-id="${account.id}" type="button">Revoke account</button>` : ''}</td></tr>`;
    const keyRows = account.keys.map((key) => `<tr class="admin-subrow"><td><span>API key</span><strong>${escapeHtml(key.name)}</strong><small>${escapeHtml(key.prefix)}...</small></td><td><span class="admin-status ${key.revokedAt ? 'admin-status--inactive' : 'admin-status--ready'}">${key.revokedAt ? 'Revoked' : 'Active'}</span></td><td>${escapeHtml(formatDate(key.lastUsedAt))}</td><td>${escapeHtml(formatDate(key.expiresAt))}</td><td>${!key.revokedAt && account.isActive ? `<button class="api-key-revoke" data-account-id="${account.id}" data-key-id="${key.id}" type="button">Revoke key</button>` : ''}</td></tr>`).join('');
    const keyForm = account.isActive ? `<tr class="admin-subrow"><td colspan="5"><form class="api-key-create-form admin-inline-form" data-account-id="${account.id}"><label class="admin-field"><span>Key name</span><input name="name" maxlength="160" placeholder="Production SDK" required></label><label class="admin-field"><span>Expires</span><input name="expiresAt" type="datetime-local"></label><button type="submit" ${view.saving ? 'disabled' : ''}>Create key</button></form></td></tr>` : '';
    return [accountRow, keyRows, keyForm];
  }).join('');
  const secret = view.secret ? `<section class="admin-section admin-secret-once"><div class="admin-section-heading"><h2>New API key</h2><p>This secret is shown once. Store it in a secret manager before leaving this page.</p></div><code>${escapeHtml(view.secret)}</code><button id="api-key-secret-dismiss" type="button">I have stored the key</button></section>` : '';
  return `<header class="admin-page-header"><div><p class="pxa-kicker">Access</p><h1>Service accounts</h1><p>Create tenant-bound credentials for SDKs and automation. API keys never receive Admin permissions.</p></div><span class="admin-record-count">${view.items.length} accounts</span></header>${view.error ? `<div class="admin-alert admin-alert--error admin-detail-alert">${escapeHtml(view.error)}</div>` : ''}${secret}<section class="admin-section"><div class="admin-section-heading"><h2>Create service account</h2><p>Use one account per application or deployment so access can be revoked independently.</p></div><form id="service-account-create-form" class="admin-inline-form"><label class="admin-field"><span>Name</span><input name="name" maxlength="160" placeholder="Production integration" required></label><button class="pxa-button pxa-button--primary" type="submit" ${view.saving ? 'disabled' : ''}>Create account</button></form></section><section class="admin-table-section" aria-busy="${view.loading}">${view.loading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading service accounts...</p></div>' : `<div class="admin-table-scroll"><table><thead><tr><th>Account / key</th><th>State</th><th>Keys / last used</th><th>Created / expires</th><th>Actions</th></tr></thead><tbody>${rows}</tbody></table></div>${rows ? '' : '<div class="admin-empty-state"><strong>No service accounts</strong><p>Create an account before issuing an API key.</p></div>'}`}</section>`;
}

function auditPage() {
  const audit = state.audit;
  const totalPages = Math.max(1, Math.ceil(audit.total / audit.pageSize));
  const rows = audit.items.map((event) => `<tr class="${audit.selected?.id === event.id ? 'admin-row-selected' : ''}"><td>${escapeHtml(formatDate(event.createdAt))}</td><td><strong>${escapeHtml(event.actorName)}</strong><small>${escapeHtml(event.actorEmail || 'System operation')}</small></td><td><strong>${escapeHtml(event.action)}</strong></td><td>${escapeHtml(event.targetType)}<small>${escapeHtml(event.targetId)}</small></td><td><span class="admin-status ${event.outcome === 'succeeded' ? 'admin-status--ready' : 'admin-status--inactive'}">${escapeHtml(event.outcome)}</span></td><td><button class="audit-detail-button" data-event-id="${event.id}" type="button">${audit.selected?.id === event.id ? 'Close' : 'Details'}</button></td></tr>`).join('');
  const selected = audit.selected;
  const details = selected ? `<section class="admin-section admin-audit-detail"><div class="admin-section-heading"><div><h2>Event details</h2><p>${escapeHtml(selected.action)} at ${escapeHtml(formatDate(selected.createdAt))}</p></div><button id="audit-detail-close" type="button">Close</button></div>${audit.detailLoading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading event...</p></div>' : `<dl class="admin-detail-list"><div><dt>Actor</dt><dd>${escapeHtml(selected.actorName)}${selected.actorEmail ? ` (${escapeHtml(selected.actorEmail)})` : ''}</dd></div><div><dt>Target</dt><dd>${escapeHtml(selected.targetType)} / ${escapeHtml(selected.targetId)}</dd></div><div><dt>Outcome</dt><dd>${escapeHtml(selected.outcome)}</dd></div><div><dt>Event ID</dt><dd>${selected.id}</dd></div></dl><div class="admin-audit-json"><span>Recorded details</span><pre>${escapeHtml(selected.details ? JSON.stringify(selected.details, null, 2) : 'No additional details were recorded.')}</pre></div>`}</section>` : '';
  return `<header class="admin-page-header"><div><p class="pxa-kicker">Operations</p><h1>Audit</h1><p>Trace privileged changes within the active organization. Events are read-only and ordered by their recorded timestamp.</p></div><span class="admin-record-count">${audit.total} events</span></header>${audit.error ? `<div class="admin-alert admin-alert--error admin-detail-alert">${escapeHtml(audit.error)}</div>` : ''}<section class="admin-table-section"><form id="audit-filter-form" class="admin-audit-filters"><label class="admin-field admin-audit-search"><span>Search</span><input name="search" type="search" value="${escapeHtml(audit.search)}" placeholder="Actor, action, target, or ID"></label><label class="admin-field"><span>Action</span><select name="action"><option value="">All actions</option>${audit.actions.map((value) => `<option value="${escapeHtml(value)}" ${audit.action === value ? 'selected' : ''}>${escapeHtml(value)}</option>`).join('')}</select></label><label class="admin-field"><span>Target</span><select name="targetType"><option value="">All targets</option>${audit.targetTypes.map((value) => `<option value="${escapeHtml(value)}" ${audit.targetType === value ? 'selected' : ''}>${escapeHtml(value)}</option>`).join('')}</select></label><label class="admin-field"><span>Outcome</span><select name="outcome"><option value="">All outcomes</option>${audit.outcomes.map((value) => `<option value="${escapeHtml(value)}" ${audit.outcome === value ? 'selected' : ''}>${escapeHtml(value)}</option>`).join('')}</select></label><label class="admin-field"><span>From</span><input name="from" type="datetime-local" value="${escapeHtml(audit.from)}"></label><label class="admin-field"><span>To</span><input name="to" type="datetime-local" value="${escapeHtml(audit.to)}"></label><label class="admin-field"><span>Order</span><select name="direction"><option value="desc" ${audit.direction === 'desc' ? 'selected' : ''}>Newest first</option><option value="asc" ${audit.direction === 'asc' ? 'selected' : ''}>Oldest first</option></select></label><div class="admin-audit-filter-actions"><button type="submit">Apply filters</button><button id="audit-clear-filters" type="button">Clear</button></div></form><div class="admin-audit-export"><div><strong>Export audit evidence</strong><p>${audit.canExport ? 'Download the current filtered result. The export itself is audited.' : 'CSV and JSON export require an Enterprise subscription.'}</p></div><div><button class="audit-export" data-format="csv" type="button" ${!audit.canExport || audit.exporting ? 'disabled' : ''}>Export CSV</button><button class="audit-export" data-format="json" type="button" ${!audit.canExport || audit.exporting ? 'disabled' : ''}>Export JSON</button></div></div>${audit.loading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading audit events...</p></div>' : `<div class="admin-table-scroll"><table><thead><tr><th>Time</th><th>Actor</th><th>Action</th><th>Target</th><th>Outcome</th><th></th></tr></thead><tbody>${rows}</tbody></table></div>${rows ? '' : '<div class="admin-empty-state"><strong>No matching events</strong><p>Adjust the filters or perform an administrative operation.</p></div>'}<footer class="admin-pagination"><span>Page ${audit.page} of ${totalPages}</span><div><button id="audit-previous" type="button" ${audit.page <= 1 ? 'disabled' : ''}>Previous</button><button id="audit-next" type="button" ${audit.page >= totalPages ? 'disabled' : ''}>Next</button></div></footer>`}</section>${details}`;
}

function rolesPage() {
  const roles = state.roles;
  const roleRows = roles.items.map((role) => `<tr><td><a class="admin-table-link" href="/roles/${role.key}"><strong>${escapeHtml(role.name)}</strong></a><small>${escapeHtml(role.description)}</small></td><td><span class="admin-status admin-status--ready">Protected</span></td><td>${role.memberCount}</td><td>${role.permissions.length}</td><td><a href="/roles/${role.key}">View role</a></td></tr>`).join('');
  const permissionRows = roles.permissions.map((permission) => `<tr><td><strong>${escapeHtml(permission.key)}</strong><small>${escapeHtml(permission.description)}</small></td><td>${escapeHtml(permission.group)}</td>${roles.items.map((role) => `<td class="admin-permission-cell"><span class="${role.permissions.some((item) => item.key === permission.key) ? 'admin-permission-granted' : 'admin-permission-none'}" aria-label="${role.permissions.some((item) => item.key === permission.key) ? 'Granted' : 'Not granted'}">${role.permissions.some((item) => item.key === permission.key) ? 'Granted' : '—'}</span></td>`).join('')}</tr>`).join('');
  return `<header class="admin-page-header"><div><p class="pxa-kicker">Identity</p><h1>Roles & permissions</h1><p>Review protected organization roles and the exact Admin permissions they grant inside the active tenant.</p></div><span class="admin-record-count">${roles.items.length} protected roles</span></header>${roles.error ? `<div class="admin-alert admin-alert--error admin-detail-alert">${escapeHtml(roles.error)}</div>` : ''}${roles.loading ? '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading roles...</p></div>' : `<section class="admin-table-section"><div class="admin-table-scroll"><table><thead><tr><th>Role</th><th>Definition</th><th>Members</th><th>Permissions</th><th></th></tr></thead><tbody>${roleRows}</tbody></table></div></section><section class="admin-table-section admin-members-section"><div class="admin-section-heading"><h2>Permission matrix</h2><p>Product entitlements remain separate and cannot be granted by an application role.</p></div><div class="admin-table-scroll"><table class="admin-permission-matrix"><thead><tr><th>Permission</th><th>Area</th>${roles.items.map((role) => `<th>${escapeHtml(role.name)}</th>`).join('')}</tr></thead><tbody>${permissionRows}</tbody></table></div></section>`}`;
}

function roleDetailPage() {
  const detail = state.roleDetail;
  if (detail.loading && !detail.data)
    return '<div class="admin-empty-state"><div class="admin-spinner"></div><p>Loading role...</p></div>';
  if (!detail.data)
    return `<section class="admin-message-page"><span class="admin-error-code">!</span><h1>Role unavailable</h1><p>${escapeHtml(detail.error || 'The role could not be loaded.')}</p><a class="pxa-button pxa-button--secondary" href="/roles">Return to roles</a></section>`;
  const { role, members, total } = detail.data;
  const totalPages = Math.max(1, Math.ceil(total / detail.pageSize));
  const eligibleUsers = detail.users.filter((user) =>
    user.id !== state.user?.id && !user.roles.includes(role.name) && user.membershipStatus !== 'Removed');
  const groupedPermissions = Object.entries(role.permissions.reduce((groups, permission) => {
    (groups[permission.group] ||= []).push(permission);
    return groups;
  }, {}));
  const permissionSections = groupedPermissions.map(([group, permissions]) => `<section><h3>${escapeHtml(group)}</h3>${permissions.map((permission) => `<div class="admin-role-permission"><strong>${escapeHtml(permission.key)}</strong><p>${escapeHtml(permission.description)}</p></div>`).join('')}</section>`).join('');
  const memberRows = members.map((member) => `<tr><td><a class="admin-table-link" href="/users/${member.userId}"><strong>${escapeHtml(member.displayName)}</strong></a><small>${escapeHtml(member.email)}</small></td><td><span class="admin-status ${member.isActive && member.membershipStatus === 'Active' ? 'admin-status--ready' : 'admin-status--inactive'}">${escapeHtml(member.membershipStatus)}</span></td><td>${escapeHtml(formatDate(member.assignedAt))}<small>by ${escapeHtml(member.assignedByName)}</small></td><td><button class="role-member-revoke" data-user-id="${member.userId}" type="button" ${detail.saving ? 'disabled' : ''}>Revoke</button></td></tr>`).join('');
  return `<header class="admin-page-header"><div><a class="admin-back-link" href="/roles">Roles & permissions</a><h1>${escapeHtml(role.name)}</h1><p>${escapeHtml(role.description)}</p></div><span class="admin-status admin-status--ready">Protected definition</span></header>${detail.error ? `<div class="admin-alert admin-alert--error admin-detail-alert">${escapeHtml(detail.error)}</div>` : ''}<div class="admin-detail-grid"><section class="admin-section"><div class="admin-section-heading"><h2>Granted permissions</h2><p>${role.permissions.length} explicit Admin permissions. Subscription entitlements are evaluated separately.</p></div><div class="admin-role-permission-groups">${permissionSections || '<div class="admin-empty-state"><strong>No Admin permissions</strong><p>This role only participates in licensed product workflows.</p></div>'}</div></section><section class="admin-section"><div class="admin-section-heading"><h2>Assign member</h2><p>Role changes revoke the user’s active sessions so new claims take effect immediately.</p></div><form id="role-member-assign-form" class="admin-form-stack"><label class="admin-field"><span>Organization user</span><select name="userId" required><option value="">Select user</option>${eligibleUsers.map((user) => `<option value="${user.id}">${escapeHtml(user.displayName)} (${escapeHtml(user.email)})</option>`).join('')}</select></label><div class="admin-form-actions"><button class="pxa-button pxa-button--primary" type="submit" ${detail.saving || !eligibleUsers.length ? 'disabled' : ''}>Assign role</button></div></form></section></div><section class="admin-table-section admin-members-section"><div class="admin-section-heading"><h2>Role members</h2><p>${total} assignments in the active organization.</p></div><div class="admin-table-scroll"><table><thead><tr><th>User</th><th>Membership</th><th>Assigned</th><th></th></tr></thead><tbody>${memberRows}</tbody></table></div>${memberRows ? '' : '<div class="admin-empty-state"><strong>No role members</strong><p>Assign an organization user to this protected role.</p></div>'}<footer class="admin-pagination"><span>Page ${detail.page} of ${totalPages}</span><div><button id="role-members-previous" type="button" ${detail.page <= 1 ? 'disabled' : ''}>Previous</button><button id="role-members-next" type="button" ${detail.page >= totalPages ? 'disabled' : ''}>Next</button></div></footer></section>`;
}

function dataPage(path) {
  const [title, description, columns] = pageDetails[path];
  return `
    <header class="admin-page-header">
      <div><p class="pxa-kicker">Administration</p><h1>${title}</h1><p>${description}</p></div>
      <button class="pxa-button pxa-button--primary" type="button" disabled>Add ${title === 'Users' ? 'user' : 'record'}</button>
    </header>
    <section class="admin-table-section">
      <div class="admin-table-toolbar">
        <label><span class="visually-hidden">Search ${title}</span><input type="search" placeholder="Search ${title.toLowerCase()}" disabled></label>
        <button type="button" disabled>Filter</button>
      </div>
      <div class="admin-table-scroll">
        <table>
          <thead><tr>${columns.map((column) => `<th>${column}</th>`).join('')}</tr></thead>
          <tbody></tbody>
        </table>
      </div>
      <div class="admin-empty-state">
        <strong>${title} API not connected yet</strong>
        <p>The authenticated shell is ready. This area will become active with its tenant-scoped Admin API.</p>
      </div>
    </section>
  `;
}

function forbiddenPage() {
  return `
    <section class="admin-message-page">
      <span class="admin-error-code">403</span>
      <h1>Administrator access required</h1>
      <p>Your account is authenticated but does not have an administrative role.</p>
      <button class="pxa-button pxa-button--secondary" id="forbidden-signout" type="button">Sign out</button>
    </section>
  `;
}

function notFoundPage() {
  return `
    <section class="admin-message-page">
      <span class="admin-error-code">404</span>
      <h1>Page not found</h1>
      <p>This administration route does not exist.</p>
      <a class="pxa-button pxa-button--secondary" href="/dashboard">Return to dashboard</a>
    </section>
  `;
}

function bindShellEvents() {
  document.querySelector('#menu-button')?.addEventListener('click', (event) => {
    const sidebar = document.querySelector('#admin-sidebar');
    const expanded = sidebar.classList.toggle('admin-sidebar--open');
    event.currentTarget.setAttribute('aria-expanded', String(expanded));
  });
  document.querySelector('#signout-button')?.addEventListener('click', handleLogout);
  document.querySelector('#forbidden-signout')?.addEventListener('click', handleLogout);
}

async function loadUsers() {
  state.users.loading = true;
  state.users.error = null;
  render();
  try {
    const response = await getAdminUsers(state.users);
    Object.assign(state.users, response, { loaded: true });
  } catch (error) {
    state.users.error = error.message;
    state.users.loaded = true;
  } finally {
    state.users.loading = false;
    render();
  }
}

async function loadUserDetail(userId) {
  Object.assign(state.userDetail, { id: userId, data: null, sessions: [], loading: true, error: null });
  render();
  try {
    const [user, sessions] = await Promise.all([
      getAdminUser(userId),
      getAdminUserSessions(userId),
    ]);
    state.userDetail.data = user;
    state.userDetail.sessions = sessions;
  } catch (error) {
    state.userDetail.error = error.message;
  } finally {
    state.userDetail.loading = false;
    render();
  }
}

function bindUsersEvents() {
  document.querySelector('#user-invitation-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const button = form.querySelector('button');
    button.disabled = true;
    state.users.error = null;
    state.users.notice = null;
    try {
      await createAdminInvitation(
        data.get('email'),
        data.get('displayName'),
        [data.get('role')]);
      state.users.notice = `Invitation queued for ${data.get('email')}.`;
      state.users.loaded = false;
      state.mail.loaded = false;
      await loadUsers();
    } catch (error) {
      state.users.error = error.message;
      render();
    }
  });
  document.querySelector('#users-filter-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    state.users.search = String(data.get('search') || '').trim();
    state.users.status = String(data.get('status') || '');
    state.users.page = 1;
    loadUsers();
  });
  document.querySelector('#users-previous')?.addEventListener('click', () => {
    state.users.page -= 1;
    loadUsers();
  });
  document.querySelector('#users-next')?.addEventListener('click', () => {
    state.users.page += 1;
    loadUsers();
  });
}

function bindUserDetailEvents() {
  document.querySelectorAll('.admin-session-revoke').forEach((button) => button.addEventListener('click', async () => {
    const sessionId = button.dataset.sessionId;
    if (!window.confirm('Revoke this browser session?')) return;
    state.userDetail.saving = true;
    state.userDetail.error = null;
    render();
    try {
      await revokeAdminUserSession(state.userDetail.id, sessionId);
      state.userDetail.sessions = await getAdminUserSessions(state.userDetail.id);
    } catch (error) {
      state.userDetail.error = error.message;
    } finally {
      state.userDetail.saving = false;
      render();
    }
  }));
  document.querySelector('#user-sessions-revoke-all')?.addEventListener('click', async () => {
    if (!window.confirm('Revoke all revocable sessions for this user?')) return;
    state.userDetail.saving = true;
    state.userDetail.error = null;
    render();
    try {
      await revokeAllAdminUserSessions(state.userDetail.id);
      state.userDetail.sessions = await getAdminUserSessions(state.userDetail.id);
    } catch (error) {
      state.userDetail.error = error.message;
    } finally {
      state.userDetail.saving = false;
      render();
    }
  });
  document.querySelector('#user-status-button')?.addEventListener('click', async () => {
    const user = state.userDetail.data;
    if (!user) return;
    if (user.isActive && !window.confirm(`Disable ${user.displayName}? Their current sessions will be revoked.`)) return;
    state.userDetail.saving = true;
    state.userDetail.error = null;
    render();
    try {
      state.userDetail.data = await updateAdminUserStatus(user.id, !user.isActive);
      state.users.loaded = false;
    } catch (error) {
      state.userDetail.error = error.message;
    } finally {
      state.userDetail.saving = false;
      render();
    }
  });
  document.querySelector('#user-roles-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const roles = new FormData(event.currentTarget).getAll('roles');
    state.userDetail.saving = true;
    state.userDetail.error = null;
    render();
    try {
      state.userDetail.data = await updateAdminUserRoles(state.userDetail.id, roles);
      state.users.loaded = false;
    } catch (error) {
      state.userDetail.error = error.message;
    } finally {
      state.userDetail.saving = false;
      render();
    }
  });
}

async function loadOrganizations() {
  state.organizations.loading = true;
  state.organizations.error = null;
  render();
  try {
    const response = await getAdminOrganizations(state.organizations);
    Object.assign(state.organizations, response, { loaded: true });
  } catch (error) {
    state.organizations.error = error.message;
    state.organizations.loaded = true;
  } finally {
    state.organizations.loading = false;
    render();
  }
}

async function loadOrganizationDetail(organizationId) {
  Object.assign(state.organizationDetail, { id: organizationId, data: null, members: [], loading: true, error: null, saving: false });
  render();
  try {
    const [organization, members] = await Promise.all([
      getAdminOrganization(organizationId),
      getAdminOrganizationMembers(organizationId),
    ]);
    state.organizationDetail.data = organization;
    state.organizationDetail.members = members;
  } catch (error) {
    state.organizationDetail.error = error.message;
  } finally {
    state.organizationDetail.loading = false;
    render();
  }
}

function bindOrganizationsEvents() {
  document.querySelector('#organizations-filter-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    state.organizations.search = String(data.get('search') || '').trim();
    state.organizations.status = String(data.get('status') || '');
    state.organizations.page = 1;
    loadOrganizations();
  });
  document.querySelector('#organizations-previous')?.addEventListener('click', () => {
    state.organizations.page -= 1;
    loadOrganizations();
  });
  document.querySelector('#organizations-next')?.addEventListener('click', () => {
    state.organizations.page += 1;
    loadOrganizations();
  });
  document.querySelector('#organization-create-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    try {
      const organization = await createAdminOrganization(data.get('name'), data.get('slug'));
      state.organizations.loaded = false;
      navigate(`/organizations/${organization.id}`);
    } catch (error) {
      state.organizations.error = error.message;
      render();
    }
  });
}

async function runOrganizationDetailMutation(operation) {
  state.organizationDetail.saving = true;
  state.organizationDetail.error = null;
  render();
  try {
    await operation();
    state.organizations.loaded = false;
    state.organizationDetail.saving = false;
    await loadOrganizationDetail(state.organizationDetail.id);
  } catch (error) {
    state.organizationDetail.error = error.message;
    state.organizationDetail.saving = false;
    render();
  }
}

function bindOrganizationDetailEvents() {
  document.querySelector('#organization-switch-button')?.addEventListener('click', async () => {
    state.organizationDetail.saving = true;
    render();
    try {
      const response = await switchOrganization(state.organizationDetail.id);
      state.user = response.user;
      state.users.loaded = false;
      state.organizationDetail.saving = false;
      render();
    } catch (error) {
      state.organizationDetail.error = error.message;
      state.organizationDetail.saving = false;
      render();
    }
  });
  document.querySelector('#organization-edit-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    runOrganizationDetailMutation(async () => {
      state.organizationDetail.data = await updateAdminOrganization(state.organizationDetail.id, {
        name: data.get('name'),
        ...(data.has('status') ? { status: data.get('status') } : {}),
      });
    });
  });
  document.querySelector('#organization-add-member-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    runOrganizationDetailMutation(() => addAdminOrganizationMember(
      state.organizationDetail.id,
      data.get('email'),
      [data.get('role')]));
  });
  document.querySelectorAll('.organization-remove-member').forEach((button) => {
    button.addEventListener('click', () => {
      if (!window.confirm(`Remove ${button.dataset.userName} from this organization?`)) return;
      runOrganizationDetailMutation(() => removeAdminOrganizationMember(
        state.organizationDetail.id,
        button.dataset.userId));
    });
  });
}

async function loadMail() {
  state.mail.loading = true;
  state.mail.error = null;
  render();
  try {
    const [response, summary] = await Promise.all([getAdminMail(state.mail), getAdminMailStatus()]);
    Object.assign(state.mail, response, { summary, loaded: true });
  } catch (error) {
    state.mail.error = error.message;
    state.mail.loaded = true;
  } finally {
    state.mail.loading = false;
    render();
  }
}

async function loadSubscriptions() {
  state.subscriptions.loading = true;
  state.subscriptions.error = null;
  render();
  try {
    const tasks = [getAdminSubscriptions(state.subscriptions)];
    if (isSystemAdministrator())
      tasks.push(getAdminOrganizations({ pageSize: 100 }));
    const [response, organizations] = await Promise.all(tasks);
    Object.assign(state.subscriptions, response, { loaded: true });
    if (organizations) Object.assign(state.organizations, organizations, { loaded: true });
  } catch (error) {
    state.subscriptions.error = error.message;
    state.subscriptions.loaded = true;
  } finally {
    state.subscriptions.loading = false;
    render();
  }
}

async function loadSubscriptionDetail(subscriptionId) {
  Object.assign(state.subscriptionDetail, {
    id: subscriptionId, data: null, seats: [], history: [], loading: true, error: null,
  });
  render();
  try {
    const [data, seats, history, usage] = await Promise.all([
      getAdminSubscription(subscriptionId),
      getAdminSubscriptionSeats(subscriptionId),
      getAdminSubscriptionHistory(subscriptionId),
      getAdminSubscriptionUsage(subscriptionId),
    ]);
    Object.assign(state.subscriptionDetail, { data, seats, history, usage });
  } catch (error) {
    state.subscriptionDetail.error = error.message;
  } finally {
    state.subscriptionDetail.loading = false;
    render();
  }
}

async function loadLicenses() {
  state.licenses.loading = true;
  state.licenses.error = null;
  render();
  try {
    const tasks = [getAdminLicenses()];
    if (isSystemAdministrator()) tasks.push(getAdminSubscriptions({ pageSize: 100 }));
    const [items, subscriptions] = await Promise.all(tasks);
    Object.assign(state.licenses, { items, loaded: true });
    if (subscriptions) Object.assign(state.subscriptions, subscriptions, { loaded: true });
  } catch (error) {
    state.licenses.error = error.message;
    state.licenses.loaded = true;
  } finally {
    state.licenses.loading = false;
    render();
  }
}

async function runLicenseMutation(operation) {
  state.licenses.saving = true;
  state.licenses.error = null;
  state.licenses.notice = null;
  render();
  try {
    await operation();
    state.licenses.saving = false;
    await loadLicenses();
  } catch (error) {
    state.licenses.error = error.message;
    state.licenses.saving = false;
    render();
  }
}

async function loadServiceAccounts() {
  Object.assign(state.serviceAccounts, { loading: true, error: null });
  render();
  try {
    state.serviceAccounts.items = await getAdminServiceAccounts();
    state.serviceAccounts.loaded = true;
  } catch (error) {
    state.serviceAccounts.error = error.message;
    state.serviceAccounts.loaded = true;
  } finally {
    state.serviceAccounts.loading = false;
    render();
  }
}

async function runServiceAccountMutation(operation, revealSecret = false) {
  Object.assign(state.serviceAccounts, { saving: true, error: null });
  render();
  try {
    const result = await operation();
    if (revealSecret) state.serviceAccounts.secret = result.secret;
    state.serviceAccounts.saving = false;
    await loadServiceAccounts();
  } catch (error) {
    Object.assign(state.serviceAccounts, { saving: false, error: error.message });
    render();
  }
}

function bindServiceAccountEvents() {
  document.querySelector('#service-account-create-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    runServiceAccountMutation(() => createAdminServiceAccount(String(data.get('name') || '').trim()));
  });
  document.querySelectorAll('.api-key-create-form').forEach((form) => form.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const expires = data.get('expiresAt');
    runServiceAccountMutation(() => createAdminApiKey(
      event.currentTarget.dataset.accountId,
      String(data.get('name') || '').trim(),
      expires ? new Date(expires).toISOString() : null), true);
  }));
  document.querySelectorAll('.api-key-revoke').forEach((button) => button.addEventListener('click', () => {
    if (window.confirm('Revoke this API key immediately?'))
      runServiceAccountMutation(() => revokeAdminApiKey(button.dataset.accountId, button.dataset.keyId));
  }));
  document.querySelectorAll('.service-account-revoke').forEach((button) => button.addEventListener('click', () => {
    if (window.confirm('Revoke this service account and all of its API keys?'))
      runServiceAccountMutation(() => revokeAdminServiceAccount(button.dataset.accountId));
  }));
  document.querySelector('#api-key-secret-dismiss')?.addEventListener('click', () => {
    state.serviceAccounts.secret = null;
    render();
  });
}

function auditApiFilters() {
  const audit = state.audit;
  return {
    search: audit.search,
    action: audit.action,
    targetType: audit.targetType,
    outcome: audit.outcome,
    from: audit.from ? new Date(audit.from).toISOString() : '',
    to: audit.to ? new Date(audit.to).toISOString() : '',
    direction: audit.direction,
    page: audit.page,
    pageSize: audit.pageSize,
  };
}

async function loadAudit() {
  Object.assign(state.audit, { loading: true, error: null });
  render();
  try {
    const response = await getAdminAudit(auditApiFilters());
    Object.assign(state.audit, response, { loaded: true });
  } catch (error) {
    Object.assign(state.audit, { error: error.message, loaded: true });
  } finally {
    state.audit.loading = false;
    render();
  }
}

async function loadAuditDetail(eventId) {
  if (state.audit.selected?.id === eventId) {
    state.audit.selected = null;
    render();
    return;
  }
  state.audit.selected = state.audit.items.find((item) => item.id === eventId) || { id: eventId };
  state.audit.detailLoading = true;
  state.audit.error = null;
  render();
  try {
    state.audit.selected = await getAdminAuditEvent(eventId);
  } catch (error) {
    state.audit.error = error.message;
    state.audit.selected = null;
  } finally {
    state.audit.detailLoading = false;
    render();
  }
}

async function runAuditExport(format) {
  Object.assign(state.audit, { exporting: true, error: null });
  render();
  try {
    const result = await exportAdminAudit(format, auditApiFilters());
    const url = URL.createObjectURL(result.blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = result.filename;
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    await loadAudit();
  } catch (error) {
    state.audit.error = error.message;
  } finally {
    state.audit.exporting = false;
    render();
  }
}

function bindAuditEvents() {
  document.querySelector('#audit-filter-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    Object.assign(state.audit, {
      search: String(data.get('search') || '').trim(),
      action: String(data.get('action') || ''),
      targetType: String(data.get('targetType') || ''),
      outcome: String(data.get('outcome') || ''),
      from: String(data.get('from') || ''),
      to: String(data.get('to') || ''),
      direction: String(data.get('direction') || 'desc'),
      page: 1,
      selected: null,
    });
    loadAudit();
  });
  document.querySelector('#audit-clear-filters')?.addEventListener('click', () => {
    Object.assign(state.audit, {
      search: '', action: '', targetType: '', outcome: '', from: '', to: '', direction: 'desc',
      page: 1, selected: null,
    });
    loadAudit();
  });
  document.querySelector('#audit-previous')?.addEventListener('click', () => {
    state.audit.page -= 1;
    state.audit.selected = null;
    loadAudit();
  });
  document.querySelector('#audit-next')?.addEventListener('click', () => {
    state.audit.page += 1;
    state.audit.selected = null;
    loadAudit();
  });
  document.querySelectorAll('.audit-detail-button').forEach((button) =>
    button.addEventListener('click', () => loadAuditDetail(button.dataset.eventId)));
  document.querySelector('#audit-detail-close')?.addEventListener('click', () => {
    state.audit.selected = null;
    render();
  });
  document.querySelectorAll('.audit-export').forEach((button) =>
    button.addEventListener('click', () => runAuditExport(button.dataset.format)));
}

async function loadRoles() {
  Object.assign(state.roles, { loading: true, error: null });
  render();
  try {
    const response = await getAdminRoles();
    Object.assign(state.roles, { items: response.roles, permissions: response.permissions, loaded: true });
  } catch (error) {
    Object.assign(state.roles, { error: error.message, loaded: true });
  } finally {
    state.roles.loading = false;
    render();
  }
}

async function loadRoleDetail(roleKey) {
  const page = state.roleDetail.key === roleKey ? state.roleDetail.page : 1;
  Object.assign(state.roleDetail, { key: roleKey, page, data: null, users: [], loading: true, error: null });
  render();
  try {
    const [data, users] = await Promise.all([
      getAdminRole(roleKey, state.roleDetail),
      getAdminUsers({ pageSize: 100 }),
    ]);
    Object.assign(state.roleDetail, { data, users: users.items, page: data.page, pageSize: data.pageSize });
  } catch (error) {
    state.roleDetail.error = error.message;
  } finally {
    state.roleDetail.loading = false;
    render();
  }
}

async function runRoleMutation(operation) {
  Object.assign(state.roleDetail, { saving: true, error: null });
  render();
  try {
    await operation();
    state.roleDetail.saving = false;
    state.roles.loaded = false;
    await loadRoleDetail(state.roleDetail.key);
  } catch (error) {
    Object.assign(state.roleDetail, { saving: false, error: error.message });
    render();
  }
}

function bindRoleDetailEvents() {
  document.querySelector('#role-member-assign-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const userId = new FormData(event.currentTarget).get('userId');
    if (userId) runRoleMutation(() => assignAdminRoleMember(state.roleDetail.key, userId));
  });
  document.querySelectorAll('.role-member-revoke').forEach((button) => button.addEventListener('click', () => {
    if (window.confirm('Revoke this organization role from the selected user?'))
      runRoleMutation(() => revokeAdminRoleMember(state.roleDetail.key, button.dataset.userId));
  }));
  document.querySelector('#role-members-previous')?.addEventListener('click', () => {
    state.roleDetail.page -= 1;
    loadRoleDetail(state.roleDetail.key);
  });
  document.querySelector('#role-members-next')?.addEventListener('click', () => {
    state.roleDetail.page += 1;
    loadRoleDetail(state.roleDetail.key);
  });
}

function bindLicenseEvents() {
  document.querySelector('#license-issue-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    runLicenseMutation(() => issueAdminLicense({
      subscriptionId: data.get('subscriptionId'),
      validFrom: new Date(data.get('validFrom')).toISOString(),
      validUntil: new Date(data.get('validUntil')).toISOString(),
      instanceLimit: Number(data.get('instanceLimit')),
    }));
  });
  document.querySelectorAll('.license-validate').forEach((button) => button.addEventListener('click', async () => {
    try {
      const result = await validateAdminLicense(button.dataset.licenseId);
      state.licenses.notice = `${result.code}: signature ${result.signatureValid ? 'valid' : 'invalid'}`;
      render();
    } catch (error) {
      state.licenses.error = error.message;
      render();
    }
  }));
  document.querySelectorAll('.license-revoke').forEach((button) => button.addEventListener('click', () => {
    const reason = window.prompt('Reason for revoking this offline license:');
    if (reason?.trim()) runLicenseMutation(() => revokeAdminLicense(button.dataset.licenseId, reason.trim()));
  }));
}

function bindSubscriptionEvents() {
  document.querySelector('#subscription-filter-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    state.subscriptions.edition = String(data.get('edition') || '');
    state.subscriptions.status = String(data.get('status') || '');
    state.subscriptions.page = 1;
    loadSubscriptions();
  });
  document.querySelector('#subscription-previous')?.addEventListener('click', () => { state.subscriptions.page -= 1; loadSubscriptions(); });
  document.querySelector('#subscription-next')?.addEventListener('click', () => { state.subscriptions.page += 1; loadSubscriptions(); });
  document.querySelector('#subscription-create-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const seatLimit = data.get('seatLimit');
    await runSubscriptionMutation(() => createAdminSubscription({
      organizationId: data.get('organizationId'), edition: data.get('edition'), accountType: data.get('accountType'),
      status: data.get('status'), billingPeriod: data.get('billingPeriod'), deploymentMode: data.get('deploymentMode'),
      seatLimit: seatLimit ? Number(seatLimit) : null, startsAt: null, trialEndsAt: null, currentPeriodEndsAt: null,
      entitlements: data.getAll('capability').map((capability) => ({ capability, enabled: true })),
    }));
  });
  document.querySelectorAll('.subscription-save-status').forEach((button) => button.addEventListener('click', () => {
    const select = document.querySelector(`.subscription-status[data-subscription-id="${button.dataset.subscriptionId}"]`);
    runSubscriptionMutation(() => updateAdminSubscription(button.dataset.subscriptionId, { status: select.value }));
  }));
}

async function runSubscriptionMutation(operation) {
  state.subscriptions.saving = true;
  state.subscriptions.error = null;
  render();
  try {
    await operation();
    state.subscriptions.loaded = false;
    state.subscriptions.saving = false;
    await loadSubscriptions();
  } catch (error) {
    state.subscriptions.error = error.message;
    state.subscriptions.saving = false;
    render();
  }
}

async function runSubscriptionDetailMutation(operation) {
  state.subscriptionDetail.saving = true;
  state.subscriptionDetail.error = null;
  render();
  try {
    await operation();
    state.subscriptions.loaded = false;
    state.subscriptionDetail.saving = false;
    await loadSubscriptionDetail(state.subscriptionDetail.id);
  } catch (error) {
    state.subscriptionDetail.error = error.message;
    state.subscriptionDetail.saving = false;
    render();
  }
}

function bindSubscriptionDetailEvents() {
  const detail = state.subscriptionDetail;
  document.querySelector('#subscription-entitlements-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const entitlements = [...document.querySelectorAll('[data-entitlement-capability]')].map((row) => ({
      capability: row.dataset.entitlementCapability,
      enabled: row.querySelector('.entitlement-enabled').checked,
      limit: row.querySelector('.entitlement-limit').value ? Number(row.querySelector('.entitlement-limit').value) : null,
      unit: row.querySelector('.entitlement-unit').value || null,
      source: row.querySelector('.entitlement-source').value,
      expiresAt: row.querySelector('.entitlement-expiry').value
        ? new Date(row.querySelector('.entitlement-expiry').value).toISOString() : null,
    }));
    const customCapability = document.querySelector('#subscription-custom-capability')?.value.trim();
    if (customCapability && !entitlements.some((item) => item.capability === customCapability))
      entitlements.push({ capability: customCapability, enabled: true, limit: null, unit: null, source: 'NegotiatedOverride', expiresAt: null });
    runSubscriptionDetailMutation(() => updateAdminSubscription(detail.id, { entitlements }));
  });
  document.querySelectorAll('.subscription-assign-seat').forEach((button) => button.addEventListener('click', () =>
    runSubscriptionDetailMutation(() => assignAdminSubscriptionSeat(detail.id, button.dataset.membershipId))));
  document.querySelectorAll('.subscription-revoke-seat').forEach((button) => button.addEventListener('click', () => {
    if (window.confirm('Revoke this subscription seat?'))
      runSubscriptionDetailMutation(() => revokeAdminSubscriptionSeat(detail.id, button.dataset.membershipId));
  }));
  document.querySelector('#subscription-trial-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    runSubscriptionDetailMutation(() => extendAdminTrial(detail.id, Number(new FormData(event.currentTarget).get('days'))));
  });
  document.querySelector('#subscription-renew-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const value = new FormData(event.currentTarget).get('periodEndsAt');
    runSubscriptionDetailMutation(() => renewAdminSubscription(detail.id, new Date(value).toISOString()));
  });
  document.querySelector('#subscription-grace-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    const value = new FormData(event.currentTarget).get('endsAt');
    runSubscriptionDetailMutation(() => startAdminGracePeriod(detail.id, new Date(value).toISOString()));
  });
  document.querySelector('#subscription-cancel-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    if (!window.confirm('Schedule cancellation for this subscription?')) return;
    const value = new FormData(event.currentTarget).get('effectiveAt');
    runSubscriptionDetailMutation(() => cancelAdminSubscription(
      detail.id, value ? new Date(value).toISOString() : null));
  });
}

function bindMailEvents() {
  document.querySelector('#mail-filter-form')?.addEventListener('submit', (event) => {
    event.preventDefault();
    state.mail.status = String(new FormData(event.currentTarget).get('status') || '');
    state.mail.page = 1;
    loadMail();
  });
  document.querySelector('#mail-previous')?.addEventListener('click', () => {
    state.mail.page -= 1;
    loadMail();
  });
  document.querySelector('#mail-next')?.addEventListener('click', () => {
    state.mail.page += 1;
    loadMail();
  });
  document.querySelectorAll('.mail-retry').forEach((button) => {
    button.addEventListener('click', () => runMailMutation(() => retryAdminMail(button.dataset.messageId)));
  });
  document.querySelectorAll('.mail-cancel').forEach((button) => {
    button.addEventListener('click', () => {
      if (window.confirm('Cancel this queued mail message?'))
        runMailMutation(() => cancelAdminMail(button.dataset.messageId));
    });
  });
}

async function runMailMutation(operation) {
  state.mail.saving = true;
  state.mail.error = null;
  render();
  try {
    await operation();
    state.mail.saving = false;
    await loadMail();
  } catch (error) {
    state.mail.error = error.message;
    state.mail.saving = false;
    render();
  }
}

async function handleLogout(event) {
  event.currentTarget.disabled = true;
  try {
    await logout();
  } finally {
    state.user = null;
    navigate('/login', true);
  }
}

function render() {
  if (state.loading) {
    app.innerHTML = '<main class="admin-loading"><div class="admin-spinner" aria-hidden="true"></div><p>Checking secure session...</p></main>';
    return;
  }

  if (!state.user && location.pathname === '/accept-invitation') {
    document.title = 'Accept invitation | PXA Admin';
    app.innerHTML = publicActionPage('invitation');
    bindPublicAction('invitation');
    return;
  }

  if (!state.user && location.pathname === '/forgot-password') {
    document.title = 'Reset password | PXA Admin';
    app.innerHTML = publicActionPage('request-reset');
    bindPublicAction('request-reset');
    return;
  }

  if (!state.user && location.pathname === '/reset-password') {
    document.title = 'Choose password | PXA Admin';
    app.innerHTML = publicActionPage('confirm-reset');
    bindPublicAction('confirm-reset');
    return;
  }

  if (!state.user) {
    if (location.pathname !== '/login') navigate('/login', true);
    else renderLogin();
    return;
  }

  if (!isAdministrator(state.user)) {
    renderShell(forbiddenPage(), 'Access denied');
    return;
  }

  if (location.pathname === '/' || location.pathname === '/login') {
    navigate('/dashboard', true);
    return;
  }

  if (location.pathname === '/dashboard') {
    renderShell(dashboardPage(), 'Dashboard');
    return;
  }

  if (location.pathname === '/users') {
    renderShell(usersPage(), 'Users');
    bindUsersEvents();
    if (!state.users.loaded && !state.users.loading) loadUsers();
    return;
  }

  if (location.pathname === '/organizations') {
    renderShell(organizationsPage(), 'Organizations');
    bindOrganizationsEvents();
    if (!state.organizations.loaded && !state.organizations.loading) loadOrganizations();
    return;
  }

  if (location.pathname === '/mail') {
    renderShell(mailPage(), 'Mail delivery');
    bindMailEvents();
    if (!state.mail.loaded && !state.mail.loading) loadMail();
    return;
  }

  if (location.pathname === '/subscriptions') {
    renderShell(subscriptionsPage(), 'Subscriptions');
    bindSubscriptionEvents();
    if (!state.subscriptions.loaded && !state.subscriptions.loading) loadSubscriptions();
    return;
  }

  if (location.pathname === '/licenses') {
    renderShell(licensesPage(), 'Offline licenses');
    bindLicenseEvents();
    if (!state.licenses.loaded && !state.licenses.loading) loadLicenses();
    return;
  }

  if (location.pathname === '/service-accounts') {
    renderShell(serviceAccountsPage(), 'Service accounts');
    bindServiceAccountEvents();
    if (!state.serviceAccounts.loaded && !state.serviceAccounts.loading) loadServiceAccounts();
    return;
  }

  if (location.pathname === '/audit') {
    renderShell(auditPage(), 'Audit');
    bindAuditEvents();
    if (!state.audit.loaded && !state.audit.loading) loadAudit();
    return;
  }

  if (location.pathname === '/roles') {
    renderShell(rolesPage(), 'Roles & permissions');
    if (!state.roles.loaded && !state.roles.loading) loadRoles();
    return;
  }

  const roleDetailMatch = location.pathname.match(/^\/roles\/([a-z-]+)$/i);
  if (roleDetailMatch) {
    renderShell(roleDetailPage(), state.roleDetail.data?.role?.name || 'Role');
    bindRoleDetailEvents();
    if (state.roleDetail.key !== roleDetailMatch[1] && !state.roleDetail.loading)
      loadRoleDetail(roleDetailMatch[1]);
    return;
  }

  const subscriptionDetailMatch = location.pathname.match(/^\/subscriptions\/([0-9a-f-]+)$/i);
  if (subscriptionDetailMatch) {
    renderShell(subscriptionDetailPage(), state.subscriptionDetail.data?.organizationName || 'Subscription');
    bindSubscriptionDetailEvents();
    if (state.subscriptionDetail.id !== subscriptionDetailMatch[1] && !state.subscriptionDetail.loading)
      loadSubscriptionDetail(subscriptionDetailMatch[1]);
    return;
  }

  const organizationDetailMatch = location.pathname.match(/^\/organizations\/([0-9a-f-]+)$/i);
  if (organizationDetailMatch) {
    renderShell(organizationDetailPage(), state.organizationDetail.data?.name || 'Organization');
    bindOrganizationDetailEvents();
    if (state.organizationDetail.id !== organizationDetailMatch[1] && !state.organizationDetail.loading) {
      loadOrganizationDetail(organizationDetailMatch[1]);
    }
    return;
  }

  const userDetailMatch = location.pathname.match(/^\/users\/([0-9a-f-]+)$/i);
  if (userDetailMatch) {
    renderShell(userDetailPage(), state.userDetail.data?.displayName || 'User');
    bindUserDetailEvents();
    if (state.userDetail.id !== userDetailMatch[1] && !state.userDetail.loading) {
      loadUserDetail(userDetailMatch[1]);
    }
    return;
  }

  if (pageDetails[location.pathname]) {
    renderShell(dataPage(location.pathname), pageDetails[location.pathname][0]);
    return;
  }

  renderShell(notFoundPage(), 'Page not found');
}

document.addEventListener('click', (event) => {
  const link = event.target.closest('a[href^="/"]');
  if (!link || link.target || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  event.preventDefault();
  navigate(link.getAttribute('href'));
});

window.addEventListener('popstate', render);

async function bootstrap() {
  try {
    state.user = await currentUser();
  } catch (error) {
    if (error.status !== 401) state.notice = error.message;
  } finally {
    state.loading = false;
    render();
  }
}

bootstrap();
