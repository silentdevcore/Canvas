import { escapeHtml } from '../shell';
import {
  createAccountApiKey,
  createAccountServiceAccount,
  getAccountServiceAccounts,
  revokeAccountApiKey,
  revokeAccountServiceAccount,
} from '../api';
import type { ApiError, AccountApiKeyResponse, AccountServiceAccountResponse } from '../api';

interface DeveloperAccessPageState {
  accounts: AccountServiceAccountResponse[];
  loading: boolean;
  loaded: boolean;
  error: string | null;
  revealedSecret: { serviceAccountId: string; keyId: string; secret: string } | null;
}

const state: DeveloperAccessPageState = {
  accounts: [],
  loading: false,
  loaded: false,
  error: null,
  revealedSecret: null,
};

function rerender(): void {
  window.dispatchEvent(new Event('pxa:rerender'));
}

async function loadServiceAccounts(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    state.accounts = (await getAccountServiceAccounts()) ?? [];
  } catch (error) {
    state.error = (error as ApiError).message;
  } finally {
    state.loading = false;
    state.loaded = true;
    rerender();
  }
}

function keyRow(serviceAccountId: string, key: AccountApiKeyResponse): string {
  const revealed = state.revealedSecret?.keyId === key.id ? state.revealedSecret.secret : null;
  return `
    <tr>
      <td>${escapeHtml(key.name)}<br><small>${escapeHtml(key.prefix)}…</small></td>
      <td>${key.revokedAt ? 'Revoked' : key.expiresAt && new Date(key.expiresAt) < new Date() ? 'Expired' : 'Active'}</td>
      <td>${key.lastUsedAt ? new Date(key.lastUsedAt).toLocaleString() : 'Never'}</td>
      <td>
        ${revealed ? `<code class="account-secret-reveal">${escapeHtml(revealed)}</code><p><small>Copy this now — it will not be shown again.</small></p>` : ''}
        ${key.revokedAt ? '' : `<button class="pxa-button pxa-button--secondary account-key-revoke" type="button" data-service-account-id="${escapeHtml(serviceAccountId)}" data-key-id="${escapeHtml(key.id)}">Revoke</button>`}
      </td>
    </tr>
  `;
}

function accountBlock(account: AccountServiceAccountResponse): string {
  return `
    <div class="account-form">
      <h2>${escapeHtml(account.name)} ${account.isActive ? '' : '<span class="account-status">Revoked</span>'}</h2>
      <table class="account-table">
        <thead><tr><th>Key</th><th>Status</th><th>Last used</th><th></th></tr></thead>
        <tbody>${account.keys.map((key) => keyRow(account.id, key)).join('') || '<tr><td colspan="4">No keys yet.</td></tr>'}</tbody>
      </table>
      ${account.isActive ? `
        <form class="account-create-key-form" data-service-account-id="${escapeHtml(account.id)}">
          <label>New key name<input name="name" required maxlength="160"></label>
          <button class="pxa-button pxa-button--secondary" type="submit">Create key</button>
        </form>
        <button class="pxa-button pxa-button--secondary account-service-account-revoke" type="button" data-service-account-id="${escapeHtml(account.id)}">Revoke service account</button>
      ` : ''}
    </div>
  `;
}

export function developerAccessPage(): string {
  if (!state.loaded && !state.loading) loadServiceAccounts();

  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>Developer access</h1>
        <p>Create and revoke service accounts and API keys for your organization.</p>
      </div>
    </header>
    <section class="account-profile-forms">
      ${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : ''}
      ${state.accounts.map(accountBlock).join('') || (state.loading ? '<p>Loading…</p>' : '')}
      <form class="account-form" id="create-service-account-form">
        <h2>New service account</h2>
        <label>Name<input name="name" required maxlength="160"></label>
        <div class="account-form-error" role="alert" hidden></div>
        <button class="pxa-button pxa-button--primary" type="submit">Create service account</button>
      </form>
    </section>
  `;
}

export function bindDeveloperAccessEvents(): void {
  const createForm = document.querySelector<HTMLFormElement>('#create-service-account-form');
  createForm?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const error = createForm.querySelector<HTMLElement>('.account-form-error')!;
    error.hidden = true;
    try {
      const name = String(new FormData(createForm).get('name') ?? '');
      await createAccountServiceAccount(name);
      await loadServiceAccounts();
    } catch (submitError) {
      error.textContent = (submitError as ApiError).message;
      error.hidden = false;
    }
  });

  document.querySelectorAll<HTMLFormElement>('.account-create-key-form').forEach((form) => {
    form.addEventListener('submit', async (event) => {
      event.preventDefault();
      const serviceAccountId = form.dataset.serviceAccountId!;
      const name = String(new FormData(form).get('name') ?? '');
      try {
        const key = await createAccountApiKey(serviceAccountId, name, null);
        if (key) state.revealedSecret = { serviceAccountId, keyId: key.id, secret: key.secret };
        await loadServiceAccounts();
      } catch (error) {
        state.error = (error as ApiError).message;
        rerender();
      }
    });
  });

  document.querySelectorAll<HTMLButtonElement>('.account-key-revoke').forEach((button) => {
    button.addEventListener('click', async () => {
      const serviceAccountId = button.dataset.serviceAccountId!;
      const keyId = button.dataset.keyId!;
      button.disabled = true;
      try {
        await revokeAccountApiKey(serviceAccountId, keyId);
        if (state.revealedSecret?.keyId === keyId) state.revealedSecret = null;
        await loadServiceAccounts();
      } catch (error) {
        state.error = (error as ApiError).message;
        button.disabled = false;
        rerender();
      }
    });
  });

  document.querySelectorAll<HTMLButtonElement>('.account-service-account-revoke').forEach((button) => {
    button.addEventListener('click', async () => {
      const serviceAccountId = button.dataset.serviceAccountId!;
      button.disabled = true;
      try {
        await revokeAccountServiceAccount(serviceAccountId);
        await loadServiceAccounts();
      } catch (error) {
        state.error = (error as ApiError).message;
        button.disabled = false;
        rerender();
      }
    });
  });
}
