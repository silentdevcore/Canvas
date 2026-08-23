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
  await dismissProductPanel(page);
  await page.getByRole('button', { name }).click();
  await waitUntilSaved(page);
}

async function dismissProductPanel(page: Page): Promise<void> {
  const panel = page.locator('.pxa-product-overlay');
  try {
    await panel.waitFor({ state: 'visible', timeout: 3_000 });
    await panel.getByRole('button', { name: 'Close panel' }).click();
  } catch {
    // The panel only opens once per newly seen release.
  }
}

test('customer can register, verify, enter Designer, persist and version a template', async ({
  browser,
  page,
  request,
}) => {
  test.setTimeout(180_000);
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const email = `designer-e2e-${unique}@example.test`;
  const password = 'Pxa-E2E-Password-2026!';
  const returnUrl = `${designerUrl}/pdf/create`;

  await page.goto(`${accountUrl}/register?returnUrl=${encodeURIComponent(returnUrl)}`);
  const storageNotice = page.locator('[data-pxa-storage-notice]');
  if (await storageNotice.isVisible()) {
    await storageNotice.getByRole('button', { name: 'Understood' }).click();
    await expect(storageNotice).toBeHidden();
  }
  await expect(page.locator('#register-form button[type="submit"]')).toBeEnabled();
  await page.locator('[name="displayName"]').fill('Designer E2E User');
  await page.locator('[name="email"]').fill(email);
  await page.locator('[name="password"]').fill(password);
  await page.locator('[name="country"]').fill('DE');
  await page.locator('[name="acceptTerms"]').check();
  await page.locator('[name="acceptPrivacy"]').check();
  await expect(page.locator('[name="displayName"]')).toHaveValue('Designer E2E User');
  await expect(page.locator('[name="email"]')).toHaveValue(email);
  await expect(page.locator('[name="password"]')).toHaveValue(password);
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
  await dismissProductPanel(page);
  expect(new URL(page.url()).searchParams.has('code')).toBe(false);
  expect(new URL(page.url()).searchParams.has('state')).toBe(false);
  await expect(page.getByRole('button', { name: 'Rename Untitled document' })).toBeVisible();
  await waitUntilSaved(page);

  await page.getByRole('button', { name: 'Rename Untitled document' }).click();
  const documentName = page.getByRole('textbox', { name: 'Document name' });
  await documentName.fill('Designer E2E Report');
  await documentName.press('Enter');
  await expect(page.getByRole('button', { name: 'Rename Designer E2E Report' })).toBeVisible();
  await waitUntilSaved(page);

  await addElement(page, /single line text block/i);
  await addElement(page, /QR Code/);

  const pageViewport = page.locator('.editor-page-viewport');
  const canvasElements = page.locator('.editor-canvas-element:not(.is-shared)');
  await expect(canvasElements).toHaveCount(2);

  await pageViewport.evaluate((element) => {
    element.scrollLeft = Math.max(0, element.scrollWidth - element.clientWidth);
    element.scrollTop = Math.max(0, element.scrollHeight - element.clientHeight);
  });

  const firstElement = canvasElements.first();
  await firstElement.click();
  const beforeDrag = await firstElement.boundingBox();
  expect(beforeDrag).not.toBeNull();
  await page.mouse.move(beforeDrag!.x + beforeDrag!.width / 2, beforeDrag!.y + beforeDrag!.height / 2);
  await page.mouse.down();
  await page.mouse.move(beforeDrag!.x + beforeDrag!.width / 2 + 28, beforeDrag!.y + beforeDrag!.height / 2 + 22);
  await page.mouse.up();
  await waitUntilSaved(page);
  const afterDrag = await firstElement.boundingBox();
  expect(afterDrag!.x).toBeGreaterThan(beforeDrag!.x + 15);
  expect(afterDrag!.y).toBeGreaterThan(beforeDrag!.y + 10);

  await firstElement.click();
  const southEastHandle = firstElement.locator('.editor-resize-handle[style*="se-resize"]');
  const handleBox = await southEastHandle.boundingBox();
  expect(handleBox).not.toBeNull();
  const beforeResize = await firstElement.boundingBox();
  await page.mouse.move(handleBox!.x + handleBox!.width / 2, handleBox!.y + handleBox!.height / 2);
  await page.mouse.down();
  await page.mouse.move(handleBox!.x + handleBox!.width / 2 + 24, handleBox!.y + handleBox!.height / 2 + 18);
  await page.mouse.up();
  await waitUntilSaved(page);
  const afterResize = await firstElement.boundingBox();
  expect(afterResize!.width).toBeGreaterThan(beforeResize!.width + 10);
  expect(afterResize!.height).toBeGreaterThan(beforeResize!.height + 8);

  await firstElement.click({ button: 'right' });
  await expect(page.locator('.editor-context-menu')).toBeVisible();
  await expect(page.locator('.editor-context-menu').getByRole('button', { name: /Duplicate/ })).toBeVisible();
  await page.locator('.editor-context-menu-backdrop').click({ position: { x: 2, y: 2 } });

  const pageSurface = page.locator('.editor-page');
  const surfaceBox = await pageSurface.boundingBox();
  const elementBoxes = await canvasElements.evaluateAll(elements => elements.map(element => {
    const rect = element.getBoundingClientRect();
    return { left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom };
  }));
  expect(surfaceBox).not.toBeNull();
  const marqueeEndX = Math.min(surfaceBox!.x + surfaceBox!.width - 2, Math.max(...elementBoxes.map(box => box.right)) + 8);
  const marqueeEndY = Math.min(surfaceBox!.y + surfaceBox!.height - 2, Math.max(...elementBoxes.map(box => box.bottom)) + 8);
  await page.mouse.move(surfaceBox!.x + 2, surfaceBox!.y + 2);
  await page.mouse.down();
  await page.mouse.move(marqueeEndX, marqueeEndY, { steps: 4 });
  await page.mouse.up();
  await expect(page.locator('.editor-canvas-element.is-multi-selected')).toHaveCount(1);

  for (const viewport of [
    { width: 1280, height: 720 },
    { width: 1440, height: 900 },
    { width: 1920, height: 1080 },
  ]) {
    await page.setViewportSize(viewport);
    const measurements = await page.evaluate(() => {
      const stage = document.querySelector<HTMLElement>('.editor-stage');
      const toolPanel = document.querySelector<HTMLElement>('.editor-tool-panel');
      const inspectorPanel = document.querySelector<HTMLElement>('.editor-inspector-panel');
      const pageViewportElement = document.querySelector<HTMLElement>('.editor-page-viewport');
      const scrollbar = document.querySelector<HTMLElement>('.editor-stage .editor-persistent-scrollbar');
      return {
        stageHeight: stage?.getBoundingClientRect().height ?? 0,
        toolHeight: toolPanel?.getBoundingClientRect().height ?? 0,
        inspectorHeight: inspectorPanel?.getBoundingClientRect().height ?? 0,
        scrollWidth: pageViewportElement?.scrollWidth ?? 0,
        clientWidth: pageViewportElement?.clientWidth ?? 0,
        scrollHeight: pageViewportElement?.scrollHeight ?? 0,
        clientHeight: pageViewportElement?.clientHeight ?? 0,
        scrollbarVisible: Boolean(scrollbar && getComputedStyle(scrollbar).display !== 'none'),
      };
    });
    expect(Math.abs(measurements.toolHeight - measurements.stageHeight)).toBeLessThanOrEqual(1);
    expect(Math.abs(measurements.inspectorHeight - measurements.stageHeight)).toBeLessThanOrEqual(1);
    expect(measurements.scrollWidth).toBeGreaterThanOrEqual(measurements.clientWidth);
    expect(measurements.scrollHeight).toBeGreaterThanOrEqual(measurements.clientHeight);
    expect(measurements.scrollbarVisible).toBe(true);
  }

  await page.getByTitle('Add page').click();
  await waitUntilSaved(page);
  await page.goto(`${designerUrl}/pdf/template`);
  await expect(page.getByRole('heading', { name: 'Saved templates' })).toBeVisible();
  await page.getByRole('button', { name: /Designer E2E Report/ }).first().click();
  await expect(page).toHaveURL(/\/pdf\/create\?templateId=/);
  const templateUrl = page.url();
  await expect(page.getByRole('button', { name: 'Rename Designer E2E Report' })).toBeVisible();
  await waitUntilSaved(page);

  await page.getByRole('button', { name: 'Preview' }).click();
  await expect(page.getByRole('heading', { name: /Preview: Designer E2E Report/ })).toBeVisible();

  await page.getByRole('button', { name: 'Export' }).click();
  const pdfDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: /Export PDF/ }).click();
  await expect((await pdfDownload).suggestedFilename()).toBe('Designer-E2E-Report.pdf');

  await expect(page.getByRole('button', { name: 'Export' })).toBeVisible({ timeout: 5_000 });
  await page.getByRole('button', { name: 'Export' }).click();
  const jsonDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: /Export JSON/ }).click();
  await expect((await jsonDownload).suggestedFilename()).toBe('Designer-E2E-Report.json');

  await expect(page.getByRole('button', { name: 'Export' })).toBeVisible({ timeout: 5_000 });
  await page.getByRole('button', { name: 'Export' }).click();
  const printTarget = page.waitForEvent('popup');
  await page.getByRole('button', { name: /^Print/ }).click();
  const pdfPrintPage = await printTarget;
  await expect.poll(() => pdfPrintPage.url()).toMatch(/^blob:/);
  await pdfPrintPage.close();

  await expect(page.getByRole('button', { name: 'Export' })).toBeVisible({ timeout: 5_000 });
  await page.getByRole('button', { name: 'Export' }).click();
  await page.getByRole('button', { name: /More formats/ }).click();
  const docxDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export as Word (.docx)' }).click();
  await expect((await docxDownload).suggestedFilename()).toBe('Designer-E2E-Report.docx');

  const pngDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export as PNG' }).click();
  await expect((await pngDownload).suggestedFilename()).toBe('Designer-E2E-Report-png-pages.zip');
  await page.getByRole('button', { name: 'Close' }).click();
  await page.getByRole('button', { name: 'Back to editor' }).click();

  await page.getByRole('button', { name: 'Design actions' }).click();
  await page.getByRole('button', { name: 'Create version' }).click();
  await expect(page.getByText(/Version 1 created/)).toBeVisible();

  await page.getByRole('button', { name: 'Design actions' }).click();
  await page.getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText(/Template published/)).toBeVisible();

  const codeUrl = new URL(templateUrl);
  codeUrl.searchParams.set('mode', 'code');
  await page.goto(codeUrl.toString());
  await expect(page.getByText('Code Editor')).toBeVisible();
  await expect(page.getByRole('tab', { name: /JSON/ })).toBeVisible();
  await expect(page.getByRole('tab', { name: /C# Model/ })).toBeVisible();
  await expect(page.getByRole('tab', { name: /C# PDF/ })).toBeVisible();
  await expect(page.getByRole('tab', { name: /FromBase64String/ })).toBeVisible();

  const target = page.getByLabel('Conversion target');
  for (const conversion of [
    { target: 'csharpModel', tab: /C# Model Generated/ },
    { target: 'csharpPdf', tab: /C# PDF Generated/ },
    { target: 'csharpBase64', tab: /FromBase64String Generated/ },
    { target: 'json', tab: /JSON Generated/ },
  ]) {
    await target.selectOption(conversion.target);
    await page.getByRole('button', { name: 'Convert' }).click();
    const review = page.getByRole('dialog', { name: 'Conversion review' });
    await expect(review).toContainText('Document fidelity: exact');
    await expect(review).toContainText(/Source preservation: (regenerated|structureLost)/);
    await review.getByRole('button', { name: 'Apply generated draft' }).click();
    await expect(page.getByRole('tab', { name: conversion.tab })).toBeVisible();
  }
  await page.getByRole('button', { name: 'Run' }).click();
  await expect(page.locator('.code-workspace-error')).toHaveCount(0);
  await expect(page.locator('.diagnostic-error')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Apply to Designer' })).toBeEnabled();
  await page.getByRole('button', { name: 'Apply to Designer' }).click();
  await expect(page.getByRole('tab', { name: /JSON Saved/ })).toBeVisible({ timeout: 20_000 });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(codeUrl.toString());
  await expect(page.getByText('Code Editor')).toBeVisible();
  await page.getByRole('tab', { name: /FromBase64String/ }).scrollIntoViewIfNeeded();
  await expect(page.getByRole('tab', { name: /FromBase64String/ })).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    .toBe(true);
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(templateUrl);
  await waitUntilSaved(page);

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

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(designerUrl);
  await dismissProductPanel(page);
  await page.getByRole('button', { name: /Sign DOCX/ }).click();
  await expect(page.getByRole('status')).toBeVisible();
  await expect(page.getByRole('button', { name: /Convert to Spreadsheet/ })).toBeDisabled();

  const languageSwitcher = page.locator('select.pdf-language-switcher').first();
  for (const locale of ['de', 'fr', 'es', 'it', 'ar', 'en']) {
    await languageSwitcher.selectOption(locale);
    await expect(page.locator('html')).toHaveAttribute('lang', locale);
    await expect(page.locator('html')).toHaveAttribute('dir', locale === 'ar' ? 'rtl' : 'ltr');
    if (locale === 'ar') {
      await expect.poll(() => page.evaluate(() => getComputedStyle(document.body).fontFamily))
        .toContain('Noto Sans Arabic');
    }
  }
  await languageSwitcher.selectOption('ar');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await page.locator('select.pdf-language-switcher').first().selectOption('en');

  await page.goto(`${designerUrl}/pdf/import`);
  const sidebarToggle = page.getByRole('button', { name: 'Collapse sidebar' });
  await sidebarToggle.click();
  await expect(page.locator('.hub-sidebar')).toHaveClass(/is-collapsed/);
  await page.reload();
  await expect(page.locator('.hub-sidebar')).toHaveClass(/is-collapsed/);
  await page.getByRole('button', { name: 'Expand sidebar' }).click();

  for (const legacyRoute of [
    ['/template', '/pdf/template'],
    ['/importer', '/pdf/import'],
    ['/pdf-viewer', '/pdf/viewer'],
    ['/migrations/code/spreadsheet', '/spreadsheet/migrations/code'],
  ] as const) {
    await page.goto(`${designerUrl}${legacyRoute[0]}`);
    await expect.poll(() => new URL(page.url()).pathname).toBe(legacyRoute[1]);
  }

  await page.evaluate(() => {
    localStorage.removeItem('pxa-spreadsheet');
    localStorage.setItem('canvas-spreadsheet', JSON.stringify({
      state: { name: 'Legacy browser workbook' },
      version: 0,
    }));
  });
  await page.goto(`${designerUrl}/spreadsheet/create`);
  await expect.poll(() => page.evaluate(() => localStorage.getItem('pxa-spreadsheet'))).not.toBeNull();
  await expect.poll(() => page.evaluate(() => localStorage.getItem('canvas-spreadsheet'))).toBeNull();

  await page.evaluate(() => sessionStorage.setItem('pxa_migration_designer_handoff', JSON.stringify({
    template: {
      id: 'migration-handoff',
      name: 'Migration Handoff Report',
      category: 'migration',
      description: '',
      pages: [{ id: 'handoff-page', elements: [] }],
    },
    pageSettings: { width: 595, height: 842 },
  })));
  await page.goto(`${designerUrl}/create`);
  await expect(page).toHaveURL(`${designerUrl}/pdf/create`);
  await expect(page.getByRole('button', { name: 'Rename Migration Handoff Report' })).toBeVisible();
  await expect.poll(() => page.evaluate(() => sessionStorage.getItem('pxa_migration_designer_handoff'))).toBeNull();

  const languageSettings = page.locator('.editor-language-multiselect');
  await languageSettings.locator('summary').click();
  await languageSettings.locator('label', { hasText: 'English' }).locator('input').check();
  await languageSettings.locator('label', { hasText: 'العربية' }).locator('input').check();
  await expect(page.locator('button[title="en"]')).toBeVisible();
  await expect(page.locator('button[title="ar (RTL)"]')).toBeVisible();
  await languageSettings.locator('label', { hasText: 'العربية' }).locator('input').uncheck();
  await expect(page.locator('button[title="ar (RTL)"]')).toHaveCount(0);

  for (const path of ['/', '/pdf/template', '/docs', '/pdf/migrations/code', '/pdf/import', '/pdf/viewer']) {
    await page.goto(`${designerUrl}${path}`);
    await expect(page.locator('.pdf-nav')).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
      .toBe(true);
  }

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

test('Home mobile drawer stays inside tablet and phone viewports', async ({ page }) => {
  await page.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'responsive-user',
      email: 'responsive@example.test',
      displayName: 'Responsive User',
      roles: ['Editor'],
      organizations: [{ id: 'responsive-org', name: 'Responsive workspace', slug: 'responsive' }],
      activeOrganizationId: 'responsive-org',
      lastLoginAt: null,
    }),
  }));

  for (const viewport of [{ width: 720, height: 1024 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(viewport);
    await page.goto(designerUrl);
    await page.getByRole('button', { name: 'Open menu' }).click();
    await expect(page.getByRole('dialog', { name: 'Mobile menu' })).toBeVisible();
    const overflow = await page.evaluate(() => {
      window.scrollTo(0, 0);
      return {
      viewport: window.innerWidth,
      documentWidth: document.documentElement.scrollWidth,
      offenders: [...document.querySelectorAll<HTMLElement>('body *')]
        .map(element => ({
          selector: `${element.tagName.toLowerCase()}.${element.className}`,
          left: element.getBoundingClientRect().left,
          right: element.getBoundingClientRect().right,
          width: element.getBoundingClientRect().width,
        }))
        .filter(item => item.left < -1 || item.right > window.innerWidth + 1)
        .slice(0, 12),
      };
    });
    expect(overflow.documentWidth, JSON.stringify(overflow, null, 2))
      .toBeLessThanOrEqual(overflow.viewport + 1);
    await page.getByRole('button', { name: 'Close menu' }).click();
  }
});
