import { escapeHtml } from '../shell';
import {
  changePassword,
  getAccountProfile,
  requestEmailChange,
  updateDisplayName,
  updateLocale,
  updateAccountConsent,
} from '../api';
import type { ApiError, AccountProfileResponse } from '../api';

interface ProfilePageState {
  data: AccountProfileResponse | null;
  loading: boolean;
  loaded: boolean;
  error: string | null;
  emailChangeNotice: string | null;
}

const state: ProfilePageState = {
  data: null,
  loading: false,
  loaded: false,
  error: null,
  emailChangeNotice: null,
};

function rerender(): void {
  window.dispatchEvent(new Event('pxa:rerender'));
}

async function loadProfile(): Promise<void> {
  if (state.loading) return;
  state.loading = true;
  state.error = null;
  try {
    state.data = await getAccountProfile();
  } catch (error) {
    state.error = (error as ApiError).message;
  } finally {
    state.loading = false;
    state.loaded = true;
    rerender();
  }
}

export function profilePage(): string {
  if (!state.loaded && !state.loading) loadProfile();

  if (!state.data) {
    return `
      <header class="account-page-header"><div><p class="pxa-kicker">Customer workspace</p><h1>Profile</h1></div></header>
      <section class="account-section">
        <div>${state.error ? `<p role="alert">${escapeHtml(state.error)}</p>` : '<p>Loading your profile…</p>'}</div>
      </section>
    `;
  }

  const data = state.data;
  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Customer workspace</p>
        <h1>Profile</h1>
        <p>Update your display name, locale, email, and password.</p>
      </div>
    </header>
    <section class="account-profile-forms">
      <form class="account-form" id="profile-display-name-form">
        <h2>Display name</h2>
        <label>Display name<input name="displayName" value="${escapeHtml(data.displayName)}" required minlength="2" maxlength="200"></label>
        <div class="account-form-error" role="alert" hidden></div>
        <button class="pxa-button pxa-button--primary" type="submit">Save name</button>
      </form>
      <form class="account-form" id="profile-locale-form">
        <h2>Language</h2>
        <label>Language<select name="locale">
          <option value="en" ${data.locale === 'en' ? 'selected' : ''}>English</option>
          <option value="de" ${data.locale === 'de' ? 'selected' : ''}>Deutsch</option>
          <option value="fr" ${data.locale === 'fr' ? 'selected' : ''}>Français</option>
          <option value="es" ${data.locale === 'es' ? 'selected' : ''}>Español</option>
          <option value="it" ${data.locale === 'it' ? 'selected' : ''}>Italiano</option>
          <option value="ar" ${data.locale === 'ar' ? 'selected' : ''}>العربية</option>
        </select></label>
        <div class="account-form-error" role="alert" hidden></div>
        <button class="pxa-button pxa-button--primary" type="submit">Save language</button>
      </form>
      <form class="account-form" id="profile-consent-form">
        <h2>Privacy and communication</h2>
        ${data.requiresTermsAcceptance ? `
          <p class="account-message account-message--info">Updated Terms ${escapeHtml(data.currentTermsVersion)} require your acceptance.</p>
          <label class="account-checkbox"><input name="acceptTerms" type="checkbox" required> I accept the current Terms.</label>
        ` : `<p>Terms accepted: ${escapeHtml(data.termsAcceptedVersion ?? 'Not recorded')}</p>`}
        ${data.requiresPrivacyAcknowledgement ? `
          <p class="account-message account-message--info">The Privacy notice ${escapeHtml(data.currentPrivacyVersion)} has changed.</p>
          <label class="account-checkbox"><input name="acceptPrivacy" type="checkbox" required> I acknowledge the current Privacy notice.</label>
        ` : `<p>Privacy notice acknowledged: ${escapeHtml(data.privacyAcknowledgedVersion ?? 'Not recorded')}</p>`}
        <label class="account-checkbox"><input name="marketingConsent" type="checkbox" ${data.marketingConsent ? 'checked' : ''}> Send me product news and updates.</label>
        <div class="account-form-error" role="alert" hidden></div>
        <button class="pxa-button pxa-button--secondary" type="submit">Save preferences</button>
      </form>
      <form class="account-form" id="profile-email-form">
        <h2>Email address</h2>
        <label>Current email<input value="${escapeHtml(data.email)}" disabled></label>
        ${data.pendingEmail ? `<p class="account-message account-message--info">Confirmation pending for ${escapeHtml(data.pendingEmail)}.</p>` : ''}
        ${state.emailChangeNotice ? `<p class="account-message account-message--info">${escapeHtml(state.emailChangeNotice)}</p>` : ''}
        <label>New email address<input name="newEmail" type="email" required></label>
        <div class="account-form-error" role="alert" hidden></div>
        <button class="pxa-button pxa-button--secondary" type="submit">Request email change</button>
      </form>
      <form class="account-form" id="profile-password-form">
        <h2>Password</h2>
        <label>Current password<input name="currentPassword" type="password" autocomplete="current-password" required></label>
        <label>New password<input name="newPassword" type="password" autocomplete="new-password" minlength="12" required><small>At least 12 characters with uppercase, lowercase, number, and symbol.</small></label>
        <div class="account-form-error" role="alert" hidden></div>
        <button class="pxa-button pxa-button--secondary" type="submit">Change password</button>
      </form>
    </section>
  `;
}

function bindProfileForm(formId: string, handler: (data: FormData) => Promise<void>): void {
  const form = document.querySelector<HTMLFormElement>(formId);
  form?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const error = form.querySelector<HTMLElement>('.account-form-error')!;
    const button = form.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    error.hidden = true;
    error.textContent = '';
    button.disabled = true;
    try {
      await handler(new FormData(form));
    } catch (submitError) {
      error.textContent = (submitError as ApiError).message;
      error.hidden = false;
      button.disabled = false;
    }
  });
}

export function bindProfileEvents(): void {
  bindProfileForm('#profile-display-name-form', async (data) => {
    state.data = await updateDisplayName(String(data.get('displayName') ?? ''));
    rerender();
  });
  bindProfileForm('#profile-locale-form', async (data) => {
    state.data = await updateLocale(String(data.get('locale') ?? ''));
    rerender();
  });
  bindProfileForm('#profile-consent-form', async (data) => {
    state.data = await updateAccountConsent(
      state.data?.requiresTermsAcceptance ? data.get('acceptTerms') === 'on' : null,
      state.data?.requiresPrivacyAcknowledgement ? data.get('acceptPrivacy') === 'on' : null,
      data.get('marketingConsent') === 'on',
    );
    rerender();
  });
  bindProfileForm('#profile-email-form', async (data) => {
    await requestEmailChange(String(data.get('newEmail') ?? ''));
    state.emailChangeNotice = 'If the address is available, a confirmation message will be sent shortly.';
    rerender();
  });
  bindProfileForm('#profile-password-form', async (data) => {
    await changePassword(String(data.get('currentPassword') ?? ''), String(data.get('newPassword') ?? ''));
    // Changing the password revokes every active session, including this one -
    // a fresh sign-in is required, so this is a hard navigation, not a rerender.
    window.location.href = '/login';
  });
}
