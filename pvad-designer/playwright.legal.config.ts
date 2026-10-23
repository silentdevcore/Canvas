import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  testMatch: /legal-.*\.spec\.ts/,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium-desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'chromium-mobile', use: { ...devices['Pixel 7'] } },
  ],
  webServer: [
    {
      command: 'npm --prefix ../websites/PVAD.Company run dev',
      url: 'http://localhost:5173',
      reuseExistingServer: true,
    },
    {
      command: 'npm --prefix ../websites/PVAD.Documentation run dev',
      url: 'http://localhost:5174',
      reuseExistingServer: true,
    },
    {
      command: 'npm --prefix ../websites/PVAD.Demo run dev',
      url: 'http://localhost:5175',
      reuseExistingServer: true,
    },
    {
      command: 'npm --prefix ../websites/PVAD.Admin run dev',
      url: 'http://localhost:5177/login',
      reuseExistingServer: true,
    },
    {
      command: 'npm --prefix ../websites/PVAD.Account run dev',
      url: 'http://localhost:5178/login',
      reuseExistingServer: true,
    },
  ],
});
