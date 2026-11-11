import { expect, test } from '@playwright/test';

const companyUrl = 'http://localhost:5173';
const accountUrl = 'http://localhost:5178';

test('Company sign-in returns to the original page without exposing the session signal', async ({ page }) => {
  await page.route('**/api/pvad/v1/auth/me', (route) => route.fulfill({
    status: 401,
    contentType: 'application/problem+json',
    body: JSON.stringify({
      title: 'Authentication required',
      status: 401,
    }),
  }));
  await page.route('**/api/pvad/v1/auth/csrf', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'company-account-e2e-csrf' }),
  }));
  await page.route('**/api/pvad/v1/account/profile', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: '11111111-1111-1111-1111-111111111111',
      displayName: 'Company Customer',
      email: 'customer@example.test',
      pendingEmail: null,
      locale: 'en',
      country: null,
      roles: ['Organization Administrator'],
      termsAcceptedVersion: '1.1',
      currentTermsVersionId: '33333333-3333-3333-3333-333333333333',
      currentTermsVersion: '1.1',
      currentTermsChangeSummary: null,
      previousTermsVersion: null,
      requiresTermsAcceptance: false,
      privacyAcknowledgedVersion: '1.1',
      currentPrivacyVersionId: '44444444-4444-4444-4444-444444444444',
      currentPrivacyVersion: '1.1',
      currentPrivacyChangeSummary: null,
      previousPrivacyVersion: null,
      requiresPrivacyAcknowledgement: false,
      legalPolicyAvailable: true,
      marketingConsent: false,
    }),
  }));
  await page.route('**/api/pvad/v1/auth/login', async (route) => {
    const request = route.request();
    expect(request.postDataJSON()).toEqual({
      identifier: 'customer@example.test',
      password: 'Pvad-E2E-Password-2026!',
      rememberMe: false,
    });
    expect(request.headers()['x-pvad-csrf']).toBe('company-account-e2e-csrf');
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: '11111111-1111-1111-1111-111111111111',
          username: 'customer@example.test',
          email: 'customer@example.test',
          displayName: 'Company Customer',
          roles: ['Organization Administrator'],
          permissions: [],
          organizations: [{
            id: '22222222-2222-2222-2222-222222222222',
            name: 'Example Company',
            slug: 'example-company',
          }],
          activeOrganizationId: '22222222-2222-2222-2222-222222222222',
          lastLoginAt: null,
        },
      }),
    });
  });

  const originalUrl = `${companyUrl}/pricing?utm_source=account-e2e#plans`;
  await page.goto(originalUrl);
  await page.locator('[data-pvad-storage-notice]').getByRole('button', { name: 'Understood' }).click();
  await page.getByRole('link', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(new RegExp(`^${accountUrl}/login\\?returnUrl=`));
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe(originalUrl);

  await page.locator('[name="identifier"]').fill('customer@example.test');
  await page.locator('[name="password"]').fill('Pvad-E2E-Password-2026!');
  await page.locator('#login-form').getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(originalUrl);
  await expect(page.getByRole('link', { name: 'My account' })).toHaveAttribute(
    'href',
    `${accountUrl}/dashboard`,
  );
  await expect(page.getByRole('link', { name: 'Sign in' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Register' })).toHaveCount(0);
  for (const marker of ['pvad_signed_in', 'pvad_signed_in']) {
    expect(await page.evaluate(key => localStorage.getItem(key), marker)).toBeNull();
    expect(new URL(page.url()).searchParams.has(marker)).toBe(false);
  }

  await page.reload();
  await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'My account' })).toHaveCount(0);
});
