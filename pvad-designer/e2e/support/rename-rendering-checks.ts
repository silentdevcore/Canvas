import { expect, type Page, type TestInfo } from '@playwright/test';
import { readFile } from 'node:fs/promises';

export async function checkRenderingAssets(page: Page, designerUrl: string, testInfo: TestInfo): Promise<void> {
  const templateId = await page.evaluate(async () => {
    const image = document.createElement('canvas'); image.width = 32; image.height = 24;
    const context = image.getContext('2d')!;
    context.fillStyle = '#db2777'; context.fillRect(0, 0, 16, 24);
    context.fillStyle = '#16a34a'; context.fillRect(16, 0, 16, 24);
    const designDocument = {
      template: {
        id: 'csp-assets', name: 'CSP rendering check', category: 'test', sharedElements: [],
        pages: [{ id: 'page-assets', elements: [
          { id: 'bound-label', type: 'text', x: 20, y: 20, width: 280, height: 30,
            content: '{{customer.name}}', binding: 'customer.name', fontFamily: 'Helvetica', fontSize: 18 },
          { id: 'local-image', type: 'image', x: 20, y: 80, width: 160, height: 120, content: image.toDataURL('image/png') },
        ] }],
      },
      pageSettings: { width: 400, height: 300, orientation: 'landscape', margins: { top: 0, right: 0, bottom: 0, left: 0 } },
      jsonData: { customer: { name: 'Synthetic customer' } }, documentMode: 'pdf', currentPageIndex: 0,
    };
    const response = await fetch('/api/pvad/v1/designer/templates', {
      method: 'POST', headers: { 'Content-Type': 'application/json', 'X-PVAD-Application': 'designer' },
      body: JSON.stringify({ name: 'CSP rendering check', designDocument }),
    });
    if (!response.ok) throw new Error(`Synthetic rendering fixture failed: ${response.status}`);
    return (await response.json()).id as string;
  });
  await page.goto(`${designerUrl}/pdf/create?templateId=${templateId}`);
  await expect(page.getByRole('button', { name: 'Rename CSP rendering check' })).toBeVisible();
  const image = page.locator('.editor-page img[src^="data:image/png"]');
  await expect(image).toBeVisible();
  await expect.poll(() => image.evaluate((element: HTMLImageElement) => element.complete && element.naturalWidth === 32)).toBe(true);
  const fontResults = await page.evaluate(async () => {
    const styles = ['18px', 'bold 18px', 'italic 18px', 'italic bold 18px'];
    return Promise.all(styles.map(async style => {
      const faces = await document.fonts.load(`${style} "PVADE Helvetica"`, 'Synthetic customer');
      return faces.length > 0 && faces.every(face => face.status === 'loaded');
    }));
  });
  expect(fontResults).toEqual([true, true, true, true]);
  await page.screenshot({ path: testInfo.outputPath('csp-local-fonts-and-image.png') });
  await page.getByRole('button', { name: 'Preview', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Preview: CSP rendering check' })).toBeVisible();
  await expect.poll(() => page.locator('img[src^="data:image/png"]').first()
    .evaluate((element: HTMLImageElement) => element.complete && element.naturalWidth === 32)).toBe(true);
  await page.getByRole('button', { name: 'Export', exact: true }).click();
  const jsonDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: /Export JSON/ }).click();
  const json = JSON.parse(await readFile((await (await jsonDownload).path())!, 'utf8'));
  const bound = json.pages.flatMap((value: any) => value.elements).find((value: any) => value.id === 'bound-label');
  expect(bound.binding).toBe('customer.name');
  // This checks binding preservation, not data substitution in the direct-design renderer.
  expect(bound.content).toBe('{{customer.name}}');
  await page.getByRole('button', { name: 'Export', exact: true }).click();
  const pdfDownload = page.waitForEvent('download');
  await page.getByRole('button', { name: /Export PDF/ }).click();
  const pdf = await pdfDownload;
  expect((await readFile((await pdf.path())!)).subarray(0, 5).toString()).toBe('%PDF-');
}
