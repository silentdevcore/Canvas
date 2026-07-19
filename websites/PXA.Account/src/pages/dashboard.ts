import { siteLinks } from '../../../shared/siteLinks.js';
import type { UserInfo } from '../api';
import { escapeHtml } from '../shell';

export function dashboardPage(user: UserInfo): string {
  const organizations = user.organizations || [];
  const active = organizations.find((item) => item.id === user.activeOrganizationId) || organizations[0];
  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>${escapeHtml(active?.name || 'Your PXA account')}</h1>
        <p>Manage your customer identity and continue into PXA products.</p>
      </div>
      <span class="account-status">Trial workspace</span>
    </header>
    <section class="account-summary" aria-label="Account summary">
      <article><span>Account</span><strong>${escapeHtml(user.displayName)}</strong><small>${escapeHtml(user.email)}</small></article>
      <article><span>Role</span><strong>${escapeHtml(user.roles.join(', ') || 'Customer')}</strong><small>Application access remains separate from product entitlements</small></article>
      <article><span>Organizations</span><strong>${organizations.length}</strong><small>${escapeHtml(active?.slug || 'No active workspace')}</small></article>
    </section>
    <section class="account-section">
      <div><h2>Continue working</h2><p>Open the product surface or guidance that matches your next task.</p></div>
      <div class="account-actions">
        <a class="pxa-button pxa-button--primary" href="${siteLinks.designer}">Open Designer</a>
        <a class="pxa-button pxa-button--secondary" href="${siteLinks.demo}">Explore demos</a>
        <a class="pxa-button pxa-button--secondary" href="${siteLinks.documentation}">Read documentation</a>
      </div>
    </section>
  `;
}
