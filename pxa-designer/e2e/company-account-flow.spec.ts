import { expect, test } from '@playwright/test';

const companyUrl = 'http://localhost:5173';
const accountUrl = 'http://localhost:5178';

test('Company sign-in returns to the original page without exposing the session signal', async ({ page }) => {
  await page.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 401,
    contentType: 'application/problem+json',
    body: JSON.stringify({
      title: 'Authentication required',
      status: 401,
    }),
  }));
  await page.route('**/api/pxa/v1/auth/csrf', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'company-account-e2e-csrf' }),
  }));
  await page.route('**/api/pxa/v1/auth/login', async (route) => {
    const request = route.request();
    expect(request.postDataJSON()).toEqual({
      identifier: 'customer@example.test',
      password: 'Pxa-E2E-Password-2026!',
      rememberMe: false,
    });
    expect(request.headers()['x-pxa-csrf']).toBe('company-account-e2e-csrf');
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
  await page.getByRole('link', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(new RegExp(`^${accountUrl}/login\\?returnUrl=`));
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe(originalUrl);

  await page.locator('[name="identifier"]').fill('customer@example.test');
  await page.locator('[name="password"]').fill('Pxa-E2E-Password-2026!');
  await page.locator('#login-form').getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(originalUrl);
  await expect(page.getByRole('link', { name: 'My account' })).toHaveAttribute(
    'href',
    `${accountUrl}/dashboard`,
  );
  await expect(page.getByRole('link', { name: 'Sign in' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Register' })).toHaveCount(0);
  expect(await page.evaluate(() => localStorage.getItem('pxa_signed_in'))).toBe('1');
  expect(new URL(page.url()).searchParams.has('pxa_signed_in')).toBe(false);
});
