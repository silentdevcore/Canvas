import { expect, test, type Page } from '@playwright/test';

const companyUrl = 'http://localhost:5173';
const hash = 'a'.repeat(64);

const legalDocument = {
  key: 'terms',
  version: '2026-08',
  contentHash: hash,
  renderedHtml: '<h1>Synthetic deployed terms</h1><p>Published legal content.</p>',
  effectiveAt: '2026-07-01T00:00:00Z',
  isAuthoritative: false,
};

function snapshot(generatedAt: string) {
  return {
    schemaVersion: 1,
    generatedAt,
    locale: 'en',
    audience: 'All',
    documents: [legalDocument],
  };
}

async function failLegalApi(page: Page) {
  await page.route('**/api/pxa/v1/legal/documents/terms/current?*', (route) =>
    route.fulfill({ status: 503, contentType: 'application/problem+json', body: '{}' }));
}

async function assertNoHorizontalOverflow(page: Page) {
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
}

test('Company prefers current Legal API content', async ({ page }) => {
  await page.route('**/api/pxa/v1/legal/documents/terms/current?*', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(legalDocument),
    }));

  await page.goto(`${companyUrl}/terms.html`);

  const content = page.locator('[data-legal-content]');
  await expect(content).toHaveAttribute('data-legal-source', 'live');
  await expect(content.getByRole('heading', { name: 'Synthetic deployed terms' })).toBeVisible();
  await expect(page.getByText(/Archived copy from/)).toHaveCount(0);
  await assertNoHorizontalOverflow(page);
});

test('Company remains readable through a valid last-known-good snapshot', async ({ page }) => {
  await failLegalApi(page);
  await page.route('**/legal-snapshots/en.json', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(snapshot(new Date().toISOString())),
  }));

  await page.goto(`${companyUrl}/terms.html`);

  const content = page.locator('[data-legal-content]');
  await expect(content).toHaveAttribute('data-legal-source', 'snapshot');
  await expect(content.getByRole('heading', { name: 'Synthetic deployed terms' })).toBeVisible();
  await expect(page.getByText(/Archived copy from/)).toBeVisible();
  await expect(page.getByText(/transactions requiring current-version verification remain disabled/)).toBeVisible();
  await assertNoHorizontalOverflow(page);
});

test('Company visibly identifies a stale but valid snapshot', async ({ page }) => {
  await failLegalApi(page);
  await page.route('**/legal-snapshots/en.json', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(snapshot('2025-01-01T00:00:00Z')),
  }));

  await page.goto(`${companyUrl}/terms.html`);

  await expect(page.locator('[data-legal-content]')).toHaveAttribute(
    'data-legal-source',
    'snapshot',
  );
  await expect(page.getByText(/Snapshot older than 30 days/)).toBeVisible();
});

test('Company rejects a corrupt snapshot instead of presenting draft copy as published', async ({
  page,
}) => {
  await failLegalApi(page);
  await page.route('**/legal-snapshots/en.json', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ ...snapshot(new Date().toISOString()), documents: [] }),
  }));

  await page.goto(`${companyUrl}/terms.html`);

  const content = page.locator('[data-legal-content]');
  await expect(content).toHaveAttribute('data-legal-source', 'unavailable');
  await expect(content.getByRole('heading', {
    name: 'Verified legal content is temporarily unavailable',
  })).toBeVisible();
  await expect(page.getByText(/Registration and other transactions.*remain disabled/)).toBeVisible();
  await expect(page.getByText('Website and demo use')).toHaveCount(0);
});
