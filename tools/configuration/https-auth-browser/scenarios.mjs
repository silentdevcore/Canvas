import assert from 'node:assert/strict';
import { createHash, randomBytes } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createInterface } from 'node:readline';
import { chromium } from '/deps/node_modules/playwright/index.mjs';
import { safeFailureEvidence } from './https-auth-evidence.mjs';

const { urls, password, fixture } = JSON.parse(await readFile('/config/config.json', 'utf8'));
await mkdir(process.env.HOME + '/.pki/nssdb', { recursive: true });
execFileSync('certutil', ['-N', '--empty-password', '-d', 'sql:' + process.env.HOME + '/.pki/nssdb'], { stdio: 'pipe' });
execFileSync('certutil', ['-A', '-t', 'C,,', '-n', 'PVAD isolated test CA', '-i', '/trust/ca.crt', '-d', 'sql:' + process.env.HOME + '/.pki/nssdb'], { stdio: 'pipe' });
const responses = new Map(); let nextId = 0;
createInterface({ input: process.stdin }).on('line', line => {
  try { const message = JSON.parse(line); const pending = responses.get(message.id); responses.delete(message.id);
    if (message.error) { for (const value of responses.values()) value.reject(new Error('Private coordinator failed.')); responses.clear(); }
    if (pending) message.error ? pending.reject(new Error('Private coordinator failed.')) : pending.resolve(message.result);
  } catch { /* The coordinator never supplies executable input. */ }
});
const control = (action, values = {}) => new Promise((resolve, reject) => {
  const id = String(++nextId); const timeout = setTimeout(() => reject(new Error('Private coordinator timed out.')), 120_000);
  responses.set(id, { resolve: result => { clearTimeout(timeout); resolve(result); }, reject: error => { clearTimeout(timeout); reject(error); } });
  process.stdout.write(JSON.stringify({ protocol: 'pvad-https-control.v1', id, action, ...values }) + '\n');
});
const checks = [], screenshots = [], contexts = [];
let blockedExternalRequests = 0;
const browser = await chromium.launch({ headless: true });
const context = async cookies => {
  const result = await browser.newContext({ locale: 'en-US', timezoneId: 'UTC', viewport: { width: 1440, height: 980 }, ignoreHTTPSErrors: false });
  result.setDefaultTimeout(45_000); contexts.push(result);
  await result.route('**/*', route => {
    const origin = new URL(route.request().url()).origin;
    if ([...Object.values(urls), 'https://untrusted.pvad.test:8443', 'https://wrong-host.pvad.test:8443'].includes(origin)) return route.continue();
    blockedExternalRequests++; return route.abort();
  });
  if (cookies) await result.addCookies(cookies);
  return result;
};
const checked = name => { checks.push(name); process.stderr.write(`HTTPS browser passed: ${name}\n`); };
const auth = '/api/pvad/v1/auth';
const designerHeaders = { 'X-PVAD-Application': 'designer' };
const expectStatus = async (client, base, path, status, headers = {}) => {
  const response = await client.get(base + path, { headers, maxRedirects: 0, timeout: 15_000 });
  assert.equal(response.status(), status, `Unexpected status for ${path}.`); return response;
};
const mutate = async (client, base, path, data, designer = false) => {
  const headers = designer ? designerHeaders : {};
  const csrf = await (await expectStatus(client, base, auth + '/csrf', 200, headers)).json();
  return client.post(base + path, { headers: { ...headers, 'X-PVAD-CSRF': csrf.token, Origin: base }, data });
};
const screenshot = async (page, name) => {
  const notice = page.locator('[data-pvad-storage-understood]');
  if (await notice.isVisible()) await notice.click();
  await page.screenshot({ path: '/output/' + name, animations: 'disabled', fullPage: false }); screenshots.push(name);
};
const loginApi = async (client, base, email) => {
  const response = await mutate(client, base, auth + '/login', { identifier: email, password });
  assert.equal(response.status(), 200, 'Synthetic login failed.'); return response.json();
};
const probeCookies = async (cookies, base, expected, designer = false) => {
  const copy = await context(cookies); return expectStatus(copy.request, base, auth + '/me', expected, designer ? designerHeaders : {});
};
try {
  const anon = await context();
  for (const host of ['untrusted.pvad.test', 'wrong-host.pvad.test']) {
    const negative = await anon.newPage();
    let refused = false;
    try { await negative.goto(`https://${host}:8443/`, { timeout: 15_000 }); }
    catch (error) { refused = /ERR_CERT_AUTHORITY_INVALID|ERR_CERT_COMMON_NAME_INVALID/.test(error.message); }
    assert.ok(refused, 'Untrusted or wrong-host TLS must be rejected, not ignored.');
    await negative.close();
  }
  const negative = await anon.newPage();
  await negative.goto(urls.account + '/login');
  assert.ok(await negative.evaluate(() => window.isSecureContext));
  checked('trusted-ca-valid-host-and-real-negative-tls-validation');
  for (const base of Object.values(urls)) await expectStatus(anon.request, base, auth + '/me', 401);

  const customer = await context(), page = await customer.newPage();
  let exchangeBody;
  page.on('request', request => {
    if (new URL(request.url()).pathname === auth + '/designer-handoff/exchange') exchangeBody = request.postDataJSON();
  });
  await page.goto(urls.designer + '/pdf/create');
  await page.waitForURL(url => url.origin === urls.account && url.pathname === '/login');
  await page.locator('#login-form [name="identifier"]').fill('editor@https.test');
  await page.locator('#login-form [name="password"]').fill(password);
  await page.locator('#login-form button[type="submit"]').click();
  await page.waitForURL(url => url.origin === urls.designer && url.pathname === '/pdf/create', { timeout: 90_000 });
  assert.ok(!/[?&](code|state)=/.test(page.url()));
  await page.getByRole('button', { name: 'Rename Untitled document' }).waitFor();
  const panel = page.locator('.pvad-product-overlay');
  await page.getByRole('status').filter({ hasText: /^Saved$/ }).waitFor();
  await panel.getByRole('button', { name: 'Close panel' }).click();
  await panel.waitFor({ state: 'hidden' });
  await screenshot(page, 'designer-https-desktop.png');
  await page.setViewportSize({ width: 390, height: 844 }); await screenshot(page, 'designer-https-mobile.png');
  await expectStatus(customer.request, urls.account, auth + '/me', 200);
  await expectStatus(customer.request, urls.designer, auth + '/me', 200, designerHeaders);
  checked('real-account-login-pkce-designer-and-template-persistence');

  const accountPage = await customer.newPage(); await accountPage.goto(urls.account + '/dashboard');
  await accountPage.locator('.account-app-shell').waitFor(); await screenshot(accountPage, 'account-https-desktop.png');
  await accountPage.setViewportSize({ width: 390, height: 844 }); await screenshot(accountPage, 'account-https-mobile.png');
  const operator = await context(), adminPage = await operator.newPage();
  await adminPage.goto(urls.admin + '/login');
  await adminPage.getByLabel('Email or username').fill('operator@https.test');
  await adminPage.getByLabel('Password', { exact: true }).fill(password);
  await adminPage.getByRole('button', { name: 'Sign in', exact: true }).click();
  await adminPage.getByRole('heading', { name: 'Dashboard', exact: true }).waitFor();
  await screenshot(adminPage, 'admin-https-desktop.png');
  await adminPage.setViewportSize({ width: 390, height: 844 }); await screenshot(adminPage, 'admin-https-mobile.png');
  await expectStatus(operator.request, urls.admin, '/api/pvad/v1/admin/users', 200);
  checked('independent-real-admin-browser-login-and-role-authorization');

  const customerCookies = await customer.cookies(), operatorCookies = await operator.cookies();
  const secureNames = [fixture.cookies.account, fixture.cookies.designer, fixture.cookies.csrf];
  for (const cookie of [...customerCookies, ...operatorCookies].filter(value => secureNames.includes(value.name))) {
    assert.ok(cookie.secure && cookie.httpOnly && cookie.sameSite === 'Strict' && cookie.path === '/');
    assert.ok(['account.pvad.test', 'designer.pvad.test', 'admin.pvad.test'].includes(cookie.domain));
  }
  assert.ok(customerCookies.some(value => value.name === fixture.cookies.account && value.domain === 'account.pvad.test'));
  assert.ok(customerCookies.some(value => value.name === fixture.cookies.designer && value.domain === 'designer.pvad.test'));
  await expectStatus(customer.request, urls.admin, auth + '/me', 401);
  await expectStatus(operator.request, urls.account, auth + '/me', 401);
  await expectStatus(operator.request, urls.designer, auth + '/me', 401, designerHeaders);
  const accountCookie = customerCookies.find(value => value.name === fixture.cookies.account);
  const designerCookie = customerCookies.find(value => value.name === fixture.cookies.designer);
  await probeCookies([{ ...accountCookie, name: fixture.cookies.designer, domain: 'designer.pvad.test' }], urls.designer, 401, true);
  await probeCookies([{ ...designerCookie, name: fixture.cookies.account, domain: 'account.pvad.test' }], urls.account, 401);
  const editorAdmin = await context(); await loginApi(editorAdmin.request, urls.admin, 'editor@https.test');
  await expectStatus(editorAdmin.request, urls.admin, '/api/pvad/v1/admin/users', 403);
  checked('secure-host-only-cookie-transport-and-designer-ticket-isolation');

  const badCsrf = await customer.request.post(urls.designer + auth + '/logout', { headers: { ...designerHeaders, 'X-PVAD-CSRF': 'invalid' } });
  assert.equal(badCsrf.status(), 400);
  const replay = await context();
  assert.ok(exchangeBody?.code);
  assert.equal((await mutate(replay.request, urls.designer, auth + '/designer-handoff/exchange', exchangeBody, true)).status(), 400);
  const verifier = randomBytes(64).toString('base64url'), state = randomBytes(24).toString('base64url');
  const requestBody = { designerOrigin: urls.designer, returnPath: '/pdf/create', state,
    codeChallenge: createHash('sha256').update(verifier).digest('base64url') };
  const handoff = await mutate(customer.request, urls.account, auth + '/designer-handoff', requestBody);
  assert.equal(handoff.status(), 200);
  const callback = new URL((await handoff.json()).redirectUrl);
  const invalidPkce = await mutate(replay.request, urls.designer, auth + '/designer-handoff/exchange', {
    code: callback.searchParams.get('code'), state, designerOrigin: urls.designer, codeVerifier: randomBytes(64).toString('base64url'),
  }, true);
  assert.equal(invalidPkce.status(), 400);
  assert.equal((await mutate(customer.request, urls.account, auth + '/designer-handoff', { ...requestBody, returnPath: '//external.invalid/' })).status(), 400);
  checked('csrf-pkce-replay-and-return-path-rejections');
  for (const value of [page, accountPage, adminPage, negative]) await value.close();

  const legacyCookies = [];
  const sessionsBeforeMigration = await control('session-count');
  for (const [surface, cookie, base] of [['account', accountCookie, urls.account], ['designer', designerCookie, urls.designer]]) {
    const converted = await control('legacy-ticket', { surface, cookie: cookie.value });
    const old = { ...cookie, name: converted.name, value: converted.value };
    const legacy = await context([old]);
    const response = await expectStatus(legacy.request, base, auth + '/me', 200, surface === 'designer' ? designerHeaders : {});
    const canonicalName = surface === 'designer' ? fixture.cookies.designer : fixture.cookies.account;
    const headers = (await response.headersArray()).filter(value => value.name.toLowerCase() === 'set-cookie');
    assert.ok(headers.some(value => value.value.startsWith(canonicalName + '=')));
    assert.ok(headers.every(value => !/;\s*domain=/i.test(value.value)));
    const renewed = (await legacy.cookies()).find(value => value.name === canonicalName);
    assert.ok(renewed);
    legacyCookies.push(old, renewed);
  }
  assert.deepEqual(await control('session-count'), sessionsBeforeMigration);
  checked('real-validated-legacy-cookie-and-ticket-renewal');
  await control('restart');
  await probeCookies(customerCookies, urls.account, 200);
  await probeCookies(customerCookies, urls.designer, 200, true);
  await probeCookies(operatorCookies, urls.admin, 200);
  checked('persistent-session-continuity-across-real-api-restart');
  await control('wrong-keys');
  await probeCookies(customerCookies, urls.account, 401);
  await probeCookies(customerCookies, urls.designer, 401, true);
  await control('correct-keys');
  await probeCookies(customerCookies, urls.account, 200);
  await probeCookies(customerCookies, urls.designer, 200, true);
  checked('wrong-keyring-denial-and-compatible-keyring-recovery');
  await control('backup');
  await control('mark-after-backup');
  const restored = await control('restore');
  assert.ok(restored.consumers.mailLegacyReadable && restored.consumers.cacheLegacyReadable);
  await probeCookies(customerCookies, urls.account, 200);
  await probeCookies(customerCookies, urls.designer, 200, true);
  await probeCookies(operatorCookies, urls.admin, 200);
  for (const cookie of legacyCookies) await probeCookies([cookie], cookie.domain === 'designer.pvad.test' ? urls.designer : urls.account, 200, cookie.domain === 'designer.pvad.test');
  checked('real-postgres-keyring-restore-and-protected-reader-compatibility');

  for (const [disable, enable] of [['disable-user', 'enable-user'], ['suspend-organization', 'resume-organization']]) {
    await control(disable);
    const denied = await context(customerCookies);
    const response = await denied.request.get(urls.designer + auth + '/me', { headers: designerHeaders });
    assert.ok([401, 403].includes(response.status()), 'A disabled user or suspended organization must be denied.');
    await control(enable);
    await probeCookies(customerCookies, urls.designer, 200, true);
  }
  await control('disable-entitlement');
  await probeCookies(customerCookies, urls.account, 200);
  await probeCookies(customerCookies, urls.designer, 403, true);
  await control('enable-entitlement');
  await probeCookies(customerCookies, urls.designer, 200, true);
  checked('disabled-user-suspended-organization-and-entitlement-denial');

  const signedIn = await context(customerCookies);
  assert.equal((await mutate(signedIn.request, urls.designer, auth + '/logout', undefined, true)).status(), 204);
  await control('restart');
  await probeCookies(customerCookies, urls.designer, 401, true);
  await probeCookies(customerCookies, urls.account, 200);
  assert.equal((await mutate(signedIn.request, urls.account, auth + '/logout')).status(), 204);
  await control('restart'); await probeCookies(customerCookies, urls.account, 401);
  await probeCookies(operatorCookies, urls.admin, 200);
  checked('persistent-logout-replay-denial-with-independent-admin-session');
  const expired = await context(); await loginApi(expired.request, urls.account, 'editor@https.test');
  const beforeExpiry = await expired.cookies(); await control('expire-sessions');
  await probeCookies(beforeExpiry, urls.account, 401);
  checked('database-session-expiry-rejects-valid-secure-cookie');
  await writeFile('/output/browser-report.json', JSON.stringify({ checks, screenshots, blockedExternalRequests, browser: browser.version(),
    platform: 'isolated-linux-chromium', tlsValidationDisabled: false, apiMocking: false, hostOnlyCookiesVerified: true,
    limitations: ['not-macos-native-browser-certification', 'not-production-retention-approval', 'account-admin-share-ticket-purpose-not-cross-host-cookie-transport',
      'fixture-rewrap-not-historical-binary-login', 'synthetic-policy-fixtures-not-legal-publication-workflow', 'point-in-time-restore-not-post-backup-revocation-reconciliation'] }, null, 2));
} catch (error) {
  await writeFile('/output/browser-failure.json', JSON.stringify({ ...safeFailureEvidence(error, [password]), checks }, null, 2));
  process.exitCode = 1;
} finally {
  for (const value of contexts) await value.close();
  await browser.close(); process.stdin.destroy();
}
