import { expect, test } from '@playwright/test';

const sites = [
  ['Company', 'http://localhost:5173/'],
  ['Documentation', 'http://localhost:5174/'],
  ['Demo', 'http://localhost:5175/'],
  ['Account', 'http://localhost:5178/login'],
] as const;

test('necessary-storage notice is keyboard accessible and remembered on every public site', async ({
  context,
  page,
}) => {
  await page.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 401,
    contentType: 'application/problem+json',
    body: JSON.stringify({ title: 'Authentication required', status: 401 }),
  }));

  for (const [name, url] of sites) {
    await context.clearCookies();
    await page.goto(url);

    const notice = page.locator('[data-pxa-storage-notice]');
    await expect(notice, `${name} storage notice`).toBeVisible();
    await expect(notice).toHaveAttribute('aria-labelledby', 'pxa-storage-notice-title');
    await expect(notice.getByText('Optional analytics and marketing storage are not used.')).toBeVisible();
    await expect(notice.getByRole('link', { name: 'Learn more' })).toHaveAttribute(
      'href',
      /cookie-storage/,
    );

    const understood = notice.getByRole('button', { name: 'Understood' });
    await understood.focus();
    await expect(understood).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(notice).toBeHidden();
    await expect.poll(async () => (await context.cookies()).some((cookie) =>
      cookie.name === 'PXA.StorageNotice' && cookie.value === '2026-07')).toBe(true);

    await page.reload();
    await expect(notice).toHaveCount(0);
    await expect.poll(() => page.evaluate(() =>
      document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
  }
});

test('Company footer can reopen storage settings without creating optional consent controls', async ({
  page,
}) => {
  await page.context().addCookies([{
    name: 'PXA.StorageNotice',
    value: '2026-07',
    url: 'http://localhost:5173',
  }]);
  await page.goto('http://localhost:5173/');
  await expect(page.locator('[data-pxa-storage-notice]')).toHaveCount(0);

  await page.getByRole('button', { name: 'Storage settings' }).click();
  const notice = page.locator('[data-pxa-storage-notice]');
  await expect(notice).toBeVisible();
  await expect(notice.getByRole('button', { name: /accept all|reject all|customize/i })).toHaveCount(0);
});
