import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

const accountUrl = 'http://localhost:5178';
const designerUrl = 'http://localhost:5176';
const mailpitUrl = 'http://localhost:8025';

type MailpitMessage = {
  ID: string;
  To: Array<{ Address: string }>;
  Subject: string;
};

async function findVerificationUrl(request: APIRequestContext, email: string): Promise<string> {
  let verificationUrl = '';

  await expect.poll(async () => {
    const listResponse = await request.get(`${mailpitUrl}/api/v1/messages`);
    if (!listResponse.ok()) return '';

    const list = await listResponse.json() as { messages?: MailpitMessage[] };
    const message = list.messages?.find((candidate) =>
      candidate.To.some((recipient) => recipient.Address.toLowerCase() === email.toLowerCase())
      && /verify/i.test(candidate.Subject));
    if (!message) return '';

    const messageResponse = await request.get(`${mailpitUrl}/api/v1/message/${message.ID}`);
    if (!messageResponse.ok()) return '';

    const detail = await messageResponse.json() as { Text?: string; HTML?: string };
    const body = `${detail.Text ?? ''}\n${detail.HTML ?? ''}`.replaceAll('&amp;', '&');
    verificationUrl = body.match(/http:\/\/localhost:5178\/verify-email\?[^\s"'<>]+/)?.[0] ?? '';
    return verificationUrl;
  }, {
    message: `verification email for ${email}`,
    timeout: 25_000,
    intervals: [500, 1_000, 2_000],
  }).not.toBe('');

  return verificationUrl;
}

async function waitUntilSaved(page: Page): Promise<void> {
  await expect(page.getByRole('status').filter({ hasText: /^Saved$/ })).toBeVisible();
}

async function addElement(page: Page, name: RegExp): Promise<void> {
  await page.getByRole('button', { name }).click();
  await waitUntilSaved(page);
}

test('customer can register, verify, enter Designer, persist and version a template', async ({
  browser,
  page,
  request,
}) => {
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const email = `designer-e2e-${unique}@example.test`;
  const password = 'Pxa-E2E-Password-2026!';
  const returnUrl = `${designerUrl}/pdf/create`;

  await page.goto(`${accountUrl}/register?returnUrl=${encodeURIComponent(returnUrl)}`);
  await page.locator('[name="displayName"]').fill('Designer E2E User');
  await page.locator('[name="email"]').fill(email);
  await page.locator('[name="password"]').fill(password);
  await page.locator('[name="country"]').fill('DE');
  await page.locator('[name="acceptTerms"]').check();
  await page.locator('[name="acceptPrivacy"]').check();
  await page.locator('#register-form button[type="submit"]').click();

  await expect(page).toHaveURL(/\/registration-pending/);
  await expect(page.getByRole('heading', { name: /check your email/i })).toBeVisible();

  const verificationUrl = await findVerificationUrl(request, email);
  await page.goto(verificationUrl);
  await expect(page.getByRole('heading', { name: 'Your Trial is ready' })).toBeVisible();
  await page.getByRole('link', { name: 'Sign in' }).click();

  await page.locator('[name="identifier"]').fill(email);
  await page.locator('[name="password"]').fill(password);
  await page.locator('#login-form').getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(new RegExp(`^${designerUrl.replaceAll('.', '\\.')}/pdf/create`));
  await expect(page.getByRole('heading', { name: 'Untitled document' })).toBeVisible();
  await waitUntilSaved(page);

  await addElement(page, /single line text block/i);
  await page.goto(`${designerUrl}/pdf/template`);
  await expect(page.getByRole('heading', { name: 'Saved templates' })).toBeVisible();
  await page.getByRole('button', { name: /Untitled document/ }).first().click();
  await expect(page).toHaveURL(/\/pdf\/create\?templateId=/);
  const templateUrl = page.url();
  await waitUntilSaved(page);

  await page.getByRole('button', { name: 'Design actions' }).click();
  await page.getByRole('button', { name: 'Create version' }).click();
  await expect(page.getByText(/Version 1 created/)).toBeVisible();

  await page.getByRole('button', { name: 'Design actions' }).click();
  await page.getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText(/Template published/)).toBeVisible();

  const secondContext = await browser.newContext({
    storageState: await page.context().storageState(),
    viewport: { width: 1440, height: 900 },
  });
  const secondPage = await secondContext.newPage();
  await secondPage.goto(templateUrl);
  await waitUntilSaved(secondPage);

  await Promise.all([
    page.getByRole('button', { name: /single line text block/i }).click(),
    secondPage.getByRole('button', { name: /QR Code/ }).click(),
  ]);

  await expect.poll(async () =>
    Number(await page.locator('.designer-save-conflict').isVisible())
      + Number(await secondPage.locator('.designer-save-conflict').isVisible()),
  {
    message: 'one concurrent editor must receive an optimistic-concurrency conflict',
    timeout: 20_000,
  }).toBe(1);

  const conflictPage = await page.locator('.designer-save-conflict').isVisible() ? page : secondPage;
  await expect(conflictPage.getByText('A newer server draft exists')).toBeVisible();
  await conflictPage.getByRole('button', { name: 'Save as new template' }).click();
  await expect(conflictPage.locator('.designer-save-conflict')).toBeHidden();
  await waitUntilSaved(conflictPage);
  await secondContext.close();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${designerUrl}/pdf/template`);
  await expect(page.getByRole('heading', { name: 'Saved templates' })).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    .toBe(true);
});

test('mobile Designer shows an explicit entitlement denial state', async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();

  await page.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 403,
    contentType: 'application/problem+json',
    body: JSON.stringify({
      type: 'https://powerdoxautomation.com/problems/designer-access',
      title: 'Designer access denied',
      status: 403,
      code: 'PXA_TRIAL_EXPIRED',
    }),
  }));

  await page.goto(`${designerUrl}/pdf/create`);
  await expect(page.getByRole('heading', { name: 'Designer subscription expired' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Open PXA Account' })).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    .toBe(true);

  await context.close();
});
