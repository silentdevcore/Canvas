import { expect, test } from '@playwright/test';

const accountUrl = 'http://localhost:5178';
const designerUrl = 'http://localhost:5176';
const email = process.env.PXA_SMOKE_EMAIL;
const password = process.env.PXA_SMOKE_PASSWORD;

test('authenticated customer completes the Code Designer roundtrip', async ({ page }) => {
  test.skip(!email || !password, 'Set PXA_SMOKE_EMAIL and PXA_SMOKE_PASSWORD for the live smoke test.');

  await page.goto(`${designerUrl}/pdf/create?mode=code`);
  await expect(page).toHaveURL(new RegExp(`^${accountUrl}/login\\?returnUrl=`));
  const authorizationUrl = new URL(page.url()).searchParams.get('returnUrl');
  expect(authorizationUrl).toMatch(new RegExp(`^${accountUrl}/designer-authorize\\?`));

  await page.locator('[name="identifier"]').fill(email!);
  await page.locator('[name="password"]').fill(password!);
  await page.locator('#login-form').getByRole('button', { name: 'Sign in' }).click();

  try {
    await page.waitForURL(new RegExp(`^${designerUrl}/pdf/create\\?mode=code`), { timeout: 12_000 });
  } catch {
    // Accounts with a pending legal acknowledgement remain on Account after login.
    // Reopening the original authorization request still uses the normal server-side
    // entitlement and one-time-code checks; it does not bypass them.
    await page.goto(authorizationUrl!);
    await page.waitForURL(new RegExp(`^${designerUrl}/pdf/create\\?mode=code`), { timeout: 20_000 });
  }

  expect(new URL(page.url()).searchParams.has('code')).toBe(false);
  expect(new URL(page.url()).searchParams.has('state')).toBe(false);
  await expect(page.getByText('Code Editor')).toBeVisible();

  const apply = page.getByRole('button', { name: 'Apply to Designer' });
  await expect(apply).toBeEnabled({ timeout: 20_000 });

  await page.getByRole('button', { name: 'Hello World' }).click();
  await expect(page.getByRole('tab', { name: /JSON Modified/ })).toBeVisible();
  await expect(page.getByRole('tab', { name: /JSON Saved/ })).toBeVisible({ timeout: 10_000 });

  await page.getByRole('button', { name: 'Convert' }).click();
  const review = page.getByRole('dialog', { name: 'Conversion review' });
  await expect(review).toContainText('exact');
  await expect(review).toContainText('Added');
  await review.getByRole('button', { name: 'Apply generated draft' }).click();

  await expect(page.getByRole('tab', { name: /C# Model Generated/ })).toBeVisible();
  await page.getByRole('button', { name: 'Apply to Designer' }).click();
  await expect(page.getByRole('tab', { name: /C# Model Saved/ })).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText('Hello, PXA PDF!')).toBeVisible();

  await page.getByRole('button', { name: 'Restore' }).click();
  await expect(page.getByRole('tab', { name: /C# Model Outdated/ })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
