import { escapeHtml } from '../shell';
import { accountLicenseDownloadUrl, getAccountLicenses, validateAccountLicense } from '../api';
import type { ApiError, AccountLicenseResponse } from '../api';
import { registerAccountStateReset } from '../accountContext';

interface LicensesPageState {
  licenses: AccountLicenseResponse[];
  loading: boolean;
  loaded: boolean;
  error: string | null;
  validation: Record<string, string>;
}

const state: LicensesPageState = { licenses: [], loading: false, loaded: false, error: null, validation: {} };
registerAccountStateReset(() => {
  Object.assign(state, {
    licenses: [],
    loading: false,
    loaded: false,
    error: null,
    validation: {},
  });
});

async function loadLicenses(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    state.licenses = (await getAccountLicenses()) ?? [];
  } catch (error) {
    state.error = (error as ApiError).message;
  } finally {
    state.loading = false;
    state.loaded = true;
    window.dispatchEvent(new Event('pxa:rerender'));
  }
}

function licenseRow(license: AccountLicenseResponse): string {
  const validity = state.validation[license.id];
  return `
    <tr>
      <td>${escapeHtml(license.licenseNumber)}<br><small>${escapeHtml(license.edition)} · ${escapeHtml(license.deploymentMode)}</small></td>
      <td>${escapeHtml(license.status)}</td>
      <td>${new Date(license.validFrom).toLocaleDateString()} – ${new Date(license.validUntil).toLocaleDateString()}</td>
      <td>${license.instanceLimit}</td>
      <td>
        <button class="pxa-button pxa-button--secondary account-license-validate" type="button" data-license-id="${escapeHtml(license.id)}">Validate</button>
        <a class="pxa-button pxa-button--secondary" href="${accountLicenseDownloadUrl(license.id)}">Download</a>
        ${validity ? `<div>${escapeHtml(validity)}</div>` : ''}
      </td>
    </tr>
  `;
}

export function licensesPage(): string {
  if (!state.loaded && !state.loading) loadLicenses();

  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>Licenses</h1>
        <p>View your offline licenses and download validation metadata.</p>
      </div>
    </header>
    <section class="account-section">
      <div>
        ${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : ''}
        <table class="account-table">
          <thead><tr><th>License</th><th>Status</th><th>Valid</th><th>Instances</th><th></th></tr></thead>
          <tbody>${state.licenses.map(licenseRow).join('') || `<tr><td colspan="5">${state.loading ? 'Loading…' : 'No offline licenses issued yet.'}</td></tr>`}</tbody>
        </table>
      </div>
    </section>
  `;
}

export function bindLicensesEvents(): void {
  document.querySelectorAll<HTMLButtonElement>('.account-license-validate').forEach((button) => {
    button.addEventListener('click', async () => {
      const licenseId = button.dataset.licenseId!;
      button.disabled = true;
      try {
        const result = await validateAccountLicense(licenseId);
        state.validation[licenseId] = result
          ? `${result.valid ? 'Valid' : 'Invalid'} (${result.code})`
          : 'Validation failed.';
      } catch (error) {
        state.validation[licenseId] = (error as ApiError).message;
      } finally {
        window.dispatchEvent(new Event('pxa:rerender'));
      }
    });
  });
}
