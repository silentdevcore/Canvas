import './site.css';
import { initializeBrowserTelemetry } from '../../shared/browserTelemetry.js';
import { extractCampaignContext } from '../../shared/campaignAttribution.js';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';
import { sanitizeReturnUrl } from '../../shared/returnUrl.js';
import { appendSignedInSignal } from '../../shared/signedInSignal.js';
import { accountLocale, accountLocales, setAccountLocale, tr } from './authI18n';
import {
  acceptInvitation,
  ApiError,
  confirmEmailChange,
  confirmPasswordReset,
  createDesignerHandoff,
  currentUser,
  getRegistrationPolicy,
  getAccountProfile,
  login,
  logout,
  register,
  requestPasswordReset,
  resendVerification,
  switchOrganization,
  verifyEmail,
} from './api';
import type { AccountProfileResponse, RegistrationPolicyResponse, UserInfo } from './api';
import { clearAccountContext, updateAccountContext } from './accountContext';
import { accountPermissions, hasAccountPermission, type AccountPermission } from './permissions';
import { bindShellEvents, closeAccountNavigation, navigation, renderShell } from './shell';
import { bindClosureEvents, closurePage } from './pages/closure';
import { dashboardPage } from './pages/dashboard';
import { bindDeveloperAccessEvents, developerAccessPage } from './pages/developerAccess';
import { bindLicensesEvents, licensesPage } from './pages/licenses';
import { bindLegalReviewEvents, legalReviewPage } from './pages/legalReview';
import { bindOrganizationEvents, organizationPage } from './pages/organization';
import { bindProfileEvents, profilePage } from './pages/profile';
import { bindSecurityEvents, securityPage } from './pages/security';
import { initializeStorageNotice } from '../../shared/storageNotice.js';
import { subscriptionPage } from './pages/subscription';
import { supportPage } from './pages/support';
import { usagePage } from './pages/usage';

initializeBrowserTelemetry({ application: 'account' });

// Stable Problem Details codes from PXA.WebApi.Infrastructure.PxaApiProblems.
const PROBLEM_CODE_VERIFICATION_REQUIRED = 'PXAAPI010';
const PROBLEM_CODE_ACCOUNT_DISABLED = 'PXAAPI015';
const PROBLEM_CODE_ORGANIZATION_SUSPENDED = 'PXAAPI016';

interface PortalPage {
  render: (user: UserInfo) => string;
  bind?: () => void;
  title: string;
  permission?: AccountPermission;
}

const portalPages: Record<string, PortalPage> = {
  '/dashboard': { render: dashboardPage, title: 'Dashboard' },
  '/profile': { render: profilePage, bind: bindProfileEvents, title: 'Profile', permission: accountPermissions.profileManage },
  '/organization': { render: organizationPage, bind: bindOrganizationEvents, title: 'Organization', permission: accountPermissions.organizationRead },
  '/subscription': { render: subscriptionPage, title: 'Subscription', permission: accountPermissions.subscriptionRead },
  '/usage': { render: usagePage, title: 'Usage', permission: accountPermissions.subscriptionRead },
  '/licenses': { render: licensesPage, bind: bindLicensesEvents, title: 'Licenses', permission: accountPermissions.licensesRead },
  '/developer-access': { render: developerAccessPage, bind: bindDeveloperAccessEvents, title: 'Developer access', permission: accountPermissions.serviceAccountsRead },
  '/security': { render: securityPage, bind: bindSecurityEvents, title: 'Security', permission: accountPermissions.sessionsManage },
  '/support': { render: supportPage, title: 'Support' },
  // Not in the primary nav (reached via a link on /support) but still a
  // full portal route: shell-rendered when authenticated, login-redirected
  // when not.
  '/closure': { render: closurePage, bind: bindClosureEvents, title: 'Account closure' },
};
const portalPaths = new Set([...navigation.map((item) => item.path), '/closure']);
portalPaths.add('/legal-review');

interface AccountState {
  user: UserInfo | null;
  loading: boolean;
  notice: string;
  registrationEmail: string;
  verificationStarted: boolean;
  designerAuthorizationStarted: boolean;
  accessDenied: ApiError | null;
  registrationPolicy: RegistrationPolicyResponse | null;
  registrationPolicyLocale: string | null;
  registrationPolicyLoading: boolean;
  registrationPolicyError: string;
  legalProfile: AccountProfileResponse | null;
  pendingReturnUrl: string | null;
}

const app = document.querySelector<HTMLElement>('#app')!;
const state: AccountState = {
  user: null,
  loading: true,
  notice: new URLSearchParams(location.search).get('reason') === 'session-expired'
    ? 'Your session expired. Sign in again.'
    : '',
  registrationEmail: '',
  verificationStarted: false,
  designerAuthorizationStarted: false,
  accessDenied: null,
  registrationPolicy: null,
  registrationPolicyLocale: null,
  registrationPolicyLoading: false,
  registrationPolicyError: '',
  legalProfile: null,
  pendingReturnUrl: null,
};

async function handleLogout(): Promise<void> {
  await logout();
  clearAccountContext();
  state.user = null;
  window.location.replace('/login');
}

async function handleOrganizationSwitch(organizationId: string): Promise<void> {
  try {
    const response = await switchOrganization(organizationId);
    state.user = response!.user;
    updateAccountContext(state.user);
    state.legalProfile = await getAccountProfile();
    state.accessDenied = null;
    navigate('/dashboard', true);
  } catch (error) {
    state.accessDenied = error as ApiError;
    render();
  }
}

function consumeReturnUrl(): string | null {
  return sanitizeReturnUrl(new URLSearchParams(location.search).get('returnUrl'));
}

function authPath(path: string, includeCampaign = false): string {
  const target = new URL(path, location.origin);
  const returnUrl = consumeReturnUrl();
  if (returnUrl) target.searchParams.set('returnUrl', returnUrl);
  if (includeCampaign) {
    const campaign = extractCampaignContext();
    Object.entries(campaign ?? {}).forEach(([key, value]) => target.searchParams.set(key, value));
  }
  return `${target.pathname}${target.search}`;
}

function withSignedInSignal(url: string): string {
  return appendSignedInSignal(url, new URL(siteLinks.company).origin);
}

function completeLegalReview(profile: AccountProfileResponse): void {
  state.legalProfile = profile;
  const target = state.pendingReturnUrl;
  state.pendingReturnUrl = null;
  if (target) {
    window.location.replace(withSignedInSignal(target));
    return;
  }
  navigate('/dashboard', true);
}

function escapeHtml(value: unknown = ''): string {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function formString(data: FormData, name: string): string {
  return String(data.get(name) ?? '');
}

function formStringOrNull(data: FormData, name: string): string | null {
  const value = data.get(name);
  return value ? String(value) : null;
}

function navigate(path: string, replace = false): void {
  state.accessDenied = null;
  history[replace ? 'replaceState' : 'pushState']({}, '', path);
  render();
}

function forbiddenPage(error?: ApiError | null): string {
  return `
    <header class="account-page-header">
      <div><p class="pxa-kicker">Access restricted</p><h1>You do not have access to this page</h1></div>
    </header>
    <section class="account-section account-forbidden" role="alert">
      <div>
        <p>${escapeHtml(error?.message || 'Your current organization role does not include the required permission.')}</p>
        <a class="pxa-button pxa-button--primary" href="/dashboard">Back to overview</a>
      </div>
    </section>`;
}

function authLayout(content: string, title: string, description: string): string {
  document.title = `${title} | PXA Account`;
  return `
    <main class="account-auth-shell">
      <section class="account-brand-panel" aria-label="Power Dox Automation">
        <a class="account-brand" href="${siteLinks.company}"><span>PXA</span> Power Dox Automation</a>
        <div>
          <p class="pxa-kicker">${tr('customerAccount')}</p>
          <h1>${title}</h1>
          <p>${description}</p>
        </div>
        <dl>
          <div><dt>Trial</dt><dd>30 days</dd></div>
          <div><dt>Products</dt><dd>One connected platform</dd></div>
          <div><dt>Account</dt><dd>Individual or Company</dd></div>
        </dl>
      </section>
      <section class="account-form-panel">
        <div class="account-form-wrap">${content}</div>
      </section>
    </main>`;
}

function message(kind: 'info' | 'error', text: string): string {
  return text ? `<div class="account-message account-message--${kind}" role="${kind === 'error' ? 'alert' : 'status'}">${escapeHtml(text)}</div>` : '';
}

function loginPage(): string {
  return authLayout(`
    <header><p class="pxa-kicker">${tr('customerAccount')}</p><h2>${tr('loginHeading')}</h2><p>${tr('loginDescription')}</p></header>
    ${message('info', state.notice)}
    <form class="account-form" id="login-form">
      <label>${tr('email')}<input name="identifier" type="email" autocomplete="username" required autofocus></label>
      <label>${tr('password')}<input name="password" type="password" autocomplete="current-password" required></label>
      <label class="account-checkbox"><input name="rememberMe" type="checkbox"> ${tr('rememberMe')}</label>
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">${tr('signIn')}</button>
      <a class="pxa-button pxa-button--secondary" href="${escapeHtml(authPath('/register', true))}">${tr('createAccount')}</a>
    </form>
    <div class="account-form-links"><a href="/forgot-password">${tr('forgotPassword')}</a></div>
  `, tr('loginTitle'), tr('loginDescription'));
}

function registerPage(): string {
  const locale = accountLocale();
  const policy = state.registrationPolicyLocale === locale ? state.registrationPolicy : null;
  const policyReady = policy?.available === true && policy.terms !== null && policy.privacy !== null;
  const policyMessage = state.registrationPolicyLoading
    ? message('info', tr('legalPolicyLoading'))
    : state.registrationPolicyError
      ? message('error', state.registrationPolicyError)
      : '';
  const termsVersion = policyReady ? ` <small>${tr('legalVersion')} ${escapeHtml(policy.terms!.version)}</small>` : '';
  const privacyVersion = policyReady ? ` <small>${tr('legalVersion')} ${escapeHtml(policy.privacy!.version)}</small>` : '';
  const languageNames: Record<string, string> = {
    en: 'English', de: 'Deutsch', fr: 'Français', es: 'Español', it: 'Italiano', ar: 'العربية',
  };
  return authLayout(`
    <header><p class="pxa-kicker">30-day Trial</p><h2>${tr('registerHeading')}</h2><p>${tr('registerDescription')}</p></header>
    <form class="account-form" id="register-form">
      <fieldset class="account-account-type">
        <legend>${tr('accountType')}</legend>
        <label><input name="accountType" type="radio" value="IndividualDeveloper" checked><span><strong>${tr('individual')}</strong><small>${tr('individualDescription')}</small></span></label>
        <label><input name="accountType" type="radio" value="Company"><span><strong>${tr('company')}</strong><small>${tr('companyDescription')}</small></span></label>
      </fieldset>
      <div class="account-form-grid">
        <label>${tr('fullName')}<input name="displayName" autocomplete="name" required maxlength="200"></label>
        <label>${tr('workEmail')}<input name="email" type="email" autocomplete="email" required></label>
      </div>
      <div class="account-company-fields" id="company-fields" hidden>
        <label>${tr('companyName')}<input name="companyName" autocomplete="organization" maxlength="200"></label>
        <label>${tr('workspaceId')}<input name="organizationSlug" pattern="[A-Za-z0-9-]{3,80}" placeholder="example-company"></label>
      </div>
      <div class="account-form-grid">
        <label>${tr('country')}<input name="country" inputmode="text" pattern="[A-Za-z]{2}" maxlength="2" placeholder="DE"></label>
        <label>${tr('language')}<select name="locale">${accountLocales.map((value) =>
          `<option value="${value}" ${locale === value ? 'selected' : ''}>${languageNames[value]}</option>`).join('')}</select></label>
      </div>
      <label>${tr('password')}<input name="password" type="password" autocomplete="new-password" minlength="12" required><small>${tr('passwordHelp')}</small></label>
      <label class="account-checkbox"><input name="acceptTerms" type="checkbox" required ${policyReady ? '' : 'disabled'}> <a href="${companyPage('terms')}" target="_blank">${tr('acceptTerms')}</a>${termsVersion}</label>
      <label class="account-checkbox"><input name="acceptPrivacy" type="checkbox" required ${policyReady ? '' : 'disabled'}> <a href="${companyPage('privacy')}" target="_blank">${tr('acceptPrivacy')}</a>${privacyVersion}</label>
      <label class="account-checkbox"><input name="subscribeToNewsletter" type="checkbox"> ${tr('marketing')}</label>
      ${policyMessage}
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit" ${policyReady ? '' : 'disabled'}>${tr('startTrial')}</button>
    </form>
    <div class="account-form-links"><span>${tr('alreadyRegistered')} <a href="${escapeHtml(authPath('/login'))}">${tr('signIn')}</a></span></div>
  `, tr('registerTitle'), tr('registerDescription'));
}

function registrationPendingPage(): string {
  return authLayout(`
    <header><p class="pxa-kicker">${tr('verificationTitle')}</p><h2>${tr('verificationHeading')}</h2><p>${tr('verificationDescription')}</p></header>
    ${message('info', state.notice)}
    <form class="account-form" id="resend-form">
      <label>${tr('email')}<input name="email" type="email" autocomplete="email" value="${escapeHtml(state.registrationEmail)}" required autofocus></label>
      <div class="account-form-error" id="form-error" role="alert" tabindex="-1" hidden></div>
      <button class="pxa-button pxa-button--secondary" type="submit">${tr('resend')}</button>
    </form>
    <div class="account-form-links"><a href="${escapeHtml(authPath('/login'))}">${tr('backToLogin')}</a></div>
  `, tr('verificationTitle'), tr('verificationDescription'));
}

function forgotPasswordPage(): string {
  return authLayout(`
    <header><p class="pxa-kicker">${tr('recoveryTitle')}</p><h2>${tr('recoveryHeading')}</h2><p>${tr('recoveryDescription')}</p></header>
    ${message('info', state.notice)}
    <form class="account-form" id="forgot-form">
      <label>${tr('email')}<input name="email" type="email" autocomplete="email" required autofocus></label>
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">${tr('sendReset')}</button>
    </form>
    <div class="account-form-links"><a href="/login">${tr('backToLogin')}</a></div>
  `, tr('recoveryTitle'), tr('recoveryDescription'));
}

function resetPasswordPage(): string {
  const token = new URLSearchParams(location.search).get('token') || '';
  return authLayout(`
    <header><p class="pxa-kicker">Secure action</p><h2>Choose a new password</h2></header>
    <form class="account-form" id="reset-form" data-token="${escapeHtml(token)}">
      <label>${tr('newPassword')}<input name="password" type="password" autocomplete="new-password" minlength="12" required autofocus></label>
      <label>${tr('confirmPassword')}<input name="confirmation" type="password" autocomplete="new-password" minlength="12" required></label>
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">${tr('updatePassword')}</button>
    </form>
  `, 'Set a new password', 'The reset link is single-use and expires automatically.');
}

function verificationPage(): string {
  return authLayout(`
    <div class="account-result" id="verification-result">
      <span class="account-progress" aria-hidden="true"></span>
      <p class="pxa-kicker">Email verification</p>
      <h2>${tr('verifying')}</h2>
      <p>${tr('verifyingDescription')}</p>
    </div>
  `, 'Verify your account', 'Complete the secure registration step before signing in.');
}

function invitationPage(): string {
  const token = new URLSearchParams(location.search).get('token') || '';
  const returnToInvitation = sanitizeReturnUrl(location.href);
  if (state.user) {
    return authLayout(`
      <div class="account-result" id="invitation-result">
        <p class="pxa-kicker">Organization invitation</p>
        <h2>Join the organization</h2>
        <p>Accept this invitation as ${escapeHtml(state.user.email)}.</p>
        <form class="account-form" id="invitation-form" data-token="${escapeHtml(token)}">
          <div class="account-form-error" id="form-error" role="alert" tabindex="-1" hidden></div>
          <button class="pxa-button pxa-button--primary" type="submit">Accept invitation</button>
        </form>
      </div>
    `, 'Accept your invitation', 'Membership is added to your existing PXA account without creating another workspace or Trial.');
  }
  const signInPath = returnToInvitation
    ? `/login?returnUrl=${encodeURIComponent(returnToInvitation)}`
    : '/login';
  return authLayout(`
    <header><p class="pxa-kicker">Organization invitation</p><h2>Complete your PXA account</h2><p>Set your name and password to accept the invitation.</p></header>
    <form class="account-form" id="invitation-form" data-token="${escapeHtml(token)}">
      <label>Full name<input name="displayName" autocomplete="name" maxlength="200"></label>
      <label>Password<input name="password" type="password" autocomplete="new-password" minlength="12" required></label>
      <label>Confirm password<input name="confirmation" type="password" autocomplete="new-password" minlength="12" required></label>
      <div class="account-form-error" id="form-error" role="alert" tabindex="-1" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">Accept invitation</button>
    </form>
    <div class="account-form-links"><span>Already have a PXA account? <a href="${escapeHtml(signInPath)}">Sign in first</a></span></div>
  `, 'Accept your invitation', 'Join the inviting organization without creating a separate workspace or Trial.');
}

function confirmEmailChangePage(): string {
  return authLayout(`
    <div class="account-result" id="confirm-email-result">
      <span class="account-progress" aria-hidden="true"></span>
      <p class="pxa-kicker">Email address change</p>
      <h2>Confirming your new email address</h2>
      <p>Please wait while PXA updates your customer identity.</p>
    </div>
  `, 'Confirm your new email', 'The confirmation link is single-use and expires automatically.');
}

function designerAuthorizationPage(): string {
  return authLayout(`
    <div class="account-result" id="designer-authorization-result">
      <span class="account-progress" aria-hidden="true"></span>
      <p class="pxa-kicker">Secure connection</p>
      <h2>Opening PXA Designer</h2>
      <p>Checking your organization and Designer access.</p>
    </div>
  `, 'Connect to PXA Designer', 'PXA Account securely transfers access without sharing your account cookie.');
}

function bindForm(formId: string, handler: (data: FormData) => Promise<void>): void {
  document.querySelector(formId)?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const form = event.currentTarget as HTMLFormElement;
    const error = form.querySelector<HTMLElement>('#form-error')!;
    const button = form.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    const originalButtonText = button.textContent ?? '';
    error.hidden = true;
    error.innerHTML = '';
    form.setAttribute('aria-busy', 'true');
    button.disabled = true;
    button.textContent = tr('working');
    try {
      await handler(new FormData(form));
    } catch (requestError) {
      const apiError = requestError as ApiError;
      form.querySelectorAll<HTMLInputElement>('input[type="password"]').forEach((input) => { input.value = ''; });
      error.hidden = false;
      error.tabIndex = -1;
      error.focus();
      form.removeAttribute('aria-busy');
      button.disabled = false;
      button.textContent = originalButtonText;
      if (apiError.code === PROBLEM_CODE_VERIFICATION_REQUIRED) {
        const email = formString(new FormData(form), 'identifier');
        error.innerHTML = `${escapeHtml(apiError.message)} <button type="button" class="account-link-button" id="resend-verification-button">Resend verification email</button>`;
        document.querySelector('#resend-verification-button')?.addEventListener('click', async (clickEvent) => {
          (clickEvent.currentTarget as HTMLButtonElement).disabled = true;
          try { await resendVerification(email, consumeReturnUrl()); }
          finally {
            state.notice = 'If the account is eligible, a new verification message will be sent shortly.';
            navigate(authPath('/login'), true);
          }
        });
        return;
      }
      if (apiError.code === PROBLEM_CODE_ACCOUNT_DISABLED) {
        error.innerHTML = `${escapeHtml(apiError.message)} <a href="${companyPage('support')}">Contact support</a>.`;
        return;
      }
      if (apiError.code === PROBLEM_CODE_ORGANIZATION_SUSPENDED) {
        error.innerHTML = `${escapeHtml(apiError.message)} <a href="${companyPage('support')}">Contact your administrator</a>.`;
        return;
      }
      error.textContent = apiError.message;
    }
  });
}

function bindEvents(): void {
  document.querySelectorAll<HTMLAnchorElement>('a[href^="/"]').forEach((link) => link.addEventListener('click', (event) => {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    closeAccountNavigation();
    navigate(link.getAttribute('href')!);
  }));
  document.querySelectorAll<HTMLInputElement>('input[name="accountType"]').forEach((input) => input.addEventListener('change', () => {
    const company = document.querySelector<HTMLInputElement>('input[name="accountType"]:checked')?.value === 'Company';
    const fields = document.querySelector<HTMLElement>('#company-fields')!;
    fields.hidden = !company;
    fields.querySelector<HTMLInputElement>('input[name="companyName"]')!.required = company;
  }));
  document.querySelector<HTMLSelectElement>('select[name="locale"]')?.addEventListener('change', (event) => {
    const locale = (event.currentTarget as HTMLSelectElement).value;
    setAccountLocale(locale);
    state.registrationPolicy = null;
    state.registrationPolicyLocale = null;
    state.registrationPolicyError = '';
    render();
  });
  bindForm('#login-form', async (data) => {
    const response = await login(formString(data, 'identifier'), formString(data, 'password'), data.get('rememberMe') === 'on');
    state.user = response!.user;
    updateAccountContext(state.user);
    const legalProfile = await getAccountProfile();
    if (!legalProfile)
      throw new Error('PXA Account returned an empty legal profile.');
    state.legalProfile = legalProfile;
    const target = consumeReturnUrl();
    if (target &&
        legalProfile.legalPolicyAvailable &&
        !legalProfile.requiresTermsAcceptance &&
        !legalProfile.requiresPrivacyAcknowledgement) {
      window.location.href = withSignedInSignal(target);
      return;
    }
    state.pendingReturnUrl = target;
    navigate('/dashboard', true);
  });
  bindForm('#register-form', async (data) => {
    const policy = state.registrationPolicy;
    if (!policy?.available || !policy.terms || !policy.privacy)
      throw new Error(tr('legalPolicyUnavailable'));
    const response = await register({
      accountType: formString(data, 'accountType'),
      displayName: formString(data, 'displayName'),
      email: formString(data, 'email'),
      password: formString(data, 'password'),
      companyName: formStringOrNull(data, 'companyName'),
      organizationSlug: formStringOrNull(data, 'organizationSlug'),
      country: formStringOrNull(data, 'country'),
      locale: formStringOrNull(data, 'locale'),
      acceptTerms: data.get('acceptTerms') === 'on',
      acceptPrivacy: data.get('acceptPrivacy') === 'on',
      subscribeToNewsletter: data.get('subscribeToNewsletter') === 'on',
      campaignContext: extractCampaignContext() as Record<string, string> | null,
      returnUrl: consumeReturnUrl(),
      termsVersionId: policy.terms.id,
      privacyVersionId: policy.privacy.id,
    });
    state.registrationEmail = formString(data, 'email');
    state.notice = response!.message;
    navigate(authPath('/registration-pending'), true);
  });
  bindForm('#resend-form', async (data) => {
    const email = formString(data, 'email');
    const response = await resendVerification(email, consumeReturnUrl());
    state.registrationEmail = email;
    state.notice = response!.message;
    render();
  });
  bindForm('#invitation-form', async (data) => {
    const password = formStringOrNull(data, 'password');
    if (password && password !== data.get('confirmation')) throw new Error('Passwords do not match.');
    const token = document.querySelector<HTMLFormElement>('#invitation-form')!.dataset.token ?? '';
    await acceptInvitation(token, password, formStringOrNull(data, 'displayName'));
    const result = document.querySelector<HTMLElement>('#invitation-result');
    if (result) {
      result.innerHTML = '<p class="pxa-kicker">Invitation accepted</p><h2>You joined the organization</h2><p>The organization is now available in your PXA account.</p><a class="pxa-button pxa-button--primary" href="/dashboard">Open Account</a>';
      bindEvents();
      return;
    }
    state.notice = 'Invitation accepted. Sign in with your new password.';
    navigate('/login', true);
  });
  bindForm('#forgot-form', async (data) => {
    await requestPasswordReset(formString(data, 'email'));
    state.notice = tr('resetSent'); render();
  });
  bindForm('#reset-form', async (data) => {
    if (data.get('password') !== data.get('confirmation')) throw new Error(tr('passwordMismatch'));
    const token = document.querySelector<HTMLFormElement>('#reset-form')!.dataset.token ?? '';
    await confirmPasswordReset(token, formString(data, 'password'));
    state.notice = tr('passwordUpdated'); navigate('/login', true);
  });
}

async function loadRegistrationPolicy(locale: string): Promise<void> {
  if (state.registrationPolicyLoading) return;
  state.registrationPolicyLoading = true;
  state.registrationPolicyError = '';
  render();
  try {
    const policy = await getRegistrationPolicy(locale);
    if (accountLocale() !== locale) return;
    state.registrationPolicy = policy;
    state.registrationPolicyLocale = locale;
  } catch (error) {
    if (accountLocale() !== locale) return;
    state.registrationPolicy = null;
    state.registrationPolicyLocale = locale;
    state.registrationPolicyError = (error as ApiError).message || tr('legalPolicyUnavailable');
  } finally {
    if (accountLocale() === locale) state.registrationPolicyLoading = false;
    render();
  }
}

async function runVerification(): Promise<void> {
  if (state.verificationStarted) return;
  state.verificationStarted = true;
  const result = document.querySelector<HTMLElement>('#verification-result')!;
  const token = new URLSearchParams(location.search).get('token') || '';
  if (!token) {
    result.innerHTML = `<p class="pxa-kicker">${tr('incompleteLink')}</p><h2>${tr('missingToken')}</h2><p>${tr('missingTokenDescription')}</p><a class="pxa-button pxa-button--secondary" href="${escapeHtml(authPath('/registration-pending'))}">${tr('requestAnother')}</a>`;
    bindEvents();
    return;
  }
  try {
    await verifyEmail(token);
    state.registrationEmail = '';
    result.innerHTML = `<p class="pxa-kicker">${tr('verified')}</p><h2>${tr('trialReady')}</h2><p>${tr('verifiedDescription')}</p><a class="pxa-button pxa-button--primary" href="${escapeHtml(authPath('/login'))}">${tr('signIn')}</a>`;
  } catch (error) {
    const apiError = error as ApiError;
    result.innerHTML = apiError.isOffline || apiError.status === 503
      ? `<p class="pxa-kicker">${tr('serviceUnavailable')}</p><h2>${tr('verificationUnavailable')}</h2><p>${tr('verificationUnavailableDescription')}</p><button class="pxa-button pxa-button--primary" id="retry-verification" type="button">${tr('retry')}</button>`
      : `<p class="pxa-kicker">${tr('expiredLink')}</p><h2>${tr('verificationFailed')}</h2><p>${tr('expiredLinkDescription')}</p><a class="pxa-button pxa-button--secondary" href="${escapeHtml(authPath('/registration-pending'))}">${tr('requestAnother')}</a>`;
  }
  bindEvents();
  document.querySelector('#retry-verification')?.addEventListener('click', () => {
    state.verificationStarted = false;
    render();
  });
}

async function runEmailChangeConfirmation(): Promise<void> {
  const result = document.querySelector<HTMLElement>('#confirm-email-result')!;
  try {
    await confirmEmailChange(new URLSearchParams(location.search).get('token') || '');
    result.innerHTML = '<p class="pxa-kicker">Email address updated</p><h2>Sign in with your new address</h2><p>Your other sessions were signed out as a precaution.</p><a class="pxa-button pxa-button--primary" href="/login">Sign in</a>';
  } catch (error) {
    const apiError = error as ApiError;
    result.innerHTML = `<p class="pxa-kicker">Confirmation failed</p><h2>We could not confirm this link</h2><p>${escapeHtml(apiError.message)}</p><a class="pxa-button pxa-button--secondary" href="/profile">Back to profile</a>`;
  }
  bindEvents();
}

async function runDesignerAuthorization(): Promise<void> {
  if (state.designerAuthorizationStarted) return;
  state.designerAuthorizationStarted = true;
  const result = document.querySelector<HTMLElement>('#designer-authorization-result')!;
  const parameters = new URLSearchParams(location.search);
  try {
    const response = await createDesignerHandoff({
      designerOrigin: parameters.get('designerOrigin') ?? '',
      returnPath: parameters.get('returnPath') ?? '',
      codeChallenge: parameters.get('codeChallenge') ?? '',
      state: parameters.get('state') ?? '',
    });
    window.location.replace(response!.redirectUrl);
  } catch (error) {
    const apiError = error as ApiError;
    result.innerHTML = `<p class="pxa-kicker">Designer unavailable</p><h2>Access could not be transferred</h2><p>${escapeHtml(apiError.message)}</p><a class="pxa-button pxa-button--secondary" href="/dashboard">Back to Account</a>`;
    bindEvents();
  }
}

function render(): void {
  if (state.loading) { app.innerHTML = '<main class="account-loading"><span class="account-progress"></span><p>Loading your account</p></main>'; return; }
  const path = location.pathname;
  const requiresLegalReview = state.user && state.legalProfile &&
    (!state.legalProfile.legalPolicyAvailable ||
      state.legalProfile.requiresTermsAcceptance ||
      state.legalProfile.requiresPrivacyAcknowledgement);
  if (requiresLegalReview && path !== '/legal-review') {
    navigate('/legal-review', true);
    return;
  }
  if (state.user && path === '/legal-review' && !requiresLegalReview) {
    navigate('/dashboard', true);
    return;
  }
  if (state.user && path === '/legal-review' && state.legalProfile) {
    renderShell(app, legalReviewPage(state.legalProfile), 'Legal review', state.user);
    bindShellEvents(handleLogout, handleOrganizationSwitch);
    bindLegalReviewEvents(state.legalProfile, completeLegalReview);
    return;
  }
  if (state.user && ['/login', '/register', '/'].includes(path)) {
    const target = consumeReturnUrl();
    if (target) { window.location.href = withSignedInSignal(target); return; }
    navigate('/dashboard', true); return;
  }
  if (!state.user && (portalPaths.has(path) || path === '/designer-authorize')) {
    const returnUrl = path === '/designer-authorize' ? location.href : null;
    navigate(returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/login', true);
    return;
  }
  const portalPage = portalPages[path];
  if (state.user && portalPage) {
    const permitted = !portalPage.permission || hasAccountPermission(state.user, portalPage.permission);
    const content = state.accessDenied || !permitted ? forbiddenPage(state.accessDenied) : portalPage.render(state.user);
    renderShell(app, content, permitted && !state.accessDenied ? portalPage.title : 'Access restricted', state.user);
    bindShellEvents(handleLogout, handleOrganizationSwitch);
    bindEvents();
    if (permitted && !state.accessDenied) portalPage.bind?.();
    return;
  }
  app.innerHTML = path === '/register' ? registerPage()
    : path === '/registration-pending' ? registrationPendingPage()
      : path === '/verify-email' ? verificationPage()
      : path === '/accept-invitation' ? invitationPage()
      : path === '/confirm-email' ? confirmEmailChangePage()
        : path === '/designer-authorize' ? designerAuthorizationPage()
        : path === '/forgot-password' ? forgotPasswordPage()
          : path === '/reset-password' ? resetPasswordPage()
            : loginPage();
  bindEvents();
  if (path === '/register' &&
      !state.registrationPolicyLoading &&
      state.registrationPolicyLocale !== accountLocale())
  {
    void loadRegistrationPolicy(accountLocale());
  }
  if (path === '/verify-email') runVerification();
  if (path === '/confirm-email') runEmailChangeConfirmation();
  if (path === '/designer-authorize') runDesignerAuthorization();
}

window.addEventListener('popstate', render);

// Page modules that load their own data asynchronously (e.g. profile.ts)
// dispatch this after a fetch/mutation completes to re-render in place,
// without importing main.ts and creating a circular module dependency.
window.addEventListener('pxa:rerender', render);

document.addEventListener('keydown', (event) => {
  if (event.key !== 'Escape') return;
  closeAccountNavigation(true);
});

window.addEventListener('pxa:session-expired', () => {
  if (!state.user) return;
  clearAccountContext();
  state.user = null;
  window.location.replace('/login?reason=session-expired');
});

window.addEventListener('pxa:access-denied', (event) => {
  state.accessDenied = (event as CustomEvent<ApiError>).detail;
  render();
});

async function initialize(): Promise<void> {
  try {
    state.user = await currentUser();
    updateAccountContext(state.user);
    state.legalProfile = await getAccountProfile();
  }
  catch (error) { if ((error as ApiError).status !== 401) state.notice = 'PXA Account cannot reach the API.'; }
  state.loading = false;
  render();
}

initialize();
initializeStorageNotice();
