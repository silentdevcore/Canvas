import { escapeHtml } from '../shell';
import { accountLicenseDownloadUrl, getAccountLicenses, validateAccountLicense,
  getAccountNativeLicenses, getAccountNativeLicense, accountNativeLicenseDownloadUrl } from '../api';
import type { UserInfo, ApiError, AccountLicenseResponse, NativeLicenseResponse, NativeLicenseDetails } from '../api';
import { registerAccountStateReset } from '../accountContext';
import { displayDiagnosticCode, displaySystemMessage } from '../../../shared/diagnosticPresentation';

interface LicensesPageState {
  licenses: AccountLicenseResponse[];
  loading: boolean;
  loaded: boolean;
  error: string | null;
  validation: Record<string, string>;
  native: NativeLicenseResponse[];
  nativeError: string | null;
  nativePage: number;
  nativeTotal: number;
  nativeDetail: NativeLicenseDetails | null;
  organizationLicenses: boolean;
  nativePending: boolean;
  nativeNeedsReview: boolean;
}

let generation = 0;
const state: LicensesPageState = { licenses: [], loading: false, loaded: false, error: null, validation: {},
  native: [], nativeError: null, nativePage: 1, nativeTotal: 0, nativeDetail: null, nativePending: false, nativeNeedsReview: false, organizationLicenses: true };
registerAccountStateReset(() => {
  generation++;
  Object.assign(state, {
    licenses: [],
    loading: false,
    loaded: false,
    error: null,
    validation: {},
    native: [], nativeError: null, nativePage: 1, nativeTotal: 0, nativeDetail: null, nativePending: false, nativeNeedsReview: false,
  });
});

async function loadLicenses(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  const current = generation;
  state.error = null;
  state.nativeError = null;
  try {
    const licenses = state.organizationLicenses ? (await getAccountLicenses()) ?? [] : [];
    if (current === generation) state.licenses = licenses;
  } catch (error) {
    if (current === generation) state.error = (error as ApiError).message;
  }
  if (current !== generation) return;
  try {
    const result = await getAccountNativeLicenses(state.nativePage);
    if (current === generation) { state.native = result?.items ?? []; state.nativeTotal = result?.total ?? 0; state.nativePending = result?.automaticIssuancePending ?? false; state.nativeNeedsReview = result?.automaticIssuanceNeedsReview ?? false; }
  } catch (error) {
    if (current === generation) { state.nativeError = (error as ApiError).message; state.native = []; }
  } finally {
    if (current !== generation) return;
    state.loading = false;
    state.loaded = true;
    window.dispatchEvent(new Event('pvad:rerender'));
  }
}

function nativeLicensesSection(): string {
  const detail = state.nativeDetail;
  return `<section class="account-section account-licenses-section" aria-busy="${state.loading}"><h2>Native library licenses</h2>
    ${state.nativeError ? `<p role="alert">${escapeHtml(displaySystemMessage(state.nativeError))}</p>` : ''}
    ${state.nativePending ? '<p role="status">Your registration is recorded for license issuance.</p>' : ''}
    ${state.nativeNeedsReview ? '<p role="status">Your license issuance needs a review. Please contact support.</p>' : ''}
    <div class="account-table-scroll"><table class="account-table"><thead><tr><th>License</th><th>Model</th><th>Valid until / updates</th><th>Actions</th></tr></thead>
    <tbody>${state.native.map(item => `<tr><td>${escapeHtml(item.id)}</td><td>${escapeHtml(item.model)}</td>
      <td>${item.validUntil || item.updatesUntil ? new Date((item.validUntil || item.updatesUntil)!).toLocaleDateString() : 'No end date'}</td>
      <td><button type="button" class="account-native-detail" data-license-id="${escapeHtml(item.id)}">Details</button>
      <a class="pvad-button pvad-button--secondary" href="${accountNativeLicenseDownloadUrl(item.id)}">Download</a></td></tr>`).join('') || `<tr><td colspan="4">${state.loading ? 'Loading...' : 'No native library licenses issued.'}</td></tr>`}</tbody></table></div>
    <div class="account-actions"><button id="account-native-previous" ${state.loading || state.nativePage <= 1 ? 'disabled' : ''}>Previous</button>
      <span>Page ${state.nativePage} / ${Math.max(1, Math.ceil(state.nativeTotal / 25))}</span>
      <button id="account-native-next" ${state.loading || state.nativePage * 25 >= state.nativeTotal ? 'disabled' : ''}>Next</button></div>
    ${detail ? `<div><h3>Native license details</h3><dl class="account-native-metadata"><dt>License</dt><dd>${escapeHtml(detail.license.id)}</dd>
      <dt>Owner</dt><dd>${detail.license.ownerUserId ? 'Personal license' : 'Organization license'}</dd>
      <dt>Products</dt><dd>${detail.grants.map(grant => `${escapeHtml(grant.product)}${grant.providers.length ? ` (${grant.providers.map(escapeHtml).join(', ')})` : ''}`).join('<br>')}</dd>
      <dt>Redistribution</dt><dd>${detail.redistributionAllowed ? 'Permitted' : 'Not permitted'}</dd>
      <dt>Feature rights</dt><dd>${detail.accountScope
        ? detail.accountScope.capabilities.map(escapeHtml).join(', ')
        : 'Legacy product-level license'}</dd>
      <dt>Artifact SHA-256</dt><dd class="account-license-hash">${escapeHtml(detail.license.artifactSha256)}</dd></dl>
      ${detail.renewedByLicenseId ? '<p>A renewed license is available. The original artifact remains unchanged.</p>' : ''}</div>` : ''}
  </section>`;
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
        <button class="pvad-button pvad-button--secondary account-license-validate" type="button" data-license-id="${escapeHtml(license.id)}">Validate</button>
        <a class="pvad-button pvad-button--secondary" href="${accountLicenseDownloadUrl(license.id)}">Download</a>
        ${validity ? `<div>${escapeHtml(displaySystemMessage(validity))}</div>` : ''}
      </td>
    </tr>
  `;
}

export function licensesPage(user?: UserInfo): string {
  state.organizationLicenses = user ? Boolean(user.activeOrganizationId && user.permissions.includes('account.licenses.read')) : true;
  if (!state.loaded && !state.loading) loadLicenses();

  return `
    <header class="account-page-header">
      <div>
        <p class="pvad-kicker">Customer workspace</p>
        <h1>Licenses</h1>
        <p>Personal and organization licenses</p>
      </div>
    </header>
    ${state.organizationLicenses ? `<section class="account-section account-licenses-section">
      <h2>Server offline licenses</h2>
      <div>
        ${state.error ? `<p role="alert">${escapeHtml(displaySystemMessage(state.error))}</p>` : ''}
        <table class="account-table">
          <thead><tr><th>License</th><th>Status</th><th>Valid</th><th>Instances</th><th></th></tr></thead>
          <tbody>${state.licenses.map(licenseRow).join('') || `<tr><td colspan="5">${state.loading ? 'Loading…' : 'No offline licenses issued yet.'}</td></tr>`}</tbody>
        </table>
      </div>
    </section>` : ''}
    ${nativeLicensesSection()}
  `;
}

export function bindLicensesEvents(): void {
  document.querySelectorAll<HTMLButtonElement>('.account-native-detail').forEach(button => button.addEventListener('click', async () => {
    const current = generation;
    button.disabled = true;
    try {
      const detail = await getAccountNativeLicense(button.dataset.licenseId!);
      if (current === generation) state.nativeDetail = detail;
    } catch (error) { if (current === generation) state.nativeError = (error as ApiError).message; }
    finally { if (current === generation) window.dispatchEvent(new Event('pvad:rerender')); }
  }));
  for (const [id, increment] of [['account-native-previous', -1], ['account-native-next', 1]] as const)
    document.getElementById(id)?.addEventListener('click', () => {
      if (state.loading) return;
      state.nativePage += increment; state.native = []; state.nativeDetail = null;
      void loadLicenses(); window.dispatchEvent(new Event('pvad:rerender'));
    });
  document.querySelectorAll<HTMLButtonElement>('.account-license-validate').forEach((button) => {
    button.addEventListener('click', async () => {
      const licenseId = button.dataset.licenseId!;
      const current = generation;
      button.disabled = true;
      try {
        const result = await validateAccountLicense(licenseId);
        if (current === generation) state.validation[licenseId] = result
          ? `${result.valid ? 'Valid' : 'Invalid'} (${displayDiagnosticCode(result.code)})`
          : 'Validation failed.';
      } catch (error) {
        if (current === generation) state.validation[licenseId] = (error as ApiError).message;
      } finally {
        if (current === generation) window.dispatchEvent(new Event('pvad:rerender'));
      }
    });
  });
}
