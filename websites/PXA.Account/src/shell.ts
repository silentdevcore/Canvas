import { siteLinks } from '../../shared/siteLinks.js';

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

export function renderShell(app: HTMLElement, content: string, title: string): void {
  document.title = `${title} | PXA Account`;
  app.innerHTML = `
    <a class="account-skip-link" href="#account-content">Skip to content</a>
    <div class="account-app-shell">
      <div class="account-sidebar-backdrop" id="account-sidebar-backdrop"></div>
      <aside class="account-sidebar" id="account-sidebar">
        <a class="account-brand" href="/dashboard"><span>PXA</span> Account</a>
        <nav class="account-navigation" id="account-navigation" aria-label="Account">${renderNavigation()}</nav>
        <div class="account-sidebar-footer">
          <a class="account-header-company-link" href="${siteLinks.company}">Back to Company site</a>
          <button class="pxa-button pxa-button--secondary" id="logout-button" type="button">Sign out</button>
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

export function bindShellEvents(onLogout: () => void): void {
  document.querySelector('#account-menu-button')?.addEventListener('click', (event) => {
    const sidebar = document.querySelector('#account-sidebar');
    const expanded = sidebar?.classList.toggle('account-sidebar--open') ?? false;
    document.querySelector('#account-sidebar-backdrop')?.classList.toggle('account-sidebar-backdrop--visible', expanded);
    (event.currentTarget as HTMLElement).setAttribute('aria-expanded', String(expanded));
  });
  document.querySelector('#account-sidebar-backdrop')?.addEventListener('click', () => closeAccountNavigation(true));
  document.querySelector('#logout-button')?.addEventListener('click', onLogout);
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
