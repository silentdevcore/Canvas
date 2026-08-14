import { siteLinks } from '../../shared/siteLinks.js';
import { pxaCommit, pxaVersion } from '../../shared/buildInfo.js';
import type { UserInfo } from './api';
import { accountPermissions, hasAccountPermission, type AccountPermission } from './permissions';

export interface NavigationItem {
  path: string;
  label: string;
  permission?: AccountPermission;
}

export const navigation: NavigationItem[] = [
  { path: '/dashboard', label: 'Overview' },
  { path: '/profile', label: 'Profile', permission: accountPermissions.profileManage },
  { path: '/organization', label: 'Organization', permission: accountPermissions.organizationRead },
  { path: '/subscription', label: 'Subscription', permission: accountPermissions.subscriptionRead },
  { path: '/usage', label: 'Usage', permission: accountPermissions.subscriptionRead },
  { path: '/licenses', label: 'Licenses', permission: accountPermissions.licensesRead },
  { path: '/developer-access', label: 'Developer access', permission: accountPermissions.serviceAccountsRead },
  { path: '/security', label: 'Security', permission: accountPermissions.sessionsManage },
  { path: '/legal-updates', label: 'Legal updates' },
  { path: '/support', label: 'Support' },
];

export function escapeHtml(value: unknown = ''): string {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function renderNavigation(user: UserInfo): string {
  return navigation
    .filter((item) => !item.permission || hasAccountPermission(user, item.permission))
    .map((item) => `
    <a href="${item.path}" ${location.pathname === item.path || location.pathname.startsWith(`${item.path}/`) ? 'aria-current="page"' : ''}>${item.label}</a>
  `).join('');
}

export function renderShell(app: HTMLElement, content: string, title: string, user: UserInfo): void {
  const activeOrganizationId = user.activeOrganizationId ?? user.organizations[0]?.id;
  document.title = `${title} | PXA Account`;
  app.innerHTML = `
    <a class="account-skip-link" href="#account-content">Skip to content</a>
    <div class="account-app-shell">
      <div class="account-sidebar-backdrop" id="account-sidebar-backdrop"></div>
      <aside class="account-sidebar" id="account-sidebar">
        <a class="account-brand" href="/dashboard"><span>PXA</span> Account</a>
        <nav class="account-navigation" id="account-navigation" aria-label="Account">${renderNavigation(user)}</nav>
        <div class="account-sidebar-footer">
          ${user.organizations.length > 1 ? `
            <label class="account-organization-switcher">
              <span>Organization</span>
              <select id="account-organization-switcher">
                ${user.organizations.map((organization) => `
                  <option value="${escapeHtml(organization.id)}" ${organization.id === activeOrganizationId ? 'selected' : ''}>${escapeHtml(organization.name)}</option>
                `).join('')}
              </select>
            </label>
          ` : ''}
          <a class="account-header-company-link" href="${siteLinks.company}">Back to Company site</a>
          <button class="pxa-button pxa-button--secondary" id="logout-button" type="button">Sign out</button>
          <small title="Commit ${escapeHtml(pxaCommit)}">PXA ${escapeHtml(pxaVersion)}</small>
        </div>
      </aside>
      <div class="account-workspace">
        <header class="account-mobile-bar">
          <button class="account-menu-button" id="account-menu-button" type="button" aria-controls="account-navigation" aria-expanded="false">Menu</button>
          <a class="account-mobile-brand" href="/dashboard">PXA Account</a>
        </header>
        <main class="account-content" id="account-content" tabindex="-1">${content}</main>
      </div>
    </div>`;
}

export function bindShellEvents(onLogout: () => void, onOrganizationSwitch: (organizationId: string) => void): void {
  document.querySelector('#account-menu-button')?.addEventListener('click', (event) => {
    const sidebar = document.querySelector('#account-sidebar');
    const expanded = sidebar?.classList.toggle('account-sidebar--open') ?? false;
    document.querySelector('#account-sidebar-backdrop')?.classList.toggle('account-sidebar-backdrop--visible', expanded);
    (event.currentTarget as HTMLElement).setAttribute('aria-expanded', String(expanded));
  });
  document.querySelector('#account-sidebar-backdrop')?.addEventListener('click', () => closeAccountNavigation(true));
  document.querySelector('#logout-button')?.addEventListener('click', onLogout);
  document.querySelector<HTMLSelectElement>('#account-organization-switcher')?.addEventListener('change', (event) => {
    const select = event.currentTarget as HTMLSelectElement;
    select.disabled = true;
    onOrganizationSwitch(select.value);
  });
}

export function closeAccountNavigation(restoreFocus = false): void {
  const sidebar = document.querySelector('#account-sidebar');
  const menuButton = document.querySelector<HTMLButtonElement>('#account-menu-button');
  if (!sidebar?.classList.contains('account-sidebar--open')) return;
  sidebar.classList.remove('account-sidebar--open');
  document.querySelector('#account-sidebar-backdrop')?.classList.remove('account-sidebar-backdrop--visible');
  menuButton?.setAttribute('aria-expanded', 'false');
  if (restoreFocus) menuButton?.focus();
}
