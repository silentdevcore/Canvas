import { escapeHtml } from '../shell';
import { getAccountSubscriptionUsage } from '../api';
import type { ApiError, AccountSubscriptionUsageResponse } from '../api';
import { registerAccountStateReset } from '../accountContext';

interface UsagePageState {
  usage: AccountSubscriptionUsageResponse | null;
  loading: boolean;
  loaded: boolean;
  error: string | null;
}

const state: UsagePageState = { usage: null, loading: false, loaded: false, error: null };
registerAccountStateReset(() => {
  Object.assign(state, { usage: null, loading: false, loaded: false, error: null });
});

function formatDate(value: string): string {
  return new Date(value).toLocaleString();
}

async function loadUsage(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    state.usage = await getAccountSubscriptionUsage();
  } catch (error) {
    state.error = (error as ApiError).message;
  } finally {
    state.loading = false;
    state.loaded = true;
    window.dispatchEvent(new Event('pxa:rerender'));
  }
}

export function usagePage(): string {
  if (!state.loaded && !state.loading) loadUsage();

  if (!state.usage) {
    return `
      <header class="account-page-header"><div><p class="pxa-kicker">Customer workspace</p><h1>Usage</h1></div></header>
      <section class="account-section">
        <div>${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : '<p>Loading your usage…</p>'}</div>
      </section>
    `;
  }

  const usage = state.usage;
  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>Usage</h1>
        <p>See current usage against your subscription limits for the current billing period.</p>
      </div>
    </header>
    <section class="account-summary" aria-label="Usage summary">
      <article><span>Period started</span><strong>${new Date(usage.periodStartsAt).toLocaleDateString()}</strong></article>
      <article><span>Period ends</span><strong>${usage.periodEndsAt ? new Date(usage.periodEndsAt).toLocaleDateString() : 'Ongoing'}</strong></article>
      <article><span>Total events</span><strong>${usage.totalQuantity}</strong></article>
    </section>
    <div class="account-form" style="margin-top: var(--pxa-space-8)">
      <h2>Usage by product</h2>
      <table class="account-table">
        <thead><tr><th>Product</th><th>Operation</th><th>Quantity</th><th>Events</th><th>Last activity</th></tr></thead>
        <tbody>${usage.items.map((item) => `
          <tr>
            <td>${escapeHtml(item.capability)}</td>
            <td>${escapeHtml(item.operation)}</td>
            <td>${item.quantity}</td>
            <td>${item.eventCount}</td>
            <td>${formatDate(item.lastOccurredAt)}</td>
          </tr>
        `).join('') || '<tr><td colspan="5">No usage recorded this period.</td></tr>'}</tbody>
      </table>
    </div>
  `;
}
