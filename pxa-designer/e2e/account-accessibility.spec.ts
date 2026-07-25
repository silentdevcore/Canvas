import { expect, test } from '@playwright/test';

const accountUrl = 'http://localhost:5178';

test('Account registration supports keyboard submission and native focus validation', async ({ page }) => {
  await page.goto(`${accountUrl}/register`);

  const submit = page.locator('#register-form button[type="submit"]');
  await submit.focus();
  await page.keyboard.press('Enter');

  await expect(page.locator('[name="displayName"]')).toBeFocused();
  expect(await page.locator('[name="displayName"]')
    .evaluate((input: HTMLInputElement) => input.validity.valueMissing)).toBe(true);

  await page.locator('[name="displayName"]').fill('Keyboard User');
  await page.locator('[name="email"]').fill('not-an-email');
  await submit.focus();
  await page.keyboard.press('Enter');

  await expect(page.locator('[name="email"]')).toBeFocused();
  expect(await page.locator('[name="email"]')
    .evaluate((input: HTMLInputElement) => input.validity.typeMismatch)).toBe(true);
});

test('Account hides inaccessible navigation and renders a consistent forbidden route', async ({ page }) => {
  await page.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: '11111111-1111-1111-1111-111111111111',
      username: 'viewer@pxa.test',
      email: 'viewer@pxa.test',
      displayName: 'Viewer',
      roles: ['Viewer'],
      permissions: [
        'account.profile.manage',
        'account.organization.read',
        'account.subscription.read',
        'account.licenses.read',
        'account.sessions.manage',
      ],
      organizations: [{
        id: '22222222-2222-2222-2222-222222222222',
        name: 'Viewer Organization',
        slug: 'viewer-organization',
      }],
      activeOrganizationId: '22222222-2222-2222-2222-222222222222',
      lastLoginAt: null,
      apiVersion: '1',
    }),
  }));

  await page.goto(`${accountUrl}/developer-access`);
  await expect(page.getByRole('heading', { name: 'You do not have access to this page' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Account' })
    .getByRole('link', { name: 'Developer access' })).toHaveCount(0);

  await page.getByRole('link', { name: 'Back to overview' }).focus();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(`${accountUrl}/dashboard`);
  await expect(page.getByRole('heading', { name: 'Viewer Organization' })).toBeVisible();
});
