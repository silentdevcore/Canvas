import './site.css';
import { companyPage, siteLinks } from '../../shared/siteLinks.js';
import {
  confirmPasswordReset,
  currentUser,
  login,
  logout,
  register,
  requestPasswordReset,
  verifyEmail,
} from './api.js';

const app = document.querySelector('#app');
const state = { user: null, loading: true, notice: '', verificationStarted: false };

function escapeHtml(value = '') {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

function navigate(path, replace = false) {
  history[replace ? 'replaceState' : 'pushState']({}, '', path);
  render();
}

function authLayout(content, title, description) {
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

function message(kind, text) {
  return text ? `<div class="account-message account-message--${kind}" role="${kind === 'error' ? 'alert' : 'status'}">${escapeHtml(text)}</div>` : '';
}

function loginPage() {
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

function registerPage() {
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
      <div class="account-form-error" id="form-error" role="alert" hidden></div>
      <button class="pxa-button pxa-button--primary" type="submit">Create account and Trial</button>
    </form>
    <div class="account-form-links"><span>Already registered? <a href="/login">Sign in</a></span></div>
  `, 'Start with Power Dox Automation', 'Create a verified customer account and evaluate the connected PXA product family.');
}

function forgotPasswordPage() {
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

function resetPasswordPage() {
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

function verificationPage() {
  return authLayout(`
    <div class="account-result" id="verification-result">
      <span class="account-progress" aria-hidden="true"></span>
      <p class="pxa-kicker">Email verification</p>
      <h2>Verifying your account</h2>
      <p>Please wait while PXA activates your customer identity.</p>
    </div>
  `, 'Verify your account', 'Complete the secure registration step before signing in.');
}

function dashboardPage() {
  const organizations = state.user.organizations || [];
  const active = organizations.find((item) => item.id === state.user.activeOrganizationId) || organizations[0];
  document.title = 'Dashboard | PXA Account';
  return `
    <div class="account-portal">
      <header class="account-header">
        <a class="account-brand" href="/dashboard"><span>PXA</span> Account</a>
        <nav aria-label="Account"><a aria-current="page" href="/dashboard">Overview</a><a href="${siteLinks.designer}">Designer</a><a href="${siteLinks.documentation}">Documentation</a></nav>
        <button class="pxa-button pxa-button--secondary" id="logout-button" type="button">Sign out</button>
      </header>
      <main class="account-content" id="main-content">
        <header class="account-page-header"><div><p class="pxa-kicker">Customer workspace</p><h1>${escapeHtml(active?.name || 'Your PXA account')}</h1><p>Manage your customer identity and continue into PXA products.</p></div><span class="account-status">Trial workspace</span></header>
        <section class="account-summary" aria-label="Account summary">
          <article><span>Account</span><strong>${escapeHtml(state.user.displayName)}</strong><small>${escapeHtml(state.user.email)}</small></article>
          <article><span>Role</span><strong>${escapeHtml(state.user.roles.join(', ') || 'Customer')}</strong><small>Application access remains separate from product entitlements</small></article>
          <article><span>Organizations</span><strong>${organizations.length}</strong><small>${escapeHtml(active?.slug || 'No active workspace')}</small></article>
        </section>
        <section class="account-section"><div><h2>Continue working</h2><p>Open the product surface or guidance that matches your next task.</p></div><div class="account-actions"><a class="pxa-button pxa-button--primary" href="${siteLinks.designer}">Open Designer</a><a class="pxa-button pxa-button--secondary" href="${siteLinks.demo}">Explore demos</a><a class="pxa-button pxa-button--secondary" href="${siteLinks.documentation}">Read documentation</a></div></section>
      </main>
    </div>`;
}

function bindForm(formId, handler) {
  document.querySelector(formId)?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const error = form.querySelector('#form-error');
    const button = form.querySelector('button[type="submit"]');
    error.hidden = true;
    button.disabled = true;
    try { await handler(new FormData(form)); }
    catch (requestError) { error.textContent = requestError.message; error.hidden = false; button.disabled = false; }
  });
}

function bindEvents() {
  document.querySelectorAll('a[href^="/"]').forEach((link) => link.addEventListener('click', (event) => {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault(); navigate(link.getAttribute('href'));
  }));
  document.querySelectorAll('input[name="accountType"]').forEach((input) => input.addEventListener('change', () => {
    const company = document.querySelector('input[name="accountType"]:checked')?.value === 'Company';
    const fields = document.querySelector('#company-fields');
    fields.hidden = !company;
    fields.querySelector('input[name="companyName"]').required = company;
  }));
  bindForm('#login-form', async (data) => {
    const response = await login(data.get('identifier'), data.get('password'), data.get('rememberMe') === 'on');
    state.user = response.user; navigate('/dashboard', true);
  });
  bindForm('#register-form', async (data) => {
    const response = await register({
      accountType: data.get('accountType'), displayName: data.get('displayName'), email: data.get('email'),
      password: data.get('password'), companyName: data.get('companyName') || null,
      organizationSlug: data.get('organizationSlug') || null, country: data.get('country') || null,
      locale: data.get('locale'), acceptTerms: data.get('acceptTerms') === 'on', acceptPrivacy: data.get('acceptPrivacy') === 'on',
    });
    state.notice = response.message; navigate('/login', true);
  });
  bindForm('#forgot-form', async (data) => {
    await requestPasswordReset(data.get('email'));
    state.notice = 'If the account is eligible, reset instructions will arrive shortly.'; render();
  });
  bindForm('#reset-form', async (data) => {
    if (data.get('password') !== data.get('confirmation')) throw new Error('Passwords do not match.');
    await confirmPasswordReset(document.querySelector('#reset-form').dataset.token, data.get('password'));
    state.notice = 'Your password was updated. Sign in with the new password.'; navigate('/login', true);
  });
  document.querySelector('#logout-button')?.addEventListener('click', async () => {
    await logout(); state.user = null; navigate('/login', true);
  });
}

async function runVerification() {
  if (state.verificationStarted) return;
  state.verificationStarted = true;
  const result = document.querySelector('#verification-result');
  try {
    await verifyEmail(new URLSearchParams(location.search).get('token') || '');
    result.innerHTML = '<p class="pxa-kicker">Account verified</p><h2>Your Trial is ready</h2><p>You can now sign in to your PXA account.</p><a class="pxa-button pxa-button--primary" href="/login">Sign in</a>';
  } catch (error) {
    result.innerHTML = `<p class="pxa-kicker">Verification failed</p><h2>We could not verify this link</h2><p>${escapeHtml(error.message)}</p><a class="pxa-button pxa-button--secondary" href="/register">Register again</a>`;
  }
  bindEvents();
}

function render() {
  if (state.loading) { app.innerHTML = '<main class="account-loading"><span class="account-progress"></span><p>Loading your account</p></main>'; return; }
  const path = location.pathname;
  if (state.user && ['/login', '/register', '/'].includes(path)) { navigate('/dashboard', true); return; }
  if (!state.user && path === '/dashboard') { navigate('/login', true); return; }
  app.innerHTML = path === '/register' ? registerPage()
    : path === '/verify-email' ? verificationPage()
      : path === '/forgot-password' ? forgotPasswordPage()
        : path === '/reset-password' ? resetPasswordPage()
          : path === '/dashboard' ? dashboardPage()
            : loginPage();
  bindEvents();
  if (path === '/verify-email') runVerification();
}

window.addEventListener('popstate', render);

async function initialize() {
  try { state.user = await currentUser(); }
  catch (error) { if (error.status !== 401) state.notice = 'PXA Account cannot reach the API.'; }
  state.loading = false;
  render();
}

initialize();
