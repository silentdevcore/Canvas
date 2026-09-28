import {
  expect,
  test,
  type APIRequestContext,
  type Page,
} from '@playwright/test';
import { createHash } from 'node:crypto';

/**
 * The electronic-invoice workflow end to end, as a signed-in customer performs it.
 *
 * Every other electronic-invoice test proves a part in isolation: the projection, the generator, the
 * fail-closed refusals, the cross-surface agreement. This proves the parts join up for a real
 * account - register, enter the Designer through the Account handoff, build an invoice, watch the
 * totals follow it, save and reload it, generate the XML and the hybrid PDF, reopen that PDF in the
 * Viewer, and read the same bytes back out of it.
 *
 * It also proves the negatives, which matter more: an unauthenticated visitor, a signed-in user
 * without the entitlement, and an unreachable API each get a stated refusal rather than a blank page
 * or a silent success.
 *
 * Infrastructure: the Designer (5176), Account (5178), Mailpit (8025), the API and PostgreSQL all
 * have to be running, exactly as `pdf-viewer-trust-authenticated.spec.ts` requires. Identities are
 * registered through the real flow; nothing here hard-codes a credential or bypasses authorization.
 */

const designerUrl = 'http://localhost:5176';
const accountUrl = 'http://localhost:5178';
const mailpitUrl = 'http://localhost:8025';

const password = 'Pxa-EInvoice-E2E-2026!';

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

/** Registers a fresh identity and lands in the Designer through the real Account handoff. */
async function registerAndEnterDesigner(
  page: Page,
  request: APIRequestContext,
  returnUrl: string,
): Promise<string> {
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const email = `e-invoice-live-${unique}@example.test`;

  await page.goto(`${accountUrl}/register?returnUrl=${encodeURIComponent(returnUrl)}`);
  const storageNotice = page.locator('[data-pxa-storage-notice]');
  if (await storageNotice.isVisible()) {
    await storageNotice.getByRole('button', { name: 'Understood' }).click();
  }
  await page.locator('[name="displayName"]').fill('E-Invoice E2E User');
  await page.locator('[name="email"]').fill(email);
  await page.locator('[name="password"]').fill(password);
  await page.locator('[name="country"]').fill('DE');
  await page.locator('[name="acceptTerms"]').check();
  await page.locator('[name="acceptPrivacy"]').check();
  await page.locator('#register-form button[type="submit"]').click();

  await expect(page).toHaveURL(/\/registration-pending/);
  await page.goto(await findVerificationUrl(request, email));
  await expect(page.getByRole('heading', { name: 'Your Trial is ready' })).toBeVisible();
  await page.getByRole('link', { name: 'Sign in' }).click();
  await page.locator('[name="identifier"]').fill(email);
  await page.locator('[name="password"]').fill(password);
  await page.locator('#login-form').getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(new RegExp(`^${designerUrl.replaceAll('.', '\\.')}/`));
  // The authorization code must never survive in the address bar.
  expect(new URL(page.url()).searchParams.has('code')).toBe(false);
  expect(new URL(page.url()).searchParams.has('state')).toBe(false);
  return email;
}

async function dismissProductPanel(page: Page): Promise<void> {
  const panel = page.locator('.pxa-product-overlay');
  try {
    await panel.waitFor({ state: 'visible', timeout: 3_000 });
    await panel.getByRole('button', { name: 'Close panel' }).click();
  } catch {
    // The release panel only opens for users who have not seen the current release.
  }
}

/** Starts an invoice if the workspace is still empty. */
async function startInvoice(page: Page): Promise<void> {
  const start = page.getByRole('button', { name: 'Start an invoice' });
  if (await start.isVisible().catch(() => false)) await start.click();
  await expect(page.getByRole('heading', { name: 'Invoice details' })).toBeVisible();
}

const field = (page: Page, id: string) => page.locator(`#${id}`);

test.describe('electronic invoice workspace', () => {
  // Opt-in, like the PDF Trust live workflow: this needs the Account, Designer, WebApi, PostgreSQL
  // and Mailpit services all running, which a plain checkout does not have.
  test.beforeEach(() => {
    test.skip(
      process.env.PXA_RUN_LIVE_AUTH_EINVOICE !== '1',
      'Requires running Account, Designer, WebApi, PostgreSQL, and Mailpit services. '
      + 'Set PXA_RUN_LIVE_AUTH_EINVOICE=1 to run it.',
    );
  });

  test('carries one invoice from the editor to a reopened hybrid PDF', async ({ page, request }) => {
    await registerAndEnterDesigner(page, request, `${designerUrl}/pdf/e-invoice`);
    await dismissProductPanel(page);
    await page.goto(`${designerUrl}/pdf/e-invoice`);
    await startInvoice(page);

    // ── Seller and buyer ──────────────────────────────────────────────────
    await field(page, 'einv-invoiceNumber').fill('E2E-INV-2026-0001');
    await field(page, 'einv-seller-name').fill('E2E Seller GmbH');
    await field(page, 'einv-seller-address-street').fill('Hauptstrasse 1');
    await field(page, 'einv-seller-address-city').fill('Berlin');
    await field(page, 'einv-seller-address-postalCode').fill('10115');
    await field(page, 'einv-seller-address-countryCode').fill('DE');
    await field(page, 'einv-buyer-name').fill('E2E Buyer AG');
    await field(page, 'einv-buyer-address-street').fill('Buyer Street 2');
    await field(page, 'einv-buyer-address-city').fill('Munich');
    await field(page, 'einv-buyer-address-postalCode').fill('80331');
    await field(page, 'einv-buyer-address-countryCode').fill('DE');

    // ── Lines: add, edit, reorder, remove ─────────────────────────────────
    const lines = page.getByRole('table', { name: 'Invoice lines' });
    const firstRow = page.getByTestId('einvoice-line-0');
    await field(page, 'einv-line-0-description').fill('Consulting');
    await field(page, 'einv-line-0-quantity').fill('1');
    await field(page, 'einv-line-0-unitPrice').fill('100');
    await field(page, 'einv-line-0-taxRate').fill('19');

    await page.getByRole('button', { name: 'Add line' }).click();
    const secondRow = page.getByTestId('einvoice-line-1');
    await field(page, 'einv-line-1-description').fill('Handbook');
    await field(page, 'einv-line-1-quantity').fill('2');
    await field(page, 'einv-line-1-unitPrice').fill('30');
    await field(page, 'einv-line-1-taxRate').fill('7');

    await page.getByRole('button', { name: 'Add line' }).click();
    const thirdRow = page.getByTestId('einvoice-line-2');
    await field(page, 'einv-line-2-description').fill('To be removed');
    await field(page, 'einv-line-2-unitPrice').fill('5');
    await thirdRow.getByRole('button', { name: /^Remove line:/ }).click();
    await expect(lines.getByRole('row')).toHaveCount(3);

    // Reorder, then put the order back, so the totals must be order-independent.
    await secondRow.getByRole('button', { name: /^Move line up:/ }).click();
    await expect(field(page, 'einv-line-0-description'))
      .toHaveValue('Handbook');
    await firstRow.getByRole('button', { name: /^Move line down:/ }).click();
    await expect(field(page, 'einv-line-0-description'))
      .toHaveValue('Consulting');

    // ── Totals and tax groups follow the lines ────────────────────────────
    // 1 x 100.00 at 19% and 2 x 30.00 at 7%: net 160.00, tax 19.00 + 4.20, payable 183.20.
    await expect(page.getByTestId('einvoice-total-net')).toContainText('160.00');
    await expect(page.getByTestId('einvoice-total-tax')).toContainText('23.20');
    await expect(page.getByTestId('einvoice-total-grand')).toContainText('183.20');
    await expect(page.getByTestId('einvoice-tax-S-19')).toBeVisible();
    await expect(page.getByTestId('einvoice-tax-S-7')).toBeVisible();

    // ── Validation and conformance states stay distinct ───────────────────
    await page.getByRole('button', { name: 'Validate' }).click();
    const diagnostics = page.getByRole('region', { name: 'Validation' });
    await expect(diagnostics).toBeVisible();
    // Structural recognition must never be presented as official conformance.
    await expect(diagnostics).not.toContainText('Validated against the pinned official artifacts');

    // ── Save and reload ───────────────────────────────────────────────────
    await expect(page.getByTestId('einvoice-save-state')).toHaveAttribute('data-state', 'saved', {
      timeout: 20_000,
    });
    await page.reload();
    await expect(field(page, 'einv-invoiceNumber')).toHaveValue('E2E-INV-2026-0001');
    await expect(lines.getByRole('row')).toHaveCount(3);
    await expect(page.getByTestId('einvoice-total-grand')).toContainText('183.20');

    // ── Generate XML, and keep its bytes ──────────────────────────────────
    await page.getByTestId('einvoice-generate-xml').click();
    await expect(page.getByTestId('einvoice-download-xml')).toBeVisible();
    const xmlDownload = page.waitForEvent('download');
    await page.getByTestId('einvoice-download-xml').click();
    const xmlStream = await (await xmlDownload).createReadStream();
    const xmlChunks: Buffer[] = [];
    for await (const chunk of xmlStream) xmlChunks.push(Buffer.from(chunk));
    const xml = Buffer.concat(xmlChunks);
    expect(xml.toString('utf8')).toContain('E2E-INV-2026-0001');
    expect(xml.toString('utf8')).toContain('183.20');
    const xmlSha256 = createHash('sha256').update(xml).digest('hex');

    // ── Create the hybrid PDF ─────────────────────────────────────────────
    await page.getByTestId('einvoice-create-pdf').click();
    await expect(page.getByTestId('einvoice-open-viewer'))
      .toBeEnabled({ timeout: 30_000 });

    // ── Reopen it in the Viewer and inspect the embedded invoice ──────────
    await page.getByTestId('einvoice-open-viewer').click();
    await expect(page).toHaveURL(/\/pdf\/viewer/);
    await expect(page.getByText('factur-x.xml')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('E2E-INV-2026-0001')).toBeVisible();
    await expect(page.getByText('183.20')).toBeVisible();

    // ── Extract the XML and compare it byte for byte ──────────────────────
    const extracted = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Download invoice XML' }).click();
    const extractedStream = await (await extracted).createReadStream();
    const extractedChunks: Buffer[] = [];
    for await (const chunk of extractedStream) extractedChunks.push(Buffer.from(chunk));
    const extractedXml = Buffer.concat(extractedChunks);

    // The bytes that came out of the finished document are the bytes that went in.
    expect(createHash('sha256').update(extractedXml).digest('hex')).toBe(xmlSha256);
  });

  test('refuses an unauthenticated visitor rather than showing the workspace', async ({ page }) => {
    await page.context().clearCookies();
    await page.goto(`${designerUrl}/pdf/e-invoice`);

    // Either the Account sign-in takes over, or the Designer states the refusal. Never the editor.
    await expect(page.getByRole('heading', { name: 'Invoice details' })).toBeHidden();
    await expect
      .poll(() => page.url().startsWith(accountUrl) || page.url().includes('/sign-in'))
      .toBe(true);
  });

  test('states the refusal when the Designer entitlement is missing', async ({ page, request }) => {
    await registerAndEnterDesigner(page, request, `${designerUrl}/pdf/e-invoice`);
    await dismissProductPanel(page);

    // The entitlement is served by the product experience endpoint; withdraw it and the workspace
    // has to say so rather than render an editor the account may not use.
    await page.route('**/api/pxa/v1/designer/features', route => route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{
        id: 'designer.e-invoice',
        titleKey: 'features.eInvoice.title',
        descriptionKey: 'features.eInvoice.description',
        fallbackTitle: 'Electronic invoicing',
        fallbackDescription: 'Electronic invoicing is unavailable for this account.',
        maturity: 'beta',
        introducedIn: '1.1.0',
        defaultEnabled: true,
        documentationPath: '/#electronic-invoicing-details',
        enabled: false,
        decisionCode: 'PXA_DESIGNER_ENTITLEMENT_REQUIRED',
        decisionReason: 'The active subscription does not include electronic invoicing.',
      }]),
    }));
    await page.goto(`${designerUrl}/pdf/e-invoice`);

    await expect(page.getByRole('heading', { name: 'Invoice details' })).toBeHidden();
    await expect(page.locator('.einv-state-card')).toBeVisible();
  });

  test('states the refusal when the invoice API is unavailable', async ({ page, request }) => {
    await registerAndEnterDesigner(page, request, `${designerUrl}/pdf/e-invoice`);
    await dismissProductPanel(page);
    await page.goto(`${designerUrl}/pdf/e-invoice`);
    await startInvoice(page);

    await page.route('**/api/pxa/v1/pdf/e-invoices/**', route => route.fulfill({
      status: 503,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        type: 'https://pxa.invalid/problems/unavailable',
        title: 'The invoice worker is unavailable.',
        status: 503,
      }),
    }));

    await page.getByRole('button', { name: 'Validate' }).click();

    // A stated failure, and nothing that could be read as a successful validation.
    await expect(page.locator('.einv-error')).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('.einv-error')).not.toContainText('Validated against');
  });
});
