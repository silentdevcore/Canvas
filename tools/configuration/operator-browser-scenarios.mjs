import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { gatewayProtocol } from './operator-gateway-rehearsal.mjs';

export async function exerciseOperatorBrowser({ origin, password, operatorEmail, memberEmail, sql, expectedDashboards, output, stopApi, startApi, onOperatorReady, onCheck = () => {} }) {
  const { chromium } = await import('../../pvad-designer/node_modules/playwright/index.mjs');
  const browser = await chromium.launch({ headless: true });
  const checks = [], screenshots = [], contexts = [];
  const checked = name => { checks.push(name); onCheck(name); };
  let blockedExternalRequests = 0;
  const context = async () => {
    const value = await browser.newContext({ viewport: { width: 1440, height: 980 }, colorScheme: 'light' });
    contexts.push(value); value.setDefaultTimeout(30_000);
    await value.route('**/*', route => {
      if (new URL(route.request().url()).origin === origin) return route.continue();
      blockedExternalRequests++; return route.abort();
    });
    return value;
  };
  const expectStatus = async (request, path, status, options = {}) => {
    const response = await request.get(`${origin}${path}`, { ...options, maxRedirects: 0, timeout: 10_000 });
    assert.equal(response.status(), status, `Unexpected status for bounded route ${path}.`);
    return response;
  };
  const login = async (value, email) => {
    const token = await (await value.request.get(`${origin}/api/pvad/v1/auth/csrf`)).json();
    const response = await value.request.post(`${origin}/api/pvad/v1/auth/login`, {
      headers: { 'X-PVAD-CSRF': token.token }, data: { identifier: email, password },
    });
    assert.equal(response.status(), 200, 'Synthetic login failed.');
    return response.json();
  };
  const screenshot = async (page, name) => {
    await page.screenshot({ path: resolve(output, name), fullPage: false, animations: 'disabled' });
    screenshots.push(name);
  };
  try {
    const anonymous = await context();
    for (const path of ['/documentation', '/dashboard', '/operator/grafana/api/user']) await expectStatus(anonymous.request, path, 401);
    await expectStatus(anonymous.request, '/_pvad_operator_auth', 404);
    const protocol = await gatewayProtocol();
    await expectStatus(anonymous.request, '/operator/grafana/api/user', 401, { headers: {
      'X-WEBAUTH-USER': 'forged-root', 'X-WEBAUTH-ROLE': 'Admin', Authorization: 'Bearer synthetic-forged',
      ...Object.fromEntries(protocol.credentialHeaders.map(key => [key, 'synthetic-forged'])),
    } });
    checked('anonymous-and-forged-header-denials');

    const operator = await context(), page = await operator.newPage();
    await page.goto(`${origin}/login`);
    await page.getByRole('heading', { name: 'Sign in to PVADE Admin' }).waitFor();
    await screenshot(page, 'login-desktop.png');
    await page.getByLabel('Email or username').fill(operatorEmail);
    await page.getByLabel('Password', { exact: true }).fill(password);
    const signedIn = page.waitForResponse(response => new URL(response.url()).pathname === '/api/pvad/v1/auth/login' && response.request().method() === 'POST');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    const loginResponse = await signedIn;
    assert.equal(loginResponse.status(), 200);
    const sessionHeaders = (await loginResponse.headersArray()).filter(header => header.name.toLowerCase() === 'set-cookie'
      && header.value.startsWith('PVAD.Session.Development='));
    assert.equal(sessionHeaders.length, 1);
    assert.ok(sessionHeaders[0].value.length < 4096);
    assert.doesNotMatch(sessionHeaders[0].value, /;\s*domain=/i);
    await page.getByRole('heading', { name: 'Dashboard', exact: true }).waitFor();
    await screenshot(page, 'dashboard-desktop.png');
    const me = await (await expectStatus(operator.request, '/api/pvad/v1/auth/me', 200)).json();
    assert.ok(me.roles.includes('System Administrator'));
    assert.match(me.id, /^[a-f0-9-]{36}$/i);
    const initialCookies = await operator.cookies();
    const sessionCookie = initialCookies.find(value => value.name === 'PVAD.Session.Development');
    assert.ok(sessionCookie?.httpOnly && sessionCookie.sameSite === 'Strict' && sessionCookie.path === '/');
    assert.equal(sessionCookie.domain, 'localhost');
    checked('real-admin-browser-login-and-host-only-cookie');

    await page.goto(`${origin}/documentation`);
    await page.locator('#runbook-content h1').waitFor();
    assert.equal(await page.locator('#runbook-navigation button').count(), 2);
    await screenshot(page, 'runbooks-desktop.png');
    await page.setViewportSize({ width: 390, height: 844 });
    await screenshot(page, 'runbooks-mobile.png');
    await page.setViewportSize({ width: 1440, height: 980 });
    checked('real-protected-runbook-content-desktop-and-mobile');

    let grafanaReady = false;
    for (let i = 0; i < 120; i++) {
      const response = await operator.request.get(`${origin}/operator/grafana/api/health`, { maxRedirects: 0, timeout: 2000 });
      if (response.ok()) { grafanaReady = true; break; }
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    assert.ok(grafanaReady, 'Real Grafana startup timed out.');
    const user = await (await expectStatus(operator.request, '/operator/grafana/api/user', 200)).json();
    const identityResponse = await expectStatus(operator.request, '/api/pvad/v1/admin/operator/access', 204);
    const identity = identityResponse.headers()['x-pvad-operator'];
    assert.ok(identity && identity === identityResponse.headers()[protocol.legacyOperatorHeader.toLowerCase()]);
    assert.equal(user.login, identity);
    assert.ok(identity.endsWith(createHash('sha256').update(me.id).digest('hex').slice(0, 24)));
    let dashboardUids = [], dashboardRecords = [];
    for (let i = 0; i < 60; i++) {
      const response = await expectStatus(operator.request, '/operator/grafana/api/search?type=dash-db', 200);
      dashboardRecords = await response.json();
      dashboardUids = dashboardRecords.map(value => value.uid).sort();
      if (JSON.stringify(dashboardUids) === JSON.stringify([...expectedDashboards].sort())) break;
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    assert.deepEqual(dashboardUids, [...expectedDashboards].sort());
    const folders = await (await expectStatus(operator.request, '/operator/grafana/api/search?type=dash-folder', 200)).json();
    assert.equal(folders.length, 1, 'The disposable instance must have exactly one provisioned folder.');
    await page.goto(`${origin}/operator/grafana/dashboards/f/${encodeURIComponent(folders[0].uid)}`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('navigation').first().waitFor({ timeout: 30_000 });
    for (const dashboard of dashboardRecords) await page.getByText(dashboard.title, { exact: true }).first().waitFor();
    assert.ok((await page.locator('body').innerText()).length > 100, 'Grafana browser UI is blank.');
    await screenshot(page, 'grafana-desktop.png');
    await page.setViewportSize({ width: 390, height: 844 });
    await screenshot(page, 'grafana-mobile.png');
    checked('real-grafana-proxy-identity-five-dashboards-and-browser-ui');

    const ordinary = await context();
    const member = await login(ordinary, memberEmail);
    assert.ok(member.user.roles.includes('Organization Administrator'));
    assert.ok(!member.user.roles.includes('System Administrator'));
    for (const path of ['/documentation', '/dashboard', '/operator/grafana/api/user']) await expectStatus(ordinary.request, path, 403);
    checked('real-organization-administrator-denied-operator-routes');

    if (onOperatorReady) await onOperatorReady({ request: operator.request, page,
      anonymousRequest: anonymous.request, memberRequest: ordinary.request, screenshot: name => screenshot(page, name), checked });

    await stopApi();
    await expectStatus(operator.request, '/documentation', 500);
    await expectStatus(operator.request, '/operator/grafana/api/user', 500);
    await startApi();
    await expectStatus(operator.request, '/documentation', 200);
    await expectStatus(operator.request, '/operator/grafana/api/user', 200);
    checked('api-outage-denies-and-restart-preserves-session');

    const csrf = await (await expectStatus(operator.request, '/api/pvad/v1/auth/csrf', 200)).json();
    const bad = await operator.request.post(`${origin}/api/pvad/v1/auth/logout`, { headers: { 'X-PVAD-CSRF': 'invalid' } });
    assert.equal(bad.status(), 400);
    await expectStatus(operator.request, '/operator/grafana/api/user', 200);
    const logout = await operator.request.post(`${origin}/api/pvad/v1/auth/logout`, { headers: { 'X-PVAD-CSRF': csrf.token } });
    assert.equal(logout.status(), 204);
    await expectStatus(operator.request, '/operator/grafana/api/user', 401);
    const replay = await context(); await replay.addCookies(initialCookies);
    await expectStatus(replay.request, '/documentation', 401);
    await expectStatus(replay.request, '/operator/grafana/api/user', 401);
    assert.equal(sql(`SELECT count(*) FROM identity.user_sessions WHERE "UserId" = '${me.id}' AND "RevokedAt" IS NOT NULL;`), '1');
    checked('real-csrf-logout-persistent-revocation-and-cookie-replay-denied');

    const expired = await context(); await login(expired, operatorEmail);
    sql(`UPDATE identity.user_sessions SET "ExpiresAt" = now() - interval '1 minute' WHERE "UserId" = '${me.id}' AND "RevokedAt" IS NULL;`);
    await expectStatus(expired.request, '/documentation', 401);
    await expectStatus(expired.request, '/operator/grafana/api/user', 401);
    checked('database-expiry-denies-protected-content');
    return { checks, screenshots, dashboardCount: dashboardUids.length, blockedExternalRequests,
      browser: 'playwright-chromium', viewports: ['1440x980', '390x844'], apiMocking: false };
  } finally {
    try { for (const value of contexts) await value.close(); }
    finally { await browser.close(); }
  }
}
