import './site.css';
import { currentUser, login, logout } from './api.js';

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
  '/roles': ['Roles & permissions', 'Review role definitions and the permissions granted to each role.', ['Role', 'Scope', 'Users', 'Permissions', 'Updated']],
  '/subscriptions': ['Subscriptions', 'Inspect editions, lifecycle state, seats, and renewal information.', ['Customer', 'Edition', 'State', 'Seats', 'Renewal']],
  '/licenses': ['Licenses', 'Issue and inspect signed licenses for approved On-Premise deployments.', ['License', 'Customer', 'Products', 'Valid until', 'State']],
  '/service-accounts': ['Service accounts', 'Manage non-interactive access and API-key rotation.', ['Name', 'Organization', 'Scopes', 'Last used', 'State']],
  '/mail': ['Mail delivery', 'Inspect transactional delivery state without exposing message secrets.', ['Recipient', 'Template', 'State', 'Attempts', 'Updated']],
  '/audit': ['Audit', 'Trace privileged administration and security events.', ['Time', 'Actor', 'Action', 'Target', 'Result']],
  '/settings': ['Settings', 'Configure organization defaults and operational administration settings.', ['Setting', 'Value', 'Scope', 'Updated']],
};

const state = {
  user: null,
  loading: true,
  notice: null,
};

function escapeHtml(value = '') {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
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
          <p class="admin-login-help">Password reset will become available when the transactional mail service is connected.</p>
        </form>
      </section>
    </main>
  `;

  document.querySelector('#login-form').addEventListener('submit', handleLogin);
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
        <a href="${item.path}" ${location.pathname === item.path ? 'aria-current="page"' : ''}>
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
        <div><strong>User administration API</strong><span class="admin-status admin-status--planned">Next</span><p>Lists and mutations remain unavailable until tenant policies are complete.</p></div>
        <div><strong>Mail and recovery</strong><span class="admin-status admin-status--planned">Planned</span><p>Password recovery depends on the transactional mail outbox.</p></div>
      </div>
    </section>
  `;
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
