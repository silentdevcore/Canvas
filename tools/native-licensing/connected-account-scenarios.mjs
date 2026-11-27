import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';

const { expect: baseExpect } = createRequire(new URL('../../pvad-designer/package.json', import.meta.url))('@playwright/test');
const expect = baseExpect.configure({ timeout: 30_000 });
const templatePath = '/api/pvad/v1/designer/templates';

async function dismissPanels(page) {
  const notice = page.locator('[data-pvad-storage-notice]');
  if (await notice.isVisible()) await notice.getByRole('button', { name: 'Understood' }).click();
  const panel = page.locator('.pvad-product-overlay');
  if (await panel.isVisible()) await panel.getByRole('button', { name: 'Close panel' }).click();
}
async function saved(page) { await expect(page.getByRole('status').filter({ hasText: /^Saved$/ })).toBeVisible({ timeout: 30_000 }); }
async function mutation(context, origin, path, data) {
  const csrf = await context.request.get(origin + '/api/pvad/v1/auth/csrf');
  assert.equal(csrf.status(), 200);
  return context.request.post(origin + path, { data, headers: { 'X-PVAD-CSRF': (await csrf.json()).token } });
}
async function upload(context, origin, path, file) {
  const csrf = await context.request.get(origin + '/api/pvad/v1/auth/csrf');
  assert.equal(csrf.status(), 200);
  return context.request.post(origin + path, { multipart: { file }, headers: { 'X-PVAD-CSRF': (await csrf.json()).token } });
}

export async function exerciseConnectedAccounts({ browser, urls, control, work, output, step, password }) {
  const cases = [];
  for (const kind of ['IndividualDeveloper', 'Company']) {
    const company = kind === 'Company', label = company ? 'company' : 'private';
    const email = `${label}@account-live.test`;
    const context = await browser.newContext({ viewport: { width: 1440, height: 1100 }, acceptDownloads: true });
    const page = await context.newPage(); page.setDefaultTimeout(30_000);
    const checks = [];
    const mark = name => { checks.push(name); step(label + '/' + name); };
    const apiResponses = [];
    const browserErrors = [];
    page.on('pageerror', error => browserErrors.push(error.message.replace(/https?:\/\/\S+/g, '[url]').slice(0, 1000)));
    page.on('response', response => {
      const url = new URL(response.url());
      if (url.pathname.startsWith('/api/')) apiResponses.push({ path: url.pathname, method: response.request().method(), status: response.status() });
    });
    await page.addInitScript(accountUrl => {
      localStorage.setItem('pvad_locale', 'en'); window.__PVAD_CONFIG__ = { accountUrl: accountUrl + '/' };
    }, urls.account);
    try {
      await control('reset-clock');
      mark('register');
      const returnUrl = urls.designer + '/pdf/create';
      await page.goto(`${urls.account}/register?returnUrl=${encodeURIComponent(returnUrl)}`);
      await dismissPanels(page);
      // Loading the legal policy replaces the form. Fill only its final, enabled instance.
      await expect(page.locator('[name=acceptTerms]')).toBeEnabled();
      await page.locator(`[name=accountType][value="${kind}"]`).check();
      await page.locator('[name=displayName]').fill('Connected ' + label);
      await page.locator('[name=email]').fill(email);
      await page.locator('[name=password]').fill(password);
      await page.locator('[name=country]').fill('DE');
      if (company) await page.locator('[name=companyName]').fill('Synthetic Connected Company');
      await page.locator('[name=acceptTerms]').check(); await page.locator('[name=acceptPrivacy]').check();
      await page.locator('#register-form button[type=submit]').click();
      await expect(page).toHaveURL(/\/registration-pending/);
      mark('verify-real-outbox-mail');
      const mail = await control('mail', { email }); await page.goto(mail.url);
      await expect(page.getByRole('heading', { name: 'Your Trial is ready' })).toBeVisible();
      await control('process');
      await page.getByRole('link', { name: 'Sign in', exact: true }).click();
      await page.locator('[name=identifier]').fill(email); await page.locator('[name=password]').fill(password);
      const created = page.waitForResponse(response => new URL(response.url()).pathname === templatePath && response.request().method() === 'POST');
      await page.locator('#login-form button[type=submit]').click();
      await expect(page).toHaveURL(new RegExp('^' + urls.designer.replaceAll('.', '\\.') + '/pdf/create'));
      const createdResponse = await created;
      assert.equal(createdResponse.status(), 201);
      const template = await createdResponse.json();
      const templateUrl = `${urls.designer}/pdf/create?templateId=${template.id}`;
      await saved(page); await dismissPanels(page);
      mark('real-login-designer-and-persistence');
      const name = 'Connected ' + label + ' document';
      await page.getByRole('button', { name: 'Rename Untitled document' }).click();
      const title = page.getByRole('textbox', { name: 'Document name' }); await title.fill(name); await title.press('Enter');
      await expect.poll(async () => {
        const retained = await context.request.get(urls.designer + templatePath + '/' + template.id);
        return retained.ok() ? (await retained.json()).name : '';
      }, { timeout: 30_000 }).toBe(name);
      await saved(page);
      await page.getByRole('button', { name: /single line text block/i }).click();
      await expect.poll(async () => {
        const response = await context.request.get(urls.designer + templatePath + '/' + template.id);
        if (!response.ok()) return 0;
        return (await response.json()).designDocument.template.pages[0].elements.length;
      }, { timeout: 30_000 }).toBe(1);
      await saved(page);
      await page.getByRole('button', { name: 'Design actions' }).click();
      await page.getByRole('button', { name: 'Create version', exact: true }).click();
      await expect(page.getByText(/Version 1 created/)).toBeVisible();
      await page.getByRole('button', { name: 'Design actions' }).click();
      await page.getByRole('button', { name: 'Publish', exact: true }).click();
      await expect(page.getByText(/Template published/)).toBeVisible();
      const published = await context.request.get(urls.designer + templatePath + '/' + template.id);
      assert.ok((await published.json()).publishedVersionId);
      mark('version-and-publish');
      // Hovering the top-right success toast pauses its dismissal timer.
      // Leave it naturally before using the preview controls beneath it.
      await page.mouse.move(350, 500);
      await expect(page.locator('.pvad-toast')).toHaveCount(0, { timeout: 15_000 });

      await page.getByRole('button', { name: 'Preview', exact: true }).click();
      await page.getByRole('button', { name: 'Export', exact: true }).click();
      if (!company) {
        await expect(page.getByRole('button', { name: /Export JSON/ })).toHaveCount(0);
        await expect(page.getByRole('button', { name: /Export Image/ })).toHaveCount(0);
      }
      const download = page.waitForEvent('download');
      await page.getByRole('button', { name: /Export PDF/ }).click();
      const pdfFile = join(work, label + '-document.pdf'); await (await download).saveAs(pdfFile);
      assert.equal(readFileSync(pdfFile).subarray(0, 5).toString(), '%PDF-');
      mark('browser-pdf-download-real-bytes');
      await page.getByRole('button', { name: /Back to Editor/ }).click();
      await page.getByRole('link', { name: 'PDF with Code', exact: true }).click();
      await expect(page.getByRole('tab', { name: /JSON/ })).toBeVisible();
      await expect(page.locator('.monaco-editor').first()).toBeVisible();
      await page.getByLabel('Conversion target').selectOption('csharpPdf');
      await page.getByRole('button', { name: 'Convert', exact: true }).click();
      const conversionReview = page.getByRole('dialog', { name: 'Conversion review' });
      await expect(conversionReview).toBeVisible();
      await conversionReview.getByRole('button', { name: 'Apply generated draft' }).click();
      await expect(page.getByRole('tab', { name: /^C# PDF/ })).toHaveAttribute('aria-selected', 'true');
      await expect(page.locator('.monaco-editor').first()).toContainText('PvadPdfCodeBuilder');
      await expect(page.getByRole('tab', { name: /C# PDF Saved/ })).toBeVisible();
      // Use the rendered editor rather than a version-specific input element.
      await page.locator('.monaco-editor').first().click({ position: { x: 150, y: 30 } });
      const endOfFile = await page.evaluate(() => /Mac/.test(navigator.platform) ? 'Meta+ArrowDown' : 'Control+End');
      await page.keyboard.press(endOfFile);
      await page.keyboard.press('Enter');
      await page.keyboard.type('// account-live-authored-edit');
      await page.keyboard.press('Enter');
      await expect.poll(async () => {
        const response = await context.request.get(urls.designer + templatePath + '/' + template.id + '/code-workspace');
        return response.ok() && (await response.json()).cSharpPdf.source.includes('account-live-authored-edit');
      }, { timeout: 20_000 }).toBe(true);
      mark('visible-code-edit-persisted');
      const execution = page.waitForResponse(response => new URL(response.url()).pathname.endsWith('/code-workspace/execute'));
      await page.locator('.live-code-topbar-right').getByRole('button', { name: 'Run', exact: true }).click();
      const executionResponse = await execution; assert.equal(executionResponse.status(), 200);
      const executed = await executionResponse.json();
      assert.equal(executed.success, true, 'Real CodeWorker execution: ' + JSON.stringify(executed.diagnostics));
      assert.ok(!executed.diagnostics.some(diagnostic => diagnostic.severity === 'error'));
      assert.ok(executed.pdfBytes && Buffer.from(executed.pdfBytes, 'base64').subarray(0, 5).toString() === '%PDF-');
      await expect(page.locator('.code-workspace-error')).toHaveCount(0);
      mark('edit-persist-and-execute-real-csharp-worker');
      await page.screenshot({ path: join(output, label + '-code-workspace.png'), fullPage: true });

      const markdown = await upload(context, urls.designer, '/api/document/import-markdown',
        { name: 'synthetic.md', mimeType: 'text/markdown', buffer: Buffer.from('# Connected workflow') });
      assert.equal(markdown.status(), 200); const design = await markdown.json();
      for (const format of ['pdf', 'word', 'odt']) {
        const response = await mutation(context, urls.designer, '/api/export?format=' + format, design);
        assert.equal(response.status(), 200); assert.ok((await response.body()).length > 0);
      }
      const png = await mutation(context, urls.designer, '/api/export?format=png', design);
      assert.equal(png.status(), company ? 200 : 403);
      if (!company) assert.equal((await png.json()).code, 'PVAD_ACCOUNT_CAPABILITY_DENIED');
      const pdfImport = await upload(context, urls.designer, '/api/document/import-pdf-engine',
        { name: 'synthetic.pdf', mimeType: 'application/pdf', buffer: readFileSync(pdfFile) });
      assert.equal(pdfImport.status(), company ? 200 : 403);
      if (!company) assert.equal((await pdfImport.json()).code, 'PVAD_ACCOUNT_CAPABILITY_DENIED');
      for (const extension of ['csv', 'tsv']) {
        const delimiter = extension === 'csv' ? ',' : '\t';
        const table = await upload(context, urls.designer, '/api/spreadsheet/import',
          { name: 'synthetic.' + extension, mimeType: 'text/plain', buffer: Buffer.from(`Name${delimiter}Value\nAlpha${delimiter}1`) });
        assert.equal(table.status(), 200);
        const xlsx = await mutation(context, urls.designer, '/api/spreadsheet/export?format=xlsx', await table.json());
        assert.equal(xlsx.status(), 200); assert.equal((await xlsx.body()).subarray(0, 2).toString(), 'PK');
      }
      const xls = await upload(context, urls.designer, '/api/spreadsheet/import',
        { name: 'synthetic.xls', mimeType: 'application/vnd.ms-excel', buffer: Buffer.from('synthetic') });
      assert.equal(xls.status(), 403);
      assert.equal((await xls.json()).code, 'PVAD_ACCOUNT_CAPABILITY_DENIED');
      const migration = await mutation(context, urls.designer, '/api/pvad/v1/migrations/pdf/code/aspose-pdf/convert', {});
      assert.equal(migration.status(), 403);
      assert.equal((await migration.json()).code, 'PVAD_ACCOUNT_CAPABILITY_DENIED');
      mark('real-format-requests-and-denials');
      await page.goto(urls.designer + '/pdf/import');
      if (company) await expect(page.getByRole('button', { name: 'Choose PDF file' })).toBeEnabled();
      else await expect(page.getByRole('heading', { name: 'Account restriction' })).toBeVisible();
      await page.goto(urls.designer + '/pdf/migrations/code');
      await expect(page.getByRole('heading', { name: 'Admin approval required' })).toBeVisible();

      mark('account-license-download');
      await page.goto(urls.account + '/licenses');
      await expect(page.getByRole('heading', { name: 'Native library licenses' })).toBeVisible();
      const list = await context.request.get(urls.account + '/api/pvad/v1/account/native-licenses');
      assert.equal(list.status(), 200); const original = (await list.json()).items[0]; assert.ok(original);
      assert.equal(company ? original.ownerUserId === null && !!original.organizationId : !!original.ownerUserId && original.organizationId === null, true);
      await page.locator(`.account-native-detail[data-license-id="${original.id}"]`).click();
      await expect(page.getByRole('button', { name: 'Renew free license' })).toBeDisabled();
      const nativeDownload = page.waitForEvent('download');
      await page.locator(`a[href$="/${original.id}/download"]`).first().click();
      const originalPath = join(work, label + '-native-original.json'); await (await nativeDownload).saveAs(originalPath);
      const originalProof = await control('offline', { path: originalPath });
      assert.equal(originalProof.accountType, company ? 'company' : 'individualDeveloper');
      mark('real-signature-offline-admission-and-expiry');
      await control('expire', { licenseId: original.id });
      await page.reload(); await page.locator(`.account-native-detail[data-license-id="${original.id}"]`).click();
      await expect(page.getByRole('button', { name: 'Renew free license' })).toBeEnabled();
      const renewal = page.waitForResponse(response => new URL(response.url()).pathname.endsWith('/renew-free') && response.request().method() === 'POST');
      await page.getByRole('button', { name: 'Renew free license' }).click();
      const renewedResponse = await renewal; assert.equal(renewedResponse.status(), 200); const renewed = await renewedResponse.json();
      await expect(page.getByRole('status').filter({ hasText: 'Your free license has been renewed' })).toBeVisible();
      const retry = await mutation(context, urls.account, `/api/pvad/v1/account/native-licenses/${original.id}/renew-free`, {});
      assert.equal(retry.status(), 200); assert.equal((await retry.json()).id, renewed.id);
      const replacementDownload = page.waitForEvent('download');
      await page.locator(`a[href$="/${renewed.id}/download"]`).first().click();
      const replacementPath = join(work, label + '-native-renewed.json'); await (await replacementDownload).saveAs(replacementPath);
      const renewedProof = await control('offline', { path: replacementPath });
      assert.equal(renewedProof.accountType, originalProof.accountType);
      const unchanged = await context.request.get(`${urls.account}/api/pvad/v1/account/native-licenses/${original.id}/download`);
      assert.deepEqual(await unchanged.body(), readFileSync(originalPath));
      await expect(page.getByRole('button', { name: 'Renew free license' })).toBeDisabled();
      mark('renew-retry-and-reload-real-signed-file');
      await page.screenshot({ path: join(output, label + '-renewed-account.png'), fullPage: true });
      cases.push({ accountType: kind, checks, original: originalProof, renewed: renewedProof,
        apiResponseCount: apiResponses.length, templateId: template.id, noApiResponseMocks: true });
    } catch (error) {
      writeFileSync(join(output, label + '-failure-diagnostics.json'), JSON.stringify({ browserErrors, apiResponses }, null, 2));
      await page.screenshot({ path: join(output, label + '-failure.png'), fullPage: true }).catch(() => {});
      throw new Error(label + '/' + checks.at(-1) + ': ' + error.message);
    } finally { await context.close(); }
  }
  return cases;
}
