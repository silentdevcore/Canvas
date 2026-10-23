import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  testMatch: /status-pages\.spec\.ts/,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    ...devices['Desktop Chrome'],
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command: 'npm --prefix ../websites/PVAD.Company run build && npm --prefix ../websites/PVAD.Company run preview -- --port 5173',
      url: 'http://localhost:5173',
      reuseExistingServer: true,
    },
    {
      command: 'npm --prefix ../websites/PVAD.Documentation run build && npm --prefix ../websites/PVAD.Documentation run preview -- --port 5174',
      url: 'http://localhost:5174',
      reuseExistingServer: true,
    },
    {
      command: 'npm --prefix ../websites/PVAD.Demo run build && npm --prefix ../websites/PVAD.Demo run preview -- --port 5175',
      url: 'http://localhost:5175',
      reuseExistingServer: true,
    },
    {
      command: 'npm run build && npm run preview -- --host localhost --port 5176',
      url: 'http://localhost:5176',
      reuseExistingServer: true,
    },
    {
      command: 'npm --prefix ../websites/PVAD.Admin run build && npm --prefix ../websites/PVAD.Admin run preview -- --port 5177',
      url: 'http://localhost:5177/login',
      reuseExistingServer: true,
    },
    {
      command: 'npm --prefix ../websites/PVAD.Account run build && npm --prefix ../websites/PVAD.Account run preview -- --port 5178',
      url: 'http://localhost:5178/login',
      reuseExistingServer: true,
    },
  ],
});
