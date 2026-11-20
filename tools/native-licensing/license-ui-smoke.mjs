import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const require = createRequire(resolve(root, 'pvad-designer/package.json'));
const { chromium } = require('playwright');
const output = resolve(root, 'artifacts/native-license-ui');
assert.ok(process.argv.slice(2).every(argument => argument === '--capture-docs'));
const captureDocs = process.argv.includes('--capture-docs');
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const report = { syntheticOnly: true, mockedHttp: true, realAuthenticationValidatedSeparately: true, cases: [] };
const organizationId = '00000000-0000-4000-8000-000000000001';
const otherOrganizationId = '00000000-0000-4000-8000-000000000002';
const id = '00000000-0000-4000-8000-000000000010';
const renewalId = '00000000-0000-4000-8000-000000000011';
const license = { id, organizationId, model: 'Subscription', issuedAt: '2026-11-20T12:00:00Z', validFrom: '2026-11-20T12:00:00Z',
  validUntil: '2027-11-20T12:00:00Z', updatesUntil: null, previousLicenseId: null, artifactSha256: 'a'.repeat(64) };
const details = row => ({ license: row, customerReference: 'Synthetic customer', redistributionAllowed: false,
  grants: [{ product: 'pdf', providers: [] }], renewedByLicenseId: row.id === id ? renewalId : null });
const artifact = '{"syntheticOnly":true,"notAUsableLicense":true}\n';
async function assertVisibleControls(page, selector) {
  const clipped = await page.locator(selector).evaluateAll(elements => elements.filter(element => {
    if (element.closest('.admin-table-scroll, .account-table-scroll')) return false;
    const rect = element.getBoundingClientRect();
    return rect.width > 0 && (rect.left < -1 || rect.right > innerWidth + 1);
  }).map(element => element.getAttribute('name') || element.textContent));
  assert.deepEqual(clipped, [], 'Non-table controls and headings must fit the viewport without clipping.');
}
try {
  for (const width of [1440, 390]) {
    const context = await browser.newContext({ viewport: { width, height: 1000 }, acceptDownloads: true });
    const page = await context.newPage();
    const errors = [], posts = [], unexpected = [];
    page.on('pageerror', error => errors.push(error.message));
    let activeOrganizationId = organizationId;
    const user = { id: 'synthetic-operator', displayName: 'Synthetic Operator', email: 'operator@example.test',
      roles: ['System Administrator'], permissions: ['licenses.manage', 'account.licenses.read', 'account.organization.read'],
      organizations: [{ id: organizationId, name: 'Synthetic Documents', slug: 'synthetic' }, { id: otherOrganizationId, name: 'Other Workspace', slug: 'other' }],
      activeOrganizationId, lastLoginAt: null };
    let items = [license];
    await page.route('**/api/**', async route => {
      const request = route.request(), url = new URL(request.url()), path = url.pathname;
      const send = body => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
      if (path === '/api/pvad/v1/auth/me') return send({ ...user, activeOrganizationId });
      if (path === '/api/pvad/v1/auth/csrf') return send({ token: 'synthetic-csrf' });
      if (path === '/api/pvad/v1/auth/switch-organization') {
        activeOrganizationId = request.postDataJSON().organizationId; return send({ user: { ...user, activeOrganizationId } });
      }
      if (path === '/api/pvad/v1/account/profile') return send({ legalPolicyAvailable: true, requiresTermsAcceptance: false, requiresPrivacyAcknowledgement: false });
      if (path === '/api/pvad/v1/account/organization') return send({ id: activeOrganizationId, status: 'Active' });
      if (path === '/api/pvad/v1/admin/native-licenses/capabilities') return send({ enabled: true, products: [{ id: 'pdf', providers: [] }, { id: 'migration.pdf.code', providers: ['aspose-pdf'] }] });
      if (path === '/api/pvad/v1/admin/organizations') return send({ items: user.organizations, total: 2 });
      if (path.endsWith('/download') && path.includes('/native-licenses/')) return route.fulfill({ status: 200, contentType: 'application/json', headers: { 'Content-Disposition': `attachment; filename="PVAD-native-synthetic.json"` }, body: artifact });
      if (path === '/api/pvad/v1/admin/native-licenses' && request.method() === 'POST') {
        const body = request.postDataJSON();
        assert.equal(request.headers()['x-pvad-csrf'], 'synthetic-csrf');
        assert.deepEqual(body.grants, [{ product: 'pdf', providers: [] }]);
        posts.push(body); items = [license]; return send(license);
      }
      if (path.endsWith('/renew')) {
        const body = request.postDataJSON(); assert.equal(request.headers()['x-pvad-csrf'], 'synthetic-csrf');
        assert.equal(body.updatesUntil, null); assert.equal(body.reasonCode, 'renewal');
        posts.push(body); const renewed = { ...license, id: renewalId, previousLicenseId: id, validUntil: body.validUntil };
        items = [renewed, license]; return send(renewed);
      }
      if (path.endsWith('/details')) {
        const row = items.find(item => path.includes(item.id)) || license;
        // The fixture exposes the renewal action until the first renewal is issued.
        return send({ ...details(row), renewedByLicenseId: items.length > 1 && row.id === id ? renewalId : null });
      }
      if (path === '/api/pvad/v1/admin/native-licenses') return send({ items, total: items.length, page: 1, pageSize: 25 });
      if (path === '/api/pvad/v1/account/licenses') return send([]);
      if (path === '/api/pvad/v1/account/native-licenses') return send({ items: activeOrganizationId === organizationId ? items : [], total: activeOrganizationId === organizationId ? items.length : 0, page: 1, pageSize: 25 });
      if (path.startsWith('/api/pvad/v1/account/native-licenses/')) return send(details(items.find(item => path.endsWith(item.id)) || license));
      if (path.includes('telemetry')) return route.fulfill({ status: 204 });
      // The organization switch returns to Overview before the license view is reopened.
      if (path.includes('/account/subscription') || path.includes('/account/entitlements')) return send(null);
      unexpected.push(path); return route.fulfill({ status: 404, body: '{}' });
    });
    await page.goto('http://localhost:5177/native-licenses');
    await page.locator('#native-license-issue').waitFor();
    await page.selectOption('[name="organizationId"]', { value: organizationId });
    await page.fill('[name="customerReference"]', 'Synthetic customer');
    await page.check('[name="product:pdf"]');
    await page.getByRole('button', { name: 'Issue signed native license', exact: true }).click();
    await page.getByText('Native license issued.', { exact: true }).waitFor();
    await page.locator('#native-license-renew').waitFor();
    await page.locator('#native-license-renew input').fill('2028-11-20T12:00');
    await page.getByRole('button', { name: 'Issue renewal', exact: true }).click();
    await page.getByText('Renewal issued; the original artifact remains unchanged.', { exact: true }).waitFor();
    const download = page.waitForEvent('download');
    await page.locator('a[href*="/admin/native-licenses/"][href$="/download"]').first().click();
    const file = await download;
    assert.equal(readFileSync(await file.path(), 'utf8'), artifact);
    await assertVisibleControls(page, '.native-license-workspace input, .native-license-workspace select, .native-license-workspace button, .native-license-workspace h1, .native-license-workspace h2');
    await page.evaluate(() => { document.activeElement?.blur(); scrollTo(0, 0); });
    await page.screenshot({ path: resolve(output, `admin-${width}.png`), fullPage: true });
    if (captureDocs) await page.screenshot({ path: resolve(root, `PVAD.WebApi/AdminDocumentation/images/native-licenses${width === 390 ? '-mobile' : ''}.png`), fullPage: true });
    assert.equal(posts.length, 2);
    assert.notEqual(posts[0].requestId, posts[1].requestId);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await page.goto('http://localhost:5178/licenses');
    await page.getByRole('heading', { name: 'Native library licenses' }).waitFor();
    const notice = page.locator('[data-pvad-storage-understood]');
    if (await notice.count()) await notice.click();
    await page.locator('.account-native-detail').first().click();
    await page.getByRole('heading', { name: 'Native license details' }).waitFor();
    await page.evaluate(() => { document.activeElement?.blur(); scrollTo(0, 0); });
    await page.screenshot({ path: resolve(output, `account-${width}.png`), fullPage: true });
    const customerDownload = page.waitForEvent('download');
    await page.locator('a[href*="/account/native-licenses/"][href$="/download"]').first().click();
    assert.equal(readFileSync(await (await customerDownload).path(), 'utf8'), artifact);
    assert.equal(await page.locator('[href*="/admin/native-licenses"]').count(), 0);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    if (width === 390) await page.locator('#account-menu-button').click();
    await page.selectOption('#account-organization-switcher', otherOrganizationId);
    await page.getByRole('heading', { name: 'Other Workspace', exact: true }).waitFor();
    if (width === 390) await page.locator('#account-menu-button').click();
    await page.locator('nav a[href="/licenses"]').click();
    await page.getByText('No native library licenses issued.', { exact: true }).waitFor();
    assert.equal(await page.locator('.account-native-detail').count(), 0);
    assert.deepEqual(errors, []); assert.deepEqual(unexpected, []);
    report.cases.push({ width, adminIssue: true, adminRenewal: true, exactDownloads: true, tenantSwitch: true, noHorizontalOverflow: true });
    await context.close();
  }
  report.status = 'passed';
} catch (error) { report.status = 'failed'; report.error = error.message; throw error; }
finally { writeFileSync(resolve(output, 'report.json'), JSON.stringify(report, null, 2) + '\n'); await browser.close(); }
console.log(JSON.stringify(report, null, 2));
