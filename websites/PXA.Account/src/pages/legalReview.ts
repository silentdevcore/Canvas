import { companyPage } from '../../../shared/siteLinks.js';
import type { AccountProfileResponse, ApiError } from '../api';
import { updateAccountConsent } from '../api';
import { escapeHtml } from '../shell';

export function legalReviewPage(profile: AccountProfileResponse): string {
  if (!profile.legalPolicyAvailable) {
    return `
      <header class="account-page-header">
        <div><p class="pxa-kicker">Legal review</p><h1>Legal documents unavailable</h1></div>
      </header>
      <section class="account-section" role="alert">
        <div>
          <h2>We cannot verify the current legal documents</h2>
          <p>Your account remains protected. Reload this page when the service is available again.</p>
        </div>
        <button class="pxa-button pxa-button--primary" id="legal-review-retry" type="button">Retry</button>
      </section>`;
  }

  return `
    <header class="account-page-header">
      <div>
        <p class="pxa-kicker">Action required</p>
        <h1>Review updated legal documents</h1>
        <p id="legal-review-description">Complete this review before continuing to your PXA workspace.</p>
      </div>
    </header>
    <form class="account-form account-legal-review" id="account-legal-review-form" aria-describedby="legal-review-description">
      ${profile.requiresTermsAcceptance ? `
        <section>
          <h2>Terms and Conditions ${escapeHtml(profile.currentTermsVersion)}</h2>
          <p>The Terms governing your use of PXA have changed.</p>
          <a href="${companyPage('terms')}" target="_blank" rel="noopener">Read the current Terms</a>
          <label class="account-checkbox">
            <input name="acceptTerms" type="checkbox" required>
            I accept the current Terms and Conditions.
          </label>
        </section>` : ''}
      ${profile.requiresPrivacyAcknowledgement ? `
        <section>
          <h2>Privacy Notice ${escapeHtml(profile.currentPrivacyVersion)}</h2>
          <p>The Privacy Notice has changed. This acknowledgement is not consent to marketing.</p>
          <a href="${companyPage('privacy')}" target="_blank" rel="noopener">Read the current Privacy Notice</a>
          <label class="account-checkbox">
            <input name="acceptPrivacy" type="checkbox" required>
            I acknowledge that I have received the current Privacy Notice.
          </label>
        </section>` : ''}
      <div class="account-form-error" role="alert" aria-live="assertive" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">Confirm and continue</button>
    </form>`;
}

export function bindLegalReviewEvents(
  profile: AccountProfileResponse,
  onCompleted: (updatedProfile: AccountProfileResponse) => void,
): void {
  document.querySelector('#legal-review-retry')?.addEventListener('click', () => location.reload());
  const form = document.querySelector<HTMLFormElement>('#account-legal-review-form');
  form?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const data = new FormData(form);
    const error = form.querySelector<HTMLElement>('.account-form-error')!;
    const button = form.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    error.hidden = true;
    button.disabled = true;
    button.setAttribute('aria-busy', 'true');
    try {
      const updatedProfile = await updateAccountConsent(
        profile.requiresTermsAcceptance ? data.get('acceptTerms') === 'on' : null,
        profile.requiresPrivacyAcknowledgement ? data.get('acceptPrivacy') === 'on' : null,
        profile.marketingConsent,
        profile.currentTermsVersionId,
        profile.currentPrivacyVersionId,
      );
      if (!updatedProfile)
        throw new Error('The legal review response was empty. Reload and try again.');
      onCompleted(updatedProfile);
    } catch (submitError) {
      const apiError = submitError as ApiError;
      error.textContent = apiError.status === 409
        ? 'The legal documents changed while you were reviewing them. Reload and review the current versions.'
        : apiError.message;
      error.hidden = false;
      error.tabIndex = -1;
      error.focus();
      button.disabled = false;
      button.removeAttribute('aria-busy');
    }
  });
}
