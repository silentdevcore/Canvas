import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test, type Browser, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

const screenshotDirectory = resolve(process.cwd(), '../artifacts/status-pages');
const user = {
  id: '11111111-1111-1111-1111-111111111111',
  username: 'status-test@example.test',
  email: 'status-test@example.test',
  displayName: 'Status Test User',
  roles: ['System Administrator'],
  permissions: ['designer.access', 'designer.pdf.access'],
  organizations: [{
    id: '22222222-2222-2222-2222-222222222222',
    name: 'Status Test Organization',
    slug: 'status-test',
  }],
  activeOrganizationId: '22222222-2222-2222-2222-222222222222',
  lastLoginAt: null,
  apiVersion: '1',
};

const applications = [
  { name: 'company', origin: 'http://localhost:5173', validPath: '/pricing' },
  { name: 'documentation', origin: 'http://localhost:5174', validPath: '/' },
  { name: 'demo', origin: 'http://localhost:5175', validPath: '/' },
  { name: 'designer', origin: 'http://localhost:5176', validPath: '/' },
  { name: 'admin', origin: 'http://localhost:5177', validPath: '/dashboard' },
  { name: 'account', origin: 'http://localhost:5178', validPath: '/login' },
] as const;

async function mockAuthentication(page: Page): Promise<void> {
  await page.route('**/api/pxa/v1/auth/me', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(user),
  }));
  await page.route('**/api/pxa/v1/designer/features', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ features: [] }),
  }));
}

async function assertNoHorizontalOverflow(page: Page): Promise<void> {
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
}

async function captureStatusPages(browser: Browser, viewport: { width: number; height: number }, suffix: string) {
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  await mockAuthentication(page);

  for (const application of applications) {
    await page.goto(`${application.origin}/missing-status-page`);
    if (application.name === 'account') {
      // Account intentionally resolves unknown routes before its auth redirect.
      await expect(page.locator('[data-status-key="notFound"]')).toBeVisible();
    } else {
      await expect(page.locator('[data-status-key="notFound"]')).toBeVisible();
    }
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await assertNoHorizontalOverflow(page);
    await page.screenshot({
      path: resolve(screenshotDirectory, `${application.name}-${suffix}.png`),
      fullPage: true,
    });
  }

  await context.close();
}

test.beforeAll(async () => {
  await mkdir(screenshotDirectory, { recursive: true });
});

test('valid routes win before each application catch-all', async ({ page }) => {
  await mockAuthentication(page);
  for (const application of applications) {
    await page.goto(`${application.origin}${application.validPath}`);
    await expect(page.locator('[data-status-key="notFound"]')).toHaveCount(0);
    await expect(page.getByRole('heading', { level: 1 }).first()).toBeVisible();
  }
});

test('unknown routes survive direct navigation and refresh', async ({ page }) => {
  await mockAuthentication(page);
  for (const application of applications) {
    await page.goto(`${application.origin}/missing-status-page`);
    await expect(page.locator('[data-status-key="notFound"]')).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toBeFocused();
    const primaryAction = page.locator('[data-status-key="notFound"] a, [data-status-key="notFound"] button').first();
    if (await primaryAction.count()) {
      const box = await primaryAction.boundingBox();
      expect(box?.height).toBeGreaterThanOrEqual(44);
      expect(box?.width).toBeGreaterThanOrEqual(44);
    }
    await page.reload();
    await expect(page.locator('[data-status-key="notFound"]')).toBeVisible();
  }
});

test('standalone fallbacks work without JavaScript or API access', async ({ browser }) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  const page = await context.newPage();
  for (const application of applications) {
    for (const fallback of ['404.html', '500.html', 'maintenance.html']) {
      await page.goto(`${application.origin}/${fallback}`);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await expect(page.locator('script')).toHaveCount(0);
      await assertNoHorizontalOverflow(page);
    }
  }
  await context.close();
});

test('status layouts remain usable on desktop and mobile', async ({ browser }) => {
  await captureStatusPages(browser, { width: 1440, height: 900 }, 'desktop');
  await captureStatusPages(browser, { width: 390, height: 844 }, 'mobile');
});

test('status pages pass automated WCAG scans and keyboard recovery', async ({ page }) => {
  await mockAuthentication(page);
  for (const application of applications) {
    await page.goto(`${application.origin}/missing-status-page`);
    await expect(page.locator('[data-status-key="notFound"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();
    expect(results.violations, `${application.name}: ${results.violations
      .map(violation => `${violation.id} (${violation.nodes.length})`)
      .join(', ')}`).toEqual([]);

    await page.keyboard.press('Tab');
    await expect(page.locator('[data-status-key="notFound"] a, [data-status-key="notFound"] button').first())
      .toBeFocused();
  }
});

test('status pages reflow with enlarged text and honor reduced motion', async ({ browser }) => {
  const context = await browser.newContext({
    viewport: { width: 320, height: 720 },
    reducedMotion: 'reduce',
  });
  const page = await context.newPage();
  await mockAuthentication(page);

  for (const application of applications) {
    await page.goto(`${application.origin}/missing-status-page`);
    await page.addStyleTag({ content: `
      html { font-size: 200% !important; }
      * { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; }
      p { margin-block-end: 2em !important; }
    ` });
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await assertNoHorizontalOverflow(page);

    const animatedElements = await page.locator('[data-status-key="notFound"] *').evaluateAll(elements =>
      elements.flatMap(element => {
        const style = getComputedStyle(element);
        const duration = style.transitionDuration
          .split(',')
          .reduce((maximum, value) => Math.max(maximum, Number.parseFloat(value) || 0), 0);
        return duration > 0.001 || (style.animationName !== 'none' && style.animationIterationCount !== '1')
          ? [{ tag: element.tagName, className: element.className, duration, animation: style.animationName }]
          : [];
      }));
    expect(animatedElements).toEqual([]);
  }

  await context.close();
});

test('standalone failure page needs no external assets', async ({ browser }) => {
  const context = await browser.newContext({ javaScriptEnabled: false });
  const page = await context.newPage();
  const assetRequests: string[] = [];
  page.on('request', request => {
    if (request.resourceType() !== 'document') assetRequests.push(request.url());
  });

  for (const application of applications) {
    assetRequests.length = 0;
    await page.goto(`${application.origin}/500.html`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(assetRequests, application.name).toEqual([]);
  }

  await context.close();
});

test('Arabic Designer status keeps diagnostics left-to-right', async ({ browser }) => {
  const context = await browser.newContext();
  await context.addInitScript(() => localStorage.setItem('pxa_locale', 'ar'));
  const page = await context.newPage();
  await mockAuthentication(page);
  await page.goto('http://localhost:5176/missing-status-page');

  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  await expect(page.getByRole('heading', { name: 'الصفحة غير موجودة' })).toBeVisible();
  expect(await page.locator('.pxa-status-page__diagnostic code').count()).toBe(0);
  await assertNoHorizontalOverflow(page);
  await context.close();
});

test('offline authentication is distinct from authorization failure', async ({ page }) => {
  await page.route('**/api/pxa/v1/auth/me', route => route.abort('internetdisconnected'));
  await page.goto('http://localhost:5176/');
  await expect(page.locator('[data-status-key="offline"]')).toBeVisible();
  await expect(page.locator('[data-status-key="forbidden"]')).toHaveCount(0);
});

test('Designer keeps entitlement, Trial, and suspension failures distinct', async ({ browser }) => {
  const scenarios = [
    { code: 'PXA_ENTITLEMENT_MISSING', statusKey: 'entitlementDenied' },
    { code: 'PXA_TRIAL_EXPIRED', statusKey: 'trialExpired' },
    { code: 'PXA_ORGANIZATION_INACTIVE', statusKey: 'suspended' },
  ] as const;

  for (const scenario of scenarios) {
    const context = await browser.newContext();
    const page = await context.newPage();
    await page.route('**/api/pxa/v1/auth/me', route => route.fulfill({
      status: 403,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        status: 403,
        title: 'Designer access unavailable',
        detail: 'Use the recovery action shown by PXA Account.',
        code: scenario.code,
      }),
    }));

    await page.goto('http://localhost:5176/?returnUrl=https://attacker.example/private');
    await expect(page.locator(`[data-status-key="${scenario.statusKey}"]`)).toBeVisible();
    await expect(page.locator('body')).not.toContainText('attacker.example');
    await expect(page.locator('body')).not.toContainText(user.email);
    await expect(page).toHaveURL(/^http:\/\/localhost:5176\//);
    await context.close();
  }
});

test('unauthenticated Designer handoff sends only the local path to Account', async ({ page }) => {
  let authorizeUrl: URL | undefined;
  page.on('request', request => {
    const url = new URL(request.url());
    if (url.origin === 'http://localhost:5178' && url.pathname === '/designer-authorize')
      authorizeUrl = url;
  });
  await page.route('**/api/pxa/v1/auth/me', route => route.fulfill({
    status: 401,
    contentType: 'application/problem+json',
    body: JSON.stringify({ status: 401, title: 'Authentication required', code: 'PXAAPI001' }),
  }));

  await page.goto('http://localhost:5176/private-designer-path?returnUrl=https://attacker.example/private');
  await expect.poll(() => authorizeUrl?.searchParams.get('returnPath')).toBe('/private-designer-path');
  expect(authorizeUrl?.searchParams.get('designerOrigin')).toBe('http://localhost:5176');
  expect(authorizeUrl?.toString()).not.toContain('attacker.example');
});

test('recovery navigation reports completed and failed outcomes without route details', async ({ page }) => {
  const events: Array<{ type: string; outcome: string; route: string; name?: string }> = [];
  await page.route('**/api/pxa/v1/telemetry/browser', async route => {
    const request = route.request();
    const body = request.postDataJSON();
    events.push(...(body.events ?? []));
    await route.fulfill({ status: 202, body: '' });
  });

  await page.goto('http://localhost:5173/missing-status-page');
  await expect(page.locator('[data-status-key="notFound"]')).toBeVisible();
  await page.locator('[data-status-action="home"]').click();
  await expect(page).toHaveURL('http://localhost:5173/');
  await expect.poll(() => events.some(event =>
    event.type === 'status_recovery' && event.outcome === 'completed' && event.name === 'home')).toBe(true);

  await page.evaluate(() => sessionStorage.setItem('pxa-status-recovery-v1', JSON.stringify({
    action: 'retry',
    at: Date.now(),
  })));
  await page.goto('http://localhost:5173/another-missing-status-page');
  await expect(page.locator('[data-status-key="notFound"]')).toBeVisible();
  await expect.poll(() => events.some(event =>
    event.type === 'status_recovery' && event.outcome === 'failed' && event.name === 'retry')).toBe(true);

  const recoveryEvents = events.filter(event => event.type === 'status_recovery');
  expect(recoveryEvents.every(event => ['home', 'retry'].includes(event.name ?? ''))).toBe(true);
  expect(JSON.stringify(recoveryEvents)).not.toContain('missing-status-page');
});
