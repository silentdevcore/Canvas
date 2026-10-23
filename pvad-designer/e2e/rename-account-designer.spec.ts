import { expect, test } from '@playwright/test';

const account = 'http://localhost:5178';
const designer = 'http://localhost:5176';
const mailpit = 'http://localhost:8025';

for (const viewport of [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'mobile', width: 390, height: 844 },
]) {
  test(`PVAD rename: real Account handoff and navigation on ${viewport.name}`, async ({ page, request }) => {
    test.setTimeout(120_000);
    await page.setViewportSize(viewport);
    const email = `pvad-rename-${Date.now()}-${viewport.name}@example.test`;
    const password = 'Pvad-Synthetic-E2E-2026!';
    const pageErrors: string[] = [];
    page.on('pageerror', error => pageErrors.push(error.name));

    // Start from the protected product to exercise the real redirect and PKCE handoff.
    await page.goto(`${designer}/pdf/create`);
    await expect(page).toHaveURL(new RegExp(`^${account}/login`));
    const notice = page.locator('[data-pxa-storage-notice]');
    if (await notice.isVisible()) await notice.getByRole('button', { name: 'Understood' }).click();
    await page.getByRole('link', { name: 'Create account' }).click();
    await expect(page).toHaveURL(/\/register/);
    await expect(page.locator('#register-form button[type="submit"]')).toBeEnabled();
    await page.locator('[name="displayName"]').fill('PVAD Rename Synthetic User');
    await page.locator('[name="email"]').fill(email);
    await page.locator('[name="password"]').fill(password);
    await page.locator('[name="country"]').fill('CH');
    await page.locator('[name="acceptTerms"]').check();
    await page.locator('[name="acceptPrivacy"]').check();
    await page.locator('#register-form button[type="submit"]').click();
    await expect(page).toHaveURL(/\/registration-pending/);

    let verificationUrl = '';
    await expect.poll(async () => {
      const response = await request.get(`${mailpit}/api/v1/messages`);
      expect(response.ok()).toBe(true);
      const list = await response.json() as {
        messages?: Array<{ ID: string; Subject: string; To: Array<{ Address: string }> }>;
      };
      const message = list.messages?.find(item => /verify/i.test(item.Subject)
        && item.To.some(recipient => recipient.Address.toLowerCase() === email));
      if (!message) return false;
      const detail = await (await request.get(`${mailpit}/api/v1/message/${message.ID}`)).json();
      verificationUrl = `${detail.Text ?? ''}\n${detail.HTML ?? ''}`.replaceAll('&amp;', '&')
        .match(/http:\/\/localhost:5178\/verify-email\?[^\s"'<>]+/)?.[0] ?? '';
      return Boolean(verificationUrl);
    }, { timeout: 30_000 }).toBe(true);

    await page.goto(verificationUrl);
    await expect(page.getByRole('heading', { name: 'Your Trial is ready' })).toBeVisible();
    await page.getByRole('link', { name: 'Sign in' }).click();
    await page.locator('[name="identifier"]').fill(email);
    await page.locator('[name="password"]').fill(password);
    await page.locator('#login-form button[type="submit"]').click();
    await expect(page).toHaveURL(new RegExp(`^${designer}/pdf/create`));
    expect(new URL(page.url()).searchParams.has('code')).toBe(false);
    expect(new URL(page.url()).searchParams.has('state')).toBe(false);
    await expect(page.getByRole('button', { name: 'Rename Untitled document' })).toBeVisible();

    const session = await page.request.get(`${designer}/api/pvad/v1/auth/me`);
    expect(session.ok()).toBe(true);
    expect((await session.json()).email).toBe(email);
    await page.reload();
    await expect(page.getByRole('button', { name: 'Rename Untitled document' })).toBeVisible();
    for (const route of ['/pdf/template', '/pdf/migrations/code', '/pdf/viewer']) {
      await page.goto(`${designer}${route}`);
      await expect(page.locator('.pdf-nav')).toBeVisible();
      await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
        .toBe(true);
    }
    expect(pageErrors).toEqual([]);
  });
}
