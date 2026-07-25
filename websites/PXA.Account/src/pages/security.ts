import { escapeHtml } from '../shell';
import { getAccountSessions, revokeAccountSession, revokeAllAccountSessions } from '../api';
import type { ApiError, AccountSessionResponse } from '../api';
import { registerAccountStateReset } from '../accountContext';

interface SecurityPageState {
  sessions: AccountSessionResponse[];
  loading: boolean;
  loaded: boolean;
  error: string | null;
  notice: string | null;
}

const state: SecurityPageState = { sessions: [], loading: false, loaded: false, error: null, notice: null };
registerAccountStateReset(() => {
  Object.assign(state, {
    sessions: [],
    loading: false,
    loaded: false,
    error: null,
    notice: null,
  });
});

function rerender(): void {
  window.dispatchEvent(new Event('pxa:rerender'));
}

async function loadSessions(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    state.sessions = (await getAccountSessions()) ?? [];
  } catch (error) {
    state.error = (error as ApiError).message;
  } finally {
    state.loading = false;
    state.loaded = true;
    rerender();
  }
}

function sessionRow(session: AccountSessionResponse): string {
  return `
    <tr>
      <td>${escapeHtml(session.userAgent)}${session.isCurrent ? ' <strong>(current session)</strong>' : ''}</td>
      <td>${new Date(session.lastSeenAt).toLocaleString()}</td>
      <td>${session.isActive ? 'Active' : 'Revoked'}</td>
      <td>${session.isActive && !session.isCurrent
        ? `<button class="pxa-button pxa-button--secondary account-session-revoke" type="button" data-session-id="${escapeHtml(session.id)}">Sign out</button>`
        : ''}</td>
    </tr>
  `;
}

export function securityPage(): string {
  if (!state.loaded && !state.loading) loadSessions();

  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>Security</h1>
        <p>List and revoke your active browser sessions.</p>
      </div>
    </header>
    <section class="account-form">
      ${state.notice ? `<p class="account-message account-message--info">${escapeHtml(state.notice)}</p>` : ''}
      ${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : ''}
      <table class="account-table">
        <thead><tr><th>Device</th><th>Last active</th><th>Status</th><th></th></tr></thead>
        <tbody>${state.sessions.map(sessionRow).join('') || `<tr><td colspan="4">${state.loading ? 'Loading…' : 'No sessions found.'}</td></tr>`}</tbody>
      </table>
      <button class="pxa-button pxa-button--secondary" id="revoke-all-sessions-button" type="button">Sign out all other sessions</button>
    </section>
  `;
}

export function bindSecurityEvents(): void {
  document.querySelectorAll<HTMLButtonElement>('.account-session-revoke').forEach((button) => {
    button.addEventListener('click', async () => {
      const sessionId = button.dataset.sessionId!;
      button.disabled = true;
      try {
        await revokeAccountSession(sessionId);
        await loadSessions();
      } catch (error) {
        state.error = (error as ApiError).message;
        button.disabled = false;
        rerender();
      }
    });
  });

  document.querySelector('#revoke-all-sessions-button')?.addEventListener('click', async (event) => {
    const button = event.currentTarget as HTMLButtonElement;
    button.disabled = true;
    try {
      const result = await revokeAllAccountSessions();
      state.notice = `Signed out ${result?.revokedCount ?? 0} other session(s).`;
      await loadSessions();
    } catch (error) {
      state.error = (error as ApiError).message;
      button.disabled = false;
      rerender();
    }
  });
}
