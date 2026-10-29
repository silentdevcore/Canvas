import { expect, test } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { designerPermissions } from '../src/auth/designerPermissions';
import { readPvadEnvironment } from '../../tools/configuration/environment.mjs';

test('Designer UI captures favorites and content preview with synthetic API fixtures', async ({ page }) => {
  const origin = readPvadEnvironment('PVAD_TEST_DESIGNER_URL') ?? 'http://localhost:5176';
  const directory = readPvadEnvironment('PVAD_VIEWER_CAPTURE_DIRECTORY');
  test.skip(!directory, 'Run through the controlled documentation capture command.');
  const id = '11111111-1111-4111-8111-111111111111';
  const document = {
    id, name: 'Example document', description: '', tags: [], status: 'Draft', revision: 1,
    checksum: 'synthetic-ui-fixture', schemaVersion: '1', designerVersion: '1.1.0', publishedVersionId: null,
    updatedAt: '2026-01-01T00:00:00Z', designDocument: {
      template: { id, name: 'Example document', description: '', category: 'blank', pages: [{ id: 'page-1', elements: [] }], sharedElements: [], data: {} },
      pageSettings: {}, jsonData: {}, documentMode: 'pdf', currentPageIndex: 0,
    },
  };
  const unhandled: string[] = [];
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('**/api/**', async route => {
    const pathname = new URL(route.request().url()).pathname;
    if (pathname === '/api/pvad/v1/telemetry/browser') {
      await route.fulfill({ status: 202, body: '' });
      return;
    }
    let body: unknown;
    if (pathname.endsWith('/auth/me')) body = {
      id: 'synthetic-user', username: 'synthetic-user', email: 'synthetic@example.test', displayName: 'Documentation User',
      roles: ['Editor'], permissions: Object.values(designerPermissions),
      organizations: [{ id: 'synthetic-org', name: 'Example Company', slug: 'example' }], activeOrganizationId: 'synthetic-org', apiVersion: '1',
    };
    else if (pathname.endsWith('/auth/csrf')) body = { token: 'synthetic-csrf' };
    else if (pathname.endsWith('/element-favorites')) body = { elementTypes: ['text', 'field', 'table', 'chart'] };
    else if (pathname.endsWith('/features')) body = [];
    else if (pathname.endsWith('/releases')) body = { releases: [], readVersions: [] };
    else if (pathname.endsWith('/notifications/unread-count')) body = { unread: 0 };
    else if (pathname.endsWith('/notifications')) body = { items: [], total: 0, page: 1, pageSize: 50 };
    else if (pathname === `/api/pvad/v1/designer/templates/${id}` || pathname === `/api/pvad/v1/designer/templates/${id}/draft`) body = document;
    else { unhandled.push(pathname); body = {}; }
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
  await page.goto(`${origin}/pdf/create?templateId=${id}`);
  const tools = page.getByRole('complementary', { name: 'Element tools' });
  await expect(tools).toBeVisible();
  await expect(tools.locator('.editor-tool-group-title').first()).toHaveText('Favorites');
  await expect(tools.locator('.editor-tool-list')).toHaveAttribute('aria-busy', 'false');
  await fs.mkdir(directory!, { recursive: true });
  await page.screenshot({ path: path.join(directory!, 'element-library-favorites.png'), fullPage: false });
  await tools.getByRole('tab', { name: 'Content', exact: true }).click();
  await tools.getByRole('button', { name: 'Preview content', exact: true }).first().click();
  await expect(page.getByRole('dialog')).toBeVisible();
  await page.screenshot({ path: path.join(directory!, 'content-library-preview.png'), fullPage: false });
  expect(errors).toEqual([]);
  expect(unhandled).toEqual([]);
});
