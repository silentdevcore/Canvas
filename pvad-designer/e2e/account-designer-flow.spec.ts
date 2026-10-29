import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { checkRenderingAssets } from './support/rename-rendering-checks';

const accountUrl = process.env.PVAD_SMOKE_ACCOUNT_URL ?? 'http://localhost:5178';
const designerUrl = process.env.PVAD_SMOKE_DESIGNER_URL ?? 'http://localhost:5176';
const mailpitUrl = process.env.PVAD_SMOKE_MAILPIT_URL ?? 'http://localhost:8025';

test.beforeEach(async ({ page }) => {
  if (process.env.PVAD_SMOKE_ACCOUNT_URL)
    await page.addInitScript(accountUrl => {
      (window as any).__PVAD_CONFIG__ = { accountUrl: `${accountUrl}/` };
    }, accountUrl);
});

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
    verificationUrl = body.match(/http:\/\/localhost:\d+\/verify-email\?[^\s"'<>]+/)?.[0] ?? '';
    if (verificationUrl) expect(new URL(verificationUrl).origin).toBe(accountUrl);
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
  const panel = page.locator('.pvad-product-overlay');
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
}, testInfo) => {
  test.setTimeout(300_000);
  page.setDefaultTimeout(30_000);
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const email = `designer-e2e-${unique}@example.test`;
  const password = 'Pvad-E2E-Password-2026!';
  const returnUrl = `${designerUrl}/pdf/create`;

  await page.goto(`${accountUrl}/register?returnUrl=${encodeURIComponent(returnUrl)}`);
  const storageNotice = page.locator('[data-pvad-storage-notice]');
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
  const productBootstrap = Promise.all(['/features', '/releases', '/notifications', '/notifications/unread-count']
    .map(suffix => page.waitForResponse(response => new URL(response.url()).pathname === `/api/pvad/v1/designer${suffix}`)
      .then(async response => {
        expect(response.ok()).toBe(true);
        await response.finished();
      })));
  await page.locator('#login-form').getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(new RegExp(`^${designerUrl.replaceAll('.', '\\.')}/pdf/create`));
  await productBootstrap;
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
  const exportedPdf = await pdfDownload;
  expect(exportedPdf.suggestedFilename()).toBe('Designer-E2E-Report.pdf');
  expect((await readFile((await exportedPdf.path())!)).subarray(0, 5).toString()).toBe('%PDF-');

  await expect(page.getByRole('button', { name: 'Export' })).toBeVisible({ timeout: 5_000 });
  await page.getByRole('button', { name: 'Export' }).click();
  const jsonDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: /Export JSON/ }).click();
  await expect((await jsonDownload).suggestedFilename()).toBe('Designer-E2E-Report.json');

  await expect(page.getByRole('button', { name: 'Export' })).toBeVisible({ timeout: 5_000 });
  await page.getByRole('button', { name: 'Export' }).click();
  // Verify the real PDF target and print request without invoking an OS printer.
  // Native print-dialog completion is a separate manual acceptance check.
  let printRequests = 0;
  await page.exposeFunction('__pvadPrintRequested', () => { printRequests++; });
  await page.evaluate(() => {
    const nativeOpen = window.open;
    (window as any).__pvadOriginalOpen = nativeOpen;
    window.open = function (...args) {
      const opened = nativeOpen.apply(window, args);
      return opened && new Proxy(opened, {
        get(target, property) {
          if (property === 'print') return () => { void (window as any).__pvadPrintRequested(); };
          const value = Reflect.get(target, property, target);
          return typeof value === 'function' ? value.bind(target) : value;
        },
        set(target, property, value) { return Reflect.set(target, property, value, target); },
      });
    };
  });
  const printTarget = page.waitForEvent('popup');
  const printResponse = page.waitForResponse(response => response.request().method() === 'POST'
    && new URL(response.url()).pathname.endsWith('/templates/render-design'));
  await page.getByRole('button', { name: /^Print/ }).click();
  const pdfPrintPage = await printTarget;
  const printPdf = await printResponse;
  expect(printPdf.ok()).toBe(true);
  expect(printPdf.headers()['content-type']).toContain('application/pdf');
  await expect.poll(() => pdfPrintPage.url()).toMatch(/^blob:/);
  const printSignature = await page.evaluate(async url => {
    const bytes = new Uint8Array(await (await fetch(url)).arrayBuffer());
    return new TextDecoder().decode(bytes.subarray(0, 5));
  }, pdfPrintPage.url());
  expect(printSignature).toBe('%PDF-');
  await expect.poll(() => printRequests).toBe(1);
  await pdfPrintPage.screenshot({ path: test.info().outputPath('pdf-print-preview.png') });
  await pdfPrintPage.close();
  await page.evaluate(() => { window.open = (window as any).__pvadOriginalOpen; });

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

  const projectDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Save PVADP project' }).click();
  const savedProject = await projectDownload;
  expect(savedProject.suggestedFilename()).toBe('Designer-E2E-Report.pvadp');
  const savedProjectPath = await savedProject.path();
  expect(savedProjectPath).not.toBeNull();
  const projectOpenResponse = page.waitForResponse(response => response.request().method() === 'POST'
    && new URL(response.url()).pathname.endsWith('/designer/projects/pdf/open'));
  await page.locator('input[accept*=".pvadp"]').setInputFiles({
    name: savedProject.suggestedFilename(),
    mimeType: 'application/vnd.pvad.pvadp+zip',
    buffer: await readFile(savedProjectPath!),
  });
  const projectResult = await projectOpenResponse;
  expect(projectResult.ok()).toBe(true);
  const projectCompatibility = (await projectResult.json()).compatibility;
  const reviewNotice = page.locator('.project-import-notice.is-review');
  if (projectCompatibility?.requiresReview) {
    await expect(reviewNotice).toBeVisible();
    await reviewNotice.getByRole('button', { name: 'Open project' }).click();
  }
  await expect(page.getByText('PVADP project opened.')).toBeVisible();
  await expect(page.locator('.project-import-notice')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Rename Designer E2E Report' })).toBeVisible();

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
  const applyReview = page.getByRole('dialog', { name: 'Compare with Designer' });
  await expect(applyReview).toBeVisible();
  await expect(applyReview).toContainText('Current Designer document');
  await page.screenshot({ path: testInfo.outputPath('code-apply-review-desktop.png') });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(applyReview).toBeVisible();
  const comparisonBounds = await applyReview.boundingBox();
  expect(comparisonBounds!.width).toBeLessThanOrEqual(390);
  await page.screenshot({ path: testInfo.outputPath('code-apply-review-mobile.png') });
  await page.setViewportSize({ width: 1440, height: 900 });
  await applyReview.getByRole('button', { name: 'Replace reviewed Designer draft' }).click();
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

  const legacyWorkbook = JSON.stringify({ state: {
    name: 'Legacy browser workbook', sheets: [{ id: 'legacy-sheet', name: 'Sheet1', rowCount: 20, colCount: 6,
      cells: {}, colWidths: {}, merges: [] }],
  }, version: 0 });
  await page.evaluate(raw => {
    localStorage.removeItem('pxa-spreadsheet');
    localStorage.setItem('canvas-spreadsheet', raw);
  }, legacyWorkbook);
  await page.goto(`${designerUrl}/spreadsheet/create`);
  await expect(page.getByRole('button', { name: 'Find old local drafts' })).toBeVisible();
  expect(await page.evaluate(() => localStorage.getItem('canvas-spreadsheet'))).toBe(legacyWorkbook);
  expect(await page.evaluate(() => localStorage.getItem('pxa-spreadsheet'))).toBeNull();
  await page.getByRole('button', { name: 'Find old local drafts' }).click();
  const recoveryDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download workbook 1' }).click();
  const recovered = await recoveryDownload;
  expect(recovered.suggestedFilename()).toBe('recovered-workbook.json');
  expect(JSON.parse(await readFile((await recovered.path())!, 'utf8')).name).toBe('Legacy browser workbook');
  expect(await page.evaluate(() => localStorage.getItem('canvas-spreadsheet'))).toBe(legacyWorkbook);

  const handoffPayload = {
    template: {
      id: 'migration-handoff',
      name: 'Migration Handoff Report',
      category: 'migration',
      description: '',
      pages: [{ id: 'handoff-page', elements: [] }],
    },
    pageSettings: { width: 595, height: 842 },
  };
  await page.evaluate(payload => sessionStorage.setItem('pxa_migration_designer_handoff', JSON.stringify(payload)), handoffPayload);
  await page.goto(`${designerUrl}/create`);
  await expect(page).toHaveURL(`${designerUrl}/pdf/create`);
  await expect(page.getByRole('button', { name: 'Rename Untitled document' })).toBeVisible();
  expect(await page.evaluate(() => sessionStorage.getItem('pxa_migration_designer_handoff'))).toBe(JSON.stringify(handoffPayload));

  // Current handoffs must belong to the actual authenticated user and active tenant.
  await page.evaluate(async payload => {
    const response = await fetch('/api/pvad/v1/auth/me', { headers: { 'X-PVAD-Application': 'designer' } });
    if (!response.ok) throw new Error('Cannot resolve the authenticated handoff owner.');
    const identity = await response.json();
    if (!identity.id || !identity.activeOrganizationId) throw new Error('Missing handoff owner.');
    sessionStorage.setItem('pvad_migration_designer_handoff', JSON.stringify({
      schemaVersion: 1, owner: { userId: identity.id, organizationId: identity.activeOrganizationId },
      expiresAt: Date.now() + 4 * 60_000, payload,
    }));
  }, handoffPayload);
  await page.goto(`${designerUrl}/create`);
  await expect(page.getByRole('button', { name: 'Rename Migration Handoff Report' })).toBeVisible();
  await expect.poll(() => page.evaluate(() => sessionStorage.getItem('pvad_migration_designer_handoff'))).toBeNull();

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

  await checkRenderingAssets(page, designerUrl, testInfo);

  for (const migration of [
    { route: '/pdf/migrations/code', provider: 'syncfusion-pdf', endpoint: '/pdf/code/syncfusion-pdf', download: 'Download .cs' },
    { route: '/pdf/migrations/designer', provider: 'devexpress-report', endpoint: '/report/designer/devexpress-report', download: 'Download JSON' },
  ]) {
    console.log(`Checking real ${migration.provider} conversion and Viewer handoff.`);
    await page.goto(`${designerUrl}${migration.route}`);
    await page.locator(`[data-provider-id="${migration.provider}"]`).click();
    await page.getByRole('button', { name: 'Load example', exact: true }).click();
    const [convertedResponse] = await Promise.all([
      page.waitForResponse(response => response.url().endsWith(`/migrations${migration.endpoint}/convert`), { timeout: 30_000 }),
      page.getByRole('button', { name: /^Convert/ }).click(),
    ]);
    expect(convertedResponse.ok()).toBe(true);
    const conversion = await convertedResponse.json();
    expect(conversion.artifacts.length).toBeGreaterThan(0);
    const artifactDownload = page.waitForEvent('download');
    await page.getByRole('button', { name: migration.download, exact: true }).click();
    const artifact = await artifactDownload;
    expect(await artifact.failure()).toBeNull();
    const artifactText = await readFile((await artifact.path())!, 'utf8');
    if (migration.provider === 'devexpress-report') {
      expect(JSON.parse(artifactText)).toEqual(conversion.artifacts.find((value: any) => value.id === 'design').content);
    } else {
      expect(artifactText).toContain('PVAD');
    }

    const [renderedResponse] = await Promise.all([
      page.waitForResponse(response => migration.provider === 'devexpress-report'
        ? response.url().endsWith('/api/templates/render-design')
        : response.url().endsWith(`/migrations${migration.endpoint}/preview`), { timeout: 30_000 }),
      page.getByRole('button', { name: 'Generate Preview', exact: true }).click(),
    ]);
    expect(renderedResponse.ok()).toBe(true);
    expect(renderedResponse.headers()['content-type']).toContain('application/pdf');
    await page.getByRole('tab', { name: 'Preview', exact: true }).click();
    await expect(page.locator('.mgr-pdf-frame')).toBeVisible();
    const previewBytes = await page.locator('.mgr-pdf-frame').evaluate(async (frame: HTMLIFrameElement) =>
      Array.from(new Uint8Array(await (await fetch(frame.src)).arrayBuffer())));
    expect(Buffer.from(previewBytes).subarray(0, 5).toString()).toBe('%PDF-');
    await page.getByRole('button', { name: 'Open in PDF Viewer', exact: true }).click();
    await expect(page).toHaveURL(/\/pdf\/viewer\?handoff=session/);
    await expect(page.locator('.pdfv-page-stack .react-pdf__Page__canvas')).toBeVisible();
    await expect(page.locator('.pdfv-page-stack .react-pdf__Page__textContent')).toContainText(
      migration.provider === 'devexpress-report' ? 'Invoice #2024-117' : 'Hello from Syncfusion');
    const viewerDownload = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Download', exact: true }).click();
    const pdfDownload = await viewerDownload;
    expect(pdfDownload.suggestedFilename()).toBe(`${migration.provider}-migration-preview.pdf`);
    const downloadedBytes = await readFile((await pdfDownload.path())!);
    expect(createHash('sha256').update(downloadedBytes).digest('hex'))
      .toBe(createHash('sha256').update(Buffer.from(previewBytes)).digest('hex'));
    await page.screenshot({ path: testInfo.outputPath(`${migration.provider}-viewer-handoff.png`) });

    if (migration.provider === 'devexpress-report') {
      await page.goto(`${designerUrl}${migration.route}`);
      await page.locator(`[data-provider-id="${migration.provider}"]`).click();
      await page.getByRole('button', { name: 'Load example', exact: true }).click();
      await page.getByRole('button', { name: /^Convert/ }).click();
      await expect(page.getByRole('button', { name: 'Open in Designer', exact: true })).toBeEnabled();
      await page.getByRole('button', { name: 'Open in Designer', exact: true }).click();
      await expect(page).toHaveURL(/\/pdf\/create/);
      await expect(page.locator('.editor-page')).toContainText('Invoice #2024-117');
      await waitUntilSaved(page);
    }
  }

  await page.goto(`${designerUrl}/spreadsheet/migrations/code`);
  await page.locator('[data-provider-id="closedxml"]').click();
  await page.getByRole('button', { name: 'Load example', exact: true }).click();
  const [spreadsheetConversion] = await Promise.all([
    page.waitForResponse(response => response.url().endsWith('/migrations/spreadsheet/code/closedxml/convert'), { timeout: 30_000 }),
    page.getByRole('button', { name: /^Convert/ }).click(),
  ]);
  expect(spreadsheetConversion.ok()).toBe(true);
  const spreadsheetDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download .cs', exact: true }).click();
  const spreadsheetCode = await spreadsheetDownload;
  expect(await readFile((await spreadsheetCode.path())!, 'utf8')).toContain('PVAD');
  const [spreadsheetPreview] = await Promise.all([
    page.waitForResponse(response => response.url().endsWith('/migrations/spreadsheet/code/closedxml/preview'), { timeout: 30_000 }),
    page.getByRole('button', { name: 'Generate Preview', exact: true }).click(),
  ]);
  expect(spreadsheetPreview.ok()).toBe(true);
  expect(spreadsheetPreview.headers()['content-type']).toContain('text/html');
  await expect(page.frameLocator('.mgr-pdf-frame').getByText('Coffee', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Open in PDF Viewer', exact: true })).toHaveCount(0);
  await page.screenshot({ path: testInfo.outputPath('closedxml-grid-preview.png') });

});

test('mobile Designer shows an explicit entitlement denial state', async ({ browser }) => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();

  await page.route('**/api/pvad/v1/auth/me', (route) => route.fulfill({
    status: 403,
    contentType: 'application/problem+json',
    body: JSON.stringify({
      type: 'https://docs.pvade.com/problems/pvad_trial_expired',
      canonicalType: 'https://docs.pvade.com/problems/pvad_trial_expired',
      legacyType: 'https://docs.powerdoxautomation.com/problems/pxa_trial_expired',
      title: 'Designer access denied',
      status: 403,
      code: 'PXA_TRIAL_EXPIRED',
    }),
  }));

  await page.goto(`${designerUrl}/pdf/create`);
  await expect(page.getByRole('heading', { name: 'Designer subscription expired' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Open PVADE Account' })).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    .toBe(true);

  await context.close();
});

test('Home mobile drawer stays inside tablet and phone viewports', async ({ page }) => {
  await page.route('**/api/pvad/v1/auth/me', (route) => route.fulfill({
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
