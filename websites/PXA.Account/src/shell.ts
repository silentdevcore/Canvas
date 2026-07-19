import type { UserInfo } from './api';

export interface NavigationItem {
  path: string;
  label: string;
}

export const navigation: NavigationItem[] = [
  { path: '/dashboard', label: 'Overview' },
  { path: '/profile', label: 'Profile' },
  { path: '/organization', label: 'Organization' },
  { path: '/subscription', label: 'Subscription' },
  { path: '/usage', label: 'Usage' },
  { path: '/licenses', label: 'Licenses' },
  { path: '/developer-access', label: 'Developer access' },
  { path: '/security', label: 'Security' },
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

function renderNavigation(): string {
  return navigation.map((item) => `
    <a href="${item.path}" ${location.pathname === item.path || location.pathname.startsWith(`${item.path}/`) ? 'aria-current="page"' : ''}>${item.label}</a>
  `).join('');
}

export function renderShell(app: HTMLElement, user: UserInfo, content: string, title: string): void {
  document.title = `${title} | PXA Account`;
  app.innerHTML = `
    <a class="account-skip-link" href="#account-content">Skip to content</a>
    <div class="account-portal">
      <header class="account-header">
        <a class="account-brand" href="/dashboard"><span>PXA</span> Account</a>
        <button class="account-menu-button" id="account-menu-button" type="button" aria-controls="account-navigation" aria-expanded="false">Menu</button>
        <nav class="account-navigation" id="account-navigation" aria-label="Account">${renderNavigation()}</nav>
        <div class="account-header-actions">
          <span class="account-header-user">${escapeHtml(user.displayName)}</span>
          <button class="pxa-button pxa-button--secondary" id="logout-button" type="button">Sign out</button>
        </div>
      </header>
      <main class="account-content" id="account-content" tabindex="-1">${content}</main>
    </div>`;
}

export function bindShellEvents(onLogout: () => void): void {
  document.querySelector('#account-menu-button')?.addEventListener('click', (event) => {
    const nav = document.querySelector('#account-navigation');
    const expanded = nav?.classList.toggle('account-navigation--open') ?? false;
    (event.currentTarget as HTMLElement).setAttribute('aria-expanded', String(expanded));
  });
  document.querySelector('#logout-button')?.addEventListener('click', onLogout);
}

export function closeAccountNavigation(restoreFocus = false): void {
  const nav = document.querySelector('#account-navigation');
  const menuButton = document.querySelector<HTMLButtonElement>('#account-menu-button');
  if (!nav?.classList.contains('account-navigation--open')) return;
  nav.classList.remove('account-navigation--open');
  menuButton?.setAttribute('aria-expanded', 'false');
  if (restoreFocus) menuButton?.focus();
}
