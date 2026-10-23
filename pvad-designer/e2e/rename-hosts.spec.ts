import { expect, test } from '@playwright/test';

const sites = {
  company: 'http://localhost:15173', documentation: 'http://localhost:15174',
  demo: 'http://localhost:15175', designer: 'http://localhost:15176',
  admin: 'http://localhost:15177', account: 'http://localhost:15178',
};

test('all six real frontend servers proxy the same-origin API', async ({ request }) => {
  for (const [name, origin] of Object.entries(sites)) {
    const response = await request.get(`${origin}/api/pvad/v1/rename-proxy-probe`);
    expect(response.status(), name).toBe(200);
    const result = await response.json();
    expect(result.synthetic).toBe(true);
    expect(result.application).toBe(name === 'designer' ? 'designer' : null);
  }
});

test('Company pages are separate documents and use configured cross-site links', async ({ page, request }) => {
  for (const path of ['/', '/products.html', '/pricing.html', '/about.html', '/support.html',
    '/contact.html', '/terms.html', '/privacy.html', '/license.html', '/cookie-storage.html',
    '/imprint.html', '/withdrawal.html', '/dpa.html', ...[
      'generator', 'migration', 'importer', 'designer', 'pdf-viewer', 'spreadsheet',
    ].map(name => `/products/${name}.html`)]) {
    const response = await request.get(sites.company + path);
    expect(response.status(), path).toBe(200);
    expect(await response.text()).toContain('<html');
  }
  await page.goto(sites.company);
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  for (const target of ['documentation', 'demo', 'designer', 'account'] as const) {
    await expect(page.locator(`a[href^="${sites[target]}"]`).first(), target).toBeVisible();
  }
  await page.goto(`${sites.company}/products.html`);
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  expect(new URL(page.url()).pathname).toBe('/products.html');
});

test('public guide links return Markdown rather than an application fallback', async ({ page, request }) => {
  await page.goto(sites.documentation);
  const links = await page.locator('a[href*="/docs/"][href$=".md"]').evaluateAll(nodes =>
    [...new Set(nodes.map(node => (node as HTMLAnchorElement).href))]);
  expect(links.length).toBeGreaterThanOrEqual(8);
  for (const link of links) {
    const response = await request.get(link);
    expect(response.status(), link).toBe(200);
    expect((await response.text()).trimStart().startsWith('#'), link).toBe(true);
  }
  const unknownAnchors = await page.locator('a[href^="#"]').evaluateAll(nodes =>
    nodes.map(node => (node as HTMLAnchorElement).hash.slice(1))
      .filter(id => id && !document.getElementById(decodeURIComponent(id))));
  expect([...new Set(unknownAnchors)]).toEqual([]);
});

for (const viewport of [{ width: 1440, height: 900 }, { width: 390, height: 844 }]) {
  test(`public sites and isolated login pages render at ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport);
    const failures: string[] = [];
    page.on('pageerror', error => failures.push(error.message));
    for (const name of ['company', 'documentation', 'demo', 'admin', 'account'] as const) {
      await page.goto(sites[name] + (['admin', 'account'].includes(name) ? '/login' : '/'));
      await expect(page.getByRole('heading', { level: 1 }).first(), name).toBeVisible();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), name).toBe(true);
      await page.screenshot({ path: `../artifacts/rename/${name}-${viewport.width}.png`, fullPage: false });
    }
    expect(failures).toEqual([]);
  });
}

test('Designer uses canonical runtime Account configuration and preserves a safe return path', async ({ page }) => {
  await page.addInitScript(accountUrl => { (window as any).__PVAD_CONFIG__ = { accountUrl }; }, sites.account);
  let handoff: URL | undefined;
  page.on('request', request => {
    const url = new URL(request.url());
    if (url.origin === sites.account && url.pathname === '/designer-authorize') handoff = url;
  });
  await page.goto(`${sites.designer}/pdf/create?returnUrl=https://attacker.example/private`);
  await expect.poll(() => handoff?.searchParams.get('designerOrigin')).toBe(sites.designer);
  expect(handoff?.searchParams.get('returnPath')).toBe('/pdf/create');
  expect(handoff?.toString()).not.toContain('attacker.example');
  await expect(page).toHaveURL(new RegExp(`^${sites.account}/login`));
  await expect(page.locator('#login-form')).toBeVisible();
});

test('storage acknowledgements use PVAD while honoring the previous cookie', async ({ page, context }) => {
  await page.goto(sites.company);
  await page.locator('[data-pxa-storage-understood]').click();
  expect((await context.cookies()).some(cookie => cookie.name === 'PVAD.StorageNotice')).toBe(true);
  expect((await context.cookies()).some(cookie => cookie.name === 'PXA.StorageNotice')).toBe(false);
  await context.clearCookies();
  await context.addCookies([{ name: 'PXA.StorageNotice', value: '2026-08', url: sites.company }]);
  await page.reload();
  await expect(page.locator('[data-pxa-storage-notice]')).toHaveCount(0);
  expect((await context.cookies()).some(cookie => cookie.name === 'PVAD.StorageNotice')).toBe(true);
});
