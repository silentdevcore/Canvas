import { escapeHtml } from '../shell';
import { accountLicenseDownloadUrl, getAccountLicenses, validateAccountLicense,
  getAccountNativeLicenses, getAccountNativeLicense, accountNativeLicenseDownloadUrl, renewAccountNativeLicenseFree } from '../api';
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
  nativeRenewing: boolean;
  nativeNotice: string | null;
}

let generation = 0;
const state: LicensesPageState = { licenses: [], loading: false, loaded: false, error: null, validation: {},
  native: [], nativeError: null, nativePage: 1, nativeTotal: 0, nativeDetail: null, nativePending: false, nativeNeedsReview: false, nativeRenewing: false, nativeNotice: null, organizationLicenses: true };
registerAccountStateReset(() => {
  generation++;
  Object.assign(state, {
    licenses: [],
    loading: false,
    loaded: false,
    error: null,
    validation: {},
    native: [], nativeError: null, nativePage: 1, nativeTotal: 0, nativeDetail: null, nativePending: false, nativeNeedsReview: false,
    nativeRenewing: false, nativeNotice: null,
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
    ${state.nativeNotice ? `<p role="status">${escapeHtml(state.nativeNotice)}</p>` : ''}
    ${state.nativePending ? '<p role="status">Your registration is recorded for license issuance.</p>' : ''}
    ${state.nativeNeedsReview ? '<p role="status">Your license issuance needs a review. Please contact support.</p>' : ''}
    <div class="account-table-scroll"><table class="account-table"><thead><tr><th>License</th><th>Model</th><th>Valid until / updates</th><th>Actions</th></tr></thead>
    <tbody>${state.native.map(item => `<tr><td>${escapeHtml(item.id)}</td><td>${escapeHtml(item.model)}</td>
      <td>${item.validUntil || item.updatesUntil ? new Date((item.validUntil || item.updatesUntil)!).toLocaleString() : 'No end date'}
      ${item.validUntil && Date.parse(item.validUntil) <= Date.now() ? '<br><strong>Term ended — open Details to check renewal.</strong>' : ''}</td>
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
      ${detail.gracePeriodDays === 0 ? '<dt>Expiry</dt><dd>No new processing is permitted at or after the signed expiry time.</dd>' : ''}
      <dt>Artifact SHA-256</dt><dd class="account-license-hash">${escapeHtml(detail.license.artifactSha256)}</dd></dl>
      ${detail.renewedByLicenseId ? `<p>A renewed license is available. <a href="${accountNativeLicenseDownloadUrl(detail.renewedByLicenseId)}">Download renewed license</a></p>` : ''}
      ${detail.freeRenewal ? `<p>Free renewal is available from ${escapeHtml(new Date(detail.freeRenewal.availableAt).toLocaleString())}.
        Each renewal is valid for three calendar months without an expiry grace period. Download the new file and load it in your offline application.</p>
        <button type="button" id="account-native-renew-free" data-license-id="${escapeHtml(detail.license.id)}"
          ${!detail.freeRenewal.canRenew || state.nativeRenewing ? 'disabled' : ''}>${state.nativeRenewing ? 'Renewing…' : 'Renew free license'}</button>` : ''}</div>` : ''}
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
  document.getElementById('account-native-renew-free')?.addEventListener('click', async () => {
    if (state.nativeRenewing || !state.nativeDetail?.freeRenewal?.canRenew) return;
    const current = generation;
    const id = state.nativeDetail.license.id;
    state.nativeRenewing = true; state.nativeError = null; state.nativeNotice = null;
    window.dispatchEvent(new Event('pvad:rerender'));
    try {
      const renewed = await renewAccountNativeLicenseFree(id);
      if (current !== generation || !renewed) return;
      const detail = await getAccountNativeLicense(renewed.id);
      if (current !== generation) return;
      state.nativeDetail = detail;
      state.nativeNotice = 'Your free license has been renewed. Download the new license and load it in your application.';
      state.nativePage = 1;
      await loadLicenses();
    } catch (error) {
      if (current === generation) state.nativeError = (error as ApiError).message;
    } finally {
      if (current === generation) { state.nativeRenewing = false; window.dispatchEvent(new Event('pvad:rerender')); }
    }
  });
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
