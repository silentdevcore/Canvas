import { escapeHtml } from '../shell';
import { getAccountSubscription, getAccountSubscriptionUsage } from '../api';
import type { ApiError, AccountSubscriptionResponse, AccountSubscriptionUsageResponse } from '../api';
import { registerAccountStateReset } from '../accountContext';

interface UsagePageState {
  usage: AccountSubscriptionUsageResponse | null;
  subscription: AccountSubscriptionResponse | null;
  loading: boolean;
  loaded: boolean;
  error: string | null;
}

const state: UsagePageState = {
  usage: null,
  subscription: null,
  loading: false,
  loaded: false,
  error: null,
};
registerAccountStateReset(() => {
  Object.assign(state, {
    usage: null,
    subscription: null,
    loading: false,
    loaded: false,
    error: null,
  });
});

function formatDate(value: string): string {
  return new Date(value).toLocaleString();
}

function formatCapability(value: string): string {
  return value
    .replace(/[._-]+/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase());
}

async function loadUsage(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    const [usage, subscription] = await Promise.all([
      getAccountSubscriptionUsage(),
      getAccountSubscription(),
    ]);
    state.usage = usage;
    state.subscription = subscription;
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

  if (!state.usage || !state.subscription) {
    return `
      <header class="account-page-header"><div><p class="pxa-kicker">Customer workspace</p><h1>Usage</h1></div></header>
      <section class="account-section">
        <div>${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : '<p>Loading your usage…</p>'}</div>
      </section>
    `;
  }

  const usage = state.usage;
  const subscription = state.subscription;
  const usageByCapability = new Map<string, number>();
  for (const item of usage.items) {
    usageByCapability.set(item.capability, (usageByCapability.get(item.capability) ?? 0) + item.quantity);
  }
  const measuredEntitlements = subscription.entitlements.filter((entitlement) => entitlement.enabled);
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
      <article><span>Processed quantity</span><strong>${usage.totalQuantity.toLocaleString()}</strong></article>
    </section>
    <section class="account-usage-limits" aria-labelledby="usage-limits-title">
      <div class="account-section-heading">
        <div>
          <h2 id="usage-limits-title">Usage against limits</h2>
          <p>Current period consumption for each enabled product entitlement.</p>
        </div>
        <a href="/subscription">View subscription</a>
      </div>
      <div class="account-usage-grid">
        ${measuredEntitlements.map((entitlement) => {
          const used = usageByCapability.get(entitlement.capability) ?? 0;
          const limit = entitlement.limit;
          const boundedValue = limit === null ? 0 : Math.min(used, limit);
          const remaining = limit === null ? null : Math.max(limit - used, 0);
          return `
            <article>
              <div>
                <h3>${escapeHtml(formatCapability(entitlement.capability))}</h3>
                <strong>${limit === null ? 'Unlimited' : `${used.toLocaleString()} / ${limit.toLocaleString()}`}</strong>
              </div>
              ${limit === null
                ? '<p>Unlimited for this subscription period</p>'
                : `
                  <progress value="${boundedValue}" max="${Math.max(limit, 1)}" aria-label="${escapeHtml(formatCapability(entitlement.capability))} usage"></progress>
                  <p>${remaining!.toLocaleString()} ${escapeHtml(entitlement.unit ?? 'units')} remaining</p>
                `}
            </article>
          `;
        }).join('') || '<p>No enabled product entitlements are available.</p>'}
      </div>
    </section>
    <div class="account-form" style="margin-top: var(--pxa-space-8)">
      <h2>Usage by product</h2>
      <table class="account-table">
        <thead><tr><th>Product</th><th>Operation</th><th>Quantity</th><th>Events</th><th>Last activity</th></tr></thead>
        <tbody>${usage.items.map((item) => `
          <tr>
            <td>${escapeHtml(formatCapability(item.capability))}</td>
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
