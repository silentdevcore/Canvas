import { expect, test, type Page } from '@playwright/test';

const accountUrl = 'http://localhost:5178';
const adminUrl = 'http://localhost:5177';
const userId = '11111111-1111-1111-1111-111111111111';
const organizationId = '22222222-2222-2222-2222-222222222222';
const documentId = '33333333-3333-3333-3333-333333333333';
const previousVersionId = '44444444-4444-4444-4444-444444444444';
const currentVersionId = '55555555-5555-5555-5555-555555555555';
const privacyVersionId = '66666666-6666-6666-6666-666666666666';

const user = {
  id: userId,
  username: 'legal-review@example.test',
  email: 'legal-review@example.test',
  displayName: 'Legal Review User',
  roles: ['System Administrator'],
  permissions: [
    'account.profile.manage',
    'account.organization.read',
    'account.subscription.read',
    'account.licenses.read',
    'account.sessions.manage',
    'legal.read',
  ],
  organizations: [{ id: organizationId, name: 'Synthetic Legal Organization', slug: 'synthetic-legal' }],
  activeOrganizationId: organizationId,
  lastLoginAt: null,
};

const pendingProfile = {
  id: userId,
  displayName: user.displayName,
  email: user.email,
  pendingEmail: null,
  locale: 'en',
  country: 'DE',
  roles: user.roles,
  termsAcceptedVersion: '2026-06',
  currentTermsVersionId: currentVersionId,
  currentTermsVersion: '2026-08',
  requiresTermsAcceptance: true,
  privacyAcknowledgedVersion: '2026-06',
  currentPrivacyVersionId: privacyVersionId,
  currentPrivacyVersion: '2026-08',
  requiresPrivacyAcknowledgement: true,
  legalPolicyAvailable: true,
  marketingConsent: false,
};

async function assertNoHorizontalOverflow(page: Page): Promise<void> {
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
}

test('Account legal review is keyboard operable, fails closed, and submits exact versions', async ({
  page,
}) => {
  let consentAttempts = 0;
  await page.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(user),
  }));
  await page.route('**/api/pxa/v1/account/profile', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(pendingProfile),
  }));
  await page.route('**/api/pxa/v1/account/organization', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: organizationId,
      name: 'Synthetic Legal Organization',
      slug: 'synthetic-legal',
      status: 'Active',
    }),
  }));
  await page.route('**/api/pxa/v1/account/profile/consent', async (route) => {
    consentAttempts += 1;
    expect(route.request().method()).toBe('PATCH');
    expect(route.request().headers()['x-pxa-csrf']).toBe('legal-account-csrf');
    expect(route.request().postDataJSON()).toEqual({
      acceptTerms: true,
      acceptPrivacy: true,
      marketingConsent: false,
      termsVersionId: currentVersionId,
      privacyVersionId,
    });
    if (consentAttempts === 1) {
      await route.fulfill({
        status: 409,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Legal policy changed', status: 409, code: 'PXAAPI017' }),
      });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...pendingProfile,
        termsAcceptedVersion: '2026-08',
        privacyAcknowledgedVersion: '2026-08',
        requiresTermsAcceptance: false,
        requiresPrivacyAcknowledgement: false,
      }),
    });
  });
  await page.route('**/api/pxa/v1/auth/csrf', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'legal-account-csrf' }),
  }));

  await page.goto(`${accountUrl}/dashboard`);
  await expect(page).toHaveURL(`${accountUrl}/legal-review`);
  await expect(page.getByRole('heading', { name: 'Review updated legal documents' })).toBeVisible();
  await page.locator('[data-pxa-storage-notice]').getByRole('button', { name: 'Understood' }).click();
  await expect(page.getByText('This acknowledgement is not consent to marketing.')).toBeVisible();

  for (const checkbox of [
    page.getByRole('checkbox', { name: /accept the current Terms/i }),
    page.getByRole('checkbox', { name: /acknowledge that I have received/i }),
  ]) {
    await checkbox.focus();
    await page.keyboard.press('Space');
    await expect(checkbox).toBeChecked();
  }

  const submit = page.getByRole('button', { name: 'Confirm and continue' });
  await submit.focus();
  await page.keyboard.press('Enter');
  const error = page.getByRole('alert');
  await expect(error).toContainText('changed while you were reviewing');
  await expect(error).toBeFocused();
  await expect(submit).toBeEnabled();

  await submit.click();
  await expect(page).toHaveURL(`${accountUrl}/dashboard`);
  expect(consentAttempts).toBe(2);
  await assertNoHorizontalOverflow(page);
});

test('Admin compares legal versions and exports audited minimized evidence', async ({ page }) => {
  const version = (id: string, value: string, previousVersionIdValue: string | null) => ({
    id,
    legalDocumentId: documentId,
    version: value,
    locale: 'en',
    audience: 'All',
    status: 'Published',
    sourceMarkdown: `# Terms ${value}`,
    renderedHtml: `<h1>Terms ${value}</h1>`,
    contentHash: id.replaceAll('-', '').padEnd(64, '0').slice(0, 64),
    changeSummary: 'Synthetic legal update',
    requiresAcceptance: true,
    isAuthoritative: true,
    createdByUserId: userId,
    createdAt: '2026-07-30T10:00:00Z',
    submittedAt: '2026-07-30T11:00:00Z',
    approvedAt: '2026-07-30T12:00:00Z',
    approvedByUserId: '77777777-7777-7777-7777-777777777777',
    publishedAt: '2026-07-30T13:00:00Z',
    publishedByUserId: '77777777-7777-7777-7777-777777777777',
    effectiveAt: '2026-08-01T00:00:00Z',
    retiredAt: null,
    previousVersionId: previousVersionIdValue,
  });
  const previous = version(previousVersionId, '2026-06', null);
  const current = version(currentVersionId, '2026-08', previousVersionId);
  let exportBody: Record<string, unknown> | null = null;

  await page.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(user),
  }));
  await page.route('**/api/pxa/v1/admin/legal/documents', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      documents: [{
        id: documentId,
        type: 'TermsAndConditions',
        key: 'terms',
        displayName: 'Terms and Conditions',
        createdAt: '2026-07-30T09:00:00Z',
        versionCount: 2,
      }],
      versions: [current, previous],
    }),
  }));
  await page.route('**/api/pxa/v1/admin/legal/versions/compare?*', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      legalDocumentId: documentId,
      displayName: 'Terms and Conditions',
      baseVersion: previous,
      targetVersion: current,
      summary: { unchanged: 1, modified: 1, added: 1, removed: 0 },
      lines: [{
        kind: 'Modified',
        baseLineNumber: 1,
        targetLineNumber: 1,
        baseText: '# Previous synthetic terms',
        targetText: '# Updated synthetic terms',
      }],
    }),
  }));
  await page.route(`**/api/pxa/v1/admin/legal/versions/${currentVersionId}/acceptance?*`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        legalDocumentVersionId: currentVersionId,
        version: '2026-08',
        requiresAcceptance: true,
        affectedAccounts: 10,
        completed: 8,
        pending: 2,
        completionPercentage: 80,
        byLocale: [{ name: 'en', affectedAccounts: 10, completed: 8 }],
        byAccountType: [{ name: 'Company', affectedAccounts: 10, completed: 8 }],
      }),
    }));
  await page.route('**/api/pxa/v1/auth/csrf', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'legal-admin-csrf' }),
  }));
  await page.route(`**/api/pxa/v1/admin/legal/versions/${currentVersionId}/acceptance/export`, async (route) => {
    expect(route.request().method()).toBe('POST');
    expect(route.request().headers()['x-pxa-csrf']).toBe('legal-admin-csrf');
    exportBody = route.request().postDataJSON();
    await new Promise((resolve) => setTimeout(resolve, 150));
    await route.fulfill({
      status: 200,
      contentType: 'text/csv',
      headers: { 'Content-Disposition': 'attachment; filename="pxa-legal-acceptance-2026-08.csv"' },
      body: 'evidenceId,organizationId,contentHash\nsynthetic,synthetic,hash\n',
    });
  });

  await page.goto(`${adminUrl}/legal`);
  await expect(page.getByRole('heading', { name: 'Legal documents' })).toBeVisible();

  await page.locator('.legal-compare').first().focus();
  await page.keyboard.press('Enter');
  await expect(page.getByLabel('Side-by-side Markdown comparison')).toBeVisible();
  await expect(page.getByText('# Updated synthetic terms')).toBeVisible();

  await page.locator(`.legal-acceptance[data-version-id="${currentVersionId}"]`).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: /Acceptance progress/ })).toBeVisible();
  await expect(page.getByRole('progressbar', { name: 'Acceptance completion' })).toHaveAttribute('value', '80');

  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export CSV' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('pxa-legal-acceptance-2026-08.csv');
  expect(exportBody).toEqual({
    format: 'csv',
    organizationId: '',
    accountType: '',
    locale: '',
    from: '',
    to: '',
  });
  await assertNoHorizontalOverflow(page);
});
