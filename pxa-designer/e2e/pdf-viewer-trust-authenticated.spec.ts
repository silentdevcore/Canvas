import { expect, test } from '@playwright/test';

const designerUrl = 'http://localhost:5176';

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

test('authenticated viewer completes PDF Trust smoke workflow with synthetic data', async ({ page }) => {
  const pdf = syntheticPdf();
  const pdfBuffer = Buffer.from(pdf, 'binary');

  await page.route('**/api/pxa/v1/auth/me', route => route.fulfill({
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
  await page.route('**/api/pxa/v1/auth/csrf', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'synthetic-csrf' }),
  }));
  await page.route('**/synthetic.pdf', route => route.fulfill({
    status: 200,
    contentType: 'application/pdf',
    body: pdfBuffer,
  }));

  await page.route('**/api/pxa/v1/pdf/trust/inspect/jobs', route => route.fulfill({
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
  await page.route('**/api/pxa/v1/pdf/compliance/preflight', route => route.fulfill({
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
  await page.route('**/api/pxa/v1/pdf/compliance/convert', route => route.fulfill({
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
  await page.route('**/api/pxa/v1/pdf/signatures/sign', route => route.fulfill({
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
  }

  await page.getByRole('button', { name: 'PDF Trust' }).click();
  await page.getByRole('button', { name: 'Inspect trust' }).click();
  await expect(page.getByText('SHA-256 aaaaaaaaaaaaaaaa...')).toBeVisible();

  await page.getByRole('button', { name: 'Preflight PDF/A' }).click();
  await expect(page.getByText(/Preflight passed|Ready/i)).toBeVisible();
  await page.getByRole('button', { name: /Convert to selected PDF\/A profile/i }).click();

  await expect(page.getByRole('button', { name: 'Sign PDF' })).toBeVisible();
  await page.locator('input[type="file"][accept*=".p12"]').setInputFiles({
    name: 'synthetic.p12',
    mimeType: 'application/x-pkcs12',
    buffer: Buffer.from('synthetic-certificate'),
  });
  await page.locator('input[type="password"]').fill('synthetic-password');
  await page.getByRole('button', { name: 'Sign PDF' }).click();
  await expect(page.getByText('The signed PDF was created.')).toBeVisible();

  await page.getByRole('button', { name: 'Review' }).click();
  await page.getByRole('button', { name: 'Redact', exact: true }).click();
  await page.locator('.pdfv-page-stack').click({ position: { x: 120, y: 120 } });
  await page.getByRole('button', { name: 'Preflight redactions' }).click();
  await expect(page.getByTestId('redaction-report')).toContainText('Preflight');
  await page.getByRole('button', { name: 'Apply redactions' }).click();
  await expect(page.getByTestId('redaction-report')).toContainText('Verify: ✓');
  await page.getByRole('button', { name: 'Compare with original' }).click();
  await expect(page.getByRole('dialog', { name: 'Redaction result comparison' })).toBeVisible();

  const download = page.waitForEvent('download');
  await page.getByTitle('Download').click();
  await expect((await download).suggestedFilename()).toBe('synthetic.pdf');
});
