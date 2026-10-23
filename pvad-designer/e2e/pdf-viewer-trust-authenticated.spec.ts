import {
  expect,
  test,
  type APIRequestContext,
  type Page,
  type TestInfo,
} from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { readPvadEnvironment } from '../../tools/configuration/environment.mjs';

const designerUrl = readPvadEnvironment('PVAD_TEST_DESIGNER_URL') ?? 'http://localhost:5176';
const accountUrl = 'http://localhost:5178';
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

async function registerAndEnterDesigner(
  page: Page,
  request: APIRequestContext,
  returnUrl: string,
): Promise<void> {
  const unique = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const email = `pdf-trust-live-${unique}@example.test`;
  const password = 'Pvad-Trust-E2E-2026!';

  await page.goto(`${accountUrl}/register?returnUrl=${encodeURIComponent(returnUrl)}`);
  const storageNotice = page.locator('[data-pxa-storage-notice]');
  if (await storageNotice.isVisible()) {
    await storageNotice.getByRole('button', { name: 'Understood' }).click();
  }
  await page.locator('[name="displayName"]').fill('PDF Trust E2E User');
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
  expect(new URL(page.url()).searchParams.has('code')).toBe(false);
  expect(new URL(page.url()).searchParams.has('state')).toBe(false);
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

async function captureDocumentationScreenshot(
  page: Page,
  testInfo: TestInfo,
  name: string,
): Promise<void> {
  if (readPvadEnvironment('PVAD_CAPTURE_PDF_TRUST_DOCS') !== '1') return;

  const viewport = testInfo.project.name.includes('mobile') ? 'mobile' : 'desktop';
  const directory = path.resolve(
    process.cwd(),
    readPvadEnvironment('PVAD_VIEWER_CAPTURE_DIRECTORY') ?? '../websites/PVAD.Documentation/public/images/pdf-trust',
  );
  await fs.mkdir(directory, { recursive: true });
  await page.screenshot({
    path: path.join(directory, `${name}-${viewport}.png`),
    fullPage: false,
  });
}

function syntheticPdf(): string {
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << >> >>',
    '<< /Length 0 >>\nstream\n\nendstream',
  ];
  let body = '%PDF-1.4\n';
  const offsets = [0];
  objects.forEach((object, index) => {
    offsets.push(body.length);
    body += `${index + 1} 0 obj\n${object}\nendobj\n`;
  });
  const xrefOffset = body.length;
  body += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  offsets.slice(1).forEach(offset => { body += `${String(offset).padStart(10, '0')} 00000 n \n`; });
  body += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xrefOffset}\n%%EOF\n`;
  return body;
}

const trustReport = {
  status: 0,
  structureStatus: 0,
  sha256: 'a'.repeat(64),
  fileSizeBytes: 256,
  pdfVersion: '1.4',
  revisionCount: 1,
  objectCount: 4,
  encryption: { isEncrypted: false, status: 4 },
  signatures: [],
  pdfA: { standard: 'PDF/A', isClaimed: false, status: 4, evidence: 'none' },
  pdfUa: { standard: 'PDF/UA', isClaimed: false, status: 4, evidence: 'none' },
  revisions: [],
  diagnostics: [],
};

const permissions = [
  'designer.access',
  'designer.pdf.access',
  'designer.pdf-viewer.read',
  'designer.pdf-viewer.annotate',
  'designer.pdf-viewer.redact',
  'designer.pdf.trust.inspect',
  'designer.pdf.signatures.sign',
  'designer.pdf.compliance.convert',
  'designer.pdf.export',
  'designer.pdf.print',
];

test('Viewer UI completes PDF Trust workflow with synthetic API fixtures', async ({ page }, testInfo) => {
  const pdf = syntheticPdf();
  const pdfBuffer = Buffer.from(pdf, 'binary');
  const catalog = JSON.parse(await fs.readFile(path.resolve(process.cwd(), '../product-metadata/designer-features.json'), 'utf8'));
  await page.route('**/api/pvad/v1/designer/features', route => route.fulfill({ status: 200,
    contentType: 'application/json', body: JSON.stringify(catalog.features.map((feature: Record<string, unknown>) => ({
      ...feature, enabled: feature.maturity !== 'alpha' && feature.defaultEnabled,
      decisionCode: 'PXA_DESIGNER_FEATURE_ENABLED', decisionReason: 'Synthetic UI fixture.',
    }))) }));
  await page.route('**/api/pvad/v1/designer/releases', route => route.fulfill({ status: 200,
    contentType: 'application/json', body: JSON.stringify({ releases: [], readVersions: [] }) }));
  await page.route('**/api/pvad/v1/designer/notifications?*', route => route.fulfill({ status: 200,
    contentType: 'application/json', body: JSON.stringify({ items: [], page: 1, pageSize: 50, total: 0 }) }));
  await page.route('**/api/pvad/v1/designer/notifications/unread-count', route => route.fulfill({ status: 200,
    contentType: 'application/json', body: JSON.stringify({ unread: 0, persistent: 0, releases: 0 }) }));
  await page.route('**/api/pvad/v1/telemetry/browser', route => route.fulfill({ status: 202, body: '' }));

  await page.route('**/api/pvad/v1/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'synthetic-user',
      username: 'synthetic-user',
      email: 'synthetic@example.test',
      displayName: 'Synthetic Viewer User',
      roles: ['Viewer'],
      permissions,
      organizations: [{ id: 'synthetic-org', name: 'Synthetic Organization', slug: 'synthetic' }],
      activeOrganizationId: 'synthetic-org',
      lastLoginAt: null,
      apiVersion: '1',
    }),
  }));
  await page.route('**/api/pvad/v1/auth/csrf', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'synthetic-csrf' }),
  }));
  await page.route('**/synthetic.pdf', route => route.fulfill({
    status: 200,
    contentType: 'application/pdf',
    body: pdfBuffer,
  }));

  await page.route('**/api/pvad/v1/pdf/trust/inspect/jobs', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ jobId: 'synthetic-inspect', status: 'accepted', statusUrl: '/api/synthetic-inspect' }),
  }));
  await page.route('**/api/synthetic-inspect', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ id: 'synthetic-inspect', status: 'completed', downloadUrl: '/api/synthetic-inspect-result' }),
  }));
  await page.route('**/api/synthetic-inspect-result', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(trustReport),
  }));
  await page.route('**/api/pvad/v1/pdf/compliance/preflight', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      targetProfile: 'PDF/A-2b',
      capability: 0,
      sourceSha256: 'b'.repeat(64),
      fileSizeBytes: 256,
      objectCount: 4,
      signatureCount: 0,
      invalidatesSignatures: false,
      originalPreserved: true,
      findings: [],
    }),
  }));
  await page.route('**/api/pvad/v1/pdf/compliance/convert', route => route.fulfill({
    status: 200,
    contentType: 'application/pdf',
    body: pdfBuffer,
  }));
  await page.route('**/api/pdf-viewer/annotations/redact/preflight', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      capability: 'Removable',
      sha256: 'c'.repeat(64),
      fileSizeBytes: 256,
      pageCount: 1,
      revisionCount: 1,
      regions: [{ regionIndex: 0, pageNumber: 1, capability: 'Removable', covered: [], findings: [] }],
      documentFindings: [],
    }),
  }));
  await page.route('**/api/pdf-viewer/annotations/redact', route => route.fulfill({
    status: 200,
    contentType: 'application/pdf',
    headers: {
      'PXA-Redaction-Result-Sha256': 'd'.repeat(64),
      'PXA-Redaction-Audit': 'e'.repeat(64),
      'PXA-Redaction-Audit-Signed': 'false',
    },
    body: pdfBuffer,
  }));
  await page.route('**/api/pvad/v1/pdf/signatures/sign', route => route.fulfill({
    status: 200,
    contentType: 'application/pdf',
    body: pdfBuffer,
  }));

  await page.goto(`${designerUrl}/pdf/viewer?src=/synthetic.pdf&name=synthetic.pdf`);
  await expect(page.getByRole('heading', { name: 'PDF Trust' })).toHaveCount(0);
  await expect(page.getByTitle('Download')).toBeVisible();
  await expect(page.getByTitle('Print')).toBeVisible();

  if ((page.viewportSize()?.width ?? 0) <= 680) {
    await page.getByRole('button', { name: 'Collapse sidebar' }).click();
    const viewport = page.viewportSize()!;
    for (const width of [320, viewport.width]) {
      await page.setViewportSize({ ...viewport, width });
      await expect.poll(async () => {
        const toggle = await page.locator('.hub-sidebar-toggle').boundingBox();
        const brand = await page.locator('.pdf-nav .pdf-logo').boundingBox();
        const actions = await page.locator('.pdf-nav-actions').boundingBox();
        return !!toggle && !!brand && !!actions
          && toggle.x + toggle.width <= brand.x
          && brand.x + brand.width <= actions.x;
      }).toBe(true);
    }
  }

  await page.getByRole('button', { name: 'PDF Trust' }).click();
  await page.getByRole('button', { name: 'Inspect trust' }).click();
  await expect(page.getByText('SHA-256 aaaaaaaaaaaaaaaa...')).toBeVisible();
  await captureDocumentationScreenshot(page, testInfo, 'viewer-trust');

  await page.getByRole('button', { name: 'Preflight PDF/A' }).click();
  await expect(page.getByText(/Preflight passed|Ready/i)).toBeVisible();
  await page.getByRole('button', { name: /Convert to selected PDF\/A profile/i }).click();
  await expect(page.getByText(/selected PDF\/A file was created/i)).toBeVisible();
  await captureDocumentationScreenshot(page, testInfo, 'viewer-compliance');

  await expect(page.getByRole('button', { name: 'Sign PDF' })).toBeVisible();
  await page.locator('input[type="file"][accept*=".p12"]').setInputFiles({
    name: 'synthetic.p12',
    mimeType: 'application/x-pkcs12',
    buffer: Buffer.from('synthetic-certificate'),
  });
  await page.locator('input[type="password"]').fill('synthetic-password');
  await page.getByRole('button', { name: 'Sign PDF' }).click();
  await expect(page.getByText('The signed PDF was created.')).toBeVisible();
  await captureDocumentationScreenshot(page, testInfo, 'viewer-signature');

  await page.getByRole('button', { name: 'Review' }).click();
  await page.getByRole('button', { name: 'Redact', exact: true }).click();
  await page.locator('.pdfv-page-stack').click({ position: { x: 120, y: 120 } });
  await page.getByRole('button', { name: 'Preflight redactions' }).click();
  await expect(page.getByTestId('redaction-report')).toContainText('Preflight');
  await page.getByRole('button', { name: 'Apply redactions' }).click();
  await expect(page.getByTestId('redaction-report')).toContainText('Verify: ✓');
  await page.getByRole('button', { name: 'Compare with original' }).click();
  await expect(page.getByRole('dialog', { name: 'Redaction result comparison' })).toBeVisible();
  await captureDocumentationScreenshot(page, testInfo, 'viewer-redaction-review');

  const download = page.waitForEvent('download');
  await page.getByTitle('Download').click();
  await expect((await download).suggestedFilename()).toBe('synthetic.pdf');
});

test.describe('live authenticated PDF Trust workflow', () => {
  test('registered customer inspects and reviews a PDF through the running application', async ({
    page,
    request,
  }, testInfo) => {
    test.skip(
      process.env.PXA_RUN_LIVE_AUTH_TRUST !== '1',
      'Requires running Account, Designer, WebApi, PostgreSQL, and Mailpit services.',
    );
    test.setTimeout(180_000);

    const pdfBuffer = Buffer.from(syntheticPdf(), 'binary');
    await page.route('**/pdf-trust-live.pdf', route => route.fulfill({
      status: 200,
      contentType: 'application/pdf',
      body: pdfBuffer,
    }));

    const viewerUrl = `${designerUrl}/pdf/viewer?src=/pdf-trust-live.pdf&name=pdf-trust-live.pdf`;
    await registerAndEnterDesigner(page, request, viewerUrl);
    await dismissProductPanel(page);
    await expect(page.getByTitle('Download')).toBeVisible();

    if ((page.viewportSize()?.width ?? 0) <= 680) {
      await page.getByRole('button', { name: 'Collapse sidebar' }).click();
    }

    await page.getByTitle('PDF Trust').click();
    await page.getByRole('button', { name: 'Inspect trust' }).click();
    await expect(page.getByText(/SHA-256 [a-f0-9]{16}\.\.\./i)).toBeVisible({ timeout: 30_000 });
    await testInfo.attach('live-trust-inspection', {
      body: await page.screenshot({ fullPage: false }),
      contentType: 'image/png',
    });

    await page.getByRole('button', { name: 'Preflight PDF/A' }).click();
    await expect(page.getByText(/Preflight passed|Ready/i)).toBeVisible({ timeout: 30_000 });
    await testInfo.attach('live-compliance-preflight', {
      body: await page.screenshot({ fullPage: false }),
      contentType: 'image/png',
    });

    const download = page.waitForEvent('download');
    await page.getByTitle('Download').click();
    await expect((await download).suggestedFilename()).toBe('pdf-trust-live.pdf');
  });
});
