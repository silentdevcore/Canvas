import { escapeHtml } from '../shell';
import { getAccountSubscription, getAccountSubscriptionSeats } from '../api';
import type { ApiError, AccountSubscriptionResponse, AccountSubscriptionSeatResponse } from '../api';
import { registerAccountStateReset } from '../accountContext';
import { companyPage } from '../../../shared/siteLinks.js';

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

function formatCapability(value: string): string {
  return value
    .replace(/[._-]+/g, ' ')
    .replace(/\b\w/g, (character) => character.toUpperCase());
}

function lifecycleDate(subscription: AccountSubscriptionResponse): {
  label: string;
  value: string;
  detail: string;
} {
  if (subscription.cancellationEffectiveAt) {
    return {
      label: 'Cancellation',
      value: formatDate(subscription.cancellationEffectiveAt),
      detail: 'Access remains available until this date',
    };
  }
  if (subscription.gracePeriodEndsAt) {
    return {
      label: 'Grace period ends',
      value: formatDate(subscription.gracePeriodEndsAt),
      detail: 'Resolve the subscription before access is restricted',
    };
  }
  if (subscription.trialEndsAt) {
    return {
      label: 'Trial ends',
      value: formatDate(subscription.trialEndsAt),
      detail: `Started ${formatDate(subscription.startsAt)}`,
    };
  }
  if (subscription.currentPeriodEndsAt) {
    return {
      label: subscription.billingPeriod === 'None' ? 'Current period ends' : 'Renews',
      value: formatDate(subscription.currentPeriodEndsAt),
      detail: `${subscription.billingPeriod} billing`,
    };
  }
  return {
    label: 'Started',
    value: formatDate(subscription.startsAt),
    detail: `${subscription.billingPeriod} billing`,
  };
}

function planAction(subscription: AccountSubscriptionResponse): {
  title: string;
  description: string;
  label: string;
  href: string;
} {
  if (subscription.edition === 'Enterprise') {
    return {
      title: 'Enterprise plan',
      description: 'Contact Sales for seat, entitlement, deployment, or support changes.',
      label: 'Contact Sales',
      href: companyPage('contact'),
    };
  }
  if (subscription.edition === 'Premium') {
    return {
      title: 'Need more capacity?',
      description: 'Review Enterprise options for additional seats, On-Premise deployment, and negotiated limits.',
      label: 'Explore Enterprise',
      href: companyPage('pricing'),
    };
  }
  return {
    title: subscription.edition === 'Trial' ? 'Continue after your Trial' : 'Unlock production features',
    description: 'Compare PXA editions and contact Sales when you are ready. Online checkout is not enabled yet.',
    label: 'Compare plans',
    href: companyPage('pricing'),
  };
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
  const lifecycle = lifecycleDate(subscription);
  const action = planAction(subscription);
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
      <article><span>${escapeHtml(lifecycle.label)}</span><strong>${escapeHtml(lifecycle.value)}</strong><small>${escapeHtml(lifecycle.detail)}</small></article>
      <article><span>Seats</span><strong>${subscription.assignedSeats}${subscription.seatLimit ? ` / ${subscription.seatLimit}` : ''}</strong><small>assigned of your limit</small></article>
      <article><span>Deployment</span><strong>${escapeHtml(subscription.deploymentMode)}</strong><small>${escapeHtml(subscription.billingPeriod)} billing</small></article>
    </section>
    <section class="account-section account-plan-action" aria-labelledby="subscription-action-title">
      <div>
        <h2 id="subscription-action-title">${escapeHtml(action.title)}</h2>
        <p>${escapeHtml(action.description)}</p>
      </div>
      <a class="pxa-button pxa-button--primary" href="${escapeHtml(action.href)}">${escapeHtml(action.label)}</a>
    </section>
    <div class="account-profile-forms">
      <div class="account-form">
        <h2>Entitlements</h2>
        <table class="account-table">
          <thead><tr><th>Product</th><th>Enabled</th><th>Limit</th><th>Available until</th></tr></thead>
          <tbody>${subscription.entitlements.map((entitlement) => `
            <tr>
              <td>${escapeHtml(formatCapability(entitlement.capability))}</td>
              <td>${entitlement.enabled ? 'Yes' : 'No'}</td>
              <td>${entitlement.limit === null ? 'Unlimited' : `${entitlement.limit}${entitlement.unit ? ` ${escapeHtml(entitlement.unit)}` : ''}`}</td>
              <td>${entitlement.expiresAt ? formatDate(entitlement.expiresAt) : 'Subscription term'}</td>
            </tr>
          `).join('') || '<tr><td colspan="4">No product entitlements are assigned.</td></tr>'}</tbody>
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
