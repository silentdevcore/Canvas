import { chromium } from '../../../pxa-designer/node_modules/playwright/index.mjs';
import { readFile } from 'node:fs/promises';

const adminUrl = process.env.PXA_ADMIN_SMOKE_URL || 'http://localhost:5177';
const handbook = JSON.parse(await readFile(
  new URL('../../../PXA.WebApi/AdminDocumentation/admin-documentation.json', import.meta.url),
  'utf8',
));
const browser = await chromium.launch({ headless: true });
const user = {
  id: '20000000-0000-4000-8000-000000000001',
  username: 'alex.admin',
  email: 'a***@example.test',
  displayName: 'Alex Morgan',
  roles: ['System Administrator', 'Organization Administrator'],
  permissions: ['users.read', 'audit.read'],
  organizations: [{
    id: '10000000-0000-4000-8000-000000000001',
    name: 'Northwind Labs',
    slug: 'northwind-labs',
  }],
  activeOrganizationId: '10000000-0000-4000-8000-000000000001',
  lastLoginAt: '2026-07-26T10:30:00Z',
  apiVersion: '1',
};

try {
  const anonymousContext = await browser.newContext();
  await anonymousContext.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 401,
    contentType: 'application/problem+json',
    body: JSON.stringify({ title: 'Authentication required', status: 401 }),
  }));
  const anonymousPage = await anonymousContext.newPage();
  await anonymousPage.goto(`${adminUrl}/documentation`, { waitUntil: 'networkidle' });
  if (new URL(anonymousPage.url()).pathname !== '/login') {
    throw new Error('Unauthenticated documentation access was not redirected to Admin login.');
  }
  await anonymousContext.close();

  const context = await browser.newContext({ viewport: { width: 1440, height: 980 } });
  await context.route('**/api/pxa/v1/auth/me', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(user),
  }));
  await context.route('**/api/pxa/v1/admin/documentation', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(handbook),
  }));
  await context.route('**/api/pxa/v1/admin/documentation/images/*', async (route) => {
    const fileName = new URL(route.request().url()).pathname.split('/').at(-1);
    const body = await readFile(new URL(
      `../../../PXA.WebApi/AdminDocumentation/images/${fileName}`,
      import.meta.url,
    ));
    await route.fulfill({ status: 200, contentType: 'image/png', body });
  });
  const page = await context.newPage();
  const browserErrors = [];
  page.on('pageerror', (error) => browserErrors.push(error.message));

  await page.goto(`${adminUrl}/documentation`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Admin documentation', exact: true }).waitFor();
  if (!await page.getByText('Admin access only', { exact: true }).isVisible()) {
    throw new Error('Protected Admin documentation status is missing.');
  }

  await page.locator('a[href="/documentation#admin-users-and-invitations"]').first().click();
  await page.waitForURL(/\/documentation#admin-users-and-invitations$/);
  if (!await page.getByRole('heading', { name: 'Users and invitations', exact: true }).isVisible()) {
    throw new Error('Selected documentation topic did not replace the previous topic.');
  }
  if (await page.getByRole('heading', { name: 'Admin overview', exact: true }).count()) {
    throw new Error('Unselected documentation content remains visible.');
  }

  const search = page.locator('#admin-help-search');
  await search.fill('offline licenses');
  const matchingLink = page.locator('a[href="/documentation#admin-offline-licenses"]');
  if (!await matchingLink.isVisible()) throw new Error('Protected handbook search did not find Offline licenses.');
  const visibleLinks = await page.locator('#admin-help-navigation a:visible').count();
  if (visibleLinks !== 1) throw new Error(`Protected handbook search returned ${visibleLinks} visible links.`);

  await page.reload({ waitUntil: 'networkidle' });
  const image = page.locator('.admin-help-screenshot img');
  await image.waitFor();
  if (!await image.evaluate((element) => element.complete && element.naturalWidth > 0)) {
    throw new Error('The selected protected handbook screenshot did not load.');
  }
  if (process.env.PXA_ADMIN_SMOKE_SCREENSHOT) {
    await page.screenshot({ path: process.env.PXA_ADMIN_SMOKE_SCREENSHOT, fullPage: false });
  }

  for (const viewport of [{ width: 1440, height: 980 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(viewport);
    await page.goto(`${adminUrl}/documentation#admin-admin-overview`, { waitUntil: 'networkidle' });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    if (overflow > 1) throw new Error(`Horizontal overflow of ${overflow}px at ${viewport.width}px.`);
  }

  if (browserErrors.length) throw new Error(`Browser errors: ${browserErrors.join(' | ')}`);
  await context.close();
} finally {
  await browser.close();
}
