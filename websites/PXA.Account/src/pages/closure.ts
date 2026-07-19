import { escapeHtml } from '../shell';
import {
  cancelAccountClosure,
  getAccountClosureRequests,
  requestAccountClosure,
  requestOrganizationClosure,
} from '../api';
import type { ApiError, AccountClosureResponse } from '../api';

interface ClosurePageState {
  requests: AccountClosureResponse[];
  loading: boolean;
  loaded: boolean;
  error: string | null;
}

const state: ClosurePageState = { requests: [], loading: false, loaded: false, error: null };

function rerender(): void {
  window.dispatchEvent(new Event('pxa:rerender'));
}

async function loadRequests(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    state.requests = (await getAccountClosureRequests()) ?? [];
  } catch (error) {
    state.error = (error as ApiError).message;
  } finally {
    state.loading = false;
    state.loaded = true;
    rerender();
  }
}

function requestRow(request: AccountClosureResponse): string {
  return `
    <tr>
      <td>${escapeHtml(request.targetType)}</td>
      <td>${escapeHtml(request.status)}</td>
      <td>${new Date(request.requestedAt).toLocaleDateString()}</td>
      <td>${new Date(request.scheduledPurgeAt).toLocaleDateString()}</td>
      <td>${request.status === 'Pending'
        ? `<button class="pxa-button pxa-button--secondary account-closure-cancel" type="button" data-request-id="${escapeHtml(request.id)}">Cancel</button>`
        : ''}</td>
    </tr>
  `;
}

export function closurePage(): string {
  if (!state.loaded && !state.loading) loadRequests();

  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>Account closure</h1>
        <p>Request closure of your personal account or your organization. Requests can be cancelled until the scheduled date.</p>
      </div>
    </header>
    <section class="account-form">
      ${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : ''}
      <table class="account-table">
        <thead><tr><th>Target</th><th>Status</th><th>Requested</th><th>Scheduled for</th><th></th></tr></thead>
        <tbody>${state.requests.map(requestRow).join('') || `<tr><td colspan="5">${state.loading ? 'Loading…' : 'No closure requests.'}</td></tr>`}</tbody>
      </table>
      <div class="account-actions" style="margin-top: var(--pxa-space-6)">
        <button class="pxa-button pxa-button--secondary" id="request-account-closure-button" type="button">Request account closure</button>
        <button class="pxa-button pxa-button--secondary" id="request-organization-closure-button" type="button">Request organization closure</button>
      </div>
    </section>
  `;
}

export function bindClosureEvents(): void {
  document.querySelector('#request-account-closure-button')?.addEventListener('click', async (event) => {
    const button = event.currentTarget as HTMLButtonElement;
    if (!window.confirm('Request closure of your personal account?')) return;
    button.disabled = true;
    try {
      await requestAccountClosure(null);
      await loadRequests();
    } catch (error) {
      state.error = (error as ApiError).message;
      button.disabled = false;
      rerender();
    }
  });

  document.querySelector('#request-organization-closure-button')?.addEventListener('click', async (event) => {
    const button = event.currentTarget as HTMLButtonElement;
    if (!window.confirm('Request closure of your organization? This affects every member.')) return;
    button.disabled = true;
    try {
      await requestOrganizationClosure(null);
      await loadRequests();
    } catch (error) {
      state.error = (error as ApiError).message;
      button.disabled = false;
      rerender();
    }
  });

  document.querySelectorAll<HTMLButtonElement>('.account-closure-cancel').forEach((button) => {
    button.addEventListener('click', async () => {
      const requestId = button.dataset.requestId!;
      button.disabled = true;
      try {
        await cancelAccountClosure(requestId);
        await loadRequests();
      } catch (error) {
        state.error = (error as ApiError).message;
        button.disabled = false;
        rerender();
      }
    });
  });
}
