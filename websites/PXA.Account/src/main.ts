import './site.css';
import { extractCampaignContext } from '../../shared/campaignAttribution.js';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';
import { sanitizeReturnUrl } from '../../shared/returnUrl.js';
import { appendSignedInSignal } from '../../shared/signedInSignal.js';
import {
  ApiError,
  confirmEmailChange,
  confirmPasswordReset,
  currentUser,
  login,
  logout,
  register,
  requestPasswordReset,
  resendVerification,
  verifyEmail,
} from './api';
import type { UserInfo } from './api';
import { bindShellEvents, closeAccountNavigation, navigation, renderShell } from './shell';
import { bindClosureEvents, closurePage } from './pages/closure';
import { dashboardPage } from './pages/dashboard';
import { bindDeveloperAccessEvents, developerAccessPage } from './pages/developerAccess';
import { bindLicensesEvents, licensesPage } from './pages/licenses';
import { bindOrganizationEvents, organizationPage } from './pages/organization';
import { bindProfileEvents, profilePage } from './pages/profile';
import { bindSecurityEvents, securityPage } from './pages/security';
import { subscriptionPage } from './pages/subscription';
import { supportPage } from './pages/support';
import { usagePage } from './pages/usage';

// Stable Problem Details codes from PXA.WebApi.Infrastructure.PxaApiProblems.
const PROBLEM_CODE_VERIFICATION_REQUIRED = 'PXAAPI010';

interface PortalPage {
  render: (user: UserInfo) => string;
  bind?: () => void;
  title: string;
}

const portalPages: Record<string, PortalPage> = {
  '/dashboard': { render: dashboardPage, title: 'Dashboard' },
  '/profile': { render: profilePage, bind: bindProfileEvents, title: 'Profile' },
  '/organization': { render: organizationPage, bind: bindOrganizationEvents, title: 'Organization' },
  '/subscription': { render: subscriptionPage, title: 'Subscription' },
  '/usage': { render: usagePage, title: 'Usage' },
  '/licenses': { render: licensesPage, bind: bindLicensesEvents, title: 'Licenses' },
  '/developer-access': { render: developerAccessPage, bind: bindDeveloperAccessEvents, title: 'Developer access' },
  '/security': { render: securityPage, bind: bindSecurityEvents, title: 'Security' },
  '/support': { render: supportPage, title: 'Support' },
  // Not in the primary nav (reached via a link on /support) but still a
  // full portal route: shell-rendered when authenticated, login-redirected
  // when not.
  '/closure': { render: closurePage, bind: bindClosureEvents, title: 'Account closure' },
};
const portalPaths = new Set([...navigation.map((item) => item.path), '/closure']);

interface AccountState {
  user: UserInfo | null;
  loading: boolean;
  notice: string;
  verificationStarted: boolean;
}

const app = document.querySelector<HTMLElement>('#app')!;
const state: AccountState = { user: null, loading: true, notice: '', verificationStarted: false };

async function handleLogout(): Promise<void> {
  await logout();
  state.user = null;
  navigate('/login', true);
}

function consumeReturnUrl(): string | null {
  return sanitizeReturnUrl(new URLSearchParams(location.search).get('returnUrl'));
}

function withSignedInSignal(url: string): string {
  return appendSignedInSignal(url, new URL(siteLinks.company).origin);
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
  history[replace ? 'replaceState' : 'pushState']({}, '', path);
  render();
}

function authLayout(content: string, title: string, description: string): string {
  document.title = `${title} | PXA Account`;
  return `
    <main class="account-auth-shell">
      <section class="account-brand-panel" aria-label="Power Dox Automation">
        <a class="account-brand" href="${siteLinks.company}"><span>PXA</span> Power Dox Automation</a>
        <div>
          <p class="pxa-kicker">Customer account</p>
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
    <header><p class="pxa-kicker">Welcome back</p><h2>Sign in</h2><p>Access your PXA workspace, Trial, and developer resources.</p></header>
    ${message('info', state.notice)}
    <form class="account-form" id="login-form">
      <label>Email<input name="identifier" type="email" autocomplete="username" required autofocus></label>
      <label>Password<input name="password" type="password" autocomplete="current-password" required></label>
      <label class="account-checkbox"><input name="rememberMe" type="checkbox"> Keep me signed in</label>
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">Sign in</button>
    </form>
    <div class="account-form-links"><a href="/forgot-password">Forgot password?</a><span>New to PXA? <a href="/register">Start free trial</a></span></div>
  `, 'Sign in to PXA', 'Continue your document automation work with one secure customer identity.');
}

function registerPage(): string {
  return authLayout(`
    <header><p class="pxa-kicker">30-day Trial</p><h2>Create your account</h2><p>Choose a personal workspace or create a workspace for your company.</p></header>
    <form class="account-form" id="register-form">
      <fieldset class="account-account-type">
        <legend>Account type</legend>
        <label><input name="accountType" type="radio" value="IndividualDeveloper" checked><span><strong>Individual Developer</strong><small>One personal workspace and seat</small></span></label>
        <label><input name="accountType" type="radio" value="Company"><span><strong>Company</strong><small>A shared organization for your team</small></span></label>
      </fieldset>
      <div class="account-form-grid">
        <label>Full name<input name="displayName" autocomplete="name" required maxlength="200"></label>
        <label>Work email<input name="email" type="email" autocomplete="email" required></label>
      </div>
      <div class="account-company-fields" id="company-fields" hidden>
        <label>Company name<input name="companyName" autocomplete="organization" maxlength="200"></label>
        <label>Workspace identifier<input name="organizationSlug" pattern="[A-Za-z0-9-]{3,80}" placeholder="example-company"></label>
      </div>
      <div class="account-form-grid">
        <label>Country<input name="country" autocomplete="country-name"></label>
        <label>Language<select name="locale"><option value="en">English</option><option value="de">Deutsch</option></select></label>
      </div>
      <label>Password<input name="password" type="password" autocomplete="new-password" minlength="12" required><small>At least 12 characters with uppercase, lowercase, number, and symbol.</small></label>
      <label class="account-checkbox"><input name="acceptTerms" type="checkbox" required> I accept the <a href="${companyPage('terms')}" target="_blank">Terms</a>.</label>
      <label class="account-checkbox"><input name="acceptPrivacy" type="checkbox" required> I acknowledge the <a href="${companyPage('privacy')}" target="_blank">Privacy notice</a>.</label>
      <label class="account-checkbox"><input name="subscribeToNewsletter" type="checkbox"> Send me product news and updates (optional).</label>
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">Create account and Trial</button>
    </form>
    <div class="account-form-links"><span>Already registered? <a href="/login">Sign in</a></span></div>
  `, 'Start with Power Dox Automation', 'Create a verified customer account and evaluate the connected PXA product family.');
}

function forgotPasswordPage(): string {
  return authLayout(`
    <header><p class="pxa-kicker">Account recovery</p><h2>Reset password</h2><p>We will send recovery instructions when the account is eligible.</p></header>
    ${message('info', state.notice)}
    <form class="account-form" id="forgot-form">
      <label>Email<input name="email" type="email" autocomplete="email" required autofocus></label>
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">Send reset instructions</button>
    </form>
    <div class="account-form-links"><a href="/login">Return to sign in</a></div>
  `, 'Recover your account', 'Secure recovery uses a short-lived, single-use action link.');
}

function resetPasswordPage(): string {
  const token = new URLSearchParams(location.search).get('token') || '';
  return authLayout(`
    <header><p class="pxa-kicker">Secure action</p><h2>Choose a new password</h2></header>
    <form class="account-form" id="reset-form" data-token="${escapeHtml(token)}">
      <label>New password<input name="password" type="password" autocomplete="new-password" minlength="12" required autofocus></label>
      <label>Confirm password<input name="confirmation" type="password" autocomplete="new-password" minlength="12" required></label>
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">Update password</button>
    </form>
  `, 'Set a new password', 'The reset link is single-use and expires automatically.');
}

function verificationPage(): string {
  return authLayout(`
    <div class="account-result" id="verification-result">
      <span class="account-progress" aria-hidden="true"></span>
      <p class="pxa-kicker">Email verification</p>
      <h2>Verifying your account</h2>
      <p>Please wait while PXA activates your customer identity.</p>
    </div>
  `, 'Verify your account', 'Complete the secure registration step before signing in.');
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

function bindForm(formId: string, handler: (data: FormData) => Promise<void>): void {
  document.querySelector(formId)?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const form = event.currentTarget as HTMLFormElement;
    const error = form.querySelector<HTMLElement>('#form-error')!;
    const button = form.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    error.hidden = true;
    error.innerHTML = '';
    button.disabled = true;
    try {
      await handler(new FormData(form));
    } catch (requestError) {
      const apiError = requestError as ApiError;
      error.hidden = false;
      button.disabled = false;
      if (apiError.code === PROBLEM_CODE_VERIFICATION_REQUIRED) {
        const email = formString(new FormData(form), 'identifier');
        error.innerHTML = `${escapeHtml(apiError.message)} <button type="button" class="account-link-button" id="resend-verification-button">Resend verification email</button>`;
        document.querySelector('#resend-verification-button')?.addEventListener('click', async (clickEvent) => {
          (clickEvent.currentTarget as HTMLButtonElement).disabled = true;
          try { await resendVerification(email); }
          finally {
            state.notice = 'If the account is eligible, a new verification message will be sent shortly.';
            navigate('/login', true);
          }
        });
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
  bindForm('#login-form', async (data) => {
    const response = await login(formString(data, 'identifier'), formString(data, 'password'), data.get('rememberMe') === 'on');
    state.user = response!.user;
    const target = consumeReturnUrl();
    if (target) { window.location.href = withSignedInSignal(target); return; }
    navigate('/dashboard', true);
  });
  bindForm('#register-form', async (data) => {
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
    });
    state.notice = response!.message; navigate('/login', true);
  });
  bindForm('#forgot-form', async (data) => {
    await requestPasswordReset(formString(data, 'email'));
    state.notice = 'If the account is eligible, reset instructions will arrive shortly.'; render();
  });
  bindForm('#reset-form', async (data) => {
    if (data.get('password') !== data.get('confirmation')) throw new Error('Passwords do not match.');
    const token = document.querySelector<HTMLFormElement>('#reset-form')!.dataset.token ?? '';
    await confirmPasswordReset(token, formString(data, 'password'));
    state.notice = 'Your password was updated. Sign in with the new password.'; navigate('/login', true);
  });
}

async function runVerification(): Promise<void> {
  if (state.verificationStarted) return;
  state.verificationStarted = true;
  const result = document.querySelector<HTMLElement>('#verification-result')!;
  try {
    await verifyEmail(new URLSearchParams(location.search).get('token') || '');
    result.innerHTML = '<p class="pxa-kicker">Account verified</p><h2>Your Trial is ready</h2><p>You can now sign in to your PXA account.</p><a class="pxa-button pxa-button--primary" href="/login">Sign in</a>';
  } catch (error) {
    const apiError = error as ApiError;
    result.innerHTML = `<p class="pxa-kicker">Verification failed</p><h2>We could not verify this link</h2><p>${escapeHtml(apiError.message)}</p><a class="pxa-button pxa-button--secondary" href="/register">Register again</a>`;
  }
  bindEvents();
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

function render(): void {
  if (state.loading) { app.innerHTML = '<main class="account-loading"><span class="account-progress"></span><p>Loading your account</p></main>'; return; }
  const path = location.pathname;
  if (state.user && ['/login', '/register', '/'].includes(path)) {
    const target = consumeReturnUrl();
    if (target) { window.location.href = withSignedInSignal(target); return; }
    navigate('/dashboard', true); return;
  }
  if (!state.user && portalPaths.has(path)) { navigate('/login', true); return; }
  const portalPage = portalPages[path];
  if (state.user && portalPage) {
    renderShell(app, portalPage.render(state.user), portalPage.title);
    bindShellEvents(handleLogout);
    bindEvents();
    portalPage.bind?.();
    return;
  }
  app.innerHTML = path === '/register' ? registerPage()
    : path === '/verify-email' ? verificationPage()
      : path === '/confirm-email' ? confirmEmailChangePage()
        : path === '/forgot-password' ? forgotPasswordPage()
          : path === '/reset-password' ? resetPasswordPage()
            : loginPage();
  bindEvents();
  if (path === '/verify-email') runVerification();
  if (path === '/confirm-email') runEmailChangeConfirmation();
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
  state.user = null;
  state.notice = 'Your session expired. Sign in again.';
  navigate('/login', true);
});

async function initialize(): Promise<void> {
  try { state.user = await currentUser(); }
  catch (error) { if ((error as ApiError).status !== 401) state.notice = 'PXA Account cannot reach the API.'; }
  state.loading = false;
  render();
}

initialize();
