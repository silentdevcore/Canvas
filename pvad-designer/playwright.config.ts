import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  expect: {
    timeout: 15_000,
  },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
  ],
  use: {
    channel: process.env.PVAD_SMOKE_CHROMIUM_CHANNEL === 'chromium' ? 'chromium' : undefined,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chrome-desktop',
      use: {
        ...devices['Desktop Chrome'],
      },
    },
    {
      name: 'pdf-viewer-mobile',
      testMatch: /pdf-viewer-trust-authenticated\.spec\.ts/,
      use: {
        ...devices['Pixel 5'],
      },
    },
    {
      // The electronic-invoice workspace is a dense editor; the mobile run is what proves the
      // lines table, the totals and the generate actions stay reachable at phone width.
      name: 'e-invoice-mobile',
      testMatch: /e-invoice-authenticated\.spec\.ts/,
      use: {
        ...devices['Pixel 5'],
      },
    },
  ],
});
