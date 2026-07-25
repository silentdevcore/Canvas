import { escapeHtml } from '../shell';
import { getAccountSubscription, getAccountSubscriptionSeats } from '../api';
import type { ApiError, AccountSubscriptionResponse, AccountSubscriptionSeatResponse } from '../api';
import { registerAccountStateReset } from '../accountContext';

interface SubscriptionPageState {
  subscription: AccountSubscriptionResponse | null;
  seats: AccountSubscriptionSeatResponse[];
  loading: boolean;
  loaded: boolean;
  error: string | null;
}

const state: SubscriptionPageState = {
  subscription: null,
  seats: [],
  loading: false,
  loaded: false,
  error: null,
};
registerAccountStateReset(() => {
  Object.assign(state, {
    subscription: null,
    seats: [],
    loading: false,
    loaded: false,
    error: null,
  });
});

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : '—';
}

async function loadSubscription(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    const [subscription, seats] = await Promise.all([
      getAccountSubscription(),
      getAccountSubscriptionSeats(),
    ]);
    state.subscription = subscription;
    state.seats = seats ?? [];
  } catch (error) {
    state.error = (error as ApiError).message;
  } finally {
    state.loading = false;
    state.loaded = true;
    window.dispatchEvent(new Event('pxa:rerender'));
  }
}

export function subscriptionPage(): string {
  if (!state.loaded && !state.loading) loadSubscription();

  if (!state.subscription) {
    return `
      <header class="account-page-header"><div><p class="pxa-kicker">Customer workspace</p><h1>Subscription</h1></div></header>
      <section class="account-section">
        <div>${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : '<p>Loading your subscription…</p>'}</div>
      </section>
    `;
  }

  const subscription = state.subscription;
  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>Subscription</h1>
        <p>Review your edition, Trial or renewal dates, products, seats, and limits.</p>
      </div>
      <span class="account-status">${escapeHtml(subscription.status)}</span>
    </header>
    <section class="account-summary" aria-label="Subscription summary">
      <article><span>Edition</span><strong>${escapeHtml(subscription.edition)}</strong><small>${escapeHtml(subscription.accountType)}</small></article>
      <article><span>Trial ends</span><strong>${formatDate(subscription.trialEndsAt)}</strong><small>Current period started ${formatDate(subscription.currentPeriodStartsAt)}</small></article>
      <article><span>Seats</span><strong>${subscription.assignedSeats}${subscription.seatLimit ? ` / ${subscription.seatLimit}` : ''}</strong><small>assigned of your limit</small></article>
    </section>
    <div class="account-profile-forms">
      <div class="account-form">
        <h2>Entitlements</h2>
        <table class="account-table">
          <thead><tr><th>Product</th><th>Enabled</th><th>Limit</th></tr></thead>
          <tbody>${subscription.entitlements.map((entitlement) => `
            <tr>
              <td>${escapeHtml(entitlement.capability)}</td>
              <td>${entitlement.enabled ? 'Yes' : 'No'}</td>
              <td>${entitlement.limit === null ? 'Unlimited' : `${entitlement.limit}${entitlement.unit ? ` ${escapeHtml(entitlement.unit)}` : ''}`}</td>
            </tr>
          `).join('')}</tbody>
        </table>
      </div>
      <div class="account-form">
        <h2>Seats</h2>
        <table class="account-table">
          <thead><tr><th>Member</th><th>Status</th><th>Seat assigned</th></tr></thead>
          <tbody>${state.seats.map((seat) => `
            <tr>
              <td>${escapeHtml(seat.displayName)}<br><small>${escapeHtml(seat.email)}</small></td>
              <td>${escapeHtml(seat.membershipStatus)}</td>
              <td>${seat.assigned ? 'Yes' : 'No'}</td>
            </tr>
          `).join('') || '<tr><td colspan="3">No seats yet.</td></tr>'}</tbody>
        </table>
      </div>
    </div>
  `;
}
