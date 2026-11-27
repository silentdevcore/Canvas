import { expect, test, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import path from 'node:path';
const permissions = [...readFileSync(path.resolve('src/auth/designerPermissions.ts'), 'utf8').matchAll(/\w+: '(designer\.[^']+)'/g)].map(row => row[1]);

const designer = 'http://127.0.0.1:5186', account = 'http://127.0.0.1:5188';
const owner = '11111111-1111-1111-1111-111111111111', organization = '22222222-2222-2222-2222-222222222222';
const oldId = '33333333-3333-3333-3333-333333333333', newId = '44444444-4444-4444-4444-444444444444';
const policy = readFileSync(path.resolve('../src/Licensing/PVAD.Licensing/PvadAccountLicensePolicy.cs'), 'utf8');
function capabilities(company: boolean) {
  return [...policy.matchAll(/new\("([a-z0-9.-]+)", (Allow|Deny|Approval|Undecided|Deferred)(?:, (Allow|Deny|Approval|Undecided|Deferred))?\)/g)]
    .filter(row => (company ? row[3] ?? 'Allow' : row[2]) === 'Allow').map(row => row[1]);
}

async function fixture(page: Page, company: boolean) {
  let renewals = 0, imports = 0;
  const license = (id: string) => ({ id, organizationId: company ? organization : null, ownerUserId: company ? null : owner,
    previousLicenseId: id === oldId ? null : oldId, model: 'Subscription', issuedAt: '2026-01-01T00:00:00Z',
    validFrom: '2026-01-01T00:00:00Z', validUntil: id === oldId ? '2026-04-01T00:00:00Z' : '2099-04-01T00:00:00Z',
    updatesUntil: null, artifactSha256: 'a'.repeat(64) });
  await page.addInitScript(() => { localStorage.setItem('i18nextLng', 'en'); });
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let body: unknown = [];
    if (path.endsWith('/auth/me')) body = { id: owner, username: 'synthetic', email: 'synthetic@example.test', displayName: 'Synthetic account',
      roles: ['Organization Administrator'], permissions: [...permissions, 'account.licenses.read'],
      accountCapabilities: capabilities(company), organizations: [{ id: organization, name: 'Synthetic', slug: 'synthetic' }],
      activeOrganizationId: organization, canReadPersonalNativeLicenses: !company, lastLoginAt: null };
    else if (path.endsWith('/auth/csrf')) body = { token: 'synthetic-ui-csrf' };
    else if (path.endsWith('/account/profile')) body = { id: owner, displayName: 'Synthetic', legalPolicyAvailable: true,
      requiresTermsAcceptance: false, requiresPrivacyAcknowledgement: false };
    else if (path.endsWith('/preferences/element-favorites')) body = { elementTypes: [] };
    else if (path.endsWith('/releases')) body = { releases: [], readVersions: [] };
    else if (path.endsWith('/notifications/unread-count')) body = { unread: 0 };
    else if (path.endsWith('/notifications')) body = { items: [], total: 0 };
    else if (path.endsWith('/renew-free')) {
      expect(route.request().method()).toBe('POST');
      expect(route.request().headers()['x-pvad-csrf']).toBe('synthetic-ui-csrf');
      expect(route.request().postDataJSON()).toEqual({}); renewals++;
      body = license(newId);
    } else if (path.endsWith('/native-licenses')) body = { items: renewals ? [license(newId), license(oldId)] : [license(oldId)], total: renewals ? 2 : 1 };
    else if (path.includes('/native-licenses/') && path.endsWith('/download')) {
      return route.fulfill({ contentType: 'application/json', headers: { 'Content-Disposition': 'attachment; filename="synthetic-license.json"' },
        body: JSON.stringify({ syntheticUiDownload: true }) });
    } else if (path.includes('/native-licenses/')) {
      const id = path.split('/').at(-1)!;
      body = { license: license(id), grants: [{ product: 'pdf', providers: [] }], redistributionAllowed: false,
        gracePeriodDays: 0, renewedByLicenseId: id === oldId && renewals ? newId : null,
        freeRenewal: { availableAt: license(id).validUntil, canRenew: id === oldId && renewals === 0 } };
    } else if (path.endsWith('/spreadsheet/import')) {
      imports++;
      return route.fulfill({ status: 403, contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Account capability denied', detail: 'This operation is not included in this account\'s rights.', code: 'PVAD_ACCOUNT_CAPABILITY_DENIED' }) });
    }
    return route.fulfill({ contentType: 'application/json', body: JSON.stringify(body) });
  });
  return { renewals: () => renewals, imports: () => imports };
}

for (const company of [false, true]) {
  test(`${company ? 'company' : 'private'}: document creation and own code workspace remain accessible`, async ({ page }) => {
    await fixture(page, company);
    await page.goto(`${designer}/pdf/create`);
    if (test.info().project.use.isMobile) await expect(page.locator('.editor-mobile-overlay')).toBeVisible();
    else await expect(page.getByRole('button', { name: 'Preview', exact: true })).toBeVisible();
    await page.goto(`${designer}/pdf/create?mode=code`);
    await expect(page.locator('.live-code-lang-toggle')).toBeVisible();
    await expect(page.locator('.live-code-topbar-right')).toBeVisible();
  });

  test(`${company ? 'company' : 'private'}: PDF import follows the account policy; unapproved migration is inaccessible`, async ({ page }) => {
    await fixture(page, company);
    await page.goto(`${designer}/pdf/import`);
    const choose = page.getByRole('button', { name: 'Choose PDF file' });
    if (company) await expect(choose).toBeEnabled();
    else {
      await expect(page.getByRole('heading', { name: 'Account restriction' })).toBeVisible();
      await expect(page.getByText('This operation is not included in your account rights.')).toBeVisible();
      await expect(choose).toHaveCount(0);
    }
    await page.screenshot({ path: test.info().outputPath('pdf-import-policy.png'), fullPage: true });
    await page.goto(`${designer}/pdf/migrations/code`);
    await expect(page.getByRole('heading', { name: 'Admin approval required' })).toBeVisible();
  });

  test(`${company ? 'company' : 'private'}: Account renewal offers the successor download`, async ({ page }) => {
    const state = await fixture(page, company);
    await page.goto(`${account}/licenses`);
    await expect(page.getByRole('heading', { name: 'Native library licenses' })).toBeVisible();
    await page.locator(`.account-native-detail[data-license-id="${oldId}"]`).click();
    await expect(page.getByRole('button', { name: 'Renew free license' })).toBeEnabled();
    await page.getByRole('button', { name: 'Renew free license' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Your free license has been renewed' })).toBeVisible();
    expect(state.renewals()).toBe(1);
    const download = page.locator(`a[href$="/${newId}/download"]`).first();
    const pending = page.waitForEvent('download'); await download.click();
    expect((await pending).suggestedFilename()).toBe('synthetic-license.json');
    await expect(page.getByRole('button', { name: 'Renew free license' })).toBeDisabled();
  });
}

for (const company of [false, true]) {
  test(`${company ? 'company' : 'private'}: preview quick exports enforce format capabilities`, async ({ page }) => {
    expect(capabilities(false)).not.toContain('export.json');
    await fixture(page, company);
    await page.goto(`${designer}/pdf/create`);
    if (test.info().project.use.isMobile) {
      await expect(page.locator('.editor-mobile-overlay')).toBeVisible();
      return;
    }
    await page.getByRole('button', { name: 'Preview', exact: true }).click();
    await page.getByRole('button', { name: 'Export', exact: true }).click();
    await page.screenshot({ path: test.info().outputPath('preview-format-rights.png'), fullPage: true });
    await expect(page.getByRole('button', { name: /Export PDF/ })).toBeVisible();
    await expect(page.getByRole('button', { name: /Print.*generated PDF/ })).toBeVisible();
    if (company) {
      await expect(page.getByRole('button', { name: /Export Image/ })).toBeVisible();
      const pending = page.waitForEvent('download');
      await page.getByRole('button', { name: /Export JSON/ }).click();
      expect((await pending).suggestedFilename()).toMatch(/\.json$/);
    } else {
      await expect(page.getByRole('button', { name: /Export JSON/ })).toHaveCount(0);
      await expect(page.getByRole('button', { name: /Export Image/ })).toHaveCount(0);
    }
  });

  test(`${company ? 'company' : 'private'}: deferred XLS is neither advertised nor uploaded and account errors are explained`, async ({ page }) => {
    const state = await fixture(page, company);
    await page.goto(`${designer}/spreadsheet/import`);
    const input = page.locator('input[type=file]');
    await expect(input).toHaveAttribute('accept', '.xlsx,.csv,.tsv,.json');
    await expect(page.locator('.mgr-heading')).not.toContainText('.xls,');
    await input.setInputFiles({ name: 'synthetic.xls', mimeType: 'application/vnd.ms-excel', buffer: Buffer.from('synthetic') });
    await expect(page.getByRole('alert')).toHaveText('XLS import is not available yet. Choose one of the listed formats.');
    expect(state.imports()).toBe(0);
    await input.setInputFiles({ name: 'synthetic.xlsx', mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: Buffer.from('synthetic') });
    await expect(page.getByRole('alert')).toHaveText('This operation is not included in your account rights.');
    expect(state.imports()).toBe(1);
    await page.screenshot({ path: test.info().outputPath('spreadsheet-format-rights.png'), fullPage: true });
  });
}
